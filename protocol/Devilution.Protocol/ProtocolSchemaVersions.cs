namespace Devilution.Protocol;

/** Named wire-schema versions used by the shared client/server identity contract. */
public static class ProtocolSchemaVersions
{
    /** Protocol v1 aliases content identity to the combined ruleset identity. */
    public const string Legacy = "0.1.0";

    /** Protocol v2 gives content and gameplay modules independent identities. */
    public const string IndependentContentAndRulesetIdentity = "0.2.0";
}
