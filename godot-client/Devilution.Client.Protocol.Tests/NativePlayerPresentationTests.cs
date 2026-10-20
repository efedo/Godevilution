using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Validates the class-bound native player art projection without Godot resources. */
public sealed class NativePlayerPresentationTests
{
    private const string ContentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string AlternateContentHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void ParsesPartialCatalogAndBindsItToExactContentHashes()
    {
        var json = CatalogJson(
            [Clip(2, "cathedral", "medium", "staff", "stand")],
            """{"spellId":3,"graphic":"lightning"}""");

        Assert.True(NativePlayerCatalog.TryParse(json, out var catalog, out var diagnostics), string.Join(" ", diagnostics));
        Assert.Equal("native-player-tests", catalog!.PackId);
        Assert.Contains(ContentHash, catalog.ContentManifestHashes);
        Assert.True(catalog.SupportsContentManifestHash(ContentHash));
        Assert.False(catalog.SupportsContentManifestHash(null));
        Assert.False(catalog.SupportsContentManifestHash(ContentHash.ToUpperInvariant()));
        Assert.False(catalog.SupportsContentManifestHash(new string('b', 64)));
        Assert.True(catalog.TryResolveLocation(ContentHash, 0, out var town));
        Assert.Equal(NativePlayerLocation.Town, town);
        Assert.True(catalog.TryResolveLocation(ContentHash, 4, out var cathedral));
        Assert.Equal(NativePlayerLocation.Cathedral, cathedral);
        Assert.False(catalog.TryResolveLocation(ContentHash, 5, out _));
        Assert.Single(catalog.Clips);
        Assert.Equal(NativePlayerGraphic.Lightning, catalog.SpellGraphics[3]);
    }

    [Theory]
    [InlineData("contentManifestHashes", "[]")]
    [InlineData("contentManifestHashes", "[\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"]")]
    [InlineData("contentManifestHashes", "[\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"]")]
    [InlineData("spells", "[{\"spellId\":0,\"graphic\":\"fire\"}]")]
    [InlineData("spells", "[{\"spellId\":3,\"graphic\":\"cold\"}]")]
    public void RefusesInvalidContentBindingsAndSpellMappings(string property, string value)
    {
        var valid = CatalogJson([Clip(0, "cathedral", "light", "unarmed", "stand")]);
        var replaced = property == "contentManifestHashes"
            ? valid.Replace($"\"contentManifestHashes\":[\"{ContentHash}\"]", $"\"contentManifestHashes\":{value}", StringComparison.Ordinal)
            : valid.Replace("\"spells\":[]", $"\"spells\":{value}", StringComparison.Ordinal);

        Assert.False(NativePlayerCatalog.TryParse(replaced, out var catalog, out var diagnostics));
        Assert.Null(catalog);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void ResolvesSameNumericLevelByAdmittedContentHashAndExplicitLocation()
    {
        var catalog = ParseWithBindings(
            [Clip(0, "town", "light", "unarmed", "town-stand"),
             Clip(0, "cathedral", "light", "unarmed", "stand")],
            [ContentHash, AlternateContentHash],
            [LevelBinding(ContentHash, 7, "cathedral"), LevelBinding(AlternateContentHash, 7, "town")]);
        var player = NewPlayer(0);
        player.LevelId = 7;

        Assert.True(catalog.TryResolveLocation(ContentHash, player.LevelId, out var cathedralLocation));
        Assert.True(catalog.TryResolveLocation(AlternateContentHash, player.LevelId, out var townLocation));
        Assert.Equal(NativePlayerLocation.Cathedral, cathedralLocation);
        Assert.Equal(NativePlayerLocation.Town, townLocation);
        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, cathedralLocation, 0, out var cathedralSelection));
        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, townLocation, 0, out var townSelection));
        Assert.Equal(NativePlayerGraphic.Stand, cathedralSelection.Graphic);
        Assert.Equal(NativePlayerGraphic.TownStand, townSelection.Graphic);
    }

    [Fact]
    public void RefusesMissingDuplicateAndUnallowlistedLevelBindings()
    {
        var valid = CatalogJson([Clip(0, "cathedral", "light", "unarmed", "stand")]);
        var empty = valid.Replace(
            $"\"levelBindings\":[{DefaultLevelBindings(ContentHash)}]",
            "\"levelBindings\":[]",
            StringComparison.Ordinal);
        Assert.True(NativePlayerCatalog.TryParse(empty, out var emptyCatalog, out var emptyDiagnostics), string.Join(" ", emptyDiagnostics));
        Assert.False(emptyCatalog!.TryResolveLocation(ContentHash, 0, out _));
        Assert.False(NativePlayerCatalog.TryParse(
            valid.Replace($"\"levelBindings\":[{DefaultLevelBindings(ContentHash)}],", string.Empty, StringComparison.Ordinal),
            out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(
            valid.Replace($"\"levelBindings\":[{DefaultLevelBindings(ContentHash)}]", $"\"levelBindings\":[{LevelBinding(ContentHash, 1, "cathedral")},{LevelBinding(ContentHash, 1, "town")}]", StringComparison.Ordinal),
            out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(
            valid.Replace($"\"levelBindings\":[{DefaultLevelBindings(ContentHash)}]", $"\"levelBindings\":[{LevelBinding(AlternateContentHash, 1, "cathedral")}]", StringComparison.Ordinal),
            out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(
            valid.Replace($"\"levelBindings\":[{DefaultLevelBindings(ContentHash)}]", $"\"levelBindings\":[{{\"contentManifestHash\":\"{ContentHash}\",\"levelId\":1,\"location\":\"crypt\"}}]", StringComparison.Ordinal),
            out _, out _));
    }

    [Fact]
    public void RefusesMoreThanTheBoundedLevelBindingCount()
    {
        var bindings = Enumerable.Range(0, NativePlayerCatalog.MaximumLevelBindingCount + 1)
            .Select(levelId => LevelBinding(ContentHash, (uint)levelId, "cathedral"));
        Assert.False(NativePlayerCatalog.TryParse(
            CatalogJson([], levelBindings: bindings),
            out _, out _));
    }

    [Fact]
    public void RejectsUnknownPropertiesDuplicateKeysUnsafePathsAndOversizedAtlases()
    {
        var root = CatalogJson([Clip(0, "cathedral", "light", "unarmed", "stand")]);
        Assert.False(NativePlayerCatalog.TryParse(root.Replace("\"spells\":[]", "\"extra\":1,\"spells\":[]", StringComparison.Ordinal), out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(CatalogJson([
            Clip(0, "cathedral", "light", "unarmed", "stand"),
            Clip(0, "cathedral", "light", "unarmed", "stand")]), out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(CatalogJson([
            Clip(0, "cathedral", "light", "unarmed", "stand", path: "../unsafe.png")]), out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(CatalogJson([
            Clip(0, "cathedral", "light", "unarmed", "stand", frameWidth: 8192, frameHeight: 8192, frames: 10)]), out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(CatalogJson([
            Clip(0, "cathedral", "light", "unarmed", "stand", animationFrames: 0)]), out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(CatalogJson([
            Clip(0, "cathedral", "light", "unarmed", "stand", animationFrames: 5)]), out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(CatalogJson([
            Clip(0, "cathedral", "light", "unarmed", "stand", actionFrame: 4, animationFrames: 3)]), out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(CatalogJson([
            Clip(0, "cathedral", "light", "unarmed", "stand", anchorWidth: 0)]), out _, out _));
        Assert.False(NativePlayerCatalog.TryParse(CatalogJson([
            Clip(0, "cathedral", "light", "unarmed", "stand", frameWidth: 112, anchorWidth: 113)]), out _, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ResolvesEachBaseClassWithNativeDirectionAndNoSnapshotMutation(uint classId)
    {
        var catalog = ParseClips(Clip(classId, "cathedral", "light", "unarmed", "stand"));
        var player = NewPlayer(classId);
        player.FacingDirection = PlayerDirection.NorthEast;
        var before = player.Clone();

        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 9, out var selection));
        Assert.Equal(classId, selection.ClassId);
        Assert.Equal(NativePlayerLocation.Cathedral, selection.Location);
        Assert.Equal<NativePlayerGraphic?>(NativePlayerGraphic.Stand, selection.Graphic);
        Assert.Equal(5, selection.DirectionIndex);
        Assert.Equal<int?>(0, selection.FrameIndex);
        Assert.Equal(NativePlayerPresentationTimingSource.SnapshotTickLoop, selection.TimingSource);
        Assert.Equal(before, player);
    }

    [Fact]
    public void UsesRequirementCheckedRightHandWeaponAndNativeArmorClass()
    {
        var catalog = ParseClips(
            Clip(0, "cathedral", "heavy", "mace-shield", "stand"),
            Clip(0, "cathedral", "medium", "sword", "stand"));
        var player = NewPlayer(0);
        player.Equipment.Add(Equipped(4, 1, weaponClass: true));
        player.Equipment.Add(Equipped(5, 4, weaponClass: true, minimumStrength: 10));
        player.Equipment.Add(Equipped(6, 9, minimumStrength: 10));

        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 0, out var selection));
        Assert.Equal(NativePlayerArmor.Heavy, selection.Armor);
        Assert.Equal(NativePlayerWeapon.Mace, selection.Weapon);

        player.Attributes!.Strength.Current = 4;
        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 0, out selection));
        Assert.Equal(NativePlayerArmor.Light, selection.Armor);
        Assert.Equal(NativePlayerWeapon.Sword, selection.Weapon);

        player.Equipment.Clear();
        player.Equipment.Add(Equipped(4, 5, statFlag: true));
        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 0, out selection));
        Assert.Equal(NativePlayerWeapon.UnarmedShield, selection.Weapon);
    }

    [Fact]
    public void AppliesTownCastAndDeathWeaponNormalizationAndStableSpellBinding()
    {
        var catalog = ParseWithSpells(
            [Clip(0, "town", "light", "unarmed", "lightning"),
             Clip(0, "town", "light", "unarmed-shield", "fire"),
             Clip(0, "town", "light", "unarmed", "death")],
            """{"spellId":3,"graphic":"lightning"}""");
        var player = NewPlayer(0);
        player.LevelId = 0;
        player.Equipment.Add(Equipped(5, 1, weaponClass: true));
        player.CurrentAction = new PlayerActionSnapshot {
            Kind = PlayerActionKind.Cast,
            StartedAtTick = 10,
            EndTick = 20,
            Cast = new CastRequested { SpellId = 3 },
        };

        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Town, 14, out var selection));
        Assert.Equal<NativePlayerGraphic?>(NativePlayerGraphic.Lightning, selection.Graphic);
        Assert.Equal(NativePlayerWeapon.Unarmed, selection.Weapon);
        Assert.Equal(NativePlayerPresentationTimingSource.AuthoritativeActionInterval, selection.TimingSource);

        player.Life = 0;
        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Town, 14, out selection));
        Assert.Equal<NativePlayerGraphic?>(NativePlayerGraphic.Death, selection.Graphic);
        Assert.Equal(NativePlayerWeapon.Unarmed, selection.Weapon);

        player.Life = 64;
        player.Equipment.Add(Equipped(4, 5, statFlag: true));
        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Town, 14, out selection));
        Assert.Equal(NativePlayerWeapon.UnarmedShield, selection.Weapon);
    }

    [Fact]
    public void MissingStableSpellMappingIsExplicitAndDoesNotGuessFromDamageType()
    {
        var catalog = ParseClips(Clip(2, "cathedral", "light", "staff", "stand"));
        var player = NewPlayer(2);
        player.CombatMode = PlayerCombatMode.Spell;
        player.CurrentAction = new PlayerActionSnapshot {
            Kind = PlayerActionKind.Cast,
            Cast = new CastRequested { SpellId = 43 },
        };
        var before = player.Clone();

        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 0, out var selection));
        Assert.Null(selection.Graphic);
        Assert.Null(selection.ClipKey);
        Assert.Equal(NativePlayerPresentationTimingSource.Unresolved, selection.TimingSource);
        Assert.Contains("stable spell id 43", selection.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(before, player);
    }

    [Fact]
    public void ActionIntervalsLoopFramesAndUntimedHitFramesAreReportedHonestly()
    {
        var catalog = ParseClips(
            Clip(1, "cathedral", "light", "unarmed", "walk", frames: 4, ticksPerFrame: 2, actionFrame: 3),
            Clip(1, "cathedral", "light", "unarmed", "hit", frames: 4, ticksPerFrame: 2));
        var player = NewPlayer(1);
        player.CombatMode = PlayerCombatMode.WalkNorthwards;
        player.CurrentAction = new PlayerActionSnapshot {
            Kind = PlayerActionKind.Move,
            StartedAtTick = 10,
            EndTick = 18,
        };

        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 15, out var moving));
        Assert.Equal<int?>(2, moving.FrameIndex);
        Assert.Equal<int?>(3, moving.ActionFrame);
        Assert.Equal(NativePlayerPresentationTimingSource.AuthoritativeActionInterval, moving.TimingSource);

        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 100, out moving));
        Assert.Equal<int?>(3, moving.FrameIndex);

        player.CombatMode = PlayerCombatMode.GotHit;
        player.CurrentAction = null;
        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 15, out var hit));
        Assert.Equal<int?>(0, hit.FrameIndex);
        Assert.Equal(NativePlayerPresentationTimingSource.FirstFrameOnly, hit.TimingSource);
        Assert.Contains("no native frame cursor", hit.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void UsesAnimationFrameCountWithoutChangingAtlasColumnCount()
    {
        var catalog = ParseClips(Clip(
            1,
            "cathedral",
            "light",
            "unarmed",
            "walk",
            frames: 21,
            ticksPerFrame: 1,
            actionFrame: 20,
            animationFrames: 20));
        var clip = Assert.Single(catalog.Clips).Value;
        Assert.Equal(21, clip.FramesPerDirection);
        Assert.Equal(20, clip.EffectiveAnimationFrames);

        var player = NewPlayer(1);
        player.CombatMode = PlayerCombatMode.WalkNorthwards;
        player.CurrentAction = new PlayerActionSnapshot {
            Kind = PlayerActionKind.Move,
            StartedAtTick = 10,
            EndTick = 40,
        };

        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 40, out var actionSelection));
        Assert.Equal<int?>(19, actionSelection.FrameIndex);
        Assert.Equal<int?>(20, actionSelection.ActionFrame);

        player.CurrentAction = null;
        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 20, out var loopSelection));
        Assert.Equal<int?>(0, loopSelection.FrameIndex);

        var legacyCatalog = ParseClips(Clip(1, "cathedral", "light", "unarmed", "stand", frames: 21));
        var legacyClip = Assert.Single(legacyCatalog.Clips).Value;
        Assert.Null(legacyClip.AnimationFrames);
        Assert.Equal(21, legacyClip.EffectiveAnimationFrames);
    }

    [Fact]
    public void RefusesUnknownEnumsDirectionsAndUnboundWorldFamilies()
    {
        var catalog = ParseClips(Clip(0, "cathedral", "light", "unarmed", "stand"));
        var player = NewPlayer(0);
        player.CombatMode = (PlayerCombatMode)99;
        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 0, out var selection));
        Assert.Null(selection.Graphic);
        Assert.Contains("combat mode 99", selection.Diagnostic, StringComparison.Ordinal);

        player.CombatMode = PlayerCombatMode.Stand;
        player.FacingDirection = (PlayerDirection)99;
        Assert.True(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 0, out selection));
        Assert.Equal(-1, selection.DirectionIndex);
        Assert.Null(selection.FrameIndex);
        Assert.Contains("direction", selection.Diagnostic, StringComparison.Ordinal);

        player.FacingDirection = PlayerDirection.South;
        player.LevelId = 5;
        Assert.False(catalog.TryResolveLocation(ContentHash, player.LevelId, out _));
        player.LevelId = 0;
        player.CharacterClassId = 3;
        Assert.False(NativePlayerPresentation.TryResolve(player, catalog, NativePlayerLocation.Cathedral, 0, out _));
    }

    [Fact]
    public void CalculatesTheNativeTileCenteredAndBottomAnchoredSpriteRect()
    {
        var destination = NativePlayerPresentation.CalculateDestinationRect(100, 200, 96, 128);

        Assert.Equal(52, destination.X);
        Assert.Equal(73, destination.Y);
        Assert.Equal(96, destination.Width);
        Assert.Equal(128, destination.Height);
    }

    [Fact]
    public void PadsTheRenderedFootprintWithoutChangingTheNominalNativeAnchor()
    {
        var legacyCatalog = ParseClips(Clip(0, "cathedral", "light", "unarmed", "stand", frameWidth: 96));
        Assert.True(legacyCatalog.TryGetClip(
            new NativePlayerClipKey(0, NativePlayerLocation.Cathedral, NativePlayerArmor.Light, NativePlayerWeapon.Unarmed, NativePlayerGraphic.Stand),
            out var legacyClip));
        Assert.Null(legacyClip.AnchorWidth);
        Assert.Equal(96, legacyClip.EffectiveAnchorWidth);

        var paddedCatalog = ParseClips(Clip(0, "cathedral", "light", "unarmed", "stand", frameWidth: 112, anchorWidth: 96));
        Assert.True(paddedCatalog.TryGetClip(legacyClip.Key, out var paddedClip));
        Assert.Equal(112, paddedClip.FrameWidth);
        Assert.Equal(96, paddedClip.EffectiveAnchorWidth);

        var destination = NativePlayerPresentation.CalculateDestinationRect(
            100,
            200,
            paddedClip.FrameWidth,
            paddedClip.FrameHeight,
            anchorWidth: paddedClip.EffectiveAnchorWidth);

        Assert.Equal(52, destination.X);
        Assert.Equal(73, destination.Y);
        Assert.Equal(112, destination.Width);
        Assert.Equal(128, destination.Height);
    }

    private static NativePlayerCatalog ParseClips(params string[] clips)
        => ParseWithSpells(clips, string.Empty);

    private static NativePlayerCatalog ParseWithSpells(string[] clips, string spells)
    {
        Assert.True(NativePlayerCatalog.TryParse(CatalogJson(clips, spells), out var catalog, out var diagnostics), string.Join(" ", diagnostics));
        return catalog!;
    }

    private static NativePlayerCatalog ParseWithBindings(
        string[] clips,
        IEnumerable<string> manifestHashes,
        IEnumerable<string> levelBindings)
    {
        Assert.True(NativePlayerCatalog.TryParse(
            CatalogJson(clips, manifestHashes: manifestHashes, levelBindings: levelBindings),
            out var catalog,
            out var diagnostics),
            string.Join(" ", diagnostics));
        return catalog!;
    }

    private static string CatalogJson(
        IEnumerable<string> clips,
        string spells = "",
        IEnumerable<string>? manifestHashes = null,
        IEnumerable<string>? levelBindings = null)
    {
        var contentHashesJson = string.Join(",", (manifestHashes ?? new[] { ContentHash })
            .Select(hash => "\"" + hash + "\""));
        var levelBindingsJson = string.Join(",", levelBindings ?? DefaultLevelBindingEntries(ContentHash));
        return $$"""
        {"schemaVersion":1,"packId":"native-player-tests","contentManifestHashes":[{{contentHashesJson}}],"levelBindings":[{{levelBindingsJson}}],"clips":[{{string.Join(",", clips)}}],"spells":[{{spells}}]}
        """;
    }

    private static string[] DefaultLevelBindingEntries(string contentHash)
        => [
            LevelBinding(contentHash, 0, "town"),
            LevelBinding(contentHash, 1, "cathedral"),
            LevelBinding(contentHash, 2, "cathedral"),
            LevelBinding(contentHash, 3, "cathedral"),
            LevelBinding(contentHash, 4, "cathedral"),
        ];

    private static string DefaultLevelBindings(string contentHash)
        => string.Join(",", DefaultLevelBindingEntries(contentHash));

    private static string LevelBinding(string contentHash, uint levelId, string location)
        => $$"""{"contentManifestHash":"{{contentHash}}","levelId":{{levelId}},"location":"{{location}}"}""";

    private static string Clip(
        uint classId,
        string location,
        string armor,
        string weapon,
        string graphic,
        string path = "sprites/test.png",
        int frameWidth = 96,
        int frameHeight = 128,
        int frames = 4,
        int ticksPerFrame = 2,
        int actionFrame = 2,
        int? anchorWidth = null,
        int? animationFrames = null)
    {
        var anchorWidthProperty = anchorWidth is { } width
            ? $",\"anchorWidth\":{width}"
            : string.Empty;
        var animationFramesProperty = animationFrames is { } count
            ? $",\"animationFrames\":{count}"
            : string.Empty;
        return $$"""{"classId":{{classId}},"location":"{{location}}","armor":"{{armor}}","weapon":"{{weapon}}","graphic":"{{graphic}}","path":"{{path}}","frameWidth":{{frameWidth}}{{anchorWidthProperty}},"frameHeight":{{frameHeight}},"framesPerDirection":{{frames}}{{animationFramesProperty}},"ticksPerFrame":{{ticksPerFrame}},"actionFrame":{{actionFrame}}}""";
    }

    private static PlayerSnapshot NewPlayer(uint classId)
        => new() {
            EntityId = 42,
            CharacterClassId = classId,
            CombatMode = PlayerCombatMode.Stand,
            FacingDirection = PlayerDirection.South,
            Life = 64,
            LevelId = 1,
            Attributes = new PlayerAttributesSnapshot {
                Strength = new AttributeSnapshot { Current = 20 },
                Magic = new AttributeSnapshot { Current = 20 },
                Dexterity = new AttributeSnapshot { Current = 20 },
            },
        };

    private static EquippedItemSnapshot Equipped(
        uint slot,
        int itemType,
        bool weaponClass = false,
        bool statFlag = false,
        int minimumStrength = 0,
        int minimumMagic = 0,
        int minimumDexterity = 0)
        => new() {
            Slot = slot,
            State = new ItemStateSnapshot {
                ItemType = itemType,
                ItemClass = weaponClass ? 1 : 0,
                StatFlag = statFlag,
                MinimumStrength = minimumStrength,
                MinimumMagic = minimumMagic,
                MinimumDexterity = minimumDexterity,
            },
        };
}
