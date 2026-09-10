using Devilution.Data;

namespace Devilution.Diablo;

/** Optional additive presentation lifecycle and family metadata for authored objects. */
public sealed record AuthoritativeWorldObjectPresentationDefinition(
    uint EntityId,
    bool HideWhenActivated = true,
    int AnimationFrame = 0,
    int ActivatedAnimationFrame = 0,
    AuthoritativeObjectPresentationKind PresentationKind
        = AuthoritativeObjectPresentationKind.Unspecified)
{
    public AuthoritativeWorldObjectPresentationDefinition Validate()
    {
        if (EntityId == 0
            || !Enum.IsDefined(PresentationKind)
            || !AuthoritativeWorldObject.IsValidPresentation(
                HideWhenActivated,
                AnimationFrame,
                ActivatedAnimationFrame)) {
            throw new InvalidDataException(
                $"Object presentation definition {EntityId} contains invalid lifecycle or family metadata.");
        }
        return this;
    }
}

/** Validated object presentation metadata loaded from an optional sidecar table. */
public sealed class AuthoritativeWorldObjectPresentationCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "entity_id",
        "hide_when_activated",
        "animation_frame",
        "activated_animation_frame",
        "presentation_kind",
    ];

    private readonly IReadOnlyList<AuthoritativeWorldObjectPresentationDefinition> definitions;

    public AuthoritativeWorldObjectPresentationCatalog(
        IEnumerable<AuthoritativeWorldObjectPresentationDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions
            .Select(definition => definition.Validate())
            .OrderBy(definition => definition.EntityId)
            .ToArray();
        if (values.Select(definition => definition.EntityId).Distinct().Count() != values.Length) {
            throw new InvalidDataException(
                "Object presentation definitions must contain unique entity IDs.");
        }
        this.definitions = values;
    }

    public IReadOnlyList<AuthoritativeWorldObjectPresentationDefinition> Definitions
        => definitions;

    /** Rejects partial object-presentation metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production object presentation table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.RequiredUInt32("entity_id");
            _ = row.RequiredBoolean("hide_when_activated");
            _ = row.RequiredInt32("animation_frame");
            _ = row.RequiredInt32("activated_animation_frame");
            _ = row.Required("presentation_kind");
        }
    }

    public static AuthoritativeWorldObjectPresentationCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeWorldObjectPresentationCatalog(table.Rows.Select(row =>
            new AuthoritativeWorldObjectPresentationDefinition(
                row.RequiredUInt32("entity_id"),
                row.OptionalBoolean("hide_when_activated", true),
                row.OptionalInt32("animation_frame"),
                row.OptionalInt32("activated_animation_frame"),
                ParsePresentationKind(row))));
    }

    /** Applies all definitions atomically to authored objects and templates. */
    public void Apply(IEnumerable<AuthoritativeWorldObject> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        var targets = objects.ToDictionary(@object => @object.EntityId);
        var updates = definitions.Select(definition => {
            if (!targets.TryGetValue(definition.EntityId, out var target)) {
                throw new InvalidDataException(
                    $"Object presentation definition {definition.EntityId} references an unknown object entity.");
            }
            return (target, definition);
        }).ToArray();

        foreach (var (target, definition) in updates) {
            target.HideWhenActivated = definition.HideWhenActivated;
            target.AnimationFrame = definition.AnimationFrame;
            target.ActivatedAnimationFrame = definition.ActivatedAnimationFrame;
            target.PresentationKind = definition.PresentationKind;
        }
    }

    private static AuthoritativeObjectPresentationKind ParsePresentationKind(TsvRow row)
    {
        if (!row.TryGet("presentation_kind", out var value)
            || string.IsNullOrWhiteSpace(value))
            return AuthoritativeObjectPresentationKind.Unspecified;
        return value.Trim() switch {
            "Unspecified" or "unspecified" => AuthoritativeObjectPresentationKind.Unspecified,
            "Generic" or "generic" => AuthoritativeObjectPresentationKind.Generic,
            "Shrine" or "shrine" => AuthoritativeObjectPresentationKind.Shrine,
            "Quest" or "quest" => AuthoritativeObjectPresentationKind.Quest,
            "Breakable" or "breakable" => AuthoritativeObjectPresentationKind.Breakable,
            "Trap" or "trap" => AuthoritativeObjectPresentationKind.Trap,
            "NativeArmorStand" or "native_armor_stand"
                => AuthoritativeObjectPresentationKind.NativeArmorStand,
            "NativeWeaponRack" or "native_weapon_rack"
                => AuthoritativeObjectPresentationKind.NativeWeaponRack,
            _ => throw new InvalidDataException(
                $"Column 'presentation_kind' on TSV row {row.LineNumber} is not a supported object presentation."),
        };
    }
}
