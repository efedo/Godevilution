using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Devilution.Client.Diagnostics;

/** Explicit provenance for the client assemblies used by a bounded runtime measurement. */
public static class ClientRuntimeAssemblyProvenance
{
    public static IReadOnlyList<ClientRuntimeAssemblyObservation> Capture(
        IEnumerable<ClientRuntimeAssemblyInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var requested = inputs.ToArray();
        if (requested.Length == 0)
            throw new InvalidDataException("At least one explicit assembly role is required.");
        RequireUniqueRoles(requested.Select(input => input?.Role), "assembly inputs");

        var observations = new List<ClientRuntimeAssemblyObservation>(requested.Length);
        foreach (var input in requested) {
            if (input is null || input.Assembly is null)
                throw new InvalidDataException("An explicit assembly input is incomplete.");
            observations.Add(CaptureOne(input.Role, input.Assembly));
        }
        return observations;
    }

    /** Captures explicit staged images and binds them to the loaded assemblies by identity and MVID. */
    public static IReadOnlyList<ClientRuntimeAssemblyObservation> CaptureStagedImages(
        IEnumerable<ClientRuntimeAssemblyImageInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var requested = inputs.ToArray();
        if (requested.Length == 0)
            throw new InvalidDataException("At least one explicit staged assembly role is required.");
        RequireUniqueRoles(requested.Select(input => input?.Role), "staged assembly inputs");

        var observations = new List<ClientRuntimeAssemblyObservation>(requested.Length);
        foreach (var input in requested) {
            if (input is null || input.Assembly is null)
                throw new InvalidDataException("An explicit staged assembly input is incomplete.");
            observations.Add(CaptureStagedImageOne(input.Role, input.Assembly, input.StagedImagePath));
        }
        return observations;
    }

    public static void Validate(
        IEnumerable<ClientRuntimeAssemblyObservation> observations,
        IEnumerable<ClientRuntimeAssemblyManifestEntry> manifest)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(manifest);
        var actual = observations.ToArray();
        var expected = manifest.ToArray();
        if (expected.Length == 0)
            throw new InvalidDataException("The expected assembly manifest is empty.");
        RequireUniqueRoles(actual.Select(item => item?.Role), "assembly observations");
        RequireUniqueRoles(expected.Select(item => item?.Role), "expected assembly manifest");
        if (actual.Length != expected.Length)
            throw new InvalidDataException("The observed assembly roles do not match the expected manifest.");

        var observationsByRole = actual.ToDictionary(item => item.Role, StringComparer.Ordinal);
        foreach (var entry in expected) {
            ValidateManifestEntry(entry);
            if (!observationsByRole.TryGetValue(entry.Role, out var observation))
                throw new InvalidDataException("An expected assembly role is missing from the observations.");
            ValidateObservation(observation);
            if (!string.Equals(observation.AssemblyName, entry.AssemblyName, StringComparison.Ordinal)
                || !string.Equals(observation.AssemblyVersion, entry.AssemblyVersion, StringComparison.Ordinal)
                || !string.Equals(observation.FullName, entry.FullName, StringComparison.Ordinal)
                || !Guid.TryParseExact(observation.ModuleVersionId, "D", out var actualMvid)
                || !Guid.TryParseExact(entry.ModuleVersionId, "D", out var expectedMvid)
                || actualMvid != expectedMvid
                || !string.Equals(observation.Configuration, entry.Configuration, StringComparison.Ordinal)
                || !FixedTimeHashEquals(observation.Sha256, entry.Sha256)
                || !string.Equals(observation.FileName, entry.FileName, StringComparison.Ordinal)
                || observation.ImageBindingKind != entry.ImageBindingKind
                || !FixedTimeHashEquals(
                    observation.CanonicalLocationSha256,
                    entry.CanonicalLocationSha256))
                throw new InvalidDataException("A loaded assembly does not match its expected provenance.");

            if (entry.RequireOptimized
                && (!IsOptimizedConfiguration(entry.Configuration)
                    || !observation.HasDebuggableAttribute
                    || observation.OptimizationsDisabled != false))
                throw new InvalidDataException("The loaded assembly does not satisfy the optimized configuration requirement.");
        }
    }

    /** Hashes a normalized absolute assembly path without exposing the path itself. */
    public static string CanonicalLocationSha256(string location)
    {
        if (string.IsNullOrWhiteSpace(location) || !Path.IsPathFullyQualified(location))
            throw new InvalidDataException("An absolute assembly location is required.");
        string canonicalLocation;
        try {
            canonicalLocation = Path.TrimEndingDirectorySeparator(Path.GetFullPath(location));
        } catch (Exception exception) when (exception is ArgumentException
            or IOException or UnauthorizedAccessException or NotSupportedException) {
            throw new InvalidDataException("The assembly location could not be normalized.");
        }
        if (OperatingSystem.IsWindows())
            canonicalLocation = canonicalLocation.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalLocation)));
    }

    private static ClientRuntimeAssemblyObservation CaptureOne(string role, Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(role) || !string.Equals(role, role.Trim(), StringComparison.Ordinal))
            throw new InvalidDataException("Assembly roles must be nonempty and canonical.");
        if (assembly.IsDynamic)
            throw new InvalidDataException("Dynamic assemblies cannot provide file provenance.");

        string location;
        string fileName;
        string fileSha256;
        string locationSha256;
        try {
            location = assembly.Location;
            if (string.IsNullOrWhiteSpace(location) || !Path.IsPathFullyQualified(location))
                throw new InvalidDataException("A loaded assembly has no absolute file location.");
            location = Path.GetFullPath(location);
            fileName = Path.GetFileName(location);
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(location))
                throw new InvalidDataException("A loaded assembly file is unavailable.");
            using var stream = new FileStream(location, FileMode.Open, FileAccess.Read, FileShare.Read);
            fileSha256 = Convert.ToHexString(SHA256.HashData(stream));
            locationSha256 = CanonicalLocationSha256(location);
        } catch (InvalidDataException) {
            throw;
        } catch (Exception exception) when (exception is ArgumentException
            or IOException or UnauthorizedAccessException or NotSupportedException
            or System.Security.SecurityException) {
            throw new InvalidDataException("Loaded assembly file provenance could not be read.");
        }

        var name = assembly.GetName();
        var configurationAttribute = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
        var debuggableAttribute = assembly.GetCustomAttribute<DebuggableAttribute>();
        int? modes = debuggableAttribute is null ? null : (int)debuggableAttribute.DebuggingFlags;
        bool? optimizationsDisabled = modes is { } value
            ? (value & (int)DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0
            : null;
        return new ClientRuntimeAssemblyObservation(
            role,
            name.Name ?? string.Empty,
            name.Version?.ToString() ?? string.Empty,
            assembly.FullName ?? string.Empty,
            assembly.ManifestModule.ModuleVersionId.ToString("D"),
            configurationAttribute is not null,
            configurationAttribute?.Configuration,
            fileSha256,
            fileName,
            locationSha256,
            debuggableAttribute is not null,
            modes,
            optimizationsDisabled,
            ClientRuntimeAssemblyImageBindingKind.LoadedFile);
    }

    private static ClientRuntimeAssemblyObservation CaptureStagedImageOne(
        string role,
        Assembly assembly,
        string stagedImagePath)
    {
        if (string.IsNullOrWhiteSpace(role) || !string.Equals(role, role.Trim(), StringComparison.Ordinal))
            throw new InvalidDataException("Assembly roles must be nonempty and canonical.");
        if (assembly.IsDynamic)
            throw new InvalidDataException("Dynamic assemblies cannot provide staged image provenance.");
        if (string.IsNullOrWhiteSpace(stagedImagePath) || !Path.IsPathFullyQualified(stagedImagePath))
            throw new InvalidDataException("An absolute staged assembly image path is required.");

        try {
            var imagePath = Path.GetFullPath(stagedImagePath);
            var imageFileName = Path.GetFileName(imagePath);
            if (string.IsNullOrWhiteSpace(imageFileName) || !File.Exists(imagePath))
                throw new InvalidDataException("The staged assembly image is unavailable.");

            using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
                throw new InvalidDataException("The staged image does not contain managed assembly metadata.");

            var metadata = peReader.GetMetadataReader();
            if (!metadata.IsAssembly)
                throw new InvalidDataException("The staged image metadata does not describe an assembly.");
            var imageAssemblyName = ReadAssemblyName(metadata);
            var loadedAssemblyName = assembly.GetName();
            if (!string.Equals(imageAssemblyName.Name, loadedAssemblyName.Name, StringComparison.Ordinal)
                || imageAssemblyName.Version != loadedAssemblyName.Version
                || !string.Equals(imageAssemblyName.FullName, assembly.FullName, StringComparison.Ordinal))
                throw new InvalidDataException("The staged image identity does not match the loaded assembly.");

            var imageModule = metadata.GetModuleDefinition();
            var imageMvid = metadata.GetGuid(imageModule.Mvid);
            var loadedMvid = assembly.ManifestModule.ModuleVersionId;
            if (imageMvid == Guid.Empty || imageMvid != loadedMvid)
                throw new InvalidDataException("The staged image MVID does not match the loaded assembly.");

            var loadedLocation = assembly.Location;
            var imageBindingKind = string.IsNullOrWhiteSpace(loadedLocation)
                ? ClientRuntimeAssemblyImageBindingKind.StagedImageWithMatchingMvid
                : ClientRuntimeAssemblyImageBindingKind.LoadedFile;
            if (!string.IsNullOrWhiteSpace(loadedLocation)
                && (!Path.IsPathFullyQualified(loadedLocation)
                    || !PathsEqual(imagePath, Path.GetFullPath(loadedLocation))))
                throw new InvalidDataException("The staged image path does not match the loaded assembly location.");

            stream.Position = 0;
            var fileSha256 = Convert.ToHexString(SHA256.HashData(stream));
            var configurationAttribute = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var debuggableAttribute = assembly.GetCustomAttribute<DebuggableAttribute>();
            int? modes = debuggableAttribute is null ? null : (int)debuggableAttribute.DebuggingFlags;
            bool? optimizationsDisabled = modes is { } value
                ? (value & (int)DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0
                : null;

            return new ClientRuntimeAssemblyObservation(
                role,
                imageAssemblyName.Name ?? string.Empty,
                imageAssemblyName.Version?.ToString() ?? string.Empty,
                imageAssemblyName.FullName ?? string.Empty,
                imageMvid.ToString("D"),
                configurationAttribute is not null,
                configurationAttribute?.Configuration,
                fileSha256,
                imageFileName,
                CanonicalLocationSha256(imagePath),
                debuggableAttribute is not null,
                modes,
                optimizationsDisabled,
                imageBindingKind);
        } catch (InvalidDataException) {
            throw;
        } catch (Exception exception) when (exception is ArgumentException
            or IOException or UnauthorizedAccessException or NotSupportedException
            or BadImageFormatException or System.Security.SecurityException
            or InvalidOperationException) {
            throw new InvalidDataException("Staged assembly image provenance could not be read or verified.");
        }
    }

    private static AssemblyName ReadAssemblyName(MetadataReader metadata)
    {
        var definition = metadata.GetAssemblyDefinition();
        var name = new AssemblyName {
            Name = metadata.GetString(definition.Name),
            Version = definition.Version,
            CultureName = definition.Culture.IsNil ? string.Empty : metadata.GetString(definition.Culture),
        };
        if (!definition.PublicKey.IsNil)
            name.SetPublicKey(metadata.GetBlobBytes(definition.PublicKey));
        else
            name.SetPublicKeyToken(Array.Empty<byte>());
        if ((definition.Flags & System.Reflection.AssemblyFlags.Retargetable) != 0)
            name.Flags |= AssemblyNameFlags.Retargetable;
        if ((definition.Flags & System.Reflection.AssemblyFlags.WindowsRuntime) != 0)
            name.ContentType = AssemblyContentType.WindowsRuntime;
        return name;
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void ValidateManifestEntry(ClientRuntimeAssemblyManifestEntry entry)
    {
        if (entry is null
            || string.IsNullOrWhiteSpace(entry.Role)
            || !string.Equals(entry.Role, entry.Role.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(entry.AssemblyName)
            || !Version.TryParse(entry.AssemblyVersion, out _)
            || !TryParseFullName(entry.FullName, entry.AssemblyName, entry.AssemblyVersion)
            || !Guid.TryParseExact(entry.ModuleVersionId, "D", out var moduleVersionId)
            || moduleVersionId == Guid.Empty
            || !IsSha256(entry.Sha256)
            || !IsSha256(entry.CanonicalLocationSha256)
            || string.IsNullOrWhiteSpace(entry.FileName)
            || !string.Equals(entry.FileName, Path.GetFileName(entry.FileName), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(entry.Configuration)
            || !string.Equals(entry.Configuration, entry.Configuration.Trim(), StringComparison.Ordinal)
            || !Enum.IsDefined(entry.ImageBindingKind))
            throw new InvalidDataException("The expected assembly manifest contains malformed provenance.");
        if (entry.RequireOptimized && !IsOptimizedConfiguration(entry.Configuration))
            throw new InvalidDataException("Optimized assemblies must declare Release or ExportRelease configuration.");
    }

    private static void ValidateObservation(ClientRuntimeAssemblyObservation observation)
    {
        if (observation is null
            || string.IsNullOrWhiteSpace(observation.Role)
            || !string.Equals(observation.Role, observation.Role.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(observation.AssemblyName)
            || !Version.TryParse(observation.AssemblyVersion, out _)
            || !TryParseFullName(observation.FullName, observation.AssemblyName, observation.AssemblyVersion)
            || !Guid.TryParseExact(observation.ModuleVersionId, "D", out var moduleVersionId)
            || moduleVersionId == Guid.Empty
            || !observation.HasConfigurationAttribute
            || string.IsNullOrWhiteSpace(observation.Configuration)
            || !string.Equals(observation.Configuration, observation.Configuration.Trim(), StringComparison.Ordinal)
            || !IsSha256(observation.Sha256)
            || !IsSha256(observation.CanonicalLocationSha256)
            || string.IsNullOrWhiteSpace(observation.FileName)
            || !string.Equals(observation.FileName, Path.GetFileName(observation.FileName), StringComparison.Ordinal))
            throw new InvalidDataException("The observed assembly provenance is incomplete or malformed.");
        if (!Enum.IsDefined(observation.ImageBindingKind))
            throw new InvalidDataException("The observed assembly image binding kind is unknown.");

        var modesConsistent = observation.HasDebuggableAttribute
            ? observation.DebuggableModes is { } modes
                && observation.OptimizationsDisabled
                    == ((modes & (int)DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0)
            : observation.DebuggableModes is null && observation.OptimizationsDisabled is null;
        if (!modesConsistent)
            throw new InvalidDataException("The observed DebuggableAttribute metadata is inconsistent.");
    }

    private static bool TryParseFullName(string? fullName, string? name, string? version)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return false;
        try {
            var parsed = new AssemblyName(fullName);
            return string.Equals(parsed.Name, name, StringComparison.Ordinal)
                && string.Equals(parsed.Version?.ToString(), version, StringComparison.Ordinal);
        } catch (Exception exception) when (exception is ArgumentException or IOException) {
            return false;
        }
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsOptimizedConfiguration(string configuration)
        => string.Equals(configuration, "Release", StringComparison.Ordinal)
            || string.Equals(configuration, "ExportRelease", StringComparison.Ordinal);

    private static bool FixedTimeHashEquals(string? actual, string? expected)
        => IsSha256(actual) && IsSha256(expected)
            && CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actual!), Convert.FromHexString(expected!));

    private static void RequireUniqueRoles(IEnumerable<string?> roles, string source)
    {
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles) {
            if (string.IsNullOrWhiteSpace(role)
                || !string.Equals(role, role.Trim(), StringComparison.Ordinal)
                || !observed.Add(role))
                throw new InvalidDataException($"The {source} contain an empty, noncanonical or duplicate role.");
        }
    }
}

public sealed record ClientRuntimeAssemblyInput(string Role, Assembly Assembly);

public sealed record ClientRuntimeAssemblyImageInput(string Role, Assembly Assembly, string StagedImagePath);

/** Expected identity captured by the explicit staging/build manifest.
 * Optimized validation accepts Release or ExportRelease with metadata showing optimizations enabled.
 */
public sealed record ClientRuntimeAssemblyManifestEntry(
    string Role,
    string AssemblyName,
    string AssemblyVersion,
    string FullName,
    string ModuleVersionId,
    string Configuration,
    string Sha256,
    string FileName,
    string CanonicalLocationSha256,
    bool RequireOptimized,
    ClientRuntimeAssemblyImageBindingKind ImageBindingKind = ClientRuntimeAssemblyImageBindingKind.LoadedFile);

/** Public-safe observation; the canonical verified image path is hashed, never serialized. */
public sealed record ClientRuntimeAssemblyObservation(
    string Role,
    string AssemblyName,
    string AssemblyVersion,
    string FullName,
    string ModuleVersionId,
    bool HasConfigurationAttribute,
    string? Configuration,
    string Sha256,
    string FileName,
    string CanonicalLocationSha256,
    bool HasDebuggableAttribute,
    int? DebuggableModes,
    bool? OptimizationsDisabled,
    ClientRuntimeAssemblyImageBindingKind ImageBindingKind = ClientRuntimeAssemblyImageBindingKind.LoadedFile);

/** MVID binding does not prove byte-for-byte identity of an in-memory assembly image. */
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClientRuntimeAssemblyImageBindingKind
{
    LoadedFile,
    StagedImageWithMatchingMvid,
}
