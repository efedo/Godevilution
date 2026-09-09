using Devilution.Data;

namespace Devilution.Diablo;

/** One content-owned inventory requirement for an authoritative object. */
public sealed record AuthoritativeObjectItemGateDefinition(
    uint EntityId,
    uint ItemId,
    bool ConsumeItem = true);

/** Validated item requirements applied at the object interaction boundary. */
public sealed class AuthoritativeObjectItemGateCatalog
{
    private readonly IReadOnlyDictionary<uint, AuthoritativeObjectItemGateDefinition> definitions;

    public AuthoritativeObjectItemGateCatalog(
        IEnumerable<AuthoritativeObjectItemGateDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(definition => definition.EntityId == 0 || definition.ItemId == 0))
            throw new InvalidDataException("Object item gates contain invalid identities.");
        if (values.GroupBy(definition => definition.EntityId)
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Object item gates contain duplicate object entities.");
        }
        this.definitions = values.ToDictionary(definition => definition.EntityId);
    }

    public IReadOnlyList<AuthoritativeObjectItemGateDefinition> Definitions
        => definitions.Values.OrderBy(definition => definition.EntityId).ToArray();

    public bool TryGet(
        uint entityId,
        out AuthoritativeObjectItemGateDefinition definition)
        => definitions.TryGetValue(entityId, out definition!);

    /** Validates gated object entities against the active object catalog. */
    public void ValidateObjects(IEnumerable<AuthoritativeWorldObject> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        var objectIds = objects.Select(@object => @object.EntityId).ToHashSet();
        foreach (var definition in definitions.Values) {
            if (!objectIds.Contains(definition.EntityId))
                throw new InvalidDataException(
                    $"Object item gate references unknown object entity {definition.EntityId}.");
        }
    }

    /** Validates required item identities against the active item catalog. */
    public void ValidateItems(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var definition in definitions.Values) {
            if (!items.TryGet(definition.ItemId, out _))
                throw new InvalidDataException(
                    $"Object item gate references unknown item {definition.ItemId}.");
        }
    }

    public static AuthoritativeObjectItemGateCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeObjectItemGateCatalog(table.Rows.Select(row =>
            new AuthoritativeObjectItemGateDefinition(
                row.RequiredUInt32("entity_id"),
                AuthoritativeItemIdentity.Resolve(row, stableIds),
                row.OptionalBoolean("consume_item", true))));
    }
}
