using Devilution.Data;

namespace Devilution.Diablo;

/** Versioned external table contract consumed by the first-party Diablo module. */
public static class DiabloContentSchema
{
    public static ContentSchemaIdentity Current { get; } = new(
        "devilution.diablo",
        "0.59.0");
}
