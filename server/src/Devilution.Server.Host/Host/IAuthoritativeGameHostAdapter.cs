using System;
using Devilution.Server.Commands;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;

namespace Devilution.Server.Host;

/** Server-owned mode shared by every participant in one simulation. */
public enum AuthoritativeGameMode
{
    SinglePlayer,
    Multiplayer,
}

/** Native-compatible difficulty shared by server-hosted game modules. */
public enum AuthoritativeGameDifficulty
{
    Normal,
    Nightmare,
    Hell,
}

/** Optional transport capabilities owned by the active game adapter. */
[Flags]
public enum AuthoritativeProtocolFeatures
{
    None = 0,
    TargetCellPathing = 1,
}

/** Combined game-facing boundary consumed by the generic authoritative host. */
public interface IAuthoritativeGameHostAdapter
    : IAuthoritativeCommandExecutor,
        IAuthoritativeTickExecutor,
        IAuthoritativeSnapshotProvider,
        IAuthoritativeEventProvider,
        IAuthoritativeSaveProvider,
        IAuthoritativeParticipantInitializer
{
    /** Additive protocol capabilities; legacy adapters advertise none. */
    AuthoritativeProtocolFeatures SupportedProtocolFeatures
        => AuthoritativeProtocolFeatures.None;
}

/** Game-neutral new-participant selection supplied before initial state access. */
public sealed record AuthoritativeParticipantSelection(uint? ArchetypeId)
{
    /** Nonzero host-owned binding supplied before complete-cohort publication. */
    public uint? EntityId { get; init; }

    /** Optional game-facing character identity requested only for a new participant. */
    public string? CharacterName { get; init; }
}

/** Allows a game adapter to create per-session module state explicitly. */
public interface IAuthoritativeParticipantInitializer
{
    void InitializeParticipant(
        string sessionId,
        AuthoritativeParticipantSelection selection);
}

/** Host-provided starting values interpreted by the selected game adapter. */
public sealed record AuthoritativePlayerStartConfiguration(
    uint? Gold,
    int? Life,
    int? Mana)
{
    /** Optional server-owned primary-attribute points for development starts. */
    public uint? StatPoints { get; init; }

    /** Optional content level override for newly initialized participants. */
    public uint? StartingLevelId { get; init; }

    /** Host-owned seed for the selected module's deterministic simulation. */
    public uint RandomSeed { get; init; }

    public AuthoritativeGameMode GameMode { get; init; } = AuthoritativeGameMode.SinglePlayer;

    public AuthoritativeGameDifficulty Difficulty { get; init; } = AuthoritativeGameDifficulty.Normal;

    /** Optional server-owned encounter replacement for the selected start level. */
    public int? ProceduralMonsterLimit { get; init; }

    /** Optional level override for the procedural encounter replacement. */
    public uint? ProceduralMonsterLevelId { get; init; }

    public AuthoritativePlayerStartConfiguration Validate()
    {
        if (Life < 0)
            throw new ArgumentOutOfRangeException(nameof(Life));
        if (Mana < 0)
            throw new ArgumentOutOfRangeException(nameof(Mana));
        if (!Enum.IsDefined(GameMode))
            throw new ArgumentOutOfRangeException(nameof(GameMode));
        if (!Enum.IsDefined(Difficulty))
            throw new ArgumentOutOfRangeException(nameof(Difficulty));
        if (ProceduralMonsterLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(ProceduralMonsterLimit));
        if (ProceduralMonsterLevelId.HasValue && !ProceduralMonsterLimit.HasValue)
            throw new InvalidOperationException(
                "A procedural monster level requires a procedural monster limit.");
        return this;
    }
}
