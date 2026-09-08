using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Data;
using Devilution.Domain.Hashing;
using Devilution.Engine;

namespace Devilution.Server.Gameplay;

/** A hash-verified, explicitly selected gameplay-module package. */
public sealed record LoadedGameplayModulePackage(
    string PackageId,
    string PackageVersion,
    string AssemblyPath,
    string AssemblySha256,
    IReadOnlyList<IGameplayModule> Modules);

/**
 * Loads trusted module types declared by an operator-selected package. This
 * host adapter owns filesystem and reflection concerns; Engine receives only
 * the resulting candidates.
 */
public sealed class GameplayModulePackageLoader
{
    private readonly IReadOnlyDictionary<string, Assembly> sharedAssemblies;

    public const string ManifestFileName = "gameplay-module.json";
    public const int CurrentFormatVersion = 1;
    public const long MaximumAssemblyBytes = 256L * 1024 * 1024;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public GameplayModulePackageLoader(
        IEnumerable<Assembly>? additionalSharedAssemblies = null)
    {
        var additional = additionalSharedAssemblies?.ToArray() ?? [];
        if (additional.Any(assembly => assembly is null)) {
            throw new ArgumentException(
                "Shared gameplay-module assemblies cannot contain null.",
                nameof(additionalSharedAssemblies));
        }
        sharedAssemblies = new[] {
            typeof(ContentManifest).Assembly,
            typeof(CanonicalStateHasher).Assembly,
            typeof(IGameplayModule).Assembly,
        }
            .Concat(additional)
            .GroupBy(assembly => assembly.GetName().Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
    }

    public LoadedGameplayModulePackage LoadDirectory(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        var root = Path.GetFullPath(packageDirectory);
        if (!Directory.Exists(root)) {
            throw new DirectoryNotFoundException(
                $"Gameplay module package directory '{packageDirectory}' does not exist.");
        }
        RejectReparsePoints(root, packageDirectory);
        var manifestPath = Path.Combine(root, ManifestFileName);
        if (!File.Exists(manifestPath)) {
            throw new FileNotFoundException(
                $"Gameplay module package '{root}' does not contain {ManifestFileName}.",
                manifestPath);
        }

        ModulePackageDocument? document;
        try {
            document = JsonSerializer.Deserialize<ModulePackageDocument>(
                ReadUtf8Text(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        } catch (JsonException exception) {
            throw PackageError(manifestPath, "is not valid JSON", exception);
        }
        if (document is null
            || document.FormatVersion != CurrentFormatVersion
            || string.IsNullOrWhiteSpace(document.Id)
            || string.IsNullOrWhiteSpace(document.Version)
            || string.IsNullOrWhiteSpace(document.Assembly)
            || string.IsNullOrWhiteSpace(document.AssemblySha256)) {
            throw PackageError(
                manifestPath,
                $"requires formatVersion {CurrentFormatVersion}, id, version, assembly, and assemblySha256");
        }
        if (!ContentSemanticVersion.TryParse(document.Version, out var packageVersion)) {
            throw PackageError(
                manifestPath,
                "version must contain three unsigned integers");
        }
        var moduleTypes = document.ModuleTypes?.ToArray() ?? [];
        if (moduleTypes.Length == 0
            || moduleTypes.Any(string.IsNullOrWhiteSpace)
            || moduleTypes.Distinct(StringComparer.Ordinal).Count() != moduleTypes.Length) {
            throw PackageError(
                manifestPath,
                "requires a non-empty list of unique moduleTypes");
        }

        var assemblyPath = ResolvePackageAssembly(root, document.Assembly, manifestPath);
        var expectedHash = NormalizeSha256(document.AssemblySha256, manifestPath);
        var assemblyLength = new FileInfo(assemblyPath).Length;
        if (assemblyLength == 0 || assemblyLength > MaximumAssemblyBytes) {
            throw PackageError(
                manifestPath,
                $"assembly size must be between 1 and {MaximumAssemblyBytes} bytes");
        }
        string actualHash;
        using (var stream = File.OpenRead(assemblyPath))
            actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal)) {
            throw PackageError(
                manifestPath,
                $"expected assembly SHA-256 {expectedHash}, but '{assemblyPath}' hashes to {actualHash}");
        }

        var loadContext = new ModuleAssemblyLoadContext(assemblyPath, sharedAssemblies);
        Assembly assembly;
        try {
            using var stream = File.OpenRead(assemblyPath);
            assembly = loadContext.LoadFromStream(stream);
        } catch (Exception exception) when (
            exception is BadImageFormatException
                or FileLoadException
                or FileNotFoundException) {
            throw PackageError(
                manifestPath,
                $"could not load assembly '{assemblyPath}'",
                exception);
        }
        var unsupportedReferences = assembly.GetReferencedAssemblies()
            .Where(reference => !loadContext.IsAllowedReference(reference))
            .Select(reference => reference.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unsupportedReferences.Length != 0) {
            throw PackageError(
                manifestPath,
                "references unsupported private assemblies: "
                + $"{string.Join(", ", unsupportedReferences)}; formatVersion 1 packages must be one managed assembly and may reference only platform libraries and shared Godevilution contracts");
        }

        var modules = new List<IGameplayModule>(moduleTypes.Length);
        foreach (var moduleTypeName in moduleTypes) {
            var type = assembly.GetType(moduleTypeName, throwOnError: false, ignoreCase: false);
            if (type is null) {
                throw PackageError(
                    manifestPath,
                    $"declares missing module type '{moduleTypeName}'");
            }
            if (!typeof(IGameplayModule).IsAssignableFrom(type)
                || type.IsAbstract
                || !(type.IsPublic || type.IsNestedPublic)) {
                throw PackageError(
                    manifestPath,
                    $"type '{moduleTypeName}' must be a public, concrete {nameof(IGameplayModule)}");
            }
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor is null) {
                throw PackageError(
                    manifestPath,
                    $"type '{moduleTypeName}' requires a public parameterless constructor");
            }
            IGameplayModule module;
            try {
                module = (IGameplayModule)constructor.Invoke(null);
            } catch (Exception exception) when (exception is TargetInvocationException or MemberAccessException) {
                throw PackageError(
                    manifestPath,
                    $"could not construct module type '{moduleTypeName}'",
                    exception);
            }
            var identity = module.Identity ?? throw PackageError(
                manifestPath,
                $"module type '{moduleTypeName}' returned no identity");
            modules.Add(new VerifiedGameplayModule(module, identity, actualHash));
        }

        return new LoadedGameplayModulePackage(
            document.Id,
            packageVersion.ToString(),
            assemblyPath,
            actualHash,
            modules.ToArray());
    }

    private static string ResolvePackageAssembly(
        string root,
        string relativeAssemblyPath,
        string manifestPath)
    {
        var canonicalPath = NormalizePackageRelativePath(relativeAssemblyPath, manifestPath);
        var assemblyPath = Path.GetFullPath(Path.Combine(root, canonicalPath));
        var containedRoot = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!assemblyPath.StartsWith(containedRoot, StringComparison.OrdinalIgnoreCase))
            throw PackageError(manifestPath, "assembly path escapes the package directory");
        if (!string.Equals(Path.GetExtension(assemblyPath), ".dll", StringComparison.OrdinalIgnoreCase))
            throw PackageError(manifestPath, "assembly must name a .dll file");
        if (!File.Exists(assemblyPath)) {
            throw new FileNotFoundException(
                $"Gameplay module package assembly '{assemblyPath}' does not exist.",
                assemblyPath);
        }
        return assemblyPath;
    }

    private static string NormalizePackageRelativePath(string path, string manifestPath)
    {
        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.None);
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(normalized)
            || normalized.Contains(':', StringComparison.Ordinal)
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.EndsWith("/", StringComparison.Ordinal)
            || normalized.Contains("//", StringComparison.Ordinal)
            || segments.Any(segment => segment is "" or "." or "..")) {
            throw PackageError(
                manifestPath,
                "assembly must be a canonical package-relative path without traversal segments; "
                + "assembly path escapes the package directory or uses an alias");
        }
        return normalized;
    }

    private static string NormalizeSha256(string value, string manifestPath)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw PackageError(manifestPath, "assemblySha256 must contain exactly 64 hexadecimal characters");
        return normalized;
    }

    private static string ReadUtf8Text(string path)
    {
        try {
            return StrictUtf8.GetString(File.ReadAllBytes(path));
        } catch (DecoderFallbackException exception) {
            throw PackageError(path, "is not valid UTF-8", exception);
        }
    }

    private static void RejectReparsePoints(string directory, string displayPath)
    {
        var directoryAttributes = File.GetAttributes(directory);
        if ((directoryAttributes & FileAttributes.ReparsePoint) != 0)
            throw PackageError(displayPath, "is a reparse-point entry");

        foreach (var entry in Directory.EnumerateFileSystemEntries(directory)) {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw PackageError(displayPath, $"contains a reparse-point entry '{entry}'");
            if ((attributes & FileAttributes.Directory) != 0)
                RejectReparsePoints(entry, displayPath);
        }
    }

    private static InvalidDataException PackageError(
        string manifestPath,
        string detail,
        Exception? innerException = null)
    {
        return new InvalidDataException(
            $"Gameplay module manifest '{manifestPath}' {detail}.",
            innerException);
    }

    private sealed record ModulePackageDocument(
        int FormatVersion,
        string? Id,
        string? Version,
        string? Assembly,
        string? AssemblySha256,
        IReadOnlyList<string>? ModuleTypes);

    private sealed class VerifiedGameplayModule : IGameplayModule
    {
        private readonly IGameplayModule inner;

        public VerifiedGameplayModule(
            IGameplayModule inner,
            GameplayModuleIdentity declaredIdentity,
            string assemblySha256)
        {
            this.inner = inner;
            Identity = new GameplayModuleIdentity(
                declaredIdentity.Id,
                declaredIdentity.Version,
                assemblySha256,
                declaredIdentity.Dependencies,
                declaredIdentity.Compatibility,
                declaredIdentity.DependencyConstraints);
        }

        public GameplayModuleIdentity Identity { get; }

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
            inner.Register(registry, context);
        }
    }

    private sealed class ModuleAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly IReadOnlyDictionary<string, Assembly> sharedAssemblies;
        private static readonly string RuntimeAssemblyRoot
            = Path.GetFullPath(RuntimeEnvironment.GetRuntimeDirectory())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        private static readonly IReadOnlySet<string> PlatformAssemblyNames
            = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(path => Path.GetFullPath(path).StartsWith(
                    RuntimeAssemblyRoot,
                    StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFileNameWithoutExtension(path)!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        public ModuleAssemblyLoadContext(
            string assemblyPath,
            IReadOnlyDictionary<string, Assembly> sharedAssemblies)
            : base($"Godevilution module: {Path.GetFileNameWithoutExtension(assemblyPath)}")
        {
            this.sharedAssemblies = sharedAssemblies;
        }

        public bool IsAllowedReference(AssemblyName assemblyName)
        {
            return assemblyName.Name is not null
                && (sharedAssemblies.ContainsKey(assemblyName.Name)
                    || PlatformAssemblyNames.Contains(assemblyName.Name));
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is not null
                && sharedAssemblies.TryGetValue(assemblyName.Name, out var shared)) {
                return shared;
            }
            if (assemblyName.Name is not null
                && PlatformAssemblyNames.Contains(assemblyName.Name)) {
                return null;
            }
            throw new FileLoadException(
                $"Gameplay module packages cannot load private assembly '{assemblyName.Name}'.");
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            throw new DllNotFoundException(
                $"Gameplay module packages cannot load private native library '{unmanagedDllName}'.");
        }
    }
}
