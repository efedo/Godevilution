using System.Text;
using System.Text.Json;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;

namespace Devilution.Server.Stores;

/**
 * Compatibility codec for server-owned session state. Version 1 retains the
 * original JSON envelope containing a base64 Protobuf snapshot; player and
 * world state stay transport-neutral. Opt-in generated profiles use version 2
 * to retain game-owned geometry/route identity outside the live wire. The separate
 * Cathedral depths profile uses version 3 without widening either older envelope.
 */
public sealed class AuthoritativeSaveCodec
{
    // Preserve the public authored-profile format constant and its original bytes.
    public const int CurrentFormatVersion = 1;
    public const int NativeGeneratedFormatVersion = 2;
    public const int CathedralDepthFormatVersion = 3;
    private static readonly string[] GeneratedLevelFields = [
        "CatalogId", "GeneratorId", "GeneratorRevision", "LevelId", "LevelSymbol",
        "DungeonSeed", "Entry", "AttemptSeed", "FinalRandomState", "CoarseFingerprint",
        "FineFingerprint", "RouteFingerprint", "CatalogFingerprint",
    ];
    private static readonly string[] CathedralDepthLevelFields = [
        "CatalogId", "GeneratorId", "GeneratorRevision", "LevelId", "LevelSymbol",
        "Depth", "Policy", "DungeonSeed", "Entry", "AttemptSeed", "FinalRandomState",
        "CoarseFingerprint", "FineFingerprint", "RouteFingerprint", "CatalogFingerprint",
    ];
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly AuthoritativeItemCatalog? items;

    public AuthoritativeSaveCodec(AuthoritativeItemCatalog? items = null)
    {
        this.items = items;
    }

    public string Serialize(DiabloSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.NativeGeneratedWorldIdentity is not null && snapshot.CathedralDepthWorldIdentity is not null)
            throw new InvalidDataException("A save cannot combine L1 and Cathedral depth generation identities.");
        var protocolSnapshot = DiabloProtocolMapper.ToProtocolSnapshot(
            snapshot,
            items: items,
            allowZeroPlayerEntityId: true);
        var snapshotBase64 = Convert.ToBase64String(protocolSnapshot.ToByteArray());
        if (snapshot.CathedralDepthWorldIdentity is not null)
            return JsonSerializer.Serialize(new CathedralDepthAuthoritativeSaveDocument(
                CathedralDepthFormatVersion, snapshotBase64, snapshot.CathedralDepthWorldIdentity));
        // The legacy envelope remains byte-for-byte unchanged for authored profiles.
        // Generated metadata belongs to this game adapter, not the neutral host or wire.
        return snapshot.NativeGeneratedWorldIdentity is null
            ? JsonSerializer.Serialize(new AuthoritativeSaveDocument(CurrentFormatVersion, snapshotBase64))
            : JsonSerializer.Serialize(new GeneratedAuthoritativeSaveDocument(
                NativeGeneratedFormatVersion, snapshotBase64, snapshot.NativeGeneratedWorldIdentity));
    }

    public DiabloSimulationSnapshot Deserialize(string serializedSave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedSave);
        try {
            StrictUtf8.GetBytes(serializedSave);
        } catch (EncoderFallbackException exception) {
            throw new InvalidDataException(
                "The authoritative save contains invalid UTF-16 text.",
                exception);
        }
        AuthoritativeSaveDocument? document;
        try {
            document = JsonSerializer.Deserialize<AuthoritativeSaveDocument>(serializedSave);
        } catch (JsonException exception) {
            throw new InvalidDataException(
                "The authoritative save is not valid JSON.",
                exception);
        }
        if (document is null || string.IsNullOrWhiteSpace(document.SnapshotBase64)) {
            throw new InvalidDataException(
                "The authoritative save format is unsupported or incomplete.");
        }

        return document.FormatVersion switch {
            1 => DeserializeVersionOne(document.SnapshotBase64),
            NativeGeneratedFormatVersion => DeserializeGenerated(serializedSave, document.SnapshotBase64),
            CathedralDepthFormatVersion => DeserializeCathedralDepths(serializedSave, document.SnapshotBase64),
            _ => throw new InvalidDataException(
                $"Authoritative save format {document.FormatVersion} is unsupported."),
        };
    }

    private static DiabloSimulationSnapshot DeserializeGenerated(string serializedSave, string snapshotBase64)
    {
        GeneratedAuthoritativeSaveDocument? document;
        try {
            using var json = JsonDocument.Parse(serializedSave);
            RequireFields(json.RootElement, ["FormatVersion", "SnapshotBase64", "NativeGeneratedWorldIdentity"]);
            var identity = json.RootElement.GetProperty("NativeGeneratedWorldIdentity");
            RequireFields(identity, ["Levels", "RouteFingerprint"]);
            var levels = identity.GetProperty("Levels");
            if (levels.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Generated save level identities must be an explicit array.");
            foreach (var level in levels.EnumerateArray())
                RequireFields(level, GeneratedLevelFields);
            document = JsonSerializer.Deserialize<GeneratedAuthoritativeSaveDocument>(serializedSave);
        } catch (JsonException exception) {
            throw new InvalidDataException("The generated authoritative save metadata is not valid JSON.", exception);
        }
        if (document?.NativeGeneratedWorldIdentity is null)
            throw new InvalidDataException("Generated authoritative saves require their geometry and route identity.");
        return DeserializeVersionOne(snapshotBase64) with {
            NativeGeneratedWorldIdentity = document.NativeGeneratedWorldIdentity,
        };
    }

    private static void RequireFields(JsonElement value, IReadOnlyList<string> fields)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Generated save metadata must be an explicit object.");
        var properties = value.EnumerateObject().Select(property => property.Name).ToArray();
        foreach (var field in fields) {
            if (properties.Count(property => string.Equals(property, field, StringComparison.Ordinal)) != 1)
                throw new InvalidDataException($"Generated save metadata requires exactly one '{field}' field.");
        }
    }

    private static DiabloSimulationSnapshot DeserializeCathedralDepths(string serializedSave, string snapshotBase64)
    {
        CathedralDepthAuthoritativeSaveDocument? document;
        try {
            using var json = JsonDocument.Parse(serializedSave);
            RequireExactFields(json.RootElement, ["FormatVersion", "SnapshotBase64", "CathedralDepthWorldIdentity"]);
            var identity = json.RootElement.GetProperty("CathedralDepthWorldIdentity");
            RequireExactFields(identity, ["Levels", "RouteFingerprint"]);
            var levels = identity.GetProperty("Levels");
            if (levels.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Cathedral depth save identities must be an explicit array.");
            foreach (var level in levels.EnumerateArray())
                RequireExactFields(level, CathedralDepthLevelFields);
            document = JsonSerializer.Deserialize<CathedralDepthAuthoritativeSaveDocument>(serializedSave);
        } catch (JsonException exception) {
            throw new InvalidDataException("The Cathedral depth authoritative save metadata is not valid JSON.", exception);
        }
        if (document?.CathedralDepthWorldIdentity is null)
            throw new InvalidDataException("Cathedral depth saves require their geometry and route identity.");
        return DeserializeVersionOne(snapshotBase64) with {
            CathedralDepthWorldIdentity = document.CathedralDepthWorldIdentity,
        };
    }

    private static void RequireExactFields(JsonElement value, IReadOnlyList<string> fields)
    {
        RequireFields(value, fields);
        if (value.EnumerateObject().Count() != fields.Count)
            throw new InvalidDataException("Cathedral depth save metadata contains unsupported fields.");
    }

    private sealed record GeneratedAuthoritativeSaveDocument(
        int FormatVersion,
        string SnapshotBase64,
        DiabloNativeGeneratedWorldIdentity NativeGeneratedWorldIdentity);

    private sealed record CathedralDepthAuthoritativeSaveDocument(
        int FormatVersion,
        string SnapshotBase64,
        DiabloCathedralDepthWorldIdentity CathedralDepthWorldIdentity);

    private static DiabloSimulationSnapshot DeserializeVersionOne(string snapshotBase64)
    {
        byte[] snapshotBytes;
        try {
            snapshotBytes = Convert.FromBase64String(snapshotBase64);
        } catch (FormatException exception) {
            throw new InvalidDataException(
                "The authoritative save snapshot is not valid Base64.",
                exception);
        }
        if (!string.Equals(
                Convert.ToBase64String(snapshotBytes),
                snapshotBase64,
                StringComparison.Ordinal)) {
            throw new InvalidDataException(
                "The authoritative save snapshot must use canonical Base64 encoding.");
        }

        try {
            return DiabloProtocolMapper.FromProtocolSnapshot(
                Snapshot.Parser.ParseFrom(snapshotBytes));
        } catch (InvalidProtocolBufferException exception) {
            throw new InvalidDataException(
                "The authoritative save snapshot is not valid Protobuf.",
                exception);
        }
    }
}
