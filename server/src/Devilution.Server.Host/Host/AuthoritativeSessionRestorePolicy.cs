namespace Devilution.Server.Host;

/** Selects how a cold session checkpoint is admitted by the host. */
public enum AuthoritativeSessionRestorePolicy
{
    /** Preserve legacy state and receipt sidecar fallback behavior. */
    Compatibility = 0,

    /** Require one current-format checkpoint bound to the active ruleset identity. */
    CurrentRulesetOnly = 1,
}
