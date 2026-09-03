using Devilution.Data;

namespace Devilution.Diablo;

/** Versioned external table contract consumed by the first-party Diablo module. */
public static class DiabloContentSchema
{
    public static ContentSchemaIdentity Current { get; } = new(
        "devilution.diablo",
        "1.447.0");

    private static ContentSchemaIdentity PreviousAdditive { get; } = new(
        "devilution.diablo",
        "1.446.0");

    /** Accepts the immediately previous additive contract for pack compatibility. */
    public static bool IsCompatible(ContentSchemaIdentity schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return schema == Current || schema == PreviousAdditive;
    }
}
