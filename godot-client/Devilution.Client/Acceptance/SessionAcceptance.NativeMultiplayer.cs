using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;

namespace Devilution.Client.Acceptance;

/** Batch 1.5a: two real clients create different classes in one native multiplayer run and walk into L1. */
public partial class SessionAcceptance
{
    private async Task RunNativeMultiplayerCreateAsync(string directory)
    {
        Require(int.TryParse(Environment.GetEnvironmentVariable("GODEVILUTION_LOCAL_PLAYER"), out var slot)
            && slot is 1 or 2, "Native multiplayer acceptance requires player slot 1 or 2.");
        var peerSlot = 3 - slot;
        var peerDirectory = System.IO.Path.Combine(System.IO.Directory.GetParent(directory)!.FullName, $"player-{peerSlot}");
        // Slot 1 is the Warrior that generates the run; slot 2 joins as a Rogue.
        var classId = slot == 1 ? 0U : 1U;
        var name = $"Native-MP-{slot}";
        Require(!System.IO.File.Exists(System.IO.Path.Combine(directory, "resume-token.txt")),
            "Native multiplayer creation needs a fresh bookmark directory.");
        if (slot == 2)
            await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, "nativemultiplayer-l1.json")),
                "The first participant did not create the run and reach L1.", 300);
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        timingObservedMain = main;
        AddChild(main);
        await Frame();
        await Frame();
        var setup = main.GetNode<CharacterSetupPresentation>("CharacterSetup");
        Require(setup.Visible && !main.TransportConnected, "Each participant must enter through the real setup form.");
        Require(((OptionButton)setup.FindChild("SessionMode", true, false)).Selected == 0,
            "A fresh participant must start in new-character mode.");
        ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = name;
        var selector = (OptionButton)setup.FindChild("CharacterClass", true, false);
        selector.Select(selector.GetItemIndex((int)classId));
        await Click(((Button)setup.FindChild("Connect", true, false)).GetGlobalRect().GetCenter());
        await Until(() => main.TransportConnected && main.ObservedSnapshot?.Players.Any(player => player.CharacterName == name) == true,
            "No authoritative native multiplayer snapshot arrived.", 120);
        var identity = main.ObservedIdentity!;
        var local = main.ObservedSnapshot!.Players.Single(player => player.CharacterName == name);
        Require(identity.GameMode == GameMode.Multiplayer && local.CharacterClassId == classId && local.LevelId == 0 && local.Life > 0,
            "The participant did not enter native multiplayer Town as its selected class.");
        // Settled() assumes one player; the peer is also listed here.
        await Until(() => main.ObservedSnapshot!.Players.Single(player => player.CharacterName == name) is { } settling
            && AuthoritativePlayerSettlement.IsSettled(settling, main.ObservedPendingCommandCount),
            "The native multiplayer Town did not settle.");
        local = main.ObservedSnapshot!.Players.Single(player => player.CharacterName == name);
        var routes = main.ObservedSnapshot.LevelGeometry.Single(level => level.LevelId == 0)
            .WarpTriggers.OrderBy(route => route.WarpId).ToArray();
        var index = Array.FindIndex(routes, route => route.Available && route.TargetLevelId == 1);
        Require(index >= 0, "Native multiplayer Town did not publish the Cathedral stairs.");
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await SelectTab(sections, 4);
        await SelectRoute((OptionButton)main.FindChild("Routes", true, false), index);
        await ClickControl(main, sections, 4, "Travel");
        await Until(() => {
            var player = main.ObservedSnapshot!.Players.Single(candidate => candidate.CharacterName == name);
            Require(player.Life > 0, "The participant died walking to the Cathedral.");
            return player.LevelId == 1 && main.ObservedPendingCommandCount == 0;
        }, "The participant did not reach native multiplayer L1.", 180);
        var arrived = main.ObservedSnapshot!;
        local = arrived.Players.Single(player => player.CharacterName == name);
        var monsters = arrived.Monsters.Where(monster => monster.LevelId == 1).ToArray();
        Require(monsters.Length > 0, "Native multiplayer L1 has no generated monsters.");
        WriteEvidence(directory, "nativemultiplayer-l1.json", new { Slot = slot, local.EntityId, local.PositionX, local.PositionY });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, "nativemultiplayer-l1.json"))
            && main.ObservedSnapshot!.Players.Count == 2 && main.ObservedSnapshot.Players.All(player => player.LevelId == 1),
            "Both participants did not become visible together on L1.", 300);
        var final = main.ObservedSnapshot!;
        var peer = final.Players.Single(player => player.CharacterName == $"Native-MP-{peerSlot}");
        Require(peer.EntityId != local.EntityId && peer.CharacterClassId == (slot == 1 ? 1U : 0U),
            "The peer participant's identity or class is wrong.");
        WriteEvidence(directory, "nativemultiplayercreate.json", new {
            Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash, Slot = slot, ClassId = classId, local.EntityId,
            PeerEntityId = peer.EntityId, PeerClassId = peer.CharacterClassId, LevelId = 1U,
            L1Monsters = monsters.Length, MaxMonsterHitPointsQ6 = monsters.Max(monster => monster.HitPointsQ6),
        });
        // Keep both scenes connected until each has observed the shared L1 pair.
        WriteEvidence(directory, "nativemultiplayer-done.json", new { Done = true });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, "nativemultiplayer-done.json")),
            "The peer did not confirm the shared L1 pair.", 120);
    }
}
