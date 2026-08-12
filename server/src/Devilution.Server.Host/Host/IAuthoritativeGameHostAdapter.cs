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

/** Combined game-facing boundary consumed by the generic authoritative host. */
public interface IAuthoritativeGameHostAdapter
    : IAuthoritativeCommandExecutor,
        IAuthoritativeTickExecutor,
        IAuthoritativeSnapshotProvider,
        IAuthoritativeEventProvider,
        IAuthoritativeSaveProvider,
        IAuthoritativeParticipantInitializer
{
}

/** Game-neutral new-participant selection supplied before initial state access. */
public sealed record AuthoritativeParticipantSelection(uint? ArchetypeId)
{
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
    /** Host-owned seed for the selected module's deterministic simulation. */
    public uint RandomSeed { get; init; }

    public AuthoritativeGameMode GameMode { get; init; } = AuthoritativeGameMode.SinglePlayer;

    public AuthoritativePlayerStartConfiguration Validate()
    {
        if (Life < 0)
            throw new ArgumentOutOfRangeException(nameof(Life));
        if (Mana < 0)
            throw new ArgumentOutOfRangeException(nameof(Mana));
        if (!Enum.IsDefined(GameMode))
            throw new ArgumentOutOfRangeException(nameof(GameMode));
        return this;
    }
}
