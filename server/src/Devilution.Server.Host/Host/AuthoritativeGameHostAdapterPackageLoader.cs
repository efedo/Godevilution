using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;

namespace Devilution.Server.Host;

/** A hash-verified package containing one server game-host adapter factory. */
public sealed record LoadedAuthoritativeGameHostAdapterPackage(
    string PackageId,
    string PackageVersion,
    string AssemblyPath,
    string AssemblySha256,
    IAuthoritativeGameHostAdapterFactory Factory);

/**
 * Loads an explicitly selected adapter package without letting filesystem or
 * reflection concerns cross into Engine. Package assemblies may reference only
 * platform libraries and the shared Godevilution host, protocol, data, and
 * engine contracts (including the shared Google.Protobuf runtime). Private
 * dependencies may be carried beside the entry assembly when each dependency
 * is listed and hash-verified by the manifest.
 */
public sealed class AuthoritativeGameHostAdapterPackageLoader
{
    public const string ManifestFileName = "game-host-adapter.json";
    public const int CurrentFormatVersion = 1;
    public const long MaximumAssemblyBytes = 256L * 1024 * 1024;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly IReadOnlyDictionary<string, Assembly> sharedAssemblies;

    public AuthoritativeGameHostAdapterPackageLoader(
        IEnumerable<Assembly>? additionalSharedAssemblies = null)
    {
        var additional = additionalSharedAssemblies?.ToArray() ?? [];
        if (additional.Any(assembly => assembly is null))
            throw new ArgumentException(
                "Shared adapter-package assemblies cannot contain null.",
                nameof(additionalSharedAssemblies));
        sharedAssemblies = new[] {
            typeof(ContentManifest).Assembly,
            typeof(IGameplayModule).Assembly,
            typeof(IAuthoritativeGameHostAdapterFactory).Assembly,
            typeof(Command).Assembly,
            typeof(IMessage).Assembly,
        }
            .Concat(additional)
            .GroupBy(assembly => assembly.GetName().Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
    }

    public LoadedAuthoritativeGameHostAdapterPackage LoadDirectory(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        var root = Path.GetFullPath(packageDirectory);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(
                $"Game-host adapter package directory '{packageDirectory}' does not exist.");
        RejectReparsePoints(root, packageDirectory);
        var manifestPath = Path.Combine(root, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException(
                $"Game-host adapter package '{root}' does not contain {ManifestFileName}.",
                manifestPath);

        AdapterPackageDocument? document;
        try {
            document = JsonSerializer.Deserialize<AdapterPackageDocument>(
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
            || string.IsNullOrWhiteSpace(document.AssemblySha256)
            || string.IsNullOrWhiteSpace(document.FactoryType)) {
            throw PackageError(
                manifestPath,
                $"requires formatVersion {CurrentFormatVersion}, id, version, assembly, assemblySha256, and factoryType");
        }
        if (!ContentSemanticVersion.TryParse(document.Version, out var packageVersion))
            throw PackageError(manifestPath, "version must contain three unsigned integers");

        var assemblyPath = ResolvePackageAssembly(root, document.Assembly, manifestPath);
        var expectedHash = NormalizeSha256(document.AssemblySha256, manifestPath);
        var actualHash = VerifyAssemblyHash(assemblyPath, expectedHash, manifestPath);
        var dependencies = LoadDependencies(
            root,
            assemblyPath,
            document.Dependencies,
            manifestPath);

        var loadContext = new AdapterAssemblyLoadContext(
            assemblyPath,
            sharedAssemblies,
            dependencies.ToDictionary(
                dependency => dependency.Name,
                dependency => dependency.Path,
                StringComparer.OrdinalIgnoreCase));
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
        ValidateReferenceClosure(assembly, loadContext, manifestPath);

        var factoryType = assembly.GetType(document.FactoryType, throwOnError: false, ignoreCase: false);
        if (factoryType is null)
            throw PackageError(manifestPath, $"declares missing factory type '{document.FactoryType}'");
        if (!typeof(IAuthoritativeGameHostAdapterFactory).IsAssignableFrom(factoryType)
            || factoryType.IsAbstract
            || !(factoryType.IsPublic || factoryType.IsNestedPublic))
            throw PackageError(
                manifestPath,
                $"type '{document.FactoryType}' must be a public, concrete {nameof(IAuthoritativeGameHostAdapterFactory)}");
        if (factoryType.GetConstructor(Type.EmptyTypes) is null)
            throw PackageError(
                manifestPath,
                $"factory type '{document.FactoryType}' requires a public parameterless constructor");

        IAuthoritativeGameHostAdapterFactory factory;
        try {
            factory = (IAuthoritativeGameHostAdapterFactory)Activator.CreateInstance(factoryType)!;
        } catch (Exception exception) when (
            exception is TargetInvocationException or MemberAccessException or InvalidCastException) {
            throw PackageError(
                manifestPath,
                $"could not construct factory type '{document.FactoryType}'",
                exception);
        }
        if (string.IsNullOrWhiteSpace(factory.Id))
            throw PackageError(manifestPath, "the constructed adapter factory has no ID");

        return new LoadedAuthoritativeGameHostAdapterPackage(
            document.Id,
            packageVersion.ToString(),
            assemblyPath,
            actualHash,
            factory);
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
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException(
                $"Game-host adapter package assembly '{assemblyPath}' does not exist.",
                assemblyPath);
        return assemblyPath;
    }

    private static string VerifyAssemblyHash(
        string assemblyPath,
        string expectedHash,
        string manifestPath)
    {
        var assemblyLength = new FileInfo(assemblyPath).Length;
        if (assemblyLength == 0 || assemblyLength > MaximumAssemblyBytes)
            throw PackageError(
                manifestPath,
                $"assembly size must be between 1 and {MaximumAssemblyBytes} bytes");
        string actualHash;
        using (var stream = File.OpenRead(assemblyPath))
            actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            throw PackageError(
                manifestPath,
                $"expected assembly SHA-256 {expectedHash}, but '{assemblyPath}' hashes to {actualHash}");
        return actualHash;
    }

    private IReadOnlyList<PackageDependency> LoadDependencies(
        string root,
        string entryAssemblyPath,
        IReadOnlyList<AdapterPackageDependencyDocument>? documents,
        string manifestPath)
    {
        var values = documents ?? [];
        if (values.Count > 32)
            throw PackageError(manifestPath, "dependencies cannot contain more than 32 assemblies");
        var entryCanonicalPath = Path.GetFullPath(entryAssemblyPath);
        var dependencies = new List<PackageDependency>(values.Count);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in values) {
            if (document is null
                || string.IsNullOrWhiteSpace(document.Assembly)
                || string.IsNullOrWhiteSpace(document.AssemblySha256))
                throw PackageError(manifestPath, "each dependency requires assembly and assemblySha256");
            var path = ResolvePackageAssembly(root, document.Assembly, manifestPath);
            if (string.Equals(path, entryCanonicalPath, StringComparison.OrdinalIgnoreCase)
                || !paths.Add(path))
                throw PackageError(manifestPath, "dependencies must use unique paths distinct from the entry assembly");
            var expectedHash = NormalizeSha256(document.AssemblySha256, manifestPath);
            VerifyAssemblyHash(path, expectedHash, manifestPath);
            AssemblyName dependencyName;
            try {
                dependencyName = AssemblyName.GetAssemblyName(path);
            } catch (Exception exception) when (
                exception is BadImageFormatException or FileLoadException or FileNotFoundException) {
                throw PackageError(manifestPath, $"dependency '{path}' is not a readable managed assembly", exception);
            }
            if (string.IsNullOrWhiteSpace(dependencyName.Name)
                || !names.Add(dependencyName.Name)
                || sharedAssemblies.ContainsKey(dependencyName.Name)
                || AdapterAssemblyLoadContext.IsPlatformAssembly(dependencyName.Name))
                throw PackageError(
                    manifestPath,
                    $"dependency '{path}' has an invalid, duplicate, or reserved assembly name");
            dependencies.Add(new PackageDependency(dependencyName.Name, path));
        }
        return dependencies;
    }

    private static void ValidateReferenceClosure(
        Assembly entryAssembly,
        AdapterAssemblyLoadContext loadContext,
        string manifestPath)
    {
        var pending = new Queue<Assembly>([entryAssembly]);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (pending.Count != 0) {
            var assembly = pending.Dequeue();
            var assemblyName = assembly.GetName().Name;
            if (assemblyName is null || !visited.Add(assemblyName))
                continue;
            foreach (var reference in assembly.GetReferencedAssemblies()) {
                if (loadContext.IsAllowedReference(reference))
                    continue;
                if (!loadContext.TryLoadPackageAssembly(reference, out var dependency))
                    throw PackageError(
                        manifestPath,
                        $"references unsupported private assembly '{reference.Name}'; list every private dependency in dependencies");
                if (!AssemblyName.ReferenceMatchesDefinition(reference, dependency.GetName()))
                    throw PackageError(
                        manifestPath,
                        $"references private assembly '{reference.Name}' with an incompatible version");
                pending.Enqueue(dependency);
            }
        }
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
            || segments.Any(segment => segment is "" or "." or ".."))
            throw PackageError(
                manifestPath,
                "assembly must be a canonical package-relative path without traversal segments; assembly path escapes the package directory or uses an alias");
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
            $"Game-host adapter manifest '{manifestPath}' {detail}.",
            innerException);
    }

    private sealed record AdapterPackageDocument(
        int FormatVersion,
        string? Id,
        string? Version,
        string? Assembly,
        string? AssemblySha256,
        string? FactoryType,
        IReadOnlyList<AdapterPackageDependencyDocument>? Dependencies);

    private sealed record AdapterPackageDependencyDocument(
        string? Assembly,
        string? AssemblySha256);

    private sealed record PackageDependency(string Name, string Path);

    private sealed class AdapterAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly IReadOnlyDictionary<string, Assembly> sharedAssemblies;
        private readonly IReadOnlyDictionary<string, string> packageAssemblies;
        private readonly Dictionary<string, Assembly> loadedPackageAssemblies = new(StringComparer.OrdinalIgnoreCase);
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

        public AdapterAssemblyLoadContext(
            string assemblyPath,
            IReadOnlyDictionary<string, Assembly> sharedAssemblies,
            IReadOnlyDictionary<string, string> packageAssemblies)
            : base($"Godevilution adapter: {Path.GetFileNameWithoutExtension(assemblyPath)}")
        {
            this.sharedAssemblies = sharedAssemblies;
            this.packageAssemblies = packageAssemblies;
        }

        public bool IsAllowedReference(AssemblyName assemblyName)
        {
            return assemblyName.Name is not null
                && (sharedAssemblies.ContainsKey(assemblyName.Name)
                    || PlatformAssemblyNames.Contains(assemblyName.Name));
        }

        public bool TryLoadPackageAssembly(AssemblyName assemblyName, out Assembly assembly)
        {
            if (assemblyName.Name is null
                || !packageAssemblies.ContainsKey(assemblyName.Name)) {
                assembly = null!;
                return false;
            }
            assembly = Load(assemblyName)!;
            return assembly is not null;
        }

        public static bool IsPlatformAssembly(string name)
            => PlatformAssemblyNames.Contains(name);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is null)
                return null;
            if (sharedAssemblies.TryGetValue(assemblyName.Name, out var shared))
                return shared;
            if (!packageAssemblies.TryGetValue(assemblyName.Name, out var path))
                return null;
            if (loadedPackageAssemblies.TryGetValue(assemblyName.Name, out var loaded))
                return loaded;
            using var stream = File.OpenRead(path);
            var assembly = LoadFromStream(stream);
            loadedPackageAssemblies[assemblyName.Name] = assembly;
            return assembly;
        }
    }
}
