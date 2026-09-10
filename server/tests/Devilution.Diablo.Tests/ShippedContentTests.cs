using System.Buffers.Binary;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Smoke-tests the checked-in base content through the module-owned bootstrap. */
public sealed class ShippedContentTests
{
    [Fact]
    public void BaseContentPackUsesTheExplicitVersionedManifest()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);

        Assert.Equal("devilution.base", pack.Id);
        Assert.Equal("0.1.0", pack.Version);
        Assert.Equal(
            new ContentSchemaIdentity("devilution.diablo", "1.463.0"),
            pack.Schema);
        Assert.Empty(pack.Overlays);
        var nativeDunMonsterTable = pack.Tables.Single(
            table => table.SourcePath == "native_dun_monsters.tsv");
        var nativeDunMonsters = AuthoritativeNativeDunMonsterCatalog.LoadTsv(
            nativeDunMonsterTable.SourcePath,
            nativeDunMonsterTable.ToCanonicalTsv());
        Assert.Equal(136, nativeDunMonsters.Definitions.Count);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(2, out var ghoulNativeId));
        Assert.Equal(1U, ghoulNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(8, out var darkOneNativeId));
        Assert.Equal(7U, darkOneNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(9, out var skeletonNativeId));
        Assert.Equal(8U, skeletonNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(17, out var scavengerNativeId));
        Assert.Equal(16U, scavengerNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(18, out var plagueEaterNativeId));
        Assert.Equal(17U, plagueEaterNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(28, out var horrorCaptainNativeId));
        Assert.Equal(27U, horrorCaptainNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(29, out var invisibleLordNativeId));
        Assert.Equal(29U, invisibleLordNativeId);
        Assert.False(nativeDunMonsters.TryGetNativeMonsterId(31, out _));
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(156, out var familiarNativeId));
        Assert.Equal(28U, familiarNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(157, out var satyrLordNativeId));
        Assert.Equal(33U, satyrLordNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(45, out var acidBeastNativeId));
        Assert.Equal(46U, acidBeastNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(65, out var obsidianLordNativeId));
        Assert.Equal(67U, obsidianLordNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(66, out var oldbonedNativeId));
        Assert.Equal(68U, oldbonedNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(93, out var balrogNativeId));
        Assert.Equal(88U, balrogNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(127, out var devilKinBruteNativeId));
        Assert.Equal(80U, devilKinBruteNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(94, out var caveViperNativeId));
        Assert.Equal(89U, caveViperNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(112, out var cabalistNativeId));
        Assert.Equal(107U, cabalistNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(113, out var advocateNativeId));
        Assert.Equal(108U, advocateNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(115, out var darkLordNativeId));
        Assert.Equal(110U, darkLordNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(117, out var golemNativeId));
        Assert.Equal(109U, golemNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(128, out var archLitchNativeId));
        Assert.Equal(111U, archLitchNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(129, out var hellboarNativeId));
        Assert.Equal(112U, hellboarNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(150, out var duplicatedSkeletonNativeId));
        Assert.Equal(129U, duplicatedSkeletonNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(154, out var naKrulNativeId));
        Assert.Equal(137U, naKrulNativeId);
        Assert.True(nativeDunMonsters.TryGetNativeMonsterId(155, out var butcherNativeId));
        Assert.Equal(51U, butcherNativeId);
        var monsterTypeTable = pack.Tables.Single(
            table => table.SourcePath == "monster_types.tsv");
        var monsterTypes = AuthoritativeMonsterTypeCatalog.LoadTsv(
            monsterTypeTable.SourcePath,
            monsterTypeTable.ToCanonicalTsv());
        Assert.Equal(139, monsterTypes.Definitions.Count);
        var spawnPolicyTable = pack.Tables.Single(
            table => table.SourcePath == "monster_spawn_policies.tsv");
        var spawnPolicies = AuthoritativeMonsterSpawnPolicyCatalog.LoadTsv(
            spawnPolicyTable.SourcePath,
            spawnPolicyTable.ToCanonicalTsv());
        Assert.True(spawnPolicies.RequiresCompleteCoverage);
        Assert.Equal(
            AuthoritativeMonsterSpawnPolicyCoverageMode.Complete,
            spawnPolicies.CoverageMode);
        spawnPolicies.ValidateCompleteMonsterTypes(monsterTypes);
        Assert.Equal(
            AuthoritativeMonsterSpawnRole.Ordinary,
            spawnPolicies.Definitions.Single(definition => definition.MonsterId == 1).SpawnRole);
        Assert.Equal(
            AuthoritativeMonsterSpawnRole.Quest,
            spawnPolicies.Definitions.Single(definition => definition.MonsterId == 52).SpawnRole);
        Assert.Equal(
            AuthoritativeMonsterSpawnRole.Summon,
            spawnPolicies.Definitions.Single(definition => definition.MonsterId == 111).SpawnRole);
        Assert.Equal(
            [
                "Ghoul", "Rotting Carcass", "Black Death", "Fallen One", "Carver", "Devil Kin", "Dark One",
                "Skeleton", "Corpse Axe", "Burning Dead", "Horror", "Fallen One", "Carver", "Devil Kin", "Dark One", "Scavenger",
                "Plague Eater", "Shadow Beast", "Bone Gasher", "Skeleton", "Corpse Bow", "Burning Dead", "Horror",
                "Skeleton Captain", "Corpse Captain", "Burning Dead Captain", "Horror Captain",
                "Invisible Lord", "Hidden", "Stalker", "Unseen", "Illusion Weaver", "Satyr Lord",
                "Flesh Clan", "Stone Clan", "Fire Clan", "Night Clan", "Fiend", "Blink", "Gloom", "Familiar",
                "Flesh Clan", "Stone Clan", "Fire Clan", "Night Clan",
            ],
            monsterTypes.Definitions
                .Where(definition => definition.MonsterId is >= 3 and <= 47)
                .Select(definition => definition.Name));
        Assert.Equal(
            [
                "Acid Beast", "Poison Spitter", "Pit Beast", "Lava Maw",
                "Overlord", "Mud Man", "Toad Demon", "Flayed One",
                "Wyrm", "Cave Slug", "Devil Wyrm", "Devourer",
                "Magma Demon", "Blood Stone", "Hell Stone", "Lava Lord",
                "Horned Demon", "Mud Runner", "Frost Charger", "Obsidian Lord",
            ],
            monsterTypes.Definitions
                .Where(definition => definition.MonsterId is >= 48 and <= 51 or >= 54 and <= 69)
                .Select(definition => definition.Name));
        Assert.Equal(
            [
                "oldboned", "Red Death", "Litch Demon", "Undead Balrog",
                "Incinerator", "Flame Lord", "Doom Fire", "Hell Burner",
                "Red Storm", "Storm Rider", "Storm Lord", "Maelstrom",
                "Devil Kin Brute", "Winged-Demon", "Gargoyle", "Blood Claw",
                "Death Wing", "Slayer", "Guardian", "Vortex Lord", "Balrog",
            ],
            monsterTypes.Definitions
                .Where(definition => definition.MonsterId is >= 70 and <= 90)
                .Select(definition => definition.Name));
        Assert.Equal(
            [
                "Cave Viper", "Fire Drake", "Gold Viper", "Azure Drake",
                "Black Knight", "Doom Guard", "Steel Lord", "Blood Knight",
                "The Shredded", "Hollow One", "Pain Master", "Reality Weaver",
                "Succubus", "Snow Witch", "Hell Spawn", "Soul Burner",
                "Counselor", "Magistrate", "Cabalist",
            ],
            monsterTypes.Definitions
                .Where(definition => definition.MonsterId is >= 91 and <= 109)
                .Select(definition => definition.Name));
        Assert.Equal(
            ["Advocate", "Golem", "The Dark Lord", "The Arch-Litch Malignus"],
            monsterTypes.Definitions
                .Where(definition => definition.MonsterId is >= 110 and <= 113)
                .Select(definition => definition.Name));
        Assert.Equal(
            [
                "Hellboar", "Stinger", "Psychorb", "Arachnon", "Felltwin", "Hork Spawn", "Venomtail",
                "Necromorb", "Spider Lord", "Lashworm", "Torchant", "Hork Demon", "Hell Bug", "Gravedigger",
                "Tomb Rat", "Firebat", "Skullwing", "Lich", "Crypt Demon", "Hellbat", "Bone Demon", "Arch Lich",
                "Biclops", "Flesh Thing", "Reaper", "Na-Krul",
            ],
            monsterTypes.Definitions
                .Where(definition => definition.MonsterId is >= 114 and <= 139)
                .Select(definition => definition.Name));
        Assert.Equal("Fallen", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 4).NativeAi);
        Assert.Equal("SkeletonMelee", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 8).NativeAi);
        Assert.Equal("Scavenger", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 16).NativeAi);
        Assert.Equal("Carver", monsterTypes.Definitions.Single(
            definition => definition.MonsterId == 15).Name);
        Assert.False(monsterTypes.TryGetNative(13, out _));
        Assert.Equal("Sneak", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 29).NativeAi);
        Assert.Equal("GoatMelee", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 34).NativeAi);
        Assert.Equal("GoatRanged", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 42).NativeAi);
        Assert.Equal("Acid", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 46).NativeAi);
        Assert.Equal("Fat", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 52).NativeAi);
        Assert.Equal("SkeletonMelee", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 56).NativeAi);
        Assert.Equal("Magma", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 60).NativeAi);
        Assert.Equal("Rhino", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 64).NativeAi);
        Assert.Equal("Storm", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 68).NativeAi);
        Assert.Equal("FireMan", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 72).NativeAi);
        Assert.Equal("Gargoyle", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 81).NativeAi);
        var avoidanceRoundWalkTuning = monsterTypes.Definitions
            .Where(definition => definition.NativeAi is "GoatMelee" or "Gargoyle")
            .Select(definition => (
                definition.AiAvoidanceRoundWalkChanceBase,
                definition.AiAvoidanceRoundWalkChanceIntelligenceScale))
            .ToArray();
        Assert.Equal(8, avoidanceRoundWalkTuning.Length);
        Assert.All(avoidanceRoundWalkTuning, tuning => Assert.Equal((16, 5), tuning));
        Assert.Equal("Mega", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 85).NativeAi);
        Assert.Equal("Snake", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 89).NativeAi);
        Assert.Equal("SkeletonMelee", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 93).NativeAi);
        Assert.Equal("Succubus", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 101).NativeAi);
        Assert.Equal("Counselor", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 105).NativeAi);
        Assert.Equal("Counselor", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 108).NativeAi);
        Assert.Equal("Counselor", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 111).NativeAi);
        Assert.Equal("Psychorb", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 114).NativeAi);
        Assert.Equal("HorkDemon", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 123).NativeAi);
        Assert.Equal("FireBat", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 127).NativeAi);
        Assert.Equal("ArchLich", monsterTypes.Definitions.Single(
            definition => definition.NativeMonsterId == 133).NativeAi);
        var placementTable = pack.Tables.Single(
            table => table.SourcePath == "unique_monster_placements.tsv");
        var placement = Assert.Single(AuthoritativeUniqueMonsterPlacementCatalog.LoadTsv(
            placementTable.SourcePath,
            placementTable.ToCanonicalTsv()).Definitions);
        Assert.Equal("Snotspill", placement.UniqueIdentity);
        Assert.Equal(140U, placement.MonsterId);
        Assert.Equal(4U, placement.LevelId);
        Assert.Equal((25, 25), (placement.PositionX, placement.PositionY));
        Assert.Equal(3008U, placement.RequiredQuestId);

        var objectTable = pack.Tables.Single(table => table.SourcePath == "objects.tsv");
        var objects = AuthoritativeWorldObjectCatalog.LoadTemplates(
            objectTable.SourcePath,
            objectTable.ToCanonicalTsv());
        var signChest = Assert.Single(objects, @object => @object.EntityId == 2002);
        Assert.Equal(97U, signChest.ObjectId);
        Assert.Equal(97U, signChest.NativeObjectId);
        Assert.Equal(3008U, signChest.QuestId);
        Assert.Equal(AuthoritativeObjectQuestTransitionKind.SetVariable1, signChest.QuestTransitionKind);
        Assert.Equal(1, signChest.QuestTransitionRequiredVariable1);
        Assert.Equal(AuthoritativeObjectRewardKind.NormalItem, signChest.RewardKind);
        Assert.Equal(2136U, signChest.RewardId);

        var templates = objects
            .Where(@object => @object.ObjectId is 71 or 72 or 88)
            .ToDictionary(@object => @object.ObjectId);
        Assert.Equal(
            [71U, 72U, 88U],
            templates.Keys.Order().ToArray());
        Assert.Equal(
            (71U, 3009U, AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1, 1, 0),
            (templates[71].NativeObjectId,
                templates[71].QuestId,
                templates[71].QuestTransitionKind,
                templates[71].QuestTransitionAmount,
                templates[71].QuestTransitionRequiredVariable1));
        Assert.Equal(255, templates[71].DialogueMessageId);
        Assert.Equal(
            (72U, 3010U, AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1, 1, 0),
            (templates[72].NativeObjectId,
                templates[72].QuestId,
                templates[72].QuestTransitionKind,
                templates[72].QuestTransitionAmount,
                templates[72].QuestTransitionRequiredVariable1));
        Assert.Equal(254, templates[72].DialogueMessageId);
        Assert.Equal(
            (88U, 3012U, AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1, 1, 0),
            (templates[88].NativeObjectId,
                templates[88].QuestId,
                templates[88].QuestTransitionKind,
                templates[88].QuestTransitionAmount,
                templates[88].QuestTransitionRequiredVariable1));
        Assert.Equal(256, templates[88].DialogueMessageId);

        var presentationTable = pack.Tables.Single(
            table => table.SourcePath == "object_presentations.tsv");
        var presentations = AuthoritativeWorldObjectPresentationCatalog.LoadTsv(
            presentationTable.SourcePath,
            presentationTable.ToCanonicalTsv());
        Assert.Equal(
            [2020U, 2021U, 2022U, 2023U, 2024U, 2025U],
            presentations.Definitions.Select(definition => definition.EntityId).ToArray());
        Assert.All(presentations.Definitions.Where(definition => definition.EntityId <= 2022), definition => {
            Assert.False(definition.HideWhenActivated);
            Assert.Equal(0, definition.AnimationFrame);
            Assert.Equal(1, definition.ActivatedAnimationFrame);
        });
        Assert.All(
            presentations.Definitions.Where(definition => definition.EntityId is 2023 or 2024),
            definition => Assert.True(definition.HideWhenActivated));
        Assert.False(presentations.Definitions.Single(definition => definition.EntityId == 2025).HideWhenActivated);
        var itemGateTable = pack.Tables.Single(table => table.SourcePath == "object_item_gates.tsv");
        var itemGates = AuthoritativeObjectItemGateCatalog.LoadTsv(
            itemGateTable.SourcePath,
            itemGateTable.ToCanonicalTsv());
        Assert.Equal([2023U, 2024U, 2025U], itemGates.Definitions.Select(definition => definition.EntityId));
        var gatewayMutation = AuthoritativeWorldMutationCatalog.LoadTsv(
                "world_mutations.tsv",
                pack.Tables.Single(table => table.SourcePath == "world_mutations.tsv").ToCanonicalTsv(),
                content: ComposedContent.Compose(new ContentManifest("gateway-mutation", "1", [pack])))
            .Definitions.Single(definition => definition.MutationId == 9310);
        Assert.Equal(2025U, gatewayMutation.AnchorEntityId);
        Assert.Equal((-11, -16), (gatewayMutation.AnchorMegaOffsetX, gatewayMutation.AnchorMegaOffsetY));
        Assert.Equal("levels/l2data/blood2.dun", gatewayMutation.MegaTileAssetPath);
        Assert.Equal(160, gatewayMutation.MegaTileAssignments.Count);
        Assert.Equal(22, gatewayMutation.MonsterLayerRectangles.Count);

        var dialogueMessages = AuthoritativeDialogueMessageCatalog.LoadTsv(
            "dialogue_messages.tsv",
            pack.Tables.Single(table => table.SourcePath == "dialogue_messages.tsv").ToCanonicalTsv());
        Assert.Equal(
            "...and so, locked beyond the Gateway of Blood and past the Hall of Fire, Valor awaits for the Hero of Light to awaken...",
            dialogueMessages.TryGet(254, out var bloody) ? bloody.Text : null);
        Assert.Equal(
            "I can see what you see not.\nVision milky then eyes rot.\nWhen you turn they will be gone,\nWhispering their hidden song.\nThen you see what cannot be,\nShadows move where light should be.\nOut of darkness, out of mind,\nCast down into the Halls of the Blind.",
            dialogueMessages.TryGet(255, out var blinding) ? blinding.Text : null);
        Assert.Equal(
            "The armories of Hell are home to the Warlord of Blood. In his wake lay the mutilated bodies of thousands. Angels and men alike have been cut down to fulfill his endless sacrifices to the Dark ones who scream for one thing - blood.",
            dialogueMessages.TryGet(256, out var bloodwar) ? bloodwar.Text : null);
        Assert.Contains(
            "Whoever finds this scroll is charged with stopping the demonic creature",
            dialogueMessages.TryGet(322, out var book9) ? book9.Text : string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteSpawnPolicyDeclarationRejectsAnIncompleteActivePack()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var source = basePack.Tables
            .Single(table => table.SourcePath == "monster_spawn_policies.tsv")
            .ToCanonicalTsv();
        var incomplete = string.Join(
                '\n',
                source.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !line.StartsWith("139\t", StringComparison.Ordinal)))
            + "\n";
        var tables = basePack.Tables
            .Where(table => table.SourcePath != "monster_spawn_policies.tsv")
            .Append(TsvTable.Parse("monster_spawn_policies.tsv", incomplete))
            .ToArray();
        var pack = new ContentPack(
            basePack.Id,
            basePack.Version,
            tables,
            dependencies: [],
            overlays: [],
            schema: basePack.Schema,
            assets: basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("incomplete-spawn-policy", "1", [pack]))));

        Assert.Contains("Monster spawn policy is missing identities", exception.Message);
        Assert.Contains("139", exception.Message);
    }

    [Fact]
    public void BaseClassAnimationsMatchNativeTables()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var external = basePack.Tables.Single(table => table.SourcePath == "class_animations.tsv");
        var externalRows = external.Rows.ToDictionary(
            row => row.Required("class_symbol"),
            StringComparer.Ordinal);
        var fields = new[] {
            (External: "unarmed_frames", Native: "unarmedFrames"),
            (External: "unarmed_action_frame", Native: "unarmedActionFrame"),
            (External: "unarmed_shield_frames", Native: "unarmedShieldFrames"),
            (External: "unarmed_shield_action_frame", Native: "unarmedShieldActionFrame"),
            (External: "sword_frames", Native: "swordFrames"),
            (External: "sword_action_frame", Native: "swordActionFrame"),
            (External: "sword_shield_frames", Native: "swordShieldFrames"),
            (External: "sword_shield_action_frame", Native: "swordShieldActionFrame"),
            (External: "bow_frames", Native: "bowFrames"),
            (External: "bow_action_frame", Native: "bowActionFrame"),
            (External: "axe_frames", Native: "axeFrames"),
            (External: "axe_action_frame", Native: "axeActionFrame"),
            (External: "mace_frames", Native: "maceFrames"),
            (External: "mace_action_frame", Native: "maceActionFrame"),
            (External: "mace_shield_frames", Native: "maceShieldFrames"),
            (External: "mace_shield_action_frame", Native: "maceShieldActionFrame"),
            (External: "staff_frames", Native: "staffFrames"),
            (External: "staff_action_frame", Native: "staffActionFrame"),
            (External: "idle_frames", Native: "idleFrames"),
            (External: "walking_frames", Native: "walkingFrames"),
            (External: "blocking_frames", Native: "blockingFrames"),
            (External: "death_frames", Native: "deathFrames"),
            (External: "casting_frames", Native: "castingFrames"),
            (External: "recovery_frames", Native: "recoveryFrames"),
            (External: "town_idle_frames", Native: "townIdleFrames"),
            (External: "town_walking_frames", Native: "townWalkingFrames"),
            (External: "casting_action_frame", Native: "castingActionFrame"),
        };
        Assert.Equal(6, externalRows.Count);
        foreach (var (classSymbol, externalRow) in externalRows) {
            var native = TsvTable.Parse(
                $"assets/txtdata/classes/{classSymbol}/animations.tsv",
                File.ReadAllText(Path.Combine(
                    repositoryRoot,
                    "assets",
                    "txtdata",
                    "classes",
                    classSymbol,
                    "animations.tsv")));
            var nativeValues = native.Rows.ToDictionary(
                row => row.Required("Variable"),
                row => row.Required("Value"),
                StringComparer.Ordinal);
            foreach (var (externalColumn, nativeVariable) in fields)
                Assert.Equal(nativeValues[nativeVariable], externalRow.Required(externalColumn));
        }
    }

    [Fact]
    public void BaseClassAttributesMatchNativeTables()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var externalRows = basePack.Tables
            .Single(table => table.SourcePath == "classes.tsv")
            .Rows
            .ToDictionary(row => row.Required("symbol"), StringComparer.Ordinal);
        var classes = new[] { "warrior", "rogue", "sorcerer", "monk", "bard", "barbarian" };
        var directFields = new[] {
            (External: "base_strength", Native: "baseStr"),
            (External: "base_magic", Native: "baseMag"),
            (External: "base_dexterity", Native: "baseDex"),
            (External: "base_vitality", Native: "baseVit"),
            (External: "maximum_strength", Native: "maxStr"),
            (External: "maximum_magic", Native: "maxMag"),
            (External: "maximum_dexterity", Native: "maxDex"),
            (External: "maximum_vitality", Native: "maxVit"),
            (External: "base_block_chance", Native: "blockBonus"),
            (External: "base_magic_to_hit", Native: "baseMagicToHit"),
            (External: "base_melee_to_hit", Native: "baseMeleeToHit"),
            (External: "base_ranged_to_hit", Native: "baseRangedToHit"),
        };
        var q6Fields = new[] {
            (External: "life_adjustment_q6", Native: "adjLife"),
            (External: "mana_adjustment_q6", Native: "adjMana"),
            (External: "life_per_level_q6", Native: "lvlLife"),
            (External: "mana_per_level_q6", Native: "lvlMana"),
            (External: "life_per_vitality_q6", Native: "chrLife"),
            (External: "mana_per_magic_q6", Native: "chrMana"),
            (External: "item_life_per_vitality_q6", Native: "itmLife"),
            (External: "item_mana_per_magic_q6", Native: "itmMana"),
        };

        Assert.Equal(classes.Length, externalRows.Count);
        foreach (var classSymbol in classes) {
            var nativeValues = TsvTable.Parse(
                    $"assets/txtdata/classes/{classSymbol}/attributes.tsv",
                    File.ReadAllText(Path.Combine(
                        repositoryRoot,
                        "assets",
                        "txtdata",
                        "classes",
                        classSymbol,
                        "attributes.tsv")))
                .Rows
                .ToDictionary(
                    row => row.Required("Attribute"),
                    row => row["Value"],
                    StringComparer.Ordinal);
            var external = externalRows[classSymbol];

            foreach (var (externalColumn, nativeVariable) in directFields)
                Assert.Equal(nativeValues[nativeVariable], external.Required(externalColumn));
            foreach (var (externalColumn, nativeVariable) in q6Fields)
                Assert.Equal(ToQ6(nativeValues[nativeVariable]), external.Required(externalColumn));
        }
    }

    [Fact]
    public void BaseLevelGenerationRulesMatchNativeTable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var native = TsvTable.Parse(
            "assets/txtdata/levels/level_generation.tsv",
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "assets",
                "txtdata",
                "levels",
                "level_generation.tsv")));
        var external = basePack.Tables.Single(table => table.SourcePath == "level_generation.tsv");
        var externalRows = external.Rows.ToDictionary(
            row => (row.RequiredInt32("minimum_level"), row.RequiredInt32("maximum_level")));
        var fields = new[] {
            (External: "monster_density_divisor", Native: "monsterDensityDivisor"),
            (External: "multiplayer_monster_multiplier_percent", Native: "mpMonsterMultiplierPercent"),
            (External: "trap_percent", Native: "trapPercent"),
        };

        Assert.Equal(native.Rows.Count, externalRows.Count);
        foreach (var nativeRow in native.Rows) {
            var key = (
                nativeRow.RequiredInt32("minLevel"),
                nativeRow.RequiredInt32("maxLevel"));
            var externalRow = externalRows[key];
            foreach (var (externalColumn, nativeColumn) in fields)
                Assert.Equal(nativeRow.Required(nativeColumn), externalRow.Required(externalColumn));
        }
    }

    [Fact]
    public void BaseItemGenerationRulesMatchNativeTable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var native = TsvTable.Parse(
            "assets/txtdata/items/item_generation.tsv",
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "assets",
                "txtdata",
                "items",
                "item_generation.tsv")));
        var external = basePack.Tables.Single(table => table.SourcePath == "item_generation.tsv");
        var fields = new[] {
            (External: "magicChanceBase", Native: "magicChanceBase"),
            (External: "magicChancePerLevel", Native: "magicChancePerLevel"),
            (External: "alwaysMagicMisc", Native: "alwaysMagicMisc"),
            (External: "uniqueChanceNormal", Native: "uniqueChanceNormal"),
            (External: "uniqueChanceUnique", Native: "uniqueChanceUnique"),
            (External: "bonusLevelsUnique", Native: "bonusLevelsUnique"),
            (External: "prefixPercent", Native: "prefixPercent"),
            (External: "suffixPercent", Native: "suffixPercent"),
            (External: "onlygoodChance", Native: "onlygoodChance"),
            (External: "noDropPercent", Native: "noDropPercent"),
            (External: "goldPercent", Native: "goldPercent"),
        };

        var nativeRow = Assert.Single(native.Rows);
        var externalRow = Assert.Single(external.Rows);
        foreach (var (externalColumn, nativeColumn) in fields)
            Assert.Equal(nativeRow.Required(nativeColumn), externalRow.Required(externalColumn));
    }

    [Fact]
    public void BaseExperienceThresholdsMatchNativeTable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var native = TsvTable.Parse(
            "assets/txtdata/Experience.tsv",
            File.ReadAllText(Path.Combine(repositoryRoot, "assets", "txtdata", "Experience.tsv")));
        var external = basePack.Tables.Single(table => table.SourcePath == "experience.tsv");

        Assert.Equal(50, native.Rows.Count);
        Assert.Equal(native.Rows.Count, external.Rows.Count);
        foreach (var (nativeRow, externalRow) in native.Rows.Zip(external.Rows)) {
            Assert.Equal(nativeRow.Required("Level"), externalRow.Required("level"));
            Assert.Equal(nativeRow.Required("Experience"), externalRow.Required("experience"));
        }
    }

    [Fact]
    public void BaseNativeMissileMetadataMatchesNativeTable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var native = TsvTable.Parse(
            "assets/txtdata/missiles/misdat.tsv",
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "assets",
                "txtdata",
                "missiles",
                "misdat.tsv")));
        var external = basePack.Tables.Single(table => table.SourcePath == "native_missiles.tsv");
        var fields = new[] {
            (External: "missile_id", Native: "id"),
            (External: "add_function", Native: "addFn"),
            (External: "process_function", Native: "processFn"),
            (External: "cast_sound", Native: "castSound"),
            (External: "hit_sound", Native: "hitSound"),
            (External: "graphic", Native: "graphic"),
            (External: "flags", Native: "flags"),
            (External: "movement_distribution", Native: "movementDistribution"),
            (External: "on_expiry_missile", Native: "onExpiryMissile"),
        };

        Assert.Equal(68, native.Rows.Count);
        Assert.Equal(native.Rows.Count, external.Rows.Count);
        foreach (var (nativeRow, externalRow) in native.Rows.Zip(external.Rows))
            foreach (var (externalColumn, nativeColumn) in fields)
                Assert.Equal(nativeRow[nativeColumn], externalRow[externalColumn]);

        var aliases = basePack.Tables
            .Single(table => table.SourcePath == "native_missile_aliases.tsv")
            .Rows
            .Select(row => row.Required("missile_id"))
            .ToArray();
        Assert.Equal(
            ["OrangeFlare", "YellowFlare", "BlueFlare", "RedFlare", "BlueFlare2"],
            aliases);
    }

    [Fact]
    public void BaseNativeMissileSpritesMatchNativeTable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var native = TsvTable.Parse(
            "assets/txtdata/missiles/missile_sprites.tsv",
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "assets",
                "txtdata",
                "missiles",
                "missile_sprites.tsv")));
        var external = basePack.Tables.Single(
            table => table.SourcePath == "native_missile_sprites.tsv");
        var fields = new[] {
            (External: "missile_id", Native: "id"),
            (External: "width", Native: "width"),
            (External: "alternate_width", Native: "width2"),
            (External: "name", Native: "name"),
            (External: "frame_count", Native: "numFrames"),
            (External: "flags", Native: "flags"),
            (External: "frame_delay", Native: "frameDelay"),
            (External: "frame_length", Native: "frameLength"),
        };

        Assert.Equal(46, native.Rows.Count);
        Assert.Equal(native.Rows.Count, external.Rows.Count);
        foreach (var (nativeRow, externalRow) in native.Rows.Zip(external.Rows))
            foreach (var (externalColumn, nativeColumn) in fields)
                Assert.Equal(nativeRow[nativeColumn], externalRow[externalColumn]);
    }

    [Fact]
    public void HellfireNativeMissileSpritesMatchNativeOverlayRows()
    {
        var repositoryRoot = FindRepositoryRoot();
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var native = TsvTable.Parse(
            "mods/Hellfire/txtdata/missiles/missile_sprites.tsv",
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "mods",
                "Hellfire",
                "txtdata",
                "missiles",
                "missile_sprites.tsv")));
        var external = hellfirePack.Tables.Single(
            table => table.SourcePath == "native_missile_sprites.tsv");
        var fields = new[] {
            (External: "missile_id", Native: "id"),
            (External: "width", Native: "width"),
            (External: "alternate_width", Native: "width2"),
            (External: "name", Native: "name"),
            (External: "frame_count", Native: "numFrames"),
            (External: "flags", Native: "flags"),
            (External: "frame_delay", Native: "frameDelay"),
            (External: "frame_length", Native: "frameLength"),
        };

        Assert.Equal(59, native.Rows.Count);
        Assert.Equal(13, external.Rows.Count);
        foreach (var (nativeRow, externalRow) in native.Rows.Skip(46).Zip(external.Rows))
            foreach (var (externalColumn, nativeColumn) in fields)
                Assert.Equal(nativeRow[nativeColumn], externalRow[externalColumn]);
    }

    [Fact]
    public void BaseNativeSpellNamespaceMatchesNativeSpellData()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var native = TsvTable.Parse(
            "assets/txtdata/spells/spelldat.tsv",
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "assets",
                "txtdata",
                "spells",
                "spelldat.tsv")));
        var externalRows = basePack.Tables
            .Single(table => table.SourcePath == "native_spells.tsv")
            .Rows
            .ToDictionary(row => row.Required("native_spell_symbol"), StringComparer.Ordinal);

        Assert.Equal(36, native.Rows.Count);
        Assert.Equal(52, externalRows.Count);
        foreach (var (index, nativeRow) in native.Rows.Select((row, index) => (index, row))) {
            var symbol = nativeRow.Required("id");
            var external = Assert.Contains(symbol, externalRows);
            Assert.Equal(index + 1, external.RequiredInt32("native_spell_id"));
            Assert.Equal(symbol, external.Required("native_spell_symbol"));
        }
    }

    [Fact]
    public void BaseSpellBehaviorRowsMatchNativeSpellIdentityNamespace()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var nativeSpellTable = basePack.Tables.Single(
            table => table.SourcePath == "native_spells.tsv");
        var nativeCatalog = AuthoritativeNativeSpellCatalog.LoadTsv(
            nativeSpellTable.SourcePath,
            nativeSpellTable.ToCanonicalTsv());
        var nativeById = nativeCatalog.Definitions.ToDictionary(
            definition => definition.NativeSpellId);
        var spellRows = basePack.Tables
            .Single(table => table.SourcePath == "spells.tsv")
            .Rows;

        foreach (var row in spellRows) {
            var nativeId = row.RequiredUInt32("native_spell_id");
            Assert.True(
                nativeById.TryGetValue(nativeId, out var native),
                $"Spell row '{row.Required("spell_symbol")}' references unknown native spell {nativeId}.");
            var spellSymbol = NormalizeSpellSymbol(row.Required("spell_symbol"));
            var nativeSymbol = NormalizeSpellSymbol(native!.Symbol);
            Assert.True(
                spellSymbol == nativeSymbol
                    || spellSymbol == "resurrection" && nativeSymbol == "resurrect",
                $"Spell row '{row.Required("spell_symbol")}' does not match native spell '{native.Symbol}'.");
        }

        static string NormalizeSpellSymbol(string symbol)
            => symbol.Replace("_", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
    }

    [Fact]
    public void HellfireNativeSpellNamespaceRetainsCompatibilityEntriesAndRuneAliases()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));
        var catalog = AuthoritativeNativeSpellCatalog.LoadTsv(
            "native_spells.tsv",
            composed.GetRequiredTable("native_spells.tsv").ToCanonicalTsv());

        // The native table has 52 identities. Some are intentionally namespace
        // compatibility entries rather than rows in the cast-behavior table.
        Assert.Equal(52, catalog.Definitions.Count);
        Assert.Equal(16U, catalog.GetRequiredId("DoomSerpents"));
        Assert.Equal(17U, catalog.GetRequiredId("BloodRitual"));
        Assert.Equal(19U, catalog.GetRequiredId("Invisibility"));
        Assert.Equal(26U, catalog.GetRequiredId("ItemRepair"));
        Assert.Equal(28U, catalog.GetRequiredId("TrapDisarm"));
        Assert.Equal(47U, catalog.GetRequiredId("RuneOfFire"));
        Assert.Equal(51U, catalog.GetRequiredId("RuneOfStone"));
    }

    [Fact]
    public void BaseContentPackShipsTheInitialSnotspillBannerFragment()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var table = pack.Tables.Single(table => table.SourcePath == "native_dun_fragments.tsv");
        var fragments = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv()).Definitions;
        Assert.Equal(4, fragments.Count);
        var fragment = fragments.Single(definition => definition.FragmentId == 1);

        Assert.Equal((1U, 4U, "hell", "levels/l1data/banner1.dun", "Snotspill", -11, -9), (
            fragment.FragmentId,
            fragment.LevelId,
            fragment.LevelSymbol,
            fragment.AssetPath,
            fragment.AnchorUniqueIdentity,
            fragment.AnchorOffsetX,
            fragment.AnchorOffsetY));
        Assert.Equal(
            2180,
            pack.Assets.Single(asset => asset.SourcePath == fragment.AssetPath).Contents.Length);

        var warlord = fragments.Single(definition => definition.FragmentId == 2);
        Assert.Equal((4U, "hell", 12, 12, 3012U, "warlord_of_blood"), (
            warlord.LevelId,
            warlord.LevelSymbol,
            warlord.AnchorPositionX,
            warlord.AnchorPositionY,
            warlord.RequiredQuestId,
            warlord.RequiredQuestSymbol));
        Assert.Equal(
            1908,
            pack.Assets.Single(asset => asset.SourcePath == warlord.AssetPath).Contents.Length);

        var halls = fragments.Single(definition => definition.FragmentId == 3);
        Assert.Equal((1U, "cathedral", 8, 8, 3009U, "halls_of_the_blind"), (
            halls.LevelId,
            halls.LevelSymbol,
            halls.AnchorPositionX,
            halls.AnchorPositionY,
            halls.RequiredQuestId,
            halls.RequiredQuestSymbol));
        Assert.Equal(
            4118,
            pack.Assets.Single(asset => asset.SourcePath == halls.AssetPath).Contents.Length);

        var gateway = fragments.Single(definition => definition.FragmentId == 4);
        Assert.Equal("levels/l2data/blood1.dun", gateway.AssetPath);
        Assert.Equal((2U, "catacombs", 10, 4, 3010U, "valor"), (
            gateway.LevelId,
            gateway.LevelSymbol,
            gateway.AnchorPositionX,
            gateway.AnchorPositionY,
            gateway.RequiredQuestId,
            gateway.RequiredQuestSymbol));
        Assert.Equal(
            5444,
            pack.Assets.Single(asset => asset.SourcePath == gateway.AssetPath).Contents.Length);
        var initialGateway = AuthoritativeDunMapAsset.Load(
            gateway.AssetPath,
            pack.Assets.Single(asset => asset.SourcePath == gateway.AssetPath).Contents);
        Assert.DoesNotContain(initialGateway.MonsterLayer, value => value != 0);
        var activatedGateway = AuthoritativeDunMapAsset.Load(
            "levels/l2data/blood2.dun",
            pack.Assets.Single(asset => asset.SourcePath == "levels/l2data/blood2.dun").Contents);
        Assert.Equal(22, activatedGateway.MonsterLayer.Count(value => value != 0));
    }

    [Fact]
    public void AvailableWarlordActivationMaterializesTheRoomAndSteelTome()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("available-warlord", "1", [pack])),
            gameMode: DiabloGameMode.Multiplayer);

        var steelTome = Assert.Single(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.EntityId == 2022);
        Assert.Equal((4U, 12, 12, 3012U), (
            steelTome.LevelId,
            steelTome.PositionX,
            steelTome.PositionY,
            steelTome.QuestId));
        var level = activation.Simulation.WorldState.World!.Levels
            .Single(candidate => candidate.LevelId == 4);
        Assert.Equal(7, level.NativeMonsterLayer.Count(value => value == 98));
    }

    [Fact]
    public void GatewayPedestalConsumesStonesAndMaterializesBlood2Monsters()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var manifest = new ContentManifest("gateway-stage", "1", [pack]);
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(manifest),
            gameMode: DiabloGameMode.Multiplayer);
        var simulation = activation.Simulation;
        var pedestalObjects = simulation.WorldState.Objects.Values
            .Where(@object => @object.EntityId is 2023 or 2024 or 2025)
            .OrderBy(@object => @object.EntityId)
            .ToArray();
        Assert.Equal([2023U, 2024U, 2025U], pedestalObjects.Select(@object => @object.EntityId));

        var quest = simulation.WorldState.Quests[3010];
        quest.Active = true;
        quest.Unavailable = false;
        quest.Completed = false;
        quest.Variable2 = 0;
        var inventory = Enumerable.Range(0, 3)
            .Select(index => {
                var seed = checked((uint)(index + 1));
                return new OwnedStoreItem(
                    0,
                    0,
                    seed,
                    0,
                    0,
                    simulation.Items!.Generate(2143, seed, 0));
            })
            .ToArray();
        var player = new AuthoritativePlayerState(
            0,
            0,
            50,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [0, 1, 2, .. Enumerable.Repeat(-1, 37)],
            [],
            20,
            19,
            50,
            1,
            2,
            0,
            inventory: inventory) {
            EntityId = 7,
        };

        for (var index = 0; index < pedestalObjects.Length; index++) {
            var result = simulation.Execute(
                player,
                new DiabloOperateObjectIntent(pedestalObjects[index].EntityId),
                checked((ulong)(index + 1)),
                [player]);
            Assert.True(result.Succeeded, result.Failure.ToString());
            Assert.Equal(index + 1, quest.Variable2);
            Assert.Equal(2 - index, player.Inventory.Count);
        }

        Assert.Equal(22, simulation.WorldState.CombatTargets.Values
            .Count(target => target.LevelId == 2));
        Assert.True(simulation.WorldState.Objects[2025].Activated);
        Assert.Contains(
            simulation.WorldState.WorldItems.Values,
            item => item.State.UniqueId == 1009);

        var activatedGateway = AuthoritativeDunMapAsset.Load(
            "levels/l2data/blood2.dun",
            pack.Assets.Single(asset => asset.SourcePath == "levels/l2data/blood2.dun").Contents);
        var projected = simulation.WorldState.LevelGeometry
            .Single(level => level.LevelId == 2);
        var projectedMegaWidth = (projected.Width + 1) / 2;
        for (var y = 0; y < activatedGateway.MegaHeight; y++)
            for (var x = 0; x < activatedGateway.MegaWidth; x++)
                Assert.Equal(
                    activatedGateway.TileLayer[y * activatedGateway.MegaWidth + x],
                    projected.NativeMegaTileLayer[(4 / 2 + y) * projectedMegaWidth + 10 / 2 + x]);
    }

    [Fact]
    public void QuestUnavailableStateSuppressesGatedWarlordRoomAndTome()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var questStates = basePack.Tables.Single(table => table.SourcePath == "quest_states.tsv");
        var unavailableStates = string.Join(
            '\n',
            questStates.ToCanonicalTsv()
                .Split('\n')
                .Select(line => {
                    if (!line.StartsWith("3012\t", StringComparison.Ordinal))
                        return line;
                    var columns = line.Split('\t');
                    columns[7] = "true";
                    return string.Join('\t', columns);
                }));
        var modifiedPack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == questStates.SourcePath
                    ? TsvTable.Parse(questStates.SourcePath, unavailableStates)
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("unavailable-warlord", "1", [modifiedPack])),
            gameMode: DiabloGameMode.Multiplayer);

        Assert.DoesNotContain(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.EntityId == 2022);
        var level = activation.Simulation.WorldState.World!.Levels
            .Single(candidate => candidate.LevelId == 4);
        Assert.DoesNotContain((ushort)98, level.NativeMonsterLayer);
    }

    [Fact]
    public void QuestUnavailableStateSuppressesGatedCatacombsRooms()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var questStates = basePack.Tables.Single(table => table.SourcePath == "quests.tsv");
        var unavailableStates = string.Join(
            '\n',
            questStates.ToCanonicalTsv()
                .Split('\n')
                .Select(line => {
                    if (!line.StartsWith("3009\t", StringComparison.Ordinal)
                        && !line.StartsWith("3010\t", StringComparison.Ordinal)) {
                        return line;
                    }
                    var columns = line.Split('\t');
                    columns[7] = "true";
                    return string.Join('\t', columns);
                }));
        var modifiedPack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == questStates.SourcePath
                    ? TsvTable.Parse(questStates.SourcePath, unavailableStates)
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("unavailable-catacombs", "1", [modifiedPack])),
            gameMode: DiabloGameMode.Multiplayer);

        Assert.DoesNotContain(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.EntityId is 2020 or 2021);
        foreach (var levelId in new uint[] { 1, 2 }) {
            var level = activation.Simulation.WorldState.World!.Levels
                .Single(candidate => candidate.LevelId == levelId);
            Assert.DoesNotContain(level.NativeMonsterLayer, value => value is 12 or 28);
        }
    }

    [Fact]
    public void AvailableCatacombsQuestActivationMaterializesTheBlindAndBloodRooms()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("available-catacombs-quests", "1", [pack])),
            gameMode: DiabloGameMode.Multiplayer);

        var hallsBook = Assert.Single(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.EntityId == 2020);
        Assert.Equal((1U, 8, 8, 3009U), (
            hallsBook.LevelId,
            hallsBook.PositionX,
            hallsBook.PositionY,
            hallsBook.QuestId));
        var gatewayBook = Assert.Single(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.EntityId == 2021);
        Assert.Equal((2U, 10, 4, 3010U), (
            gatewayBook.LevelId,
            gatewayBook.PositionX,
            gatewayBook.PositionY,
            gatewayBook.QuestId));
        var catacombs = activation.Simulation.WorldState.World!.Levels
            .Single(candidate => candidate.LevelId == 2);
        Assert.DoesNotContain(catacombs.NativeMonsterLayer, value => value is 12 or 28);
    }

    [Fact]
    public void BaseContentPackShipsTheSnotspillBannerMapTransition()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var mutationTable = pack.Tables.Single(table => table.SourcePath == "world_mutations.tsv");
        var openingMutation = Assert.Single(
            AuthoritativeWorldMutationCatalog.LoadTsv(
                mutationTable.SourcePath,
                mutationTable.ToCanonicalTsv(),
                content: ComposedContent.Compose(new ContentManifest("snotspill-opening", "1", [pack]))).Definitions,
            definition => definition.MutationId == 9301);
        var openingTransparency = Assert.Single(openingMutation.TransparencyRectangles);
        Assert.Equal(("Snotspill", -11, -9), (
            openingMutation.AnchorUniqueIdentity,
            openingTransparency.X,
            openingTransparency.Y));
        Assert.Equal((16, 8, (ushort)9), (
            openingTransparency.Width,
            openingTransparency.Height,
            openingTransparency.Value));
        Assert.True(openingMutation.CreateTransparencyLayer);
        var mutation = Assert.Single(
            AuthoritativeWorldMutationCatalog.LoadTsv(
                mutationTable.SourcePath,
                mutationTable.ToCanonicalTsv(),
                content: ComposedContent.Compose(new ContentManifest("snotspill-transition", "1", [pack]))).Definitions,
            definition => definition.MutationId == 9300);

        Assert.Equal(("Snotspill", -11, -9), (
            mutation.AnchorUniqueIdentity,
            mutation.AnchorMegaOffsetX,
            mutation.AnchorMegaOffsetY));
        Assert.True(mutation.CreateTransparencyLayer);
        Assert.Equal(
            ( -11, -9, 16, 8, (ushort)9),
            mutation.TransparencyRectangles.Select(rectangle => (
                rectangle.X,
                rectangle.Y,
                rectangle.Width,
                rectangle.Height,
                rectangle.Value)).Single());
        Assert.Equal(
            [(3, 4, (ushort)136), (3, 5, (ushort)135), (3, 6, (ushort)134),
                (4, 5, (ushort)137), (5, 5, (ushort)135), (6, 5, (ushort)135)],
            mutation.MegaTileAssignments.Select(assignment => (
                assignment.X,
                assignment.Y,
                assignment.TileId)));

        var stageTable = pack.Tables.Single(table => table.SourcePath == "quest_dialogue_stages.tsv");
        var stage = Assert.Single(
            AuthoritativeQuestDialogueStageCatalog.LoadTsv(
                stageTable.SourcePath,
                stageTable.ToCanonicalTsv()).Definitions,
            definition => definition.QuestId == 3008 && definition.RequiredVariable1 == 2);
        Assert.Equal(AuthoritativeQuestDialogueFollowUpKind.WorldMutation, stage.FollowUpKind);
        Assert.Equal(9300U, stage.FollowUpAmount);
    }

    [Fact]
    public void QuestUnavailableStateSuppressesGatedSnotspillPlacement()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var questStates = basePack.Tables.Single(table => table.SourcePath == "quest_states.tsv");
        var unavailableStates = string.Join(
            '\n',
            questStates.ToCanonicalTsv()
                .Split('\n')
                .Select(line => {
                    if (!line.StartsWith("3008\t", StringComparison.Ordinal))
                        return line;
                    var columns = line.Split('\t');
                    columns[7] = "true";
                    return string.Join('\t', columns);
                }));
        var modifiedPack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == questStates.SourcePath
                    ? TsvTable.Parse(questStates.SourcePath, unavailableStates)
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("unavailable-banner", "1", [modifiedPack])),
            gameMode: DiabloGameMode.Multiplayer);

        Assert.DoesNotContain(
            activation.Simulation.WorldState.CombatTargets.Values,
            target => target.UniqueIdentity == "Snotspill");
        Assert.DoesNotContain(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.EntityId == 2002 || @object.ObjectId == 97);
    }

    [Fact]
    public void AvailableSnotspillActivationMaterializesTheBannerSignChest()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("available-banner", "1", [pack])),
            gameMode: DiabloGameMode.Multiplayer);

        var signChest = Assert.Single(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.EntityId == 2002);
        Assert.Equal(97U, signChest.ObjectId);
        Assert.Equal((38, 35), (signChest.PositionX, signChest.PositionY));
    }

    [Fact]
    public void PreviousAdditiveContentSchemaRemainsActivatable()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var currentPack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var legacyPack = new ContentPack(
            currentPack.Id,
            currentPack.Version,
            currentPack.Tables,
            currentPack.Dependencies,
            currentPack.Overlays,
            new ContentSchemaIdentity("devilution.diablo", "1.462.0"),
            currentPack.DependencyConstraints,
            currentPack.Assets);
        var content = ComposedContent.Compose(new ContentManifest(
            "legacy-schema",
            "1",
            [legacyPack]));

        var activation = new DiabloModule().ContentBootstrap.Activate(content);

        Assert.NotNull(activation);
    }

    [Fact]
    public void BaseContentActivationUsesExternalExperienceCatalog()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("experience", "1", [pack])));

        var experience = Assert.IsType<AuthoritativeExperienceCatalog>(activation.Experience);
        Assert.Same(experience, activation.Simulation.Experience);
        Assert.Equal(50U, experience.MaximumLevel);
        Assert.Equal(2000U, experience.GetRequiredExperience(1));
        Assert.Equal(4620U, experience.GetRequiredExperience(2));
        Assert.Equal(1583495809U, experience.GetRequiredExperience(50));
    }

    [Theory]
    [InlineData("store_services.tsv")]
    [InlineData("item_generation.tsv")]
    [InlineData("experience.tsv")]
    [InlineData("content_ids.tsv")]
    [InlineData("item_affixes.tsv")]
    [InlineData("items.tsv")]
    [InlineData("unique_items.tsv")]
    [InlineData("spell_books.tsv")]
    [InlineData("spells.tsv")]
    [InlineData("native_item_effects.tsv")]
    [InlineData("native_item_hellfire_effects.tsv")]
    [InlineData("item_cursor_graphics.tsv")]
    [InlineData("native_missiles.tsv")]
    [InlineData("native_missile_aliases.tsv")]
    [InlineData("native_missile_sprites.tsv")]
    [InlineData("monster_types.tsv")]
    [InlineData("monster_missiles.tsv")]
    [InlineData("monsters.tsv")]
    [InlineData("world_rules.tsv")]
    [InlineData("combat.tsv")]
    public void ContentBootstrapRejectsMissingCoreExternalTable(string missingTable)
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Where(table => table.SourcePath != missingTable)
                .ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    $"missing-{missingTable}",
                    "1",
                    [incompletePack]))));

        Assert.Contains(
            $"requires external '{missingTable}' content",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsWorldRulesCompatibilityDefaults()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var worldRules = basePack.Tables.Single(table => table.SourcePath == "world_rules.tsv");
        var incompleteWorldRules = TsvTable.Parse(
            "world_rules.tsv",
            "town_level_id\tadria_store_id\tstoryteller_store_id\n0\t10\t11\n");
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == worldRules.SourcePath
                    ? incompleteWorldRules
                    : table)
                .ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "missing-world-rules-geometry",
                    "1",
                    [incompletePack]))));

        Assert.Contains("Production world rules table", exception.Message, StringComparison.Ordinal);
        Assert.Contains("town_portal_destination_position_x", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviousSchemaRetainsPartialWorldRulesCompatibility()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var worldRules = basePack.Tables.Single(table => table.SourcePath == "world_rules.tsv");
        var partialWorldRules = TsvTable.Parse(
            worldRules.SourcePath,
            "town_level_id\tadria_store_id\tstoryteller_store_id\n0\t10\t11\n");
        var previousPack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == worldRules.SourcePath
                    ? partialWorldRules
                    : table)
                .ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            new ContentSchemaIdentity("devilution.diablo", "1.462.0"),
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "previous-world-rules-contract",
                "1",
                [previousPack])));

        Assert.False(activation.WorldRules.HasTownPortalDestinationGeometry);
    }

    [Theory]
    [InlineData("native_spells.tsv")]
    [InlineData("native_item_misc.tsv")]
    [InlineData("native_item_topology.tsv")]
    [InlineData("native_item_tags.tsv")]
    public void ContentBootstrapAllowsMissingLegacyOnlyCatalog(string omittedTable)
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var normalizedPack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Where(table => table.SourcePath != omittedTable)
                .ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                $"without-{omittedTable}",
                "1",
                [normalizedPack])));

        Assert.NotNull(activation);
        var items = Assert.IsType<AuthoritativeItemCatalog>(activation.Simulation.Items);
        Assert.NotEmpty(items.Definitions);
    }

    [Fact]
    public void ContentBootstrapUsesNormalizedContractWithoutLegacyItemTables()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var legacyTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "native_spells.tsv",
            "native_item_misc.tsv",
            "native_item_topology.tsv",
            "native_item_tags.tsv",
            "itemdat.tsv",
            "item_prefixes.tsv",
            "item_suffixes.tsv",
            "unique_itemdat.tsv",
        };
        var normalizedPack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Where(table => !legacyTables.Contains(table.SourcePath))
                .ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "without-all-legacy-item-tables",
                "1",
                [normalizedPack])));

        var items = Assert.IsType<AuthoritativeItemCatalog>(activation!.Simulation!.Items);
        var spells = Assert.IsType<AuthoritativeSpellCatalog>(activation.Simulation.Spells);
        Assert.NotEmpty(items.Definitions);
        Assert.NotEmpty(spells.Definitions);
    }

    [Fact]
    public void HellfireMonsterTypesDeclareSkeletonSummonCandidates()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            composed.GetRequiredTable("monster_types.tsv").ToCanonicalTsv());

        Assert.All(catalog.Definitions, definition =>
            Assert.True(definition.SkeletonSummonCandidate.HasValue));
        Assert.Equal(
            38,
            catalog.Definitions.Count(definition => definition.SkeletonSummonCandidate == true));
    }

    [Fact]
    public void HellfireMonsterTypesMatchNativeCoreMetadata()
    {
        var repositoryRoot = FindRepositoryRoot();
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var native = TsvTable.Parse(
            "mods/Hellfire/txtdata/monsters/monstdat.tsv",
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "mods",
                "Hellfire",
                "txtdata",
                "monsters",
                "monstdat.tsv")));
        var external = hellfirePack.Tables.Single(table => table.SourcePath == "monster_types.tsv");
        var externalRows = external.Rows.ToDictionary(
            row => row.RequiredInt32("native_monster_id"));
        Assert.Equal(native.Rows.Count, externalRows.Count);

        var fields = new[] {
            (External: "name", Native: "name"),
            (External: "minimum_dungeon_level", Native: "minDunLvl"),
            (External: "maximum_dungeon_level", Native: "maxDunLvl"),
            (External: "monster_level", Native: "level"),
            (External: "minimum_hit_points", Native: "hitPointsMinimum"),
            (External: "maximum_hit_points", Native: "hitPointsMaximum"),
            (External: "minimum_damage", Native: "minDamage"),
            (External: "maximum_damage", Native: "maxDamage"),
            (External: "minimum_special_damage", Native: "minDamageSpecial"),
            (External: "maximum_special_damage", Native: "maxDamageSpecial"),
            (External: "armor_class", Native: "armorClass"),
            (External: "to_hit", Native: "toHit"),
            (External: "to_hit_special", Native: "toHitSpecial"),
            (External: "intelligence", Native: "intelligence"),
            (External: "experience_reward", Native: "exp"),
        };
        for (var nativeId = 0; nativeId < native.Rows.Count; nativeId++) {
            var nativeRow = native.Rows[nativeId];
            var externalRow = externalRows[nativeId];
            foreach (var (externalColumn, nativeColumn) in fields)
                Assert.Equal(nativeRow.Required(nativeColumn), externalRow.Required(externalColumn));

            var nativeFrames = nativeRow.Required("frames[6]").Split(',')[0];
            var nativeRate = nativeRow.Required("rate[6]").Split(',')[0];
            Assert.Equal(nativeFrames, externalRow.Required("stand_animation_frames"));
            Assert.Equal(nativeRate, externalRow.Required("stand_animation_ticks_per_frame"));
        }
    }

    [Fact]
    public void BaseContentPackDeclaresNativeSetLevelObjectPlacements()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var placements = AuthoritativeSetLevelObjectCatalog.LoadTsv(
            "set_level_objects.tsv",
            pack.Tables.Single(table => table.SourcePath == "set_level_objects.tsv").ToCanonicalTsv());

        Assert.Equal(11, placements.Definitions.Count);
        Assert.Equal(6, placements.GetForSetLevel(1).Count);
        Assert.Equal(2, placements.GetForSetLevel(2).Count);
        Assert.Equal(3, placements.GetForSetLevel(5).Count);
        Assert.Equal((64, 34), (
            placements.Definitions.First(placement => placement.PlacementId == 1001).PositionX,
            placements.Definitions.First(placement => placement.PlacementId == 1001).PositionY));
        Assert.Equal(
            2001U,
            placements.Definitions.First(placement => placement.PlacementId == 1001).TemplateEntityId);
    }

    [Fact]
    public void BaseContentPackDeclaresVileBetrayerBookProjectileTemplates()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            pack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
        var templates = AuthoritativeWorldObjectCatalog.LoadTemplates(
            "objects.tsv",
            pack.Tables.Single(table => table.SourcePath == "objects.tsv").ToCanonicalTsv(),
            stableIds);

        var leftBook = Assert.Single(templates, @object => @object.EntityId == 2010);
        Assert.Equal(1U, leftBook.ObjectId);
        Assert.Equal(25U, leftBook.NativeObjectId);
        Assert.Equal(AuthoritativeObjectEffectKind.WorldMutation, leftBook.EffectKind);
        Assert.Equal(9302, leftBook.EffectAmount);
        Assert.Equal(
            AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile,
            leftBook.QuestSequenceFollowUpKind);
        Assert.Equal(10U, leftBook.QuestSequenceFollowUpAmount);
        Assert.Equal(
            (1, -16),
            (leftBook.QuestSequenceFollowUpOffsetX, leftBook.QuestSequenceFollowUpOffsetY));
        Assert.Equal(
            AuthoritativeObjectProjectileSourceKind.ActivatingPlayer,
            leftBook.ProjectileSourceKind);

        var rightBook = Assert.Single(templates, @object => @object.EntityId == 2011);
        Assert.Equal(2U, rightBook.ObjectId);
        Assert.Equal(25U, rightBook.NativeObjectId);
        Assert.Equal(9303, rightBook.EffectAmount);
        Assert.Equal(
            AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile,
            rightBook.QuestSequenceFollowUpKind);
        Assert.Equal(
            (-2, -17),
            (rightBook.QuestSequenceFollowUpOffsetX, rightBook.QuestSequenceFollowUpOffsetY));

        var circle = Assert.Single(templates, @object => @object.EntityId == 2014);
        Assert.Equal(3U, circle.ObjectId);
        Assert.Equal(85U, circle.NativeObjectId);
        Assert.Equal(3016U, circle.QuestId);
        Assert.True(circle.ActivateOnOccupancy);
        Assert.Equal(2, circle.RequiredActivatedLinkedObjects);
        Assert.Equal(
            AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1,
            circle.QuestTransitionKind);
        Assert.Equal(4, circle.QuestTransitionAmount);
        Assert.Equal(
            (0, 10),
            (circle.QuestSequenceFollowUpOffsetX, circle.QuestSequenceFollowUpOffsetY));

        var mutationTable = pack.Tables.Single(table => table.SourcePath == "world_mutations.tsv");
        var mutations = AuthoritativeWorldMutationCatalog.LoadTsv(
            mutationTable.SourcePath,
            mutationTable.ToCanonicalTsv(),
            stableIds,
            ComposedContent.Compose(new ContentManifest("set-level-mutations", "1", [pack])));
        Assert.Equal(
            (AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(5, true),
                AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(5, false)),
            (mutations.Definitions.Single(definition => definition.MutationId == 9302).LevelId,
                mutations.Definitions.Single(definition => definition.MutationId == 9302).MegaTileCopySourceLevelId));
        Assert.Equal(
            (1, 1, 9, 10),
            mutations.Definitions.Single(definition => definition.MutationId == 9302)
                .MegaTileCopyRectangles.Select(rectangle => (
                    rectangle.X,
                    rectangle.Y,
                    rectangle.Width,
                    rectangle.Height)).Single());
        Assert.Equal(
            (11, 1, 10, 10),
            mutations.Definitions.Single(definition => definition.MutationId == 9303)
                .MegaTileCopyRectangles.Select(rectangle => (
                    rectangle.X,
                    rectangle.Y,
                    rectangle.Width,
                    rectangle.Height)).Single());
        Assert.Equal(
            (7, 11, 6, 7),
            mutations.Definitions.Single(definition => definition.MutationId == 9304)
                .MegaTileCopyRectangles.Select(rectangle => (
                    rectangle.X,
                    rectangle.Y,
                    rectangle.Width,
                    rectangle.Height)).Single());

        var placements = AuthoritativeSetLevelObjectCatalog.LoadTsv(
            "set_level_objects.tsv",
            pack.Tables.Single(table => table.SourcePath == "set_level_objects.tsv").ToCanonicalTsv());
        Assert.Equal(2010U, placements.Definitions.Single(placement => placement.PlacementId == 1009).TemplateEntityId);
        Assert.Equal(2011U, placements.Definitions.Single(placement => placement.PlacementId == 1010).TemplateEntityId);
        Assert.Equal(2014U, placements.Definitions.Single(placement => placement.PlacementId == 1011).TemplateEntityId);
    }

    [Fact]
    public void BaseContentPackDeclaresChamberOfBoneCompletionBookTemplates()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            pack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
        var templates = AuthoritativeWorldObjectCatalog.LoadTemplates(
            "objects.tsv",
            pack.Tables.Single(table => table.SourcePath == "objects.tsv").ToCanonicalTsv(),
            stableIds);

        foreach (var entityId in new uint[] { 2012, 2013 }) {
            var book = Assert.Single(templates, @object => @object.EntityId == entityId);
            Assert.Equal(25U, book.NativeObjectId);
            Assert.Equal(3015U, book.QuestId);
            Assert.Equal(
                AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile,
                book.EffectKind);
            Assert.Equal(7, book.EffectAmount);
            Assert.Equal((-2, -4), (book.ProjectileTargetOffsetX, book.ProjectileTargetOffsetY));
            Assert.Equal(
                AuthoritativeObjectProjectileSourceKind.ActivatingPlayer,
                book.ProjectileSourceKind);
            Assert.Equal(
                AuthoritativeObjectQuestTransitionKind.None,
                book.QuestTransitionKind);
        }

        var placements = AuthoritativeSetLevelObjectCatalog.LoadTsv(
            "set_level_objects.tsv",
            pack.Tables.Single(table => table.SourcePath == "set_level_objects.tsv").ToCanonicalTsv());
        Assert.Equal(2012U, placements.Definitions.Single(placement => placement.PlacementId == 1007).TemplateEntityId);
        Assert.Equal(2013U, placements.Definitions.Single(placement => placement.PlacementId == 1008).TemplateEntityId);
    }

    [Fact]
    public void BaseContentPackDeclaresNativeSetLevelMapAssets()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var maps = AuthoritativeSetLevelMapCatalog.LoadTsv(
            "set_level_maps.tsv",
            pack.Tables.Single(table => table.SourcePath == "set_level_maps.tsv").ToCanonicalTsv());

        Assert.Equal(3, maps.Definitions.Count);
        var skeletonKing = Assert.Single(maps.Definitions, map => map.SetLevelId == 1);
        Assert.Equal("levels/l1data/sklkng1.dun", skeletonKing.PreMapAssetPath);
        Assert.Equal("levels/l1data/sklkng2.dun", skeletonKing.ActiveMapAssetPath);
        Assert.Equal("levels/l1data/l1.til", skeletonKing.TileAssetPath);
        Assert.Equal("levels/l1data/l1.sol", skeletonKing.SolAssetPath);
        Assert.Equal("levels/l1data/sklkngt.dun", skeletonKing.TransparencyAssetPath);
        Assert.Equal((83, 44), (skeletonKing.SpawnX, skeletonKing.SpawnY));
        var boneChamber = maps.Definitions.Single(map => map.SetLevelId == 2);
        Assert.Equal("Catacombs", boneChamber.DungeonType);
        Assert.Equal("levels/l2data/bonechat.dun", boneChamber.TransparencyAssetPath);
        var vileBetrayer = maps.Definitions.Single(map => map.SetLevelId == 5);
        Assert.Equal("Cathedral", vileBetrayer.DungeonType);
        Assert.Equal("levels/l1data/vile1.dun", vileBetrayer.TransparencyAssetPath);

        var skeletonTransparency = AuthoritativeDunMapAsset.Load(
            skeletonKing.TransparencyAssetPath,
            pack.Assets.Single(asset => asset.SourcePath == skeletonKing.TransparencyAssetPath).Contents);
        Assert.Equal((37, 25), (skeletonTransparency.MegaWidth, skeletonTransparency.MegaHeight));
        var boneTransparency = AuthoritativeDunMapAsset.Load(
            boneChamber.TransparencyAssetPath,
            pack.Assets.Single(asset => asset.SourcePath == boneChamber.TransparencyAssetPath).Contents);
        Assert.Equal((32, 18), (boneTransparency.MegaWidth, boneTransparency.MegaHeight));
    }

    [Fact]
    public void BaseContentPackDeclaresProceduralUniqueSelectors()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var uniqueTable = pack.Tables.Single(table => table.SourcePath == "unique_monsters.tsv");
        var catalog = AuthoritativeUniqueMonsterCatalog.LoadTsv(
            uniqueTable.SourcePath,
            uniqueTable.ToCanonicalTsv());

        Assert.Equal(
            ["Gharbad", "Lazarus", "Snotspill", "Warlord", "Zhar"],
            catalog.Definitions.Select(definition => definition.Identity).ToArray());
        Assert.All(catalog.Definitions, definition => {
            Assert.Equal(0U, definition.EntityId);
            Assert.Equal(0U, definition.MonsterId);
            Assert.NotEmpty(definition.NativeAi);
        });
    }

    [Fact]
    public void BaseActivationRejectsUnknownExplicitUniqueMonsterType()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var uniqueTable = basePack.Tables.Single(table => table.SourcePath == "unique_monsters.tsv");
        var invalidUniqueRows = string.Join(
            '\n',
            uniqueTable.ToCanonicalTsv()
                .Split('\n')
                .Select(line => line.StartsWith("0\t0\t\tGharbad\t", StringComparison.Ordinal)
                    ? line.Replace("0\t0\t\tGharbad\t", "0\t999\t\tGharbad\t", StringComparison.Ordinal)
                    : line));
        var modifiedPack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == uniqueTable.SourcePath
                    ? TsvTable.Parse(uniqueTable.SourcePath, invalidUniqueRows)
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest(
                    "invalid-unique-monster", "1", [modifiedPack]))));

        Assert.Contains("Gharbad", exception.Message, StringComparison.Ordinal);
        Assert.Contains("unknown monster 999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BaseContentPackDeclaresGharbadDialogueStages()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var table = pack.Tables.Single(table => table.SourcePath == "quest_dialogue_stages.tsv");
        var catalog = AuthoritativeQuestDialogueStageCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv());

        Assert.Equal(
            [(0, 144, 1), (1, 145, 3), (3, 146, 4), (4, 147, 5)],
            catalog.Definitions
                .Where(stage => stage.QuestId == 3003)
                .Select(stage => (
                stage.RequiredVariable1,
                stage.MessageId,
                stage.NextVariable1)));
        var gharbadReward = Assert.Single(
            catalog.Definitions,
            stage => stage.QuestId == 3003 && stage.RequiredVariable1 == 1);
        Assert.Equal(AuthoritativeQuestDialogueRewardKind.RandomUniqueMonsterDrop, gharbadReward.RewardKind);
        Assert.Equal((1, 1), (gharbadReward.RewardOffsetX, gharbadReward.RewardOffsetY));
        Assert.Equal(AuthoritativeQuestDialogueFollowUpKind.None, gharbadReward.FollowUpKind);
        var snotspillIntro = Assert.Single(
            catalog.Definitions,
            stage => stage.QuestId == 3008 && stage.RequiredVariable1 == 0);
        Assert.Equal((12, 1), (snotspillIntro.MessageId, snotspillIntro.NextVariable1));
        var snotspillOpening = Assert.Single(
            catalog.Definitions,
            stage => stage.QuestId == 3008 && stage.RequiredVariable1 == 1);
        Assert.Equal(21, snotspillOpening.MessageId);
        Assert.Equal(22, snotspillOpening.NextMessageId);
        Assert.Equal(AuthoritativeQuestDialogueFollowUpKind.WorldMutation, snotspillOpening.FollowUpKind);
        Assert.Equal(9301U, snotspillOpening.FollowUpAmount);
        var snotspill = Assert.Single(
            catalog.Definitions,
            stage => stage.QuestId == 3008 && stage.RequiredVariable1 == 2);
        Assert.Equal((2136U, true, true, 23, 3), (
            snotspill.RequiredItemId,
            snotspill.ConsumeItem,
            snotspill.CompleteQuest,
            snotspill.MessageId,
            snotspill.NextVariable1));
        Assert.Equal(
            [(0, 110, 2), (2, 110, 3)],
            catalog.Definitions
                .Where(stage => stage.QuestId == 3012)
                .Select(stage => (
                    stage.RequiredVariable1,
                    stage.MessageId,
                    stage.NextVariable1)));
        var zhar = Assert.Single(
            catalog.Definitions,
            stage => stage.QuestId == 3004);
        Assert.Equal((AuthoritativeQuestDialogueRewardKind.NormalItem, 2203U, 8, 1, 1), (
            zhar.RewardKind,
            zhar.RewardItemId,
            zhar.RewardItemLevel,
            zhar.RewardOffsetX,
            zhar.RewardOffsetY));
    }

    [Fact]
    public void BaseContentPackDeclaresTextForAuthoredDialogueStages()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var table = pack.Tables.Single(table => table.SourcePath == "dialogue_messages.tsv");
        var catalog = AuthoritativeDialogueMessageCatalog.LoadTsv(
            table.SourcePath,
            table.ToCanonicalTsv());

        Assert.Equal([12, 21, 22, 23, 110, 144, 145, 146, 147, 148, 254, 255, 256, 322],
            catalog.Definitions.Select(message => message.MessageId));
        Assert.Equal(
            "Master, I have a strange experience to relate. I know that you have a great knowledge of those monstrosities that inhabit the labyrinth, and this is something that I cannot understand for the very life of me... I was awakened during the night by a scraping sound just outside of my tavern. When I looked out from my bedroom, I saw the shapes of small demon-like creatures in the inn yard. After a short time, they ran off, but not before stealing the sign to my inn. I don't know why the demons would steal my sign but leave my family in peace... 'tis strange, no?",
            catalog.TryGet(12, out var ogden) ? ogden.Text : null);
        Assert.Equal(
            "Something for you I am making. Again, not kill Gharbad. Live and give good. \n \nYou take this as proof I keep word...",
            catalog.TryGet(145, out var gharbad) ? gharbad.Text : null);
        Assert.Equal(
            "You kill uglies, get banner. You bring to me, or else...",
            catalog.TryGet(22, out var snotspill) ? snotspill.Text : null);
        catalog.ValidateMessageIds([144, 145, 146, 147, 148, 21, 22, 23, 110, 254, 255, 256]);
    }

    [Fact]
    public void BaseContentPackExternalizesAuthoredQuestContent()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            pack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            pack.Tables.Single(table => table.SourcePath == "items.tsv").ToCanonicalTsv(),
            stableIds: stableIds);
        var uniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            pack.Tables.Single(table => table.SourcePath == "unique_items.tsv").ToCanonicalTsv(),
            stableIds: stableIds);
        var questTypes = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            pack.Tables.Single(table => table.SourcePath == "quest_types.tsv").ToCanonicalTsv());
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            pack.Tables.Single(table => table.SourcePath == "quests.tsv").ToCanonicalTsv(),
            questTypes,
            stableIds);
        var magicRock = Assert.Single(quests, quest => quest.QuestId == 3001);
        Assert.Equal((115, 117, 0, 0U, 62, 63, 2135U, 1, 1004U, 5, 61, 64), (
            magicRock.MessageId,
            magicRock.TurnInMessageId,
            magicRock.TurnInTownerId,
            magicRock.TurnInLevelId,
            magicRock.TurnInPositionX,
            magicRock.TurnInPositionY,
            magicRock.TurnInItemId,
            magicRock.TurnInRequiredVariable2,
            magicRock.TurnInRewardUniqueId,
            magicRock.TurnInRewardItemLevel,
            magicRock.TurnInRewardPositionX,
            magicRock.TurnInRewardPositionY));
        Assert.Equal("magic_rock", items.GetRequired(2135).StableSymbol);
        Assert.True(uniques.TryGet(1004, out var empyreanBand));
        Assert.NotNull(empyreanBand);
        Assert.Equal((2195U, 8000, 4), (
            empyreanBand.BaseItemId,
            empyreanBand.Value,
            empyreanBand.Powers.Count));
        var effects = AuthoritativeItemQuestEffectCatalog.LoadTsv(
            "item_quest_effects.tsv",
            pack.Tables.Single(table => table.SourcePath == "item_quest_effects.tsv").ToCanonicalTsv(),
            stableIds);
        Assert.Equal(5, effects.Definitions.Count);
        var pickupEffect = Assert.Single(effects.Definitions, effect => effect.ItemId == 2135);
        Assert.Equal((2135U, 3001U, AuthoritativeItemQuestEffectKind.ActivateQuest, 1), (
            pickupEffect.ItemId,
            pickupEffect.QuestId,
            pickupEffect.EffectKind,
            pickupEffect.EffectAmount));
        var anvilPickupEffect = Assert.Single(effects.Definitions, effect => effect.ItemId == 2138);
        Assert.Equal((2138U, 3011U, AuthoritativeItemQuestEffectKind.ActivateQuest, 1), (
            anvilPickupEffect.ItemId,
            anvilPickupEffect.QuestId,
            anvilPickupEffect.EffectKind,
            anvilPickupEffect.EffectAmount));
        var mushroomPickupEffect = Assert.Single(effects.Definitions, effect => effect.ItemId == 2139);
        Assert.Equal((2139U, 3002U, AuthoritativeItemQuestEffectKind.SetVariable1, 4), (
            mushroomPickupEffect.ItemId,
            mushroomPickupEffect.QuestId,
            mushroomPickupEffect.EffectKind,
            mushroomPickupEffect.EffectAmount));
        Assert.Equal(3, mushroomPickupEffect.RequiredVariable1);
        var opticAmuletPickupEffect = Assert.Single(
            effects.Definitions,
            effect => effect.ItemId == 114);
        Assert.Equal((114U, 3009U, AuthoritativeItemQuestEffectKind.CompleteQuest, 0), (
            opticAmuletPickupEffect.ItemId,
            opticAmuletPickupEffect.QuestId,
            opticAmuletPickupEffect.EffectKind,
            opticAmuletPickupEffect.EffectAmount));
        var valorPickupEffect = Assert.Single(
            effects.Definitions,
            effect => effect.ItemId == 118);
        Assert.Equal((118U, 3010U, AuthoritativeItemQuestEffectKind.CompleteQuest, 0), (
            valorPickupEffect.ItemId,
            valorPickupEffect.QuestId,
            valorPickupEffect.EffectKind,
            valorPickupEffect.EffectAmount));

        var anvilQuest = Assert.Single(quests, quest => quest.QuestId == 3011);
        Assert.Equal((88, 90, 0, 0U, 62, 63, 2138U, 1, 1010U, 1, 61, 64), (
            anvilQuest.MessageId,
            anvilQuest.TurnInMessageId,
            anvilQuest.TurnInTownerId,
            anvilQuest.TurnInLevelId,
            anvilQuest.TurnInPositionX,
            anvilQuest.TurnInPositionY,
            anvilQuest.TurnInItemId,
            anvilQuest.TurnInRequiredVariable2,
            anvilQuest.TurnInRewardUniqueId,
            anvilQuest.TurnInRewardItemLevel,
            anvilQuest.TurnInRewardPositionX,
            anvilQuest.TurnInRewardPositionY));
        Assert.Equal("anvil_of_fury", items.GetRequired(2138).StableSymbol);
        Assert.Equal("griswold_s_edge", items.GetRequired(119).StableSymbol);
        Assert.Equal("optic_amulet", items.GetRequired(114).StableSymbol);
        Assert.Equal("arkaine_s_valor", items.GetRequired(118).StableSymbol);
        Assert.True(uniques.TryGet(1005, out var opticAmulet));
        Assert.NotNull(opticAmulet);
        Assert.Equal((114U, 9750, 4), (
            opticAmulet.BaseItemId,
            opticAmulet.Value,
            opticAmulet.Powers.Count));
        Assert.True(uniques.TryGet(1009, out var arkaineValor));
        Assert.NotNull(arkaineValor);
        Assert.Equal((118U, 42000, 4), (
            arkaineValor.BaseItemId,
            arkaineValor.Value,
            arkaineValor.Powers.Count));
        Assert.True(uniques.TryGet(1010, out var griswoldsEdge));
        Assert.NotNull(griswoldsEdge);
        Assert.Equal((119U, 42000, 6), (
            griswoldsEdge.BaseItemId,
            griswoldsEdge.Value,
            griswoldsEdge.Powers.Count));

        var ogdenQuest = Assert.Single(
            AuthoritativeQuestCatalog.LoadTsv(
                "quest_states.tsv",
                pack.Tables.Single(table => table.SourcePath == "quest_states.tsv").ToCanonicalTsv(),
                questTypes,
                stableIds),
            quest => quest.QuestId == 3008);
        Assert.Equal((12, 13, 3, 0U, 55, 62, 2136U, 1, 1007U, 4, 54, 63), (
            ogdenQuest.MessageId,
            ogdenQuest.TurnInMessageId,
            ogdenQuest.TurnInTownerId,
            ogdenQuest.TurnInLevelId,
            ogdenQuest.TurnInPositionX,
            ogdenQuest.TurnInPositionY,
            ogdenQuest.TurnInItemId,
            ogdenQuest.TurnInRequiredVariable2,
            ogdenQuest.TurnInRewardUniqueId,
            ogdenQuest.TurnInRewardItemLevel,
            ogdenQuest.TurnInRewardPositionX,
            ogdenQuest.TurnInRewardPositionY));
        Assert.Equal("tavern_sign", items.GetRequired(2136).StableSymbol);
        Assert.True(uniques.TryGet(1007, out var harlequinCrest));
        Assert.NotNull(harlequinCrest);
        Assert.Equal((116U, 4000, 5), (
            harlequinCrest.BaseItemId,
            harlequinCrest.Value,
            harlequinCrest.Powers.Count));

        var butcherQuest = Assert.Single(
            AuthoritativeQuestCatalog.LoadTsv(
                "quest_states.tsv",
                pack.Tables.Single(table => table.SourcePath == "quest_states.tsv").ToCanonicalTsv(),
                questTypes,
                stableIds),
            quest => quest.QuestId == 3007);
        Assert.Equal((65, 53U, 2, 0U, 24, 32, true, 1), (
            butcherQuest.MessageId,
            butcherQuest.CompletionMonsterId,
            butcherQuest.TurnInTownerId,
            butcherQuest.TurnInLevelId,
            butcherQuest.TurnInPositionX,
            butcherQuest.TurnInPositionY,
            butcherQuest.TurnInActivationOnly,
            butcherQuest.TurnInActivationVariable1));

        var monsterTypes = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            pack.Tables.Single(table => table.SourcePath == "monster_types.tsv").ToCanonicalTsv(),
            stableIds);
        var butcher = Assert.Single(monsterTypes.Definitions, monster => monster.MonsterId == 53);
        Assert.Equal((51U, "The Butcher", "Butcher"), (
            butcher.NativeMonsterId,
            butcher.Name,
            butcher.NativeAi));

        var skeletonKingQuest = Assert.Single(
            AuthoritativeQuestCatalog.LoadTsv(
                "quest_states.tsv",
                pack.Tables.Single(table => table.SourcePath == "quest_states.tsv").ToCanonicalTsv(),
                questTypes,
                stableIds),
            quest => quest.QuestId == 3013);
        Assert.Equal((3U, 52U, true, 1, false), (
            skeletonKingQuest.LevelId,
            skeletonKingQuest.CompletionMonsterId,
            skeletonKingQuest.ActivateOnRoute,
            skeletonKingQuest.RouteActivationVariable1,
            skeletonKingQuest.Active));
        var skeletonKing = Assert.Single(
            monsterTypes.Definitions,
            monster => monster.MonsterId == 52);
        Assert.Equal((50U, "Skeleton King", "SkeletonKing"), (
            skeletonKing.NativeMonsterId,
            skeletonKing.Name,
            skeletonKing.NativeAi));
    }

    [Fact]
    public void OptionalProceduralLevelTableMaterializesAuthoritativeGeometry()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var generation = basePack.Tables
            .Single(table => table.SourcePath == "level_generation.tsv")
            .ToCanonicalTsv()
            .Replace("7\t24\t30", "7\t25\t30", StringComparison.Ordinal);
        var procedural = basePack.Tables
            .Single(table => table.SourcePath == "procedural_levels.tsv")
            .ToCanonicalTsv()
            + "25\tgenerated_test\t25\t21\t1234\t60\t70\t0\t0\t\n";
        var tables = basePack.Tables
            .Where(table => table.SourcePath is not ("level_generation.tsv" or "procedural_levels.tsv"))
            .Append(TsvTable.Parse("level_generation.tsv", generation))
            .Append(TsvTable.Parse("procedural_levels.tsv", procedural))
            .ToArray();
        var pack = new ContentPack(
            basePack.Id,
            basePack.Version,
            tables,
            dependencies: [],
            overlays: [],
            schema: basePack.Schema,
            assets: basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("procedural-test", "1", [pack])));
        var generated = Assert.Single(
            activation.Simulation.WorldState.World!.Levels,
            level => level.LevelId == 25);

        Assert.Equal("generated_test", generated.StableSymbol);
        Assert.True(generated.BlockedCells.Count > 0);
        Assert.True(generated.MissileBlockedCells.Count >= generated.BlockedCells.Count);
        Assert.True(generated.IsWalkable(1, 1));
    }

    [Fact]
    public void BaseContentPackShipsProceduralLevelsForTheOrdinaryDungeonRange()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var catalog = AuthoritativeProceduralLevelCatalog.LoadTsv(
            "procedural_levels.tsv",
            pack.Tables.Single(table => table.SourcePath == "procedural_levels.tsv").ToCanonicalTsv());

        Assert.Equal(14, catalog.Definitions.Count);
        Assert.Equal(
            Enumerable.Range(3, 14).Select(levelId => (uint)levelId),
            catalog.Definitions.Select(definition => definition.LevelId));
        Assert.Equal("caves", catalog.Definitions.First().StableSymbol);
        Assert.Equal("hell_depth_16", catalog.Definitions.Last().StableSymbol);
        Assert.All(catalog.Definitions, definition => {
            Assert.Equal(40, definition.Width);
            Assert.Equal(40, definition.Height);
            Assert.False(definition.IsDefaultStart);
            Assert.False(definition.SuppressMultiplayerDeathDrops);
        });
    }

    [Fact]
    public void OptionalProceduralOriginRowsActivateAgainstGeneratedGeometry()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var originTable = TsvTable.Parse(
            "procedural_set_piece_origins.tsv",
            "set_level_id\tset_level_symbol\tlevel_id\tlevel_symbol\tfootprint_width\tfootprint_height\tselection_seed\talignment\n"
                + "6\tSL_ARENA_CHURCH\t3\tcaves\t4\t4\t1234\t2\n");
        var pack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables.Append(originTable).ToArray(),
            basePack.Dependencies,
            basePack.Overlays,
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("procedural-origin", "1", [pack])));

        var definition = Assert.Single(activation.ProceduralSetPieceOrigins!.Definitions);
        var origin = Assert.Single(activation.Simulation.SetLevelObjectOrigins);
        Assert.Equal((6, 3U, "SL_ARENA_CHURCH", "caves"), (
            definition.SetLevelId,
            definition.LevelId,
            definition.SetLevelSymbol,
            definition.LevelSymbol));
        Assert.Equal(3U, origin.Value.LevelId);
        Assert.Equal(0, origin.Value.PositionX % 2);
        Assert.Equal(0, origin.Value.PositionY % 2);
    }

    [Fact]
    public void ExplicitLevelRowsTakePrecedenceOverShippedProceduralRows()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var explicitLevels = TsvTable.Parse(
            "levels.tsv",
            basePack.Tables.Single(table => table.SourcePath == "levels.tsv").ToCanonicalTsv()
                + "3\tcaves\t38\t39\tnone\tnone\t0\t0\n");
        var tables = basePack.Tables
            .Where(table => table.SourcePath != "levels.tsv")
            .Append(explicitLevels)
            .ToArray();
        var pack = new ContentPack(
            basePack.Id,
            basePack.Version,
            tables,
            schema: basePack.Schema,
            assets: basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("explicit-level", "1", [pack])));
        var level = Assert.Single(
            activation.Simulation.WorldState.World!.Levels,
            candidate => candidate.LevelId == 3);

        Assert.Equal((38, 39, "caves"), (level.Width, level.Height, level.StableSymbol));
    }

    [Fact]
    public void CompleteSetLevelBinaryBundleMaterializesDuringContentActivation()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var mapTable = TsvTable.Parse(
            "set_level_maps.tsv",
            "set_level_id\tset_level_symbol\tpre_map_asset\tactive_map_asset\ttil_asset\tsol_asset\ttransparency_asset\tdungeon_type\tspawn_x\tspawn_y\n"
                + "1\tSL_SKELKING\tfixture/pre.dun\tfixture/active.dun\tfixture/map.til\tfixture/map.sol\t\tCathedral\t4\t5\n");
        var objectTable = TsvTable.Parse(
            "objects.tsv",
            "entity_id\tobject_id\tobject_symbol\tlevel_id\tlevel_symbol\tposition_x\tposition_y\tactivated\tquest_id\tquest_symbol\teffect_kind\teffect_amount\teffect_item_symbol\tmissile_passable\tbreakable\tdestroyed\ttrap_armed\ttemplate_only\tquest_sequence_follow_up_kind\tquest_sequence_follow_up_amount\tquest_sequence_follow_up_monster_symbol\tquest_sequence_follow_up_offset_x\tquest_sequence_follow_up_offset_y\tquest_sequence_follow_up_world_mutation_id\teffect_spell_symbol\tprojectile_target_offset_x\tprojectile_target_offset_y\tprojectile_source\tquest_transition_kind\tquest_transition_amount\tquest_transition_required_variable1\tquest_transition_required_variable2\tactivate_on_occupancy\trequired_activated_linked_objects\tnative_object_id\treward_kind\treward_id\treward_item_level\treward_offset_x\treward_offset_y\tdialogue_message_id\n"
                + "9001\t40\t\t\t\t\t\t0\t0\t\tGrantSpellLevel\t7\t\t0\t0\t0\t0\t1\tNone\t0\t\t0\t0\t0\t\t0\t0\tObject\tNone\t0\t\t\tfalse\t0\t0\tNone\t0\t0\t0\t0\t0\n"
                + "9002\t41\t\t\t\t\t\t0\t0\t\tWorldMutation\t9100\t\t0\t0\t0\t0\t1\tNone\t0\t\t0\t0\t0\t\t0\t0\tObject\tNone\t0\t\t\tfalse\t0\t0\tNone\t0\t0\t0\t0\t0\n");
        var placementTable = TsvTable.Parse(
            "set_level_objects.tsv",
            "placement_id\tset_level_id\tset_level_symbol\tobject_id\tposition_x\tposition_y\ttemplate_x\ttemplate_y\ttemplate_width\ttemplate_height\ttemplate_entity_id\n"
                + "9001\t1\tSL_SKELKING\t40\t4\t5\t0\t0\t1\t1\t9001\n"
                + "9002\t1\tSL_SKELKING\t41\t6\t5\t0\t0\t1\t1\t9002\n");
        var mutationTable = TsvTable.Parse(
            "world_mutations.tsv",
            $"mutation_id\tlevel_id\tanchor_entity_id\tanchor_unique_identity\tblocked_cells_add\tblocked_cells_remove\tmissile_blocked_cells_add\tmissile_blocked_cells_remove\tgeometry_rect_add\tgeometry_rect_remove\ttransparency_rects\tobject_layer_rects\tpiece_layer_rects\tanchor_mega_offset_x\tanchor_mega_offset_y\tmega_tile_assignments\tcreate_transparency_layer\tmega_tile_copy_source_level_id\tmega_tile_copy_rects\tpiece_property_overrides\tmonster_layer_rects\tmega_tile_asset_path\ttile_asset_path\tsol_asset_path\tnative_layer_asset_path\n"
                + $"9100\t{AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true)}\t\t\t4\t\t\t\t\t\t\t\t\t0\t0\tnone\tfalse\t0\tnone\tnone\t\tnone\tnone\tnone\tnone\n");
        var warpTable = TsvTable.Parse(
            "warps.tsv",
            "warp_id\twarp_symbol\tsource_level_id\tsource_level_symbol\ttrigger_x\ttrigger_y\ttarget_level_id\ttarget_level_symbol\ttarget_x\ttarget_y\trequired_quest_id\trequired_quest_symbol\trequired_quest_completed\ttarget_set_level_id\ttarget_set_level_symbol\ttarget_set_level_active_map\n"
                + "9901\t\t0\t\t1\t1\t\t\t4\t5\t0\t\t0\t\tSL_SKELKING\ttrue\n");
        var portalTable = TsvTable.Parse(
            "portals.tsv",
            "portal_id\tportal_symbol\tsource_level_id\tsource_level_symbol\tsource_position_x\tsource_position_y\tdestination_level_id\tdestination_level_symbol\tdestination_position_x\tdestination_position_y\tdestination_set_level_id\tdestination_set_level_symbol\tdestination_set_level_active_map\n"
                + "9902\t\t0\t\t1\t1\t\t\t4\t5\t\tSL_SKELKING\ttrue\n");
        var overlayPack = new ContentPack(
            "geometry-fixture",
            "1.0.0",
            [mapTable, objectTable, placementTable, mutationTable, warpTable, portalTable],
            dependencies: [basePack.Id],
            overlays: [
                new ContentTableOverlay("set_level_maps.tsv", ["set_level_id"]),
                new ContentTableOverlay("objects.tsv", ["entity_id"]),
                new ContentTableOverlay("set_level_objects.tsv", ["placement_id"]),
                new ContentTableOverlay("world_mutations.tsv", ["mutation_id"]),
                new ContentTableOverlay("warps.tsv", ["warp_id"]),
                new ContentTableOverlay("portals.tsv", ["portal_id"]),
            ],
            assets: [
                new ContentAsset("fixture/pre.dun", CreateSyntheticDun()),
                new ContentAsset("fixture/active.dun", CreateSyntheticDun()),
                new ContentAsset("fixture/map.til", CreateSyntheticTil()),
                new ContentAsset("fixture/map.sol", CreateSyntheticSol()),
            ]);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "geometry-fixture",
                "1",
                [basePack, overlayPack])));

        var levels = activation.Simulation.WorldState.World!.Levels;
        var pre = Assert.Single(
            levels,
            level => level.LevelId
                == AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: false));
        var active = Assert.Single(
            levels,
            level => level.LevelId
                == AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true));
        Assert.Equal("SL_SKELKING", pre.StableSymbol);
        Assert.Equal("SL_SKELKING", active.StableSymbol);
        Assert.Equal(80, pre.Width);
        Assert.Equal(80, active.Height);
        var setLevelWarp = Assert.Single(
            activation.Simulation.WorldState.World.WarpTargets,
            warp => warp.WarpId == 9901);
        Assert.Equal(active.LevelId, setLevelWarp.TargetLevelId);
        Assert.Equal(1, setLevelWarp.TargetSetLevelId);
        Assert.Equal("SL_SKELKING", setLevelWarp.TargetSetLevelSymbol);
        Assert.True(setLevelWarp.TargetSetLevelActiveMap);
        var setLevelPortal = Assert.Single(
            activation.Simulation.WorldState.Portals.Values,
            portal => portal.PortalId == 9902);
        Assert.Equal(active.LevelId, setLevelPortal.DestinationLevelId);
        Assert.Equal(1, setLevelPortal.DestinationSetLevelId);
        Assert.Equal("SL_SKELKING", setLevelPortal.DestinationSetLevelSymbol);
        Assert.True(setLevelPortal.DestinationSetLevelActiveMap);
        var gate = Assert.Single(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.EntityId
                == AuthoritativeSetLevelObjectCatalog.RuntimeObjectEntityIdBase + 1002);
        Assert.Equal(2U, gate.ObjectId);
        Assert.Equal(active.LevelId, gate.LevelId);
        Assert.Equal(64, gate.PositionX);
        Assert.Equal(59, gate.PositionY);

        var authored = Assert.Single(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.EntityId
                == AuthoritativeSetLevelObjectCatalog.RuntimeObjectEntityIdBase + 9001);
        Assert.Equal(40U, authored.ObjectId);
        Assert.Equal(AuthoritativeObjectEffectKind.GrantSpellLevel, authored.EffectKind);
        Assert.Equal(7, authored.EffectAmount);
        Assert.Equal(active.LevelId, authored.LevelId);
        Assert.Equal((4, 5), (authored.PositionX, authored.PositionY));

        var mutationObject = Assert.Single(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.EntityId
                == AuthoritativeSetLevelObjectCatalog.RuntimeObjectEntityIdBase + 9002);
        Assert.Equal(AuthoritativeObjectEffectKind.WorldMutation, mutationObject.EffectKind);
        Assert.Equal(9100, mutationObject.EffectAmount);
        Assert.Equal(active.LevelId, mutationObject.LevelId);
        Assert.Equal(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true),
            activation.Simulation.WorldState.WorldMutations!.Definitions
                .Single(mutation => mutation.MutationId == 9100)
                .LevelId);
    }

    [Fact]
    public void BaseContentActivationRejectsMalformedBooleanCandidate()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var classes = basePack.Tables.Single(table => table.SourcePath == "classes.tsv");
        var malformedClasses = TsvTable.Parse(
            classes.SourcePath,
            string.Join(
                '\n',
                classes.ToCanonicalTsv()
                    .Split('\n')
                    .Select((line, index) => index == 1
                        ? line.Replace("\ttrue\t", "\t2\t", StringComparison.Ordinal)
                        : line)));
        var malformedPack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == classes.SourcePath ? malformedClasses : table)
                .ToArray(),
            dependencies: [],
            overlays: [],
            schema: basePack.Schema);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("malformed", "1", [malformedPack]))));

        Assert.Contains("Boolean", exception.Message, StringComparison.Ordinal);
        Assert.Contains("is_default", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShippedItemDefinitionsUseTheCompleteStableItemNamespace()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));

        var baseStableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
        var baseItems = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            basePack.Tables.Single(table => table.SourcePath == "items.tsv").ToCanonicalTsv(),
            stableIds: baseStableIds);
        var baseItemIds = baseStableIds.Entries
            .Where(entry => entry.Kind == "item")
            .ToArray();

        Assert.Equal(baseItems.Definitions.Count, baseItemIds.Length);
        Assert.All(baseItems.Definitions, definition =>
            Assert.False(string.IsNullOrWhiteSpace(definition.StableSymbol)));
        baseStableIds.ValidateReferences("item", id => baseItems.TryGet(id, out _));
        var baseArmorExpectations = new[] {
            ("helm", 152U, 50, 4, 6),
            ("skull_cap", 153U, 49, 2, 4),
            ("great_helm", 154U, 53, 10, 15),
            ("crown", 155U, 52, 8, 12),
            ("rags", 156U, 55, 2, 6),
            ("studded_leather_armor", 157U, 61, 15, 17),
            ("cloak", 158U, 56, 3, 7),
            ("leather_armor", 159U, 59, 10, 13),
            ("robe", 160U, 57, 4, 7),
            ("chain_mail", 161U, 63, 18, 22),
            ("breast_plate", 162U, 65, 20, 24),
            ("cape", 163U, 54, 1, 5),
            ("plate_mail", 164U, 67, 42, 50),
            ("full_plate_mail", 165U, 70, 60, 75),
            ("kite_shield", 167U, 74, 8, 15),
            ("small_shield", 168U, 72, 3, 8),
            ("large_shield", 169U, 73, 5, 10),
            ("gothic_shield", 170U, 76, 14, 18),
        };
        foreach (var (symbol, itemId, nativeIndex, minimumArmor, maximumArmor) in baseArmorExpectations) {
            Assert.Equal(itemId, baseStableIds.Resolve("item", symbol));
            var item = baseItems.GetRequired(itemId);
            Assert.Equal(symbol, item.StableSymbol);
            Assert.Equal(nativeIndex, item.NativeItemIndex);
            Assert.Equal(minimumArmor, item.MinArmorClass);
            Assert.Equal(maximumArmor, item.MaxArmorClass);
            Assert.False(item.Usable);
            Assert.Equal(DiabloItemSpellSourceKind.None, item.SpellSourceKind);
            Assert.Equal(DiabloItemActionKind.None, item.ItemActionKind);
        }
        var baseRemainingArmorExpectations = new[] {
            ("cap", 2169U, 48, 1, 3),
            ("full_helm", 2170U, 51, 6, 8),
            ("quilted_armor", 2171U, 58, 7, 10),
            ("hard_leather_armor", 2172U, 60, 11, 14),
            ("ring_mail", 2173U, 62, 17, 20),
            ("scale_mail", 2174U, 64, 23, 28),
            ("splint_mail", 2175U, 66, 30, 35),
            ("field_plate", 2176U, 68, 40, 45),
            ("gothic_plate", 2177U, 69, 50, 60),
            ("buckler_standard", 2178U, 71, 1, 5),
            ("tower_shield", 2179U, 75, 12, 20),
        };
        foreach (var (symbol, itemId, nativeIndex, minimumArmor, maximumArmor) in baseRemainingArmorExpectations) {
            Assert.Equal(itemId, baseStableIds.Resolve("item", symbol));
            var item = baseItems.GetRequired(itemId);
            Assert.Equal(symbol, item.StableSymbol);
            Assert.Equal(nativeIndex, item.NativeItemIndex);
            Assert.Equal(minimumArmor, item.MinArmorClass);
            Assert.Equal(maximumArmor, item.MaxArmorClass);
            Assert.Contains("Armor", item.GenerationTags, StringComparer.OrdinalIgnoreCase);
            Assert.False(item.Usable);
            Assert.Equal(DiabloItemSpellSourceKind.None, item.SpellSourceKind);
            Assert.Equal(DiabloItemActionKind.None, item.ItemActionKind);
        }
        var baseWeaponExpectations = new[] {
            ("dagger", 127U, 118, 1, 4),
            ("short_sword_standard", 2160U, 119, 2, 6),
            ("falchion", 129U, 120, 4, 8),
            ("scimitar", 134U, 121, 3, 7),
            ("claymore", 132U, 122, 1, 12),
            ("blade", 2161U, 123, 3, 8),
            ("sabre", 128U, 124, 1, 8),
            ("long_sword", 131U, 125, 2, 10),
            ("broad_sword", 130U, 126, 4, 12),
            ("bastard_sword", 135U, 127, 6, 15),
            ("two_handed_sword", 136U, 128, 8, 16),
            ("great_sword", 2162U, 129, 10, 20),
            ("small_axe", 140U, 130, 2, 10),
            ("axe", 2163U, 131, 4, 12),
            ("large_axe", 137U, 132, 6, 16),
            ("broad_axe", 138U, 133, 8, 20),
            ("battle_axe", 139U, 134, 10, 25),
            ("great_axe", 141U, 135, 12, 30),
            ("mace", 142U, 136, 1, 8),
            ("morning_star", 145U, 137, 1, 10),
            ("war_hammer", 146U, 138, 5, 9),
            ("spiked_club", 2164U, 139, 3, 6),
            ("club", 2165U, 140, 1, 6),
            ("flail", 144U, 141, 2, 12),
            ("maul", 143U, 142, 6, 20),
            ("short_bow", 124U, 143, 1, 4),
            ("hunters_bow", 122U, 144, 2, 5),
            ("long_bow", 123U, 145, 1, 6),
            ("composite_bow", 2166U, 146, 3, 6),
            ("short_battle_bow", 126U, 147, 3, 7),
            ("long_battle_bow", 2167U, 148, 1, 10),
            ("short_war_bow", 2168U, 149, 4, 8),
            ("long_war_bow", 125U, 150, 1, 14),
            ("short_staff", 149U, 151, 2, 4),
            ("long_staff", 147U, 152, 4, 8),
            ("composite_staff", 150U, 153, 5, 10),
            ("quarter_staff", 151U, 154, 6, 12),
            ("war_staff", 148U, 155, 8, 16),
        };
        foreach (var (symbol, itemId, nativeIndex, minimumDamage, maximumDamage) in baseWeaponExpectations) {
            Assert.Equal(itemId, baseStableIds.Resolve("item", symbol));
            var item = baseItems.GetRequired(itemId);
            Assert.Equal(symbol, item.StableSymbol);
            Assert.Equal(nativeIndex, item.NativeItemIndex);
            Assert.Equal(minimumDamage, item.MinDamage);
            Assert.Equal(maximumDamage, item.MaxDamage);
            Assert.Contains("Weapon", item.GenerationTags, StringComparer.OrdinalIgnoreCase);
            Assert.False(item.Usable);
            Assert.Equal(DiabloItemSpellSourceKind.None, item.SpellSourceKind);
            Assert.Equal(DiabloItemActionKind.None, item.ItemActionKind);
        }
        var baseConsumableExpectations = new[] {
            ("potion_healing_standard", 2180U, 77, 3, 1, 50),
            ("potion_full_healing", 2181U, 78, 2, 1, 150),
            ("potion_mana_standard", 2182U, 79, 6, 1, 50),
            ("potion_full_mana", 2183U, 80, 7, 1, 150),
            ("potion_rejuvenation_standard", 2184U, 81, 19, 3, 120),
            ("potion_full_rejuvenation_standard", 2185U, 82, 20, 7, 600),
            ("oil_of_blacksmith_standard", 2186U, 83, 37, 1, 100),
            ("oil_of_accuracy_standard", 2187U, 84, 32, 1, 500),
            ("oil_of_sharpness_standard", 2188U, 85, 34, 1, 500),
            ("oil_standard", 2189U, 86, 31, 10, 0),
            ("elixir_of_strength_standard", 2190U, 87, 11, 15, 5000),
            ("elixir_of_magic_standard", 2191U, 88, 12, 15, 5000),
            ("elixir_of_dexterity_standard", 2192U, 89, 13, 15, 5000),
            ("elixir_of_vitality_standard", 2193U, 90, 14, 20, 5000),
        };
        foreach (var (symbol, itemId, nativeIndex, miscId, minimumLevel, value) in baseConsumableExpectations) {
            Assert.Equal(itemId, baseStableIds.Resolve("item", symbol));
            var item = baseItems.GetRequired(itemId);
            Assert.Equal(symbol, item.StableSymbol);
            Assert.Equal(nativeIndex, item.NativeItemIndex);
            Assert.Equal(miscId, item.MiscId);
            Assert.Equal(minimumLevel, item.MinimumMonsterLevel);
            Assert.Equal(value, item.Value);
            Assert.Contains("Misc", item.GenerationTags, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Belt", item.GenerationTags, StringComparer.OrdinalIgnoreCase);
            Assert.True(item.Usable);
            Assert.Equal(DiabloItemSpellSourceKind.None, item.SpellSourceKind);
            Assert.Equal(
                miscId switch {
                    3 => DiabloItemActionKind.RestorePartialLife,
                    2 => DiabloItemActionKind.RestoreFullLife,
                    6 => DiabloItemActionKind.RestorePartialMana,
                    7 => DiabloItemActionKind.RestoreFullMana,
                    19 => DiabloItemActionKind.RestorePartialLifeAndMana,
                    20 => DiabloItemActionKind.RestoreFullLifeAndMana,
                    11 => DiabloItemActionKind.IncreaseStrength,
                    12 => DiabloItemActionKind.IncreaseMagic,
                    13 => DiabloItemActionKind.IncreaseDexterity,
                    14 => DiabloItemActionKind.IncreaseVitality,
                    37 => DiabloItemActionKind.ApplyBlacksmithOil,
                    32 => DiabloItemActionKind.ApplyAccuracyOil,
                    34 => DiabloItemActionKind.ApplySharpnessOil,
                    31 => DiabloItemActionKind.ApplyFortitudeOil,
                    _ => DiabloItemActionKind.None,
                },
                item.ItemActionKind);
        }
        var baseCathedralMap = baseItems.GetRequired(43);
        Assert.Equal(43U, baseStableIds.Resolve("item", "cathedral_map"));
        Assert.Equal("cathedral_map", baseCathedralMap.StableSymbol);
        Assert.Equal(22, baseCathedralMap.NativeItemIndex);
        Assert.Equal(43, baseCathedralMap.MiscId);
        Assert.True(baseCathedralMap.Usable);
        Assert.Equal(DiabloItemActionKind.OpenCathedralMap, baseCathedralMap.ItemActionKind);
        Assert.Equal(1, baseCathedralMap.ItemActionAmount);
        foreach (var (symbol, itemId) in new[] {
            ("torn_note_1", 2001U),
            ("torn_note_2", 2002U),
            ("torn_note_3", 2003U),
            ("reconstructed_note", 2004U),
        }) {
            var note = baseItems.GetRequired(itemId);
            Assert.Equal(itemId, baseStableIds.Resolve("item", symbol));
            Assert.True(note.Usable);
            Assert.Equal(DiabloItemActionKind.ReadQuestNote, note.ItemActionKind);
            Assert.Equal(1, note.ItemActionAmount);
            Assert.Equal(322, note.ItemActionMessageId);
        }
        var baseJewelryExpectations = new[] {
            ("ring_level_5", 2195U, 156, 5, 26, 1000),
            ("ring_level_10", 2196U, 157, 10, 26, 1000),
            ("ring_level_15", 2197U, 158, 15, 26, 1000),
            ("amulet_level_8", 2198U, 159, 8, 27, 1200),
            ("amulet_level_16", 2199U, 160, 16, 27, 1200),
        };
        foreach (var (symbol, itemId, nativeIndex, minimumLevel, miscId, value) in baseJewelryExpectations) {
            Assert.Equal(itemId, baseStableIds.Resolve("item", symbol));
            var item = baseItems.GetRequired(itemId);
            Assert.Equal(symbol, item.StableSymbol);
            Assert.Equal(nativeIndex, item.NativeItemIndex);
            Assert.Equal(minimumLevel, item.MinimumMonsterLevel);
            Assert.Equal(miscId, item.MiscId);
            Assert.Equal(value, item.Value);
            Assert.Contains("Misc", item.GenerationTags, StringComparer.OrdinalIgnoreCase);
            Assert.False(item.Usable);
            Assert.Equal(DiabloItemSpellSourceKind.None, item.SpellSourceKind);
            Assert.Equal(DiabloItemActionKind.None, item.ItemActionKind);
        }
        var arenaPotion = baseItems.GetRequired(2200);
        Assert.Equal(2200U, baseStableIds.Resolve("item", "arena_potion"));
        Assert.Equal("arena_potion", arenaPotion.StableSymbol);
        Assert.Equal(167, arenaPotion.NativeItemIndex);
        Assert.Equal(55, arenaPotion.MiscId);
        Assert.Equal(7, arenaPotion.MinimumMonsterLevel);
        Assert.Equal(0, arenaPotion.DropRate);
        Assert.True(arenaPotion.Usable);
        Assert.True(arenaPotion.ArenaOnly);
        Assert.Contains("Misc", arenaPotion.GenerationTags, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(DiabloItemSpellSourceKind.None, arenaPotion.SpellSourceKind);
        Assert.Equal(DiabloItemActionKind.RestoreFullLifeAndMana, arenaPotion.ItemActionKind);

        var bookTemplateExpectations = new[] {
            ("book_template_level_2", 2201U, 114, 2, 1),
            ("book_template_level_8", 2202U, 115, 8, 4),
            ("book_template_level_14", 2203U, 116, 14, 7),
            ("book_template_level_20", 2204U, 117, 20, 10),
        };
        foreach (var (symbol, itemId, nativeIndex, minimumLevel, minimumItemLevel) in bookTemplateExpectations) {
            Assert.Equal(itemId, baseStableIds.Resolve("item", symbol));
            var book = baseItems.GetRequired(itemId);
            Assert.Equal(symbol, book.StableSymbol);
            Assert.Equal(nativeIndex, book.NativeItemIndex);
            Assert.Equal(25, book.MiscId);
            Assert.Equal(minimumLevel, book.MinimumMonsterLevel);
            Assert.Equal(minimumItemLevel, book.BookTemplateMinimumItemLevel);
            Assert.Equal(1, book.DropRate);
            Assert.Equal("BOOK_BLUE", book.CursorGraphic);
            Assert.True(book.Usable);
            Assert.Equal(DiabloItemSpellSourceKind.None, book.SpellSourceKind);
            Assert.Equal(DiabloItemActionKind.None, book.ItemActionKind);
            Assert.Contains("Book", book.GenerationTags, StringComparer.OrdinalIgnoreCase);
        }

        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));
        var composedStableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            composed.GetRequiredTable("content_ids.tsv").ToCanonicalTsv());
        var composedCursorCatalog = AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            composed.GetRequiredTable("item_cursor_graphics.tsv").ToCanonicalTsv());
        var composedItems = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            composed.GetRequiredTable("items.tsv").ToCanonicalTsv(),
            cursorCatalog: composedCursorCatalog,
            stableIds: composedStableIds);
        var composedItemIds = composedStableIds.Entries
            .Where(entry => entry.Kind == "item")
            .ToArray();

        Assert.Equal(composedItems.Definitions.Count, composedItemIds.Length);
        Assert.All(composedItems.Definitions, definition =>
            Assert.False(string.IsNullOrWhiteSpace(definition.StableSymbol)));
        composedStableIds.ValidateReferences("item", id => composedItems.TryGet(id, out _));
        var hellfireStaff = composedItems.GetRequired(112);
        Assert.Equal("hellfire_staff_charged_bolt", hellfireStaff.StableSymbol);
        Assert.Equal("Short Staff of Charged Bolt", hellfireStaff.Name);
        Assert.Equal("Short Staff of Charged Bolt", hellfireStaff.IdentifiedName);
        Assert.Equal(109, hellfireStaff.NativeCursorGraphic);
        Assert.Equal(1, hellfireStaff.SpellId);
        Assert.Equal(DiabloItemSpellSourceKind.ChargedItem, hellfireStaff.SpellSourceKind);
        Assert.Contains("Staff", hellfireStaff.GenerationTags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("TwoHanded", hellfireStaff.GenerationTags, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpellBookMetadataGeneratesNativeDynamicBooks()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            basePack.Tables.Single(table => table.SourcePath == "spells.tsv").ToCanonicalTsv(),
            stableIds);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            basePack.Tables.Single(table => table.SourcePath == "items.tsv").ToCanonicalTsv(),
            stableIds: stableIds);
        var books = AuthoritativeSpellBookCatalog.LoadTsv(
            "spell_books.tsv",
            basePack.Tables.Single(table => table.SourcePath == "spell_books.tsv").ToCanonicalTsv(),
            stableIds);

        books.Validate(spells, items);
        Assert.Equal(23, books.Definitions.Count);
        Assert.All(books.Definitions, definition =>
            Assert.True(
                definition.SinglePlayerExcluded.HasValue,
                $"Spell-book metadata for spell {definition.SpellId} must declare single-player admission."));
        var healOther = Assert.Single(books.Definitions, definition => definition.SpellId == 34);
        Assert.True(healOther.SinglePlayerExcluded);

        var firebolt = books.Generate(items, 1, 0x12345678, itemLevel: 1);
        Assert.Equal(1, firebolt.SpellId);
        Assert.Equal(114, firebolt.ItemIndex);
        Assert.Equal(1000, firebolt.Value);
        Assert.Equal(15, firebolt.MinimumMagic);
        Assert.Equal(DiabloItemActionKind.ReadBook, firebolt.ItemActionKind);
        Assert.Equal("Book of Firebolt", firebolt.BaseName);
        Assert.Equal("Book of Firebolt", firebolt.IdentifiedName);
        Assert.Equal(87, firebolt.NativeCursorGraphic);

        var lightning = books.Generate(items, 3, 0x12345678, itemLevel: 10);
        Assert.Equal(3, lightning.SpellId);
        Assert.Equal(3000, lightning.Value);
        Assert.Equal(20, lightning.MinimumMagic);
        Assert.Equal(DiabloItemActionKind.ReadBook, lightning.ItemActionKind);
        Assert.Equal("Book of Lightning", lightning.BaseName);
        Assert.Equal(88, lightning.NativeCursorGraphic);
        Assert.Contains(lightning.ItemIndex, new[] { 114, 115, 116, 117 });

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "base",
                "1",
                [basePack])));
        Assert.NotNull(activation.SpellBooks);
        Assert.Equal(23, activation.SpellBooks!.Definitions.Count);
        Assert.NotNull(activation.SetLevelObjects);
        Assert.Equal(11, activation.SetLevelObjects!.Definitions.Count);
        Assert.Equal(11, activation.Simulation.SetLevelObjects!.Definitions.Count);
        Assert.NotNull(activation.SetLevelMaps);
        Assert.Equal(3, activation.SetLevelMaps!.Definitions.Count);
        Assert.Equal(3, activation.Simulation.SetLevelMaps!.Definitions.Count);
    }

    [Fact]
    public void HellfireQuestItemsUseNormalizedStableDefinitions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            composed.GetRequiredTable("content_ids.tsv").ToCanonicalTsv());
        var cursorCatalog = AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            composed.GetRequiredTable("item_cursor_graphics.tsv").ToCanonicalTsv());
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            composed.GetRequiredTable("items.tsv").ToCanonicalTsv(),
            cursorCatalog: cursorCatalog,
            stableIds: stableIds);

        var expected = new[] {
            ("rune_bomb", 2101U, 39, 187),
            ("theodore", 2102U, 40, 188),
            ("auric_amulet", 2103U, 41, 180),
            ("brown_suit", 2104U, 46, 199),
            ("grey_suit", 2105U, 47, 198),
        };
        foreach (var (symbol, itemId, nativeIndex, cursorGraphic) in expected) {
            Assert.Equal(itemId, stableIds.Resolve("item", symbol));
            var definition = items.GetRequired(itemId);
            Assert.Equal(symbol, definition.StableSymbol);
            Assert.Equal(nativeIndex, definition.NativeItemIndex);
            Assert.Equal(cursorGraphic, definition.NativeCursorGraphic);
            Assert.Contains("Quest", definition.GenerationTags, StringComparer.OrdinalIgnoreCase);
        }
        stableIds.ValidateReferences("item", id => items.TryGet(id, out _));
    }

    [Fact]
    public void ShippedStaffOfLazarusDeclaresNoSalePolicy()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            basePack.Tables.Single(table => table.SourcePath == "items.tsv").ToCanonicalTsv(),
            stableIds: stableIds);

        var staff = items.GetRequired(stableIds.Resolve("item", "lazarus_staff"));
        Assert.Contains("NoSale", staff.GenerationTags, StringComparer.OrdinalIgnoreCase);
        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            staff.Generate(0x12345678),
            items,
            null,
            DiabloGameVariant.Diablo));
        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            staff.Generate(0x12345678),
            items,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void BaseBookTemplatesDeclareAuthoritativeQuestRewards()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            basePack.Tables.Single(table => table.SourcePath == "items.tsv").ToCanonicalTsv(),
            stableIds: stableIds);
        var objectsTable = basePack.Tables.Single(table => table.SourcePath == "objects.tsv");
        var templates = AuthoritativeWorldObjectCatalog.LoadTemplates(
            objectsTable.SourcePath,
            objectsTable.ToCanonicalTsv(),
            stableIds);

        var halls = Assert.Single(templates, @object => @object.ObjectId == 71);
        Assert.Equal(AuthoritativeObjectRewardKind.UniqueItem, halls.RewardKind);
        Assert.Equal(1005U, halls.RewardId);
        Assert.Equal((1, 5, 5), (
            halls.RewardItemLevel,
            halls.RewardOffsetX,
            halls.RewardOffsetY));

        var gateway = Assert.Single(templates, @object => @object.ObjectId == 72);
        Assert.Equal(AuthoritativeObjectRewardKind.NormalItem, gateway.RewardKind);
        Assert.Equal(stableIds.Resolve("item", "blood_stone"), gateway.RewardId);
        Assert.Equal((0, 9, 17), (
            gateway.RewardItemLevel,
            gateway.RewardOffsetX,
            gateway.RewardOffsetY));
        Assert.Equal("Blood Stone", items.GetRequired(gateway.RewardId).Name);
    }

    [Fact]
    public void ShippedWorldObjectsUseStableObjectSymbols()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            composed.GetRequiredTable("content_ids.tsv").ToCanonicalTsv());
        var objectsTable = composed.GetRequiredTable("objects.tsv");
        var objects = AuthoritativeWorldObjectCatalog.LoadTsv(
            objectsTable.SourcePath,
            objectsTable.ToCanonicalTsv(),
            stableIds);
        var templates = AuthoritativeWorldObjectCatalog.LoadTemplates(
            objectsTable.SourcePath,
            objectsTable.ToCanonicalTsv(),
            stableIds);

        var objectIds = stableIds.Entries
            .Where(entry => entry.Kind == "object")
            .ToArray();
        var stableObjectIds = objectIds.Select(entry => entry.NumericId).ToHashSet();
        var allObjects = objects
            .Concat(templates)
            .Where(@object => stableObjectIds.Contains(@object.ObjectId))
            .ToArray();
        Assert.Equal(objectIds.Length, allObjects.Select(@object => @object.ObjectId).Distinct().Count());
        Assert.All(objectsTable.Rows, row => {
            if (row.OptionalBoolean("template_only"))
                return;
            var symbol = row.Required("object_symbol");
            Assert.Equal(row.RequiredUInt32("object_id"), stableIds.Resolve("object", symbol));
        });
        stableIds.ValidateReferences("object", id => allObjects.Any(@object => @object.ObjectId == id));
        Assert.Equal("grave_matters_cathedral_map", stableIds.Resolve(6004, "object"));
        Assert.Equal("hive_entrance", stableIds.Resolve(6005, "object"));
    }

    [Fact]
    public void ShippedStoresUseStableStoreSymbols()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
        var storesTable = basePack.Tables.Single(table => table.SourcePath == "stores.tsv");
        var stores = StoreCatalog.LoadTsv(
            storesTable.SourcePath,
            storesTable.ToCanonicalTsv(),
            stableIds: stableIds);
        var worldRules = AuthoritativeWorldRules.LoadTsv(
            "world_rules.tsv",
            basePack.Tables.Single(table => table.SourcePath == "world_rules.tsv").ToCanonicalTsv(),
            stableIds);

        var storeIds = stableIds.Entries
            .Where(entry => entry.Kind == "store")
            .ToArray();
        Assert.Equal(6, storeIds.Length);
        Assert.All(storesTable.Rows, row =>
            Assert.Equal(row.RequiredUInt32("store_id"), stableIds.Resolve("store", row.Required("store_symbol"))));
        stableIds.ValidateReferences(
            "store",
            id => stores.ContainsStore(id)
                || id == worldRules.AdriaStoreId
                || id == worldRules.StorytellerStoreId);
        Assert.Equal(10U, stableIds.Resolve("store", "adria"));
        Assert.Equal(11U, stableIds.Resolve("store", "storyteller"));
        Assert.True(stores.TryGetRole(stableIds.Resolve("store", "smith"), out var smithRole));
        Assert.Equal(AuthoritativeStoreRole.Smith, smithRole);
        Assert.True(stores.TryGetRole(stableIds.Resolve("store", "witch"), out var witchRole));
        Assert.Equal(AuthoritativeStoreRole.Witch, witchRole);
        Assert.True(stores.TryGetRole(stableIds.Resolve("store", "wirt"), out var wirtRole));
        Assert.Equal(AuthoritativeStoreRole.Wirt, wirtRole);
        Assert.True(stores.TryGetRole(stableIds.Resolve("store", "healer"), out var healerRole));
        Assert.Equal(AuthoritativeStoreRole.Healer, healerRole);
    }

    [Fact]
    public void ShippedPortalsUseStablePortalSymbols()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
        var portalsTable = basePack.Tables.Single(table => table.SourcePath == "portals.tsv");
        var portals = AuthoritativePortalCatalog.LoadTsv(
            portalsTable.SourcePath,
            portalsTable.ToCanonicalTsv(),
            stableIds);

        var portalIds = stableIds.Entries
            .Where(entry => entry.Kind == "portal")
            .ToArray();
        Assert.Equal(portalIds.Length, portals.Count);
        Assert.All(portalsTable.Rows, row =>
            Assert.Equal(row.RequiredUInt32("portal_id"), stableIds.Resolve("portal", row.Required("portal_symbol"))));
        Assert.Equal("cathedral_entrance", Assert.Single(portals).StableSymbol);
        stableIds.ValidateReferences("portal", id => portals.Any(portal => portal.PortalId == id));
    }

    [Fact]
    public void ShippedHellfireWarpsUseStableWarpSymbols()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            composed.GetRequiredTable("content_ids.tsv").ToCanonicalTsv());
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(composed);
        var world = activation.Simulation.WorldState.World!;
        var warpsTable = composed.GetRequiredTable("warps.tsv");
        var warps = world.WarpTargets;

        var warpIds = stableIds.Entries
            .Where(entry => entry.Kind == "warp")
            .ToArray();
        Assert.Equal(warpIds.Length, warps.Count);
        Assert.All(warpsTable.Rows, row =>
            Assert.Equal(row.RequiredUInt32("warp_id"), stableIds.Resolve("warp", row.Required("warp_symbol"))));
        Assert.Contains(warps, warp => warp.StableSymbol == "grave_matters_dungeon");
        Assert.Contains(warps, warp => warp.StableSymbol == "farmers_orchard_dungeon");
        Assert.Contains(warps, warp => warp.StableSymbol == "town_to_cathedral");
        Assert.Contains(warps, warp => warp.StableSymbol == "caves_15_to_hell_16");
        Assert.Equal(18, warps.Count);
        stableIds.ValidateReferences("warp", id => warps.Any(warp => warp.WarpId == id));
    }

    [Fact]
    public void ShippedBaseProgressionWarpsExecuteThroughTheAuthoritativeTriggerPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest(
                "base",
                "1",
                [basePack])));
        var worldState = activation.Simulation.WorldState;
        var world = worldState.World!;
        var warps = world.WarpTargets
            .Where(warp => warp.WarpId is >= 1001 and <= 1016)
            .OrderBy(warp => warp.WarpId)
            .ToArray();
        var player = new AuthoritativePlayerState(
            0,
            0,
            100,
            0,
            100,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            100,
            1,
            0,
            0) {
            EntityId = 1,
        };
        var processor = new DiabloWorldCommandProcessor(worldState);

        Assert.Equal(16, warps.Length);
        for (var index = 0; index < warps.Length; index++) {
            var warp = warps[index];
            player.LevelId = warp.SourceLevelId;
            player.PositionX = warp.TriggerX;
            player.PositionY = warp.TriggerY;

            var result = processor.ExecuteLevelTrigger(player, (ulong)index + 1, [player]);

            Assert.True(result.Succeeded, $"Warp {warp.StableSymbol} was rejected.");
            Assert.Equal(warp.TargetLevelId, player.LevelId);
            Assert.Equal(warp.TargetX, player.PositionX);
            Assert.Equal(warp.TargetY, player.PositionY);
            Assert.Contains(player.LevelId, player.VisitedLevelIds);
        }
    }

    [Fact]
    public void ShippedUniqueItemsUseStableBaseItemSymbols()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            composed.GetRequiredTable("content_ids.tsv").ToCanonicalTsv());
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            composed.GetRequiredTable("items.tsv").ToCanonicalTsv(),
            stableIds: stableIds);
        var uniqueTable = composed.GetRequiredTable("unique_items.tsv");
        var uniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            uniqueTable.SourcePath,
            uniqueTable.ToCanonicalTsv(),
            stableIds: stableIds);

        Assert.NotEmpty(uniques.Definitions);
        var uniqueIds = stableIds.Entries
            .Where(entry => entry.Kind == "unique")
            .ToArray();
        Assert.Equal(uniqueIds.Length, uniques.Definitions.Count);
        Assert.All(uniques.Definitions, definition =>
            Assert.False(string.IsNullOrWhiteSpace(definition.StableSymbol)));
        stableIds.ValidateReferences("unique", id => uniques.TryGet(id, out _));
        Assert.All(uniqueTable.Rows, row => {
            var uniqueSymbol = row.Required("unique_symbol");
            Assert.Equal(row.RequiredUInt32("unique_id"), stableIds.Resolve("unique", uniqueSymbol));
            var symbol = row.Required("base_item_symbol");
            Assert.Equal(row.RequiredUInt32("base_item_id"), stableIds.Resolve("item", symbol));
        });
        uniques.ValidateBaseItems(items);
    }

    [Fact]
    public void ContentBootstrapRejectsQuestGatedWarpsWithoutAQuestCatalog()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var warps = basePack.Tables.Single(table => table.SourcePath == "warps.tsv");
        var gatedWarps = TsvTable.Parse(
            warps.SourcePath,
            warps.ToCanonicalTsv()
                + "2\t\t0\t\t0\t0\t0\t\t0\t0\t3017\t\t1\t\t\t\n");
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Where(table => table.SourcePath is not "quests.tsv" and not "quest_states.tsv")
                .Select(table => table.SourcePath == warps.SourcePath ? gatedWarps : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Quest-gated warps and objects require an active quest catalog", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsQuestLinkedObjectsWithoutAQuestCatalog()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var objects = basePack.Tables.Single(table => table.SourcePath == "objects.tsv");
        var questLinkedObjects = TsvTable.Parse(
            objects.SourcePath,
            objects.ToCanonicalTsv()
                + "9001\t6001\t\t0\t\t0\t0\t0\t3017\t\tNone\t0\t\t0\t0\t0\t0\t0\tNone\t0\t\t0\t0\t0\t\t0\t0\tObject\tNone\t0\t\t\tfalse\t0\t0\tNone\t0\t0\t0\t0\t0\n");
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Where(table => table.SourcePath is not "quests.tsv" and not "quest_states.tsv")
                .Select(table => table.SourcePath == objects.SourcePath ? questLinkedObjects : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Quest-gated warps and objects require an active quest catalog", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownFollowOnQuestFromPrimaryQuestTable()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var quests = basePack.Tables.Single(table => table.SourcePath == "quests.tsv");
        var invalidQuests = string.Join(
            '\n',
            quests.ToCanonicalTsv()
                .Split('\n')
                .Select((line, index) => {
                    if (index != 1)
                        return line;
                    var columns = line.Split('\t');
                    columns[20] = "9999";
                    return string.Join('\t', columns);
                }));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == quests.SourcePath
                    ? TsvTable.Parse(quests.SourcePath, invalidQuests)
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Quest 3001 activates unknown quest 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsPortalEndpointsOutsideTheActiveWorld()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var portals = basePack.Tables.Single(table => table.SourcePath == "portals.tsv");
        var invalidPortals = TsvTable.Parse(
            portals.SourcePath,
            portals.ToCanonicalTsv().Replace(
                "1\tcathedral_entrance\t1\tcathedral\t2\t2\t2\tcatacombs\t3\t4",
                "1\tcathedral_entrance\t1\tcathedral\t40\t2\t2\tcatacombs\t3\t4",
                StringComparison.Ordinal));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == portals.SourcePath ? invalidPortals : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Portal 1 contains an endpoint outside its level", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownExternalTownLevel()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var worldRules = basePack.Tables.Single(table => table.SourcePath == "world_rules.tsv");
        var invalidWorldRules = TsvTable.Parse(
            worldRules.SourcePath,
            worldRules.ToCanonicalTsv().Replace(
                "0\ttown\t10\tadria\t11\tstoryteller",
                "999\t\t10\tadria\t11\tstoryteller",
                StringComparison.Ordinal));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == worldRules.SourcePath
                    ? invalidWorldRules
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("World rules reference unknown town level 999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapActivatesConfiguredAdriaStoreIdentity()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var worldRules = basePack.Tables.Single(table => table.SourcePath == "world_rules.tsv");
        var stableIds = basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv");
        var configuredWorldRules = TsvTable.Parse(
            worldRules.SourcePath,
            "town_level_id\ttown_level_symbol\tadria_store_id\tadria_store_symbol\t"
                + "storyteller_store_id\tstoryteller_store_symbol\t"
                + "town_portal_destination_position_x\ttown_portal_destination_position_y\t"
                + "town_portal_destination_slot_count\ttown_portal_destination_slot_spacing\n"
                + "0\ttown\t73\tadria\t74\tstoryteller\t60\t41\t2\t4\n");
        var configuredStableIds = TsvTable.Parse(
            stableIds.SourcePath,
            stableIds.ToCanonicalTsv().Replace(
                "store\tadria\t10",
                "store\tadria\t73",
                StringComparison.Ordinal));
        configuredStableIds = TsvTable.Parse(
            stableIds.SourcePath,
            configuredStableIds.ToCanonicalTsv().Replace(
                "store\tstoryteller\t11",
                "store\tstoryteller\t74",
                StringComparison.Ordinal));
        var configuredPack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == worldRules.SourcePath
                    ? configuredWorldRules
                    : table.SourcePath == stableIds.SourcePath
                        ? configuredStableIds
                        : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("base", "1", [configuredPack])));

        Assert.Equal(73U, activation.AdriaStoreId);
        Assert.Equal(74U, activation.StorytellerStoreId);
        Assert.True(activation.WorldRules.HasTownPortalDestinationGeometry);
        Assert.Equal(60, activation.WorldRules.TownPortalDestinationPositionX);
    }

    [Fact]
    public void ContentBootstrapRejectsObjectPositionsOutsideTheActiveWorld()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var objects = basePack.Tables.Single(table => table.SourcePath == "objects.tsv");
        var invalidObjects = TsvTable.Parse(
            objects.SourcePath,
            objects.ToCanonicalTsv().Replace(
                "2001\t1\tl1_left_door\t1\tcathedral\t4\t4",
                "2001\t1\tl1_left_door\t1\tcathedral\t40\t4",
                StringComparison.Ordinal));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == objects.SourcePath ? invalidObjects : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Object 2001 contains a position outside its level", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsMonsterPositionsOutsideTheActiveWorld()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var existingMonsters = basePack.Tables.Single(table => table.SourcePath == "monsters.tsv");
        var invalidMonsters = TsvTable.Parse(
            existingMonsters.SourcePath,
            existingMonsters.ToCanonicalTsv().Replace(
                "9\t1\tbase_monster\t1\tcathedral\t1\t0",
                "9001\t1\tbase_monster\t1\tcathedral\t40\t4",
                StringComparison.Ordinal));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == existingMonsters.SourcePath
                    ? invalidMonsters
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Monster 9001 contains a position outside its level", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownNormalizedItemSpell()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var items = basePack.Tables.Single(table => table.SourcePath == "items.tsv");
        var invalidItems = TsvTable.Parse(
            items.SourcePath,
            string.Join(
                '\n',
                items.ToCanonicalTsv()
                    .Split('\n')
                    .Select((line, index) => {
                        if (index == 0 || !line.StartsWith("107\t", StringComparison.Ordinal))
                            return line;
                        var columns = line.Split('\t');
                        columns[17] = "9999";
                        return string.Join('\t', columns);
                    })));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == items.SourcePath
                    ? invalidItems
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Item 107 references unknown spell 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownItemActionDialogue()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var items = basePack.Tables.Single(table => table.SourcePath == "items.tsv");
        var invalidItems = TsvTable.Parse(
            items.SourcePath,
            string.Join(
                '\n',
                items.ToCanonicalTsv()
                    .Split('\n')
                    .Select((line, index) => {
                        if (index == 0 || !line.StartsWith("2001\t", StringComparison.Ordinal))
                            return line;
                        var columns = line.Split('\t');
                        columns[41] = "9999";
                        return string.Join('\t', columns);
                    })));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == items.SourcePath
                    ? invalidItems
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Dialogue route references unknown message 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownUniqueItemBase()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var uniqueItems = basePack.Tables.Single(table => table.SourcePath == "unique_items.tsv");
        var invalidUniqueItems = TsvTable.Parse(
            uniqueItems.SourcePath,
            string.Join(
                '\n',
                uniqueItems.ToCanonicalTsv()
                    .Split('\n')
                    .Select((line, index) => {
                        if (index == 0 || !line.StartsWith("1001\t", StringComparison.Ordinal))
                            return line;
                        var columns = line.Split('\t');
                        columns[3] = "9999";
                        columns[4] = string.Empty;
                        return string.Join('\t', columns);
                    })));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == uniqueItems.SourcePath
                    ? invalidUniqueItems
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Unique item 1001 references unknown base item 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownStoreSpellPricing()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var pricing = basePack.Tables.Single(table => table.SourcePath == "store_services.tsv");
        var invalidPricing = TsvTable.Parse(
            pricing.SourcePath,
            pricing.ToCanonicalTsv() + "spell_staff_cost\t9999\t\t100\n");
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == pricing.SourcePath
                    ? invalidPricing
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Store service pricing references unknown spell 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownMonsterMissileSpawn()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var missiles = basePack.Tables.Single(table => table.SourcePath == "monster_missiles.tsv");
        var spawnColumns = Enumerable.Repeat(string.Empty, missiles.Columns.Count).ToArray();
        spawnColumns[0] = "HorkDemon";
        spawnColumns[1] = "HorkSpawn";
        spawnColumns[2] = "HorkSpawn";
        spawnColumns[9] = "9999";
        var invalidMissiles = TsvTable.Parse(
            missiles.SourcePath,
            string.Join('\t', missiles.Columns)
                + "\n"
                + string.Join('\t', spawnColumns)
                + "\n");
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == missiles.SourcePath
                    ? invalidMissiles
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("references unknown spawn monster 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownMonsterMissileSelector()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var missiles = basePack.Tables.Single(table => table.SourcePath == "monster_missiles.tsv");
        var selectorColumns = Enumerable.Repeat(string.Empty, missiles.Columns.Count).ToArray();
        selectorColumns[0] = "Zombie";
        selectorColumns[1] = "Arrow";
        selectorColumns[2] = "MonsterArrow";
        selectorColumns[5] = "9999";
        var invalidMissiles = TsvTable.Parse(
            missiles.SourcePath,
            string.Join('\t', missiles.Columns)
                + "\n"
                + string.Join('\t', selectorColumns)
                + "\n");
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == missiles.SourcePath
                    ? invalidMissiles
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("references unknown monster type 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownCombatPvpEarItem()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var combat = basePack.Tables.Single(table => table.SourcePath == "combat.tsv");
        var invalidCombat = TsvTable.Parse(
            combat.SourcePath,
            "base_attack_damage\tminimum_damage\tdefeat_experience\tpvp_ear_item_id\n"
                + "10\t1\t100\t9999\n");
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == combat.SourcePath
                    ? invalidCombat
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("references unknown item 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownStableSpellReference()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var stableIds = basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv");
        var invalidStableIds = TsvTable.Parse(
            stableIds.SourcePath,
            stableIds.ToCanonicalTsv().Replace(
                "ability\tidentify\t5",
                "ability\tidentify\t9999",
                StringComparison.Ordinal));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == stableIds.SourcePath
                    ? invalidStableIds
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains(
            "maps ability 5 to symbol 'identify', which resolves to 9999",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownStableLevelReference()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var stableIds = basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv");
        var invalidStableIds = TsvTable.Parse(
            stableIds.SourcePath,
            stableIds.ToCanonicalTsv()
                + "level\tmissing\t9999\n");
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == stableIds.SourcePath
                    ? invalidStableIds
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Stable content level symbol 'missing' references unknown ID 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownStablePortalReference()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var stableIds = basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv");
        var invalidStableIds = TsvTable.Parse(
            stableIds.SourcePath,
            stableIds.ToCanonicalTsv()
                + "portal\tmissing\t9999\n");
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == stableIds.SourcePath
                    ? invalidStableIds
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));

        Assert.Contains("Stable content portal symbol 'missing' references unknown ID 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentBootstrapRejectsUnknownQuestCompletionMonster()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        var quests = basePack.Tables.Single(table => table.SourcePath == "quests.tsv");
        var invalidQuests = string.Join(
            '\n',
            quests.ToCanonicalTsv()
                .Split('\n')
                .Select((line, index) => {
                    if (index != 1)
                        return line;
                    var columns = line.Split('\t');
                    columns[13] = "9999";
                    return string.Join('\t', columns);
                }));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == quests.SourcePath
                    ? TsvTable.Parse(quests.SourcePath, invalidQuests)
                    : table)
                .ToArray(),
            [],
            [],
            basePack.Schema,
            basePack.DependencyConstraints,
            basePack.Assets);
        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(new ContentManifest("base", "1", [incompletePack]))));
        Assert.Contains("Quest 3001 references unknown completion monster 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BaseProceduralActivationCarriesTheShippedMonsterDrop()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        // Keep the generated level-1 sample on the canonical base monster as
        // the validated ordinary-family candidate pool grows.
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(new ContentManifest("base", "1", [pack])),
            randomSeed: 5,
            monsterSpawns: new DiabloMonsterSpawnConfiguration(2));

        var target = Assert.Single(
            activation.Simulation.WorldState.CombatTargets.Values,
            candidate => candidate.MonsterId == 1 && candidate.Drop is not null);
        Assert.NotNull(target.Drop);
        Assert.Equal(
            AuthoritativeMonsterSpawnPlanner.FirstGeneratedDropEntityId
                + (target.EntityId - AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId),
            target.Drop!.EntityId);
        Assert.NotEqual(0U, target.Drop.ItemSeed);
        Assert.Equal(1, target.Drop.State.ItemIndex);
        var nativeMissiles = Assert.IsType<AuthoritativeNativeMissileCatalog>(
            activation.NativeMissiles);
        Assert.Same(nativeMissiles, activation.Simulation.NativeMissiles);
        Assert.Equal(73, nativeMissiles.Definitions.Count);
        Assert.Equal("Fireball", nativeMissiles.GetRequired("Firebolt").Graphic);
        Assert.Equal(
            "MagmaBallExplosion",
            nativeMissiles.GetRequired("Firebolt").OnExpiryMissile);
        Assert.All(
            ["OrangeFlare", "YellowFlare", "BlueFlare", "RedFlare", "BlueFlare2"],
            alias => Assert.True(nativeMissiles.GetRequired(alias).CompatibilityAlias));
        var nativeMissileSprites = Assert.IsType<AuthoritativeNativeMissileSpriteCatalog>(
            activation.NativeMissileSprites);
        Assert.Same(nativeMissileSprites, activation.Simulation.NativeMissileSprites);
        Assert.Equal(46, nativeMissileSprites.Definitions.Count);
        Assert.Equal("fireba", nativeMissileSprites.GetRequired("Fireball").Name);
        Assert.Equal(16, nativeMissileSprites.GetRequired("Fireball").FrameCount);
        Assert.Equal(14, nativeMissileSprites.GetRequired("Fireball").FrameLength[0]);
        Assert.NotNull(activation.MonsterMissiles);
        Assert.True(activation.MonsterMissiles!.TryGet("GoatRanged", out var goatRanged));
        Assert.Equal("Arrow", goatRanged.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, goatRanged.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles!.TryGet("Succubus", out var succubus));
        Assert.Equal("BloodStar", succubus.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.BloodStar, succubus.ProjectileBehavior);
        Assert.Equal(36U, succubus.SpellId);
        Assert.Equal(16, succubus.ProjectileSpeed);
        Assert.Equal(256, succubus.ProjectileLifetime);
        Assert.True(activation.MonsterMissiles.TryGet("Lich", out var lich));
        Assert.Equal("OrangeFlare", lich.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, lich.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("ArchLich", out var archLich));
        Assert.Equal("YellowFlare", archLich.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, archLich.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("Psychorb", out var psychorb));
        Assert.Equal("BlueFlare", psychorb.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, psychorb.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("Necromorb", out var necromorb));
        Assert.Equal("RedFlare", necromorb.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, necromorb.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("BoneDemon", out var boneDemon));
        Assert.Equal("BlueFlare2", boneDemon.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, boneDemon.ProjectileBehavior);
        Assert.True(activation.MonsterMissiles.TryGet("Storm", out var storm));
        Assert.Equal("ThinLightningControl", storm.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.Lightning, storm.ProjectileBehavior);
        Assert.Equal(3U, storm.SpellId);
        Assert.Equal(32, storm.ProjectileSpeed);
        Assert.Equal(256, storm.ProjectileLifetime);
        Assert.True(activation.MonsterMissiles.TryGet("Diablo", out var diablo));
        Assert.Equal("DiabloApocalypse", diablo.MissileId);
        Assert.Equal(40, diablo.ProjectileDamage);
        Assert.Equal(AuthoritativeProjectileBehavior.ApocalypseController, diablo.ProjectileBehavior);
        Assert.Equal(25U, diablo.SpellId);
        Assert.Equal(0, diablo.ProjectileSpeed);
        Assert.Equal(15, diablo.ProjectileLifetime);
        Assert.True(activation.MonsterMissiles.TryGet("Magma", out var magma));
        Assert.Equal("MagmaBall", magma.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, magma.ProjectileBehavior);
        Assert.Equal(3, magma.InitialTravelQuanta);
        Assert.Equal(3, activation.MonsterMissiles.Definitions
            .Single(definition => definition.NativeAi == "Counselor"
                && definition.MissileId == "ChargedBolt")
            .ProjectileCount);
        Assert.Equal(15, activation.MonsterMissiles.Definitions
            .Single(definition => definition.NativeAi == "Counselor"
                && definition.MissileId == "ChargedBolt")
            .ProjectileDamage);
        Assert.True(activation.MonsterMissiles.TryGet("Mega", out var mega));
        Assert.Equal("InfernoControl", mega.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.InfernoController, mega.ProjectileBehavior);
        Assert.Equal(21U, mega.SpellId);
        Assert.Equal(32, mega.ProjectileSpeed);
        Assert.Equal(256, mega.ProjectileLifetime);
        Assert.Equal((5, 40, 10, 3, 5, 5, 1, 2), (
            mega.InfernoMaximumDistance,
            mega.InfernoAdjacentChanceBase,
            mega.InfernoAdjacentChanceIntelligenceScale,
            mega.InfernoRangedFarDistance,
            mega.InfernoRangedChanceBase,
            mega.InfernoRangedChanceIntelligenceScale,
            mega.InfernoRangedFarIntelligenceOffset,
            mega.InfernoAdjacentChoiceModulo));
        Assert.True(activation.MonsterMissiles.TryGet("Acid", out var acid));
        Assert.Equal("Acid", acid.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.Acid, acid.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Acid, acid.DamageType);
        Assert.Equal(16, acid.ProjectileSpeed);
        Assert.Equal(20, acid.ProjectileLifetimeBase);
        Assert.Equal(5, acid.ProjectileLifetimeIntelligenceScale);
        Assert.Equal(15, acid.PuddleDurationRandomRange);
        Assert.Equal(40, acid.PuddleDurationBase);
        Assert.Equal(40, acid.PuddleDurationIntelligenceScale);
        Assert.Equal(2, acid.PuddleDamageMinimumMonsterLevel);
        Assert.Equal(1, acid.PuddleDamageLow);
        Assert.Equal(2, acid.PuddleDamageHigh);
        Assert.Equal(2, acid.PuddleImpactLifetime);
        Assert.Equal(1, acid.ProjectileImpactLifetime);
        Assert.Equal((2, 3, 500, 500, 1), (
            acid.RangedAvoidanceMinimumDistance,
            acid.RangedAvoidanceFarDistance,
            acid.RangedAvoidanceChanceBase,
            acid.RangedAvoidanceChanceIntelligenceScale,
            acid.RangedAvoidanceFarIntelligenceOffset));
        Assert.Equal(8, acid.RangedAvoidanceMovementChanceModulo);
        Assert.Equal((50, 10, 30), (
            acid.RangedAvoidanceMovementChanceBase,
            acid.RangedAvoidanceMovementChanceIntelligenceScale,
            acid.RangedAvoidanceMovementFreshChanceBonus));
        Assert.Equal(2, acid.RangedAvoidanceAdjacentAttackChanceDivisor);
        Assert.True(activation.MonsterMissiles.TryGet("Magma", out var magmaRangedAvoidance));
        Assert.Equal(4, magmaRangedAvoidance.RangedAvoidanceMovementChanceModulo);
        Assert.Equal((50, 10, 30), (
            magmaRangedAvoidance.RangedAvoidanceMovementChanceBase,
            magmaRangedAvoidance.RangedAvoidanceMovementChanceIntelligenceScale,
            magmaRangedAvoidance.RangedAvoidanceMovementFreshChanceBonus));
        Assert.Equal((6_000, 1_000, 1), (
            magmaRangedAvoidance.RangedAvoidanceAdjacentAttackChanceBase,
            magmaRangedAvoidance.RangedAvoidanceAdjacentAttackChanceIntelligenceScale,
            magmaRangedAvoidance.RangedAvoidanceAdjacentAttackChanceDivisor));
        Assert.True(activation.MonsterMissiles.TryGet("AcidUnique", out var acidUnique));
        Assert.Equal("Acid", acidUnique.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.Acid, acidUnique.ProjectileBehavior);
        Assert.Equal(16, acidUnique.ProjectileSpeed);
        Assert.Equal(20, acidUnique.ProjectileLifetimeBase);
        Assert.Equal(5, acidUnique.ProjectileLifetimeIntelligenceScale);
        Assert.Equal(15, acidUnique.PuddleDurationRandomRange);
        Assert.Equal(40, acidUnique.PuddleDurationBase);
        Assert.Equal(40, acidUnique.PuddleDurationIntelligenceScale);
        Assert.Equal(2, acidUnique.PuddleDamageMinimumMonsterLevel);
        Assert.Equal(1, acidUnique.PuddleDamageLow);
        Assert.Equal(2, acidUnique.PuddleDamageHigh);
        Assert.Equal(2, acidUnique.PuddleImpactLifetime);
        Assert.Equal(1, acidUnique.ProjectileImpactLifetime);
        Assert.Equal((4, 70, 10), (
            acidUnique.RangedRetreatMinimumDistance,
            acidUnique.RangedRetreatChanceBase,
            acidUnique.RangedRetreatChanceIntelligenceScale));
        Assert.True(activation.MonsterMissiles.TryGet("SkeletonRanged", out var skeletonRanged));
        Assert.Equal("Arrow", skeletonRanged.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, skeletonRanged.ProjectileBehavior);
        Assert.Equal(1, skeletonRanged.ProjectileSpeed);
        Assert.True(activation.MonsterMissiles.TryGet("Snake", out var snake));
        Assert.Equal("Rhino", snake.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.SnakeCharge, snake.ProjectileBehavior);
        Assert.Equal(AuthoritativeDamageType.Physical, snake.DamageType);
        Assert.Equal(2, snake.ChargeMinimumDistance);
        Assert.Equal(18, snake.ProjectileSpeed);
        Assert.Equal(256, snake.ProjectileLifetime);
        Assert.True(activation.MonsterMissiles.TryGet("Bat", 42, out var gloom));
        Assert.Equal("Rhino", gloom.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.BatCharge, gloom.ProjectileBehavior);
        Assert.Equal(42U, gloom.MonsterId);
        Assert.Equal(AuthoritativeDamageType.Physical, gloom.DamageType);
        Assert.Equal((5, 33, 4), (
            gloom.ChargeMinimumDistance,
            gloom.ChargeChanceBase,
            gloom.ChargeChanceIntelligenceScale));
        Assert.Equal(18, gloom.ProjectileSpeed);
        Assert.Equal(256, gloom.ProjectileLifetime);
        Assert.True(activation.MonsterMissiles.TryGet("Counselor", 107, out var counselorFirebolt));
        Assert.Equal(("Firebolt", AuthoritativeProjectileBehavior.Firebolt), (
            counselorFirebolt.MissileId,
            counselorFirebolt.ProjectileBehavior));
        Assert.True(activation.MonsterMissiles.TryGet("Counselor", 108, out var counselorChargedBolt));
        Assert.Equal(("ChargedBolt", AuthoritativeProjectileBehavior.MonsterChargedBolt), (
            counselorChargedBolt.MissileId,
            counselorChargedBolt.ProjectileBehavior));
        Assert.Equal(30U, counselorChargedBolt.SpellId);
        Assert.True(activation.MonsterMissiles.TryGet("Counselor", 109, out var counselorLightning));
        Assert.Equal(("LightningControl", AuthoritativeProjectileBehavior.Lightning), (
            counselorLightning.MissileId,
            counselorLightning.ProjectileBehavior));
        Assert.True(activation.MonsterMissiles.TryGet("Counselor", 110, out var counselorFireball));
        Assert.Equal(("Fireball", AuthoritativeProjectileBehavior.Fireball), (
            counselorFireball.MissileId,
            counselorFireball.ProjectileBehavior));
        Assert.True(activation.MonsterMissiles.TryGet("Counselor", 113, out var counselorArchbishop));
        Assert.Equal(("Fireball", AuthoritativeProjectileBehavior.Fireball), (
            counselorArchbishop.MissileId,
            counselorArchbishop.ProjectileBehavior));
        Assert.True(activation.MonsterMissiles.TryGet("Zhar", 107, out var zhar));
        Assert.Equal(("Fireball", AuthoritativeProjectileBehavior.Fireball), (
            zhar.MissileId,
            zhar.ProjectileBehavior));
        Assert.True(activation.MonsterMissiles.TryGet("Lazarus", 113, out var lazarus));
        Assert.Equal(("Fireball", AuthoritativeProjectileBehavior.Fireball), (
            lazarus.MissileId,
            lazarus.ProjectileBehavior));
    }

    [Fact]
    public void HellfireOverlaySelectsTheExternalNativeGameVariant()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var manifest = new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]);

        var composed = ComposedContent.Compose(manifest);
        var composedSpriteTable = composed.GetRequiredTable("native_missile_sprites.tsv");
        Assert.Equal(59, composedSpriteTable.Rows.Count);
        Assert.Equal(
            composedSpriteTable.Rows.Count,
            composedSpriteTable.Rows.Select(row => row.Required("missile_id")).Distinct().Count());
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            composed.GetRequiredTable("content_ids.tsv").ToCanonicalTsv());
        Assert.Equal(3024U, stableIds.Resolve("quest", "jerseys_jersey"));
        var combat = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            composed.GetRequiredTable("combat.tsv").ToCanonicalTsv());

        Assert.Equal("devilution.hellfire", hellfirePack.Id);
        Assert.Equal(
            new ContentSchemaIdentity("devilution.diablo", "1.463.0"),
            hellfirePack.Schema);
        Assert.Equal(DiabloGameVariant.Hellfire, combat.GameVariant);
        Assert.Equal(2, combat.UniqueExperienceMultiplier);
        Assert.Equal(3, combat.MultipleArrowsCount);
        Assert.Equal(2, combat.MultipleArrowsDelayTicks);
        Assert.Equal(1, combat.MultipleArrowsSpreadDistance);
        var nativeMissileSprites = AuthoritativeNativeMissileSpriteCatalog.LoadTsv(
            "native_missile_sprites.tsv",
            composed.GetRequiredTable("native_missile_sprites.tsv").ToCanonicalTsv());
        Assert.Equal(59, nativeMissileSprites.Definitions.Count);
        Assert.Equal("ms_ora", nativeMissileSprites.GetRequired("OrangeFlare").Name);
        Assert.Equal(8, nativeMissileSprites.GetRequired("HorkSpawn").FrameCount);
        Assert.Equal(-12, nativeMissileSprites.GetRequired("OrangeFlareExplosion").AlternateWidth);
        var counselorCatalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            composed.GetRequiredTable("monster_types.tsv").ToCanonicalTsv());
        var counselorTypes = counselorCatalog.Definitions
            .Where(definition => definition.NativeAi == "Counselor")
            .ToArray();
        Assert.NotEmpty(counselorTypes);
        Assert.All(
            counselorTypes,
            definition => Assert.Equal((20, 2), (
                definition.CounselorAdjacentAttackChanceBase,
                definition.CounselorAdjacentAttackChanceIntelligenceScale)));
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            composed.GetRequiredTable("items.tsv").ToCanonicalTsv());
        Assert.Equal(22, items.GetRequired(43).NativeItemIndex);
        var combinations = AuthoritativeItemCombinationCatalog.LoadTsv(
            "item_combinations.tsv",
            composed.GetRequiredTable("item_combinations.tsv").ToCanonicalTsv());
        combinations.ValidateItems(items);
        var noteCombination = Assert.Single(combinations.Definitions);
        Assert.Equal(2004U, noteCombination.ResultItemId);
        Assert.Equal([2001U, 2002U, 2003U], noteCombination.RequiredItemIds);
        var itemQuestEffects = AuthoritativeItemQuestEffectCatalog.LoadTsv(
            "item_quest_effects.tsv",
            composed.GetRequiredTable("item_quest_effects.tsv").ToCanonicalTsv());
        itemQuestEffects.ValidateItems(items);
        Assert.Equal(6, itemQuestEffects.Definitions.Count);
        var mapEffect = Assert.Single(
            itemQuestEffects.Definitions,
            effect => effect.ItemId == 43);
        Assert.Equal((43U, 3017U, AuthoritativeItemQuestEffectKind.ActivateQuest, 1), (
            mapEffect.ItemId,
            mapEffect.QuestId,
            mapEffect.EffectKind,
            mapEffect.EffectAmount));
        var rockEffect = Assert.Single(
            itemQuestEffects.Definitions,
            effect => effect.ItemId == 2135);
        Assert.Equal((2135U, 3001U, AuthoritativeItemQuestEffectKind.ActivateQuest, 1), (
            rockEffect.ItemId,
            rockEffect.QuestId,
            rockEffect.EffectKind,
            rockEffect.EffectAmount));
        var opticAmuletEffect = Assert.Single(
            itemQuestEffects.Definitions,
            effect => effect.ItemId == 114);
        Assert.Equal((114U, 3009U, AuthoritativeItemQuestEffectKind.CompleteQuest, 0), (
            opticAmuletEffect.ItemId,
            opticAmuletEffect.QuestId,
            opticAmuletEffect.EffectKind,
            opticAmuletEffect.EffectAmount));
        var anvilEffect = Assert.Single(
            itemQuestEffects.Definitions,
            effect => effect.ItemId == 2138);
        Assert.Equal((2138U, 3011U, AuthoritativeItemQuestEffectKind.ActivateQuest, 1), (
            anvilEffect.ItemId,
            anvilEffect.QuestId,
            anvilEffect.EffectKind,
            anvilEffect.EffectAmount));
        var valorEffect = Assert.Single(
            itemQuestEffects.Definitions,
            effect => effect.ItemId == 118);
        Assert.Equal((118U, 3012U, AuthoritativeItemQuestEffectKind.CompleteQuest, 0), (
            valorEffect.ItemId,
            valorEffect.QuestId,
            valorEffect.EffectKind,
            valorEffect.EffectAmount));
        var mushroomEffect = Assert.Single(
            itemQuestEffects.Definitions,
            effect => effect.ItemId == 2139);
        Assert.Equal((2139U, 3002U, AuthoritativeItemQuestEffectKind.SetVariable1, 4), (
            mushroomEffect.ItemId,
            mushroomEffect.QuestId,
            mushroomEffect.EffectKind,
            mushroomEffect.EffectAmount));
        Assert.Equal(3, mushroomEffect.RequiredVariable1);
        Assert.Equal(2, combat.MonsterRegenerationLevelDivisor);
        Assert.Equal((2, 5, 95), (
            combat.PlayerMagicHitCharacterLevelScale,
            combat.PlayerMagicMinimumHitChance,
            combat.PlayerMagicMaximumHitChance));
        Assert.Equal((15, 10, 20, 25, 30, 2, 30, 2, 10, 2, 2, 40, 2, 500, 10, 40, 75, 8, 3, 5, 3, 5, 40, 20), (
            combat.MonsterMeleeMinimumHitChance,
            combat.MonsterRangedMinimumHitChance,
            combat.MonsterMinimumHitChanceLevel14,
            combat.MonsterMinimumHitChanceLevel15,
            combat.MonsterMinimumHitChanceLevel16,
            combat.MonsterToHitLevelScale,
            combat.MonsterToHitBaseBonus,
            combat.MonsterRangedDistancePenalty,
            combat.MonsterMagicMinimumHitChance,
            combat.MonsterMagicToHitMonsterLevelScale,
            combat.MonsterMagicToHitPlayerLevelScale,
            combat.MonsterMagicToHitBaseBonus,
            combat.MonsterMagicDistancePenalty,
            combat.MonsterChargeBaseToHit,
            combat.MonsterAcidPuddleMinimumHitChance,
            combat.MonsterAcidPuddleBaseHitChance,
            combat.MaximumResistance,
            combat.RandomStealLifeDivisor,
            combat.StealLife3Percent,
            combat.StealLife5Percent,
            combat.StealMana3Percent,
            combat.StealMana5Percent,
            combat.HellfireArmorBonusAgainstDemons,
            combat.HellfireArmorBonusAgainstUndead));
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(composed);
        Assert.NotNull(activation.MonsterMissiles);
        Assert.NotNull(activation.NativeDunMonsters);
        Assert.Equal(138, activation.NativeDunMonsters!.Definitions.Count);
        Assert.True(activation.NativeDunMonsters.TryGetNativeMonsterId(157, out var lordSaytrNativeId));
        Assert.Equal(33U, lordSaytrNativeId);
        Assert.True(activation.MonsterMissiles!.TryGet("Bat", 43, out var familiar));
        Assert.Equal("FamiliarArc", familiar.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, familiar.ProjectileBehavior);
        Assert.Equal(43U, familiar.MonsterId);
        Assert.Equal(3U, familiar.SpellId);
        Assert.Equal(AuthoritativeDamageType.Lightning, familiar.DamageType);
        Assert.Equal(16, familiar.ProjectileSpeed);
        Assert.Equal(2, familiar.ProjectileLifetime);
        Assert.Equal(8, familiar.ProjectileImpactLifetime);
        Assert.Equal(10, familiar.ProjectileDamageRandomRange);
        Assert.True(familiar.IsPostMeleeMissile);
        itemQuestEffects.ValidateQuests(activation.Simulation.WorldState.Quests.Values);
        var farmerQuest = activation.Simulation.WorldState.Quests[3018];
        Assert.Equal((277, 10, 0U, 62, 16, 2101U, 1, 63, 16), (
            farmerQuest.MessageId,
            farmerQuest.TurnInTownerId,
            farmerQuest.TurnInLevelId,
            farmerQuest.TurnInPositionX,
            farmerQuest.TurnInPositionY,
            farmerQuest.TurnInRewardItemId,
            farmerQuest.TurnInRewardItemLevel,
            farmerQuest.TurnInRewardPositionX,
            farmerQuest.TurnInRewardPositionY));
        Assert.Equal(1, farmerQuest.CompletionVariable1);
        Assert.False(farmerQuest.ActivateOnRoute);
        Assert.True(farmerQuest.TurnInActivationOnly);
        Assert.Equal((true, 1, 280, 2103U, 0, 63, 16), (
            farmerQuest.TurnInOnCompletion,
            farmerQuest.TurnInCompletionVariable2,
            farmerQuest.TurnInCompletionMessageId,
            farmerQuest.TurnInCompletionRewardItemId,
            farmerQuest.TurnInCompletionRewardItemLevel,
            farmerQuest.TurnInCompletionRewardPositionX,
            farmerQuest.TurnInCompletionRewardPositionY));
        var graveQuest = activation.Simulation.WorldState.Quests[3017];
        Assert.Equal((7, 0U, 43, 66, 43U, 274, true), (
            graveQuest.TurnInTownerId,
            graveQuest.TurnInLevelId,
            graveQuest.TurnInPositionX,
            graveQuest.TurnInPositionY,
            graveQuest.TurnInItemId,
            graveQuest.TurnInMessageId,
            graveQuest.TurnInDialogueOnly));
        var poisonedWaterQuest = activation.Simulation.WorldState.Quests[3014];
        Assert.Equal((1, 0U, 55, 79, true, -1, 0, 2, 41, 1006U, 2, 54, 80), (
            poisonedWaterQuest.TurnInTownerId,
            poisonedWaterQuest.TurnInLevelId,
            poisonedWaterQuest.TurnInPositionX,
            poisonedWaterQuest.TurnInPositionY,
            poisonedWaterQuest.TurnInOnCompletion,
            poisonedWaterQuest.TurnInCompletionVariable2,
            poisonedWaterQuest.TurnInCompletionRequiredVariable1,
            poisonedWaterQuest.TurnInCompletionVariable1,
            poisonedWaterQuest.TurnInCompletionMessageId,
            poisonedWaterQuest.TurnInCompletionRewardUniqueId,
            poisonedWaterQuest.TurnInCompletionRewardItemLevel,
            poisonedWaterQuest.TurnInCompletionRewardPositionX,
            poisonedWaterQuest.TurnInCompletionRewardPositionY));
        var generatedPoisonWaterReward = activation.Simulation.Items!.GenerateUnique(
            1006,
            0x12345678,
            poisonedWaterQuest.TurnInCompletionRewardItemLevel);
        Assert.Equal(1006, generatedPoisonWaterReward.UniqueId);
        Assert.Equal(9100, generatedPoisonWaterReward.Value);
        Assert.Equal(10 << 6, generatedPoisonWaterReward.PlusHitPoints);
        Assert.Equal(10, generatedPoisonWaterReward.PlusMagicResistance);
        var mushroomQuest = activation.Simulation.WorldState.Quests[3002];
        Assert.Equal((1, 0U, 55, 79, 2140U, 124, 2142U, 55, 80, 5, 7, true), (
            mushroomQuest.TurnInTownerId,
            mushroomQuest.TurnInLevelId,
            mushroomQuest.TurnInPositionX,
            mushroomQuest.TurnInPositionY,
            mushroomQuest.TurnInItemId,
            mushroomQuest.TurnInMessageId,
            mushroomQuest.TurnInRewardItemId,
            mushroomQuest.TurnInRewardPositionX,
            mushroomQuest.TurnInRewardPositionY,
            mushroomQuest.TurnInRequiredVariable1,
            mushroomQuest.TurnInVariable1,
            mushroomQuest.TurnInProgressOnly));
        var mushroomInteractions = activation.QuestTownerInteractions!.Definitions;
        Assert.Equal(28, mushroomInteractions.Count);
        Assert.Equal(
            [(300101U, 0, 2135U, AuthoritativeQuestTownerInteractionKind.Complete, -1, -1, 117),
             (300201U, 6, 2141U, AuthoritativeQuestTownerInteractionKind.Activate, -1, 2, 129),
             (300202U, 6, 2139U, AuthoritativeQuestTownerInteractionKind.Advance, 2, 5, 131),
             (300203U, 6, 2140U, AuthoritativeQuestTownerInteractionKind.Dialogue, 5, -1, 132),
             (300204U, 6, 2142U, AuthoritativeQuestTownerInteractionKind.Complete, 7, -1, 133),
             (300205U, 3, 0U, AuthoritativeQuestTownerInteractionKind.Dialogue, -1, 2, 3),
             (300206U, 3, 0U, AuthoritativeQuestTownerInteractionKind.Activate, -1, 1, 2),
             (300207U, 0, 0U, AuthoritativeQuestTownerInteractionKind.Activate, -1, 0, 115),
             (300208U, 0, 0U, AuthoritativeQuestTownerInteractionKind.Activate, -1, 0, 88),
             (300209U, 1, 0U, AuthoritativeQuestTownerInteractionKind.Activate, -1, 0, 39),
             (300210U, 1, 0U, AuthoritativeQuestTownerInteractionKind.Dialogue, -1, -1, 39),
             (300211U, 1, 2140U, AuthoritativeQuestTownerInteractionKind.Advance, 5, 6, 124),
             (300701U, 2, 0U, AuthoritativeQuestTownerInteractionKind.Activate, -1, 1, 63),
             (300801U, 3, 0U, AuthoritativeQuestTownerInteractionKind.Activate, -1, 1, 12),
             (300802U, 3, 2136U, AuthoritativeQuestTownerInteractionKind.Complete, -1, -1, 13),
             (301101U, 0, 2138U, AuthoritativeQuestTownerInteractionKind.Complete, -1, -1, 90),
             (301401U, 1, 0U, AuthoritativeQuestTownerInteractionKind.CompleteAfterQuest, 0, 2, 41),
             (301601U, 4, 0U, AuthoritativeQuestTownerInteractionKind.CompleteAfterQuest, 7, 8, 25),
             (301602U, 4, 34U, AuthoritativeQuestTownerInteractionKind.Activate, -1, 2, 23),
             (301603U, 4, 0U, AuthoritativeQuestTownerInteractionKind.Dialogue, -1, -1, 23),
             (301701U, 7, 43U, AuthoritativeQuestTownerInteractionKind.Dialogue, -1, -1, 274),
             (301801U, 10, 0U, AuthoritativeQuestTownerInteractionKind.Activate, -1, 1, 277),
             (301802U, 10, 0U, AuthoritativeQuestTownerInteractionKind.CompleteAfterQuest, -1, -1, 280),
             (301901U, 11, 0U, AuthoritativeQuestTownerInteractionKind.Activate, -1, 1, 283),
             (301902U, 11, 0U, AuthoritativeQuestTownerInteractionKind.Dialogue, 1, -1, 284),
             (301903U, 11, 2102U, AuthoritativeQuestTownerInteractionKind.Complete, -1, -1, 285),
             (302401U, 12, 2105U, AuthoritativeQuestTownerInteractionKind.Dialogue, -1, -1, 303),
             (302402U, 12, 2104U, AuthoritativeQuestTownerInteractionKind.Complete, -1, -1, 304)],
            mushroomInteractions.Select(interaction => (
                interaction.InteractionId,
                interaction.TownerId,
                interaction.ItemId,
                interaction.Kind,
                interaction.RequiredVariable1,
                interaction.Variable1,
                interaction.MessageId)));
        var graveMattersDialogue = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301701);
        Assert.False(graveMattersDialogue.ConsumeItem);
        Assert.True(graveMattersDialogue.RequireUnlogged);
        var farmersOrchardActivation = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301801);
        Assert.True(farmersOrchardActivation.UseQuestTurnInReward);
        var jerseyCompletion = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 302402);
        Assert.True(jerseyCompletion.UseQuestTurnInReward);
        Assert.Equal(1, jerseyCompletion.RequiredVariable2);
        var poisonedWaterCompletion = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301401);
        Assert.True(poisonedWaterCompletion.AllowCompleted);
        Assert.True(poisonedWaterCompletion.UseQuestCompletionReward);
        var lazarusCompletion = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301601);
        Assert.Equal((7, 0, 8, 1), (
            lazarusCompletion.RequiredVariable1,
            lazarusCompletion.RequiredVariable2,
            lazarusCompletion.Variable1,
            lazarusCompletion.Variable2));
        var lazarusActivation = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301602);
        Assert.Equal(34U, lazarusActivation.ItemId);
        Assert.Equal(23, lazarusActivation.MessageId);
        var lazarusOpeningDialogue = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301603);
        Assert.True(lazarusOpeningDialogue.RequireUnlogged);
        Assert.False(lazarusOpeningDialogue.ConsumeItem);
        var farmersOrchardCompletion = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301802);
        Assert.True(farmersOrchardCompletion.UseQuestCompletionReward);
        var kingCompletionDialogue = mushroomInteractions.Single(interaction => interaction.InteractionId == 300205);
        Assert.True(kingCompletionDialogue.AllowCompleted);
        Assert.Equal(1, kingCompletionDialogue.RequiredVariable2);
        Assert.Equal(2, kingCompletionDialogue.Variable2);
        var kingActivation = mushroomInteractions.Single(interaction => interaction.InteractionId == 300206);
        Assert.Equal((2U, 4U), (
            kingActivation.RequiredVisitedLevel1,
            kingActivation.RequiredVisitedLevel2));
        var magicRockActivation = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 300207);
        Assert.Equal((0, 1, 4U, 5U), (
            magicRockActivation.RequiredVariable2,
            magicRockActivation.Variable2,
            magicRockActivation.RequiredVisitedLevel1,
            magicRockActivation.RequiredVisitedLevel2));
        var magicRockCompletion = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 300101);
        Assert.True(magicRockCompletion.UseQuestTurnInReward);
        Assert.Equal(1, magicRockCompletion.RequiredVariable2);
        var butcherActivation = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 300701);
        Assert.Equal((2, 24, 32), (
            butcherActivation.TownerId,
            butcherActivation.PositionX,
            butcherActivation.PositionY));
        var ogdenActivation = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 300801);
        Assert.Equal((0, 1, 1, 3U, 4U), (
            ogdenActivation.RequiredVariable2,
            ogdenActivation.Variable1,
            ogdenActivation.Variable2,
            ogdenActivation.RequiredVisitedLevel1,
            ogdenActivation.RequiredVisitedLevel2));
        var ogdenCompletion = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 300802);
        Assert.True(ogdenCompletion.UseQuestTurnInReward);
        Assert.Equal(2136U, ogdenCompletion.ItemId);
        var anvilCompletion = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301101);
        Assert.True(anvilCompletion.UseQuestTurnInReward);
        Assert.Equal(1, anvilCompletion.RequiredVariable2);
        var anvilActivation = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 300208);
        Assert.Equal((0, 1, 9U, 10U), (
            anvilActivation.RequiredVariable2,
            anvilActivation.Variable2,
            anvilActivation.RequiredVisitedLevel1,
            anvilActivation.RequiredVisitedLevel2));
        var poisonedWaterActivation = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 300209);
        Assert.True(poisonedWaterActivation.RequireUnlogged);
        Assert.Equal((1U, 5U), (
            poisonedWaterActivation.RequiredVisitedLevel1,
            poisonedWaterActivation.RequiredVisitedLevel2));
        var poisonedWaterFallback = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 300210);
        Assert.True(poisonedWaterFallback.RequireUnlogged);
        var mushroomHealerAdvance = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 300211);
        Assert.True(mushroomHealerAdvance.UseQuestTurnInReward);
        Assert.Equal((1, 5, 6, 124), (
            mushroomHealerAdvance.TownerId,
            mushroomHealerAdvance.RequiredVariable1,
            mushroomHealerAdvance.Variable1,
            mushroomHealerAdvance.MessageId));
        var girlActivation = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301901);
        Assert.Equal((17U, 2102U, false), (
            girlActivation.RequiredVisitedLevel1,
            girlActivation.RequiredItemId,
            girlActivation.RequireItemPresent));
        var girlDialogue = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301902);
        Assert.Equal((1, 2102U, false), (
            girlDialogue.RequiredVariable1,
            girlDialogue.RequiredItemId,
            girlDialogue.RequireItemPresent));
        var girlCompletion = mushroomInteractions.Single(
            interaction => interaction.InteractionId == 301903);
        Assert.True(girlCompletion.AllowInitial);
        Assert.True(girlCompletion.UseQuestTurnInReward);
        var jerseyQuest = activation.Simulation.WorldState.Quests[3024];
        Assert.Equal((300, 304, 12, 0U, 61, 22, 2104U, 1, 1011U, 1, 62, 23), (
            jerseyQuest.MessageId,
            jerseyQuest.TurnInMessageId,
            jerseyQuest.TurnInTownerId,
            jerseyQuest.TurnInLevelId,
            jerseyQuest.TurnInPositionX,
            jerseyQuest.TurnInPositionY,
            jerseyQuest.TurnInItemId,
            jerseyQuest.TurnInRequiredVariable2,
            jerseyQuest.TurnInRewardUniqueId,
            jerseyQuest.TurnInRewardItemLevel,
            jerseyQuest.TurnInRewardPositionX,
            jerseyQuest.TurnInRewardPositionY));
        Assert.Equal((2105U, 303), (
            jerseyQuest.TurnInAlternateItemId,
            jerseyQuest.TurnInAlternateMessageId));
        Assert.Equal(
            Enumerable.Range(0, 25).Select(levelId => (uint)levelId),
            activation.Simulation.WorldState.World!.Levels.Select(level => level.LevelId));
        Assert.True(activation.Simulation.WorldState.World!.IsWalkable(24, 39, 39));
        Assert.Equal(5, activation.LevelGeneration!.Definitions.Count);
        Assert.Equal(25, activation.LevelGeneration.GetRequired(24).TrapPercent);
        Assert.Equal(190, activation.LevelGeneration.GetRequired(24).OrdinaryMonsterCapacity);
        Assert.NotNull(activation.MonsterSpawnPlanner);
        var plannedSpawns = activation.MonsterSpawnPlanner.Plan(
            24,
            seed: 0x1234,
            multiplayer: false,
            maximumMonsters: 3);
        Assert.Equal(3, plannedSpawns.Count);
        Assert.All(plannedSpawns, spawn => {
            Assert.Equal(24U, spawn.LevelId);
            Assert.True(spawn.Definition.MinimumDungeonLevel <= 24);
            Assert.True(spawn.Definition.MaximumDungeonLevel >= 24);
        });
        var proceduralActivation = DiabloModule.Instance.ContentBootstrap.Activate(
            composed,
            randomSeed: 0x1234,
            gameMode: DiabloGameMode.Multiplayer,
            monsterSpawns: new DiabloMonsterSpawnConfiguration(3, 24));
        var proceduralTargets = proceduralActivation.Simulation.WorldState.CombatTargets.Values
            .Where(target => target.LevelId == 24)
            .ToArray();
        Assert.Equal(3, proceduralTargets.Length);
        Assert.Contains(
            proceduralActivation.Simulation.WorldState.CombatTargets.Values,
            target => target.LevelId == 4 && target.IsUnique && target.MonsterId == 140);
        Assert.All(
            proceduralTargets,
            target => {
                Assert.InRange(
                    target.EntityId,
                    AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId,
                    AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId + 2);
                Assert.Equal(24U, target.LevelId);
                Assert.True(target.HitPoints > 0);
                Assert.True(target.MonsterId > 0);
            });
        Assert.Equal(24, activation.QuestTypes!.Definitions.Count);
        Assert.Equal(13, activation.QuestPools!.Definitions.Count);
        Assert.Equal(24, activation.Simulation.WorldState.Quests.Count);
        var cornerstoneQuestType = activation.QuestTypes!.Definitions
            .Single(definition => definition.QuestId == 3023);
        Assert.Equal((14, 14), (
            cornerstoneQuestType.WorldItemAnchorPositionX,
            cornerstoneQuestType.WorldItemAnchorPositionY));
        var nativeFragments = activation.NativeDunFragments!.Definitions;
        Assert.Equal(4, nativeFragments.Count);
        var nakrulFragment = nativeFragments.Single(definition => definition.FragmentId == 1);
        Assert.Equal((1U, 24U, 2403U, -6, -8), (
            nakrulFragment.FragmentId,
            nakrulFragment.LevelId,
            nakrulFragment.AnchorEntityId,
            nakrulFragment.AnchorOffsetX,
            nakrulFragment.AnchorOffsetY));
        var uberroom = AuthoritativeDunMapAsset.Load(
            nakrulFragment.AssetPath,
            composed.GetRequiredAsset(nakrulFragment.AssetPath).Contents);
        var hellfireLevel = activation.Simulation.WorldState.World!.Levels
            .Single(level => level.LevelId == 24);
        Assert.Equal(20 * 20, hellfireLevel.NativeMegaTileLayer.Count);
        Assert.Equal(uberroom.TileLayer[0], hellfireLevel.NativeMegaTileLayer[6 * 20 + 9]);
        Assert.Equal(
            uberroom.TileLayer[^1],
            hellfireLevel.NativeMegaTileLayer[(6 + uberroom.MegaHeight - 1) * 20
                + 9 + uberroom.MegaWidth - 1]);
        var cornerstoneFragment = nativeFragments.Single(definition => definition.FragmentId == 2);
        Assert.Equal((21U, "hellfire_level_21", "nlevels/l5data/cornerstone.dun", 14, 14), (
            cornerstoneFragment.LevelId,
            cornerstoneFragment.LevelSymbol,
            cornerstoneFragment.AssetPath,
            cornerstoneFragment.AnchorPositionX,
            cornerstoneFragment.AnchorPositionY));
        var cornerstoneMap = AuthoritativeDunMapAsset.Load(
            cornerstoneFragment.AssetPath,
            composed.GetRequiredAsset(cornerstoneFragment.AssetPath).Contents);
        var cornerstoneLevel = activation.Simulation.WorldState.World!.Levels
            .Single(level => level.LevelId == 21);
        Assert.Equal(
            cornerstoneMap.TileLayer[0],
            cornerstoneLevel.NativeMegaTileLayer[7 * 20 + 7]);
        Assert.Equal(
            cornerstoneMap.TileLayer[^1],
            cornerstoneLevel.NativeMegaTileLayer[(7 + cornerstoneMap.MegaHeight - 1) * 20
                + 7 + cornerstoneMap.MegaWidth - 1]);
        Assert.Equal(
            [(1U, 1U, 2401U, 2403U, -4, 0), (2U, 1U, 2402U, 2403U, -2, 0),
             (3U, 1U, 2409U, 2403U, 3, -1), (4U, 4U, 2021U, 0U, 0, 0),
             (5U, 4U, 2023U, 0U, 9, 16), (6U, 4U, 2024U, 0U, 10, 16),
             (7U, 4U, 2025U, 0U, 11, 16)],
            activation.NativeDunFragmentObjects!.Placements.Select(placement => (
                placement.PlacementId,
                placement.FragmentId,
                placement.EntityId,
                placement.AnchorEntityId,
                placement.OffsetX,
                placement.OffsetY)).ToArray());
        var nakrulTomes = activation.Simulation.WorldState.Objects.Values
            .Where(@object => @object.QuestId == 3022
                && @object.EffectKind == AuthoritativeObjectEffectKind.QuestSequence)
            .OrderBy(@object => @object.EffectAmount)
            .ToArray();
        Assert.Equal(18, activation.QuestSequenceVariants!.Definitions.Count);
        Assert.Equal(
            6,
            activation.QuestSequenceVariants.Definitions
                .Select(step => step.VariantId)
                .Distinct()
                .Count());
        Assert.Equal(3, nakrulTomes.Length);
        Assert.Equal(
            [1, 2, 3],
            nakrulTomes.Select(@object => @object.EffectAmount).ToArray());
        Assert.All(nakrulTomes, @object => Assert.Equal(105U, @object.NativeObjectId));
        Assert.Equal(
            [(20, 20), (22, 20), (24, 20)],
            nakrulTomes.Select(@object => (@object.PositionX, @object.PositionY)).ToArray());
        var finalNakrulTome = nakrulTomes.Single(@object => @object.EffectAmount == 3);
        Assert.Equal(
            (AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster, 139U, 9200U),
            (finalNakrulTome.QuestSequenceFollowUpKind,
             finalNakrulTome.QuestSequenceFollowUpAmount,
             finalNakrulTome.QuestSequenceFollowUpWorldMutationId));
        var nakrulLever = Assert.Single(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.ObjectId == 6007);
        Assert.Equal((27, 19, 3022U, AuthoritativeObjectQuestTransitionKind.CompleteQuest), (
            nakrulLever.PositionX,
            nakrulLever.PositionY,
            nakrulLever.QuestId,
            nakrulLever.QuestTransitionKind));
        Assert.Equal(3, nakrulLever.QuestTransitionRequiredVariable1);
        Assert.Equal(107U, nakrulLever.NativeObjectId);
        var nakrulRoomMutation = Assert.Single(
            activation.Simulation.WorldState.WorldMutations!.Definitions,
            mutation => mutation.MutationId == 9200);
        Assert.Equal(2403U, nakrulRoomMutation.AnchorEntityId);
        Assert.Equal(
            [(22, 20, (ushort)299), (23, 20, (ushort)300),
             (24, 20, (ushort)297), (25, 20, (ushort)298)],
            nakrulRoomMutation
                .GetPieceLayerCells(40, 40, finalNakrulTome.PositionX, finalNakrulTome.PositionY)
                .Select(entry => (entry.Cell % 40, entry.Cell / 40, entry.PieceId)));
        Assert.Equal(
            [(ushort)297, (ushort)298, (ushort)299, (ushort)300],
            nakrulRoomMutation.PiecePropertyOverrides
                .Select(property => property.PieceId)
                .ToArray());
        Assert.All(
            nakrulRoomMutation.PiecePropertyOverrides,
            property => Assert.Equal(AuthoritativeDunTileFlags.None, property.Flags));
        Assert.True(activation.Simulation.WorldState.ApplyWorldMutation(9200));
        var projectedNakrulLevel = Assert.Single(
            activation.Simulation.WorldState.LevelGeometry,
            level => level.LevelId == 24);
        Assert.Equal(
            [(22, 20, (ushort)299), (23, 20, (ushort)300),
             (24, 20, (ushort)297), (25, 20, (ushort)298)],
            projectedNakrulLevel.NativePieceLayer
                .Select((pieceId, cell) => (X: cell % 40, Y: cell / 40, PieceId: pieceId))
                .Where(entry => entry.PieceId != 0)
                .ToArray());
        Assert.All(
            new[] { 22 + 20 * 40, 23 + 20 * 40, 24 + 20 * 40, 25 + 20 * 40 },
            cell => {
                Assert.DoesNotContain(cell, projectedNakrulLevel.BlockedCells);
                Assert.DoesNotContain(cell, projectedNakrulLevel.MissileBlockedCells);
            });
        var graveObject = Assert.Single(
            activation.Simulation.WorldState.Objects.Values,
            @object => @object.ObjectId == 6004);
        Assert.Equal((3017U, AuthoritativeObjectEffectKind.QuestItemCompletion, 43), (
            graveObject.QuestId,
            graveObject.EffectKind,
            graveObject.EffectAmount));
        var hiveObjects = activation.Simulation.WorldState.Objects.Values
            .Where(@object => @object.ObjectId == 6005)
            .OrderBy(@object => @object.EntityId)
            .ToArray();
        Assert.Equal(4, hiveObjects.Length);
        Assert.All(hiveObjects, @object => {
            Assert.Equal((3018U, AuthoritativeObjectEffectKind.QuestItemCompletion, 2101), (
                @object.QuestId,
                @object.EffectKind,
                @object.EffectAmount));
        });
        var cryptWarp = Assert.Single(
            activation.Simulation.WorldState.World!.WarpTargets,
            warp => warp.RequiredQuestId == 3017);
        Assert.Equal((0U, 36, 22, 17U, 36, 24, true), (
            cryptWarp.SourceLevelId,
            cryptWarp.TriggerX,
            cryptWarp.TriggerY,
            cryptWarp.TargetLevelId,
            cryptWarp.TargetX,
            cryptWarp.TargetY,
            cryptWarp.RequiredQuestCompleted));
        var hiveWarp = Assert.Single(
            activation.Simulation.WorldState.World!.WarpTargets,
            warp => warp.RequiredQuestId == 3018);
        Assert.Equal((0U, 80, 62, 17U, 20, 20, true), (
            hiveWarp.SourceLevelId,
            hiveWarp.TriggerX,
            hiveWarp.TriggerY,
            hiveWarp.TargetLevelId,
            hiveWarp.TargetX,
            hiveWarp.TargetY,
            hiveWarp.RequiredQuestCompleted));
        Assert.Equal(0, activation.Simulation.WorldState.Quests[3022].CompletionVariable1);
        var baseQuest = activation.Simulation.WorldState.Quests[3001];
        Assert.Equal(
            activation.QuestPools.SelectUnavailableQuestIds(0).Contains(baseQuest.QuestId),
            baseQuest.Unavailable);
        Assert.False(activation.Simulation.WorldState.Quests[3021].Active);
        Assert.Equal("Defiler", activation.Simulation.WorldState.Quests[3021].CompletionUniqueIdentity);
        Assert.False(activation.Simulation.WorldState.Quests[3006].Active);
        Assert.False(activation.Simulation.WorldState.Quests[3006].ActivateOnRoute);
        Assert.True(activation.Simulation.WorldState.Quests[3006].CompletionDiablo);
        Assert.Equal(1, activation.QuestTypes.Definitions[12].SetLevel);
        Assert.Equal("Archbishop Lazarus' Lair", activation.SetLevelNames!.GetDisplayName(5));
        Assert.Equal("Gharbad", activation.Simulation.WorldState.Quests[3003].CompletionUniqueIdentity);
        Assert.Equal("Zhar", activation.Simulation.WorldState.Quests[3004].CompletionUniqueIdentity);
        Assert.Equal("Warlord", activation.Simulation.WorldState.Quests[3012].CompletionUniqueIdentity);
        Assert.Equal("Lazarus", activation.Simulation.WorldState.Quests[3016].CompletionUniqueIdentity);
        Assert.Equal((4, true, 1, 7, 8, 25), (
            activation.Simulation.WorldState.Quests[3016].TurnInTownerId,
            activation.Simulation.WorldState.Quests[3016].TurnInOnCompletion,
            activation.Simulation.WorldState.Quests[3016].TurnInCompletionVariable2,
            activation.Simulation.WorldState.Quests[3016].TurnInCompletionRequiredVariable1,
            activation.Simulation.WorldState.Quests[3016].TurnInCompletionVariable1,
            activation.Simulation.WorldState.Quests[3016].TurnInCompletionMessageId));
        Assert.Equal((81, 0U, 0U, 0, 0, 0, 0, false), (
            activation.Simulation.WorldState.Quests[3005].MessageId,
            activation.Simulation.WorldState.Quests[3005].DialogueItemId,
            activation.Simulation.WorldState.Quests[3005].DialogueRewardUniqueId,
            activation.Simulation.WorldState.Quests[3005].DialogueRewardItemLevel,
            activation.Simulation.WorldState.Quests[3005].DialogueRewardOffsetX,
            activation.Simulation.WorldState.Quests[3005].DialogueRewardOffsetY,
            activation.Simulation.WorldState.Quests[3005].DialogueCompletionMessageId,
            activation.Simulation.WorldState.Quests[3005].DialogueCompletionDefeatsTarget));
        var lachdananStages = activation.QuestDialogueStages!.Definitions
            .Where(stage => stage.QuestId == 3005)
            .ToArray();
        Assert.Equal(2, lachdananStages.Length);
        Assert.Equal(
            [(81, false, AuthoritativeQuestDialogueRewardKind.None, 0U),
             (83, true, AuthoritativeQuestDialogueRewardKind.UniqueItem, 1008U)],
            lachdananStages.OrderBy(stage => stage.MessageId).Select(stage => (
                stage.MessageId,
                stage.RequireItemPresent,
                stage.RewardKind,
                stage.RewardUniqueId)));
        Assert.Equal("TEXT_VEIL9", activation.DialogueMessages!.Definitions
            .Single(message => message.MessageId == 81)
            .Symbol);
        Assert.Equal("TEXT_VEIL11", activation.DialogueMessages.Definitions
            .Single(message => message.MessageId == 83)
            .Symbol);
        Assert.Equal((0U, 0, 0, 0, false), (
            activation.Simulation.WorldState.Quests[3004].DialogueRewardItemId,
            activation.Simulation.WorldState.Quests[3004].DialogueRewardItemLevel,
            activation.Simulation.WorldState.Quests[3004].DialogueRewardOffsetX,
            activation.Simulation.WorldState.Quests[3004].DialogueRewardOffsetY,
            activation.Simulation.WorldState.Quests[3004].DialogueRewardOnActivation));
        Assert.Equal(144, activation.Simulation.WorldState.Quests[3003].MessageId);
        Assert.Equal(148, activation.Simulation.WorldState.Quests[3004].MessageId);
        Assert.Equal(110, activation.Simulation.WorldState.Quests[3012].MessageId);
        Assert.Equal(35, activation.Simulation.WorldState.Quests[3016].MessageId);
        Assert.Equal(1, activation.Simulation.WorldState.Quests[3003].DialogueVariable1);
        Assert.Equal(2, activation.Simulation.WorldState.Quests[3004].DialogueVariable1);
        Assert.Equal(2, activation.Simulation.WorldState.Quests[3012].DialogueVariable1);
        Assert.Equal(3, activation.Simulation.WorldState.Quests[3012].DialogueCompletionVariable1);
        Assert.Equal(6, activation.Simulation.WorldState.Quests[3016].DialogueVariable1);
        Assert.Equal(5, activation.Simulation.WorldState.Quests[3003].CompletionVariable1);
        Assert.Equal(3, activation.Simulation.WorldState.Quests[3004].CompletionVariable1);
        Assert.Equal(3, activation.Simulation.WorldState.Quests[3012].CompletionVariable1);
        Assert.Equal(7, activation.Simulation.WorldState.Quests[3016].CompletionVariable1);
        var hellfireMonster = activation.Simulation.WorldState.CombatTargets[10];
        Assert.Equal(2U, hellfireMonster.MonsterId);
        Assert.True(hellfireMonster.IsUndead);
        Assert.True(activation.Simulation.WorldState.CombatTargets[14].IsAnimal);
        Assert.False(hellfireMonster.IsDemon);
        Assert.Equal(100, hellfireMonster.MagicResistance);
        Assert.Equal(16, hellfireMonster.DeathAnimationTicks);
        Assert.Equal(10, hellfireMonster.ToHit);
        Assert.Equal(0, hellfireMonster.ToHitSpecial);
        Assert.Equal("Zombie", hellfireMonster.NativeAi);
        Assert.Equal(0, hellfireMonster.Intelligence);
        Assert.Equal(11, hellfireMonster.MaxHitPoints);
        Assert.Equal(9, activation.Simulation.WorldState.CombatTargets.Count);
        Assert.Contains(
            activation.Simulation.WorldState.CombatTargets.Values,
            target => target.LevelId == 4 && target.IsUnique && target.MonsterId == 140);
        Assert.Equal(25, activation.Simulation.WorldState.CombatTargets[11].MaxHitPoints);
        Assert.True(activation.Simulation.WorldState.CombatTargets[12].IsUndead);
        Assert.Equal(5, activation.Simulation.WorldState.CombatTargets[15].MaxDamageSpecial);
        var defiler = activation.Simulation.WorldState.CombatTargets[16];
        Assert.Equal(126U, defiler.MonsterId);
        Assert.True(defiler.IsUnique);
        Assert.Equal("Defiler", defiler.UniqueIdentity);
        Assert.Equal("SkeletonMelee", defiler.NativeAi);
        Assert.Null(defiler.Drop);
        Assert.Equal(43U, activation.Simulation.WorldState.Quests[3021].CompletionRewardItemId);
        Assert.Equal(0, activation.Simulation.WorldState.Quests[3021].CompletionRewardItemLevel);
        var monsterTypes = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            composed.GetRequiredTable("monster_types.tsv").ToCanonicalTsv());
        Assert.Equal(140, monsterTypes.Definitions.Count);
        Assert.Equal(6, monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).AggroRange);
        Assert.Equal(16, monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).DeathAnimationTicks);
        Assert.Equal(54U, monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).ExperienceReward);
        Assert.Equal(10, monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).ToHit);
        Assert.Equal("Zombie", monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).NativeAi);
        Assert.Equal(0, monsterTypes.Definitions.Single(definition => definition.MonsterId == 2).Intelligence);
        Assert.Equal(
            (4, 8, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Fallen")
                .Select(definition => (
                    definition.FallenFearRadius,
                    definition.FallenFearLevelOffset,
                    definition.FallenFearMinimumTicks))
                .Distinct()
                .Single());
        Assert.Equal(
            (5, 1, 3, 9),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Sneak")
                .Select(definition => (
                    definition.SneakDistanceBase,
                    definition.SneakDistanceIntelligenceScale,
                    definition.SneakRetreatReleaseDistance,
                    definition.SneakRetreatTicks))
                .Distinct()
                .Single());
        Assert.Equal(
            (8, 4, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Bat")
                .Select(definition => (
                    definition.BatAttackChanceBase,
                    definition.BatAttackChanceIntelligenceScale,
                    definition.BatRetreatDurationTicks))
                .Distinct()
                .Single());
        Assert.Equal(
            (35, 4, 20, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "SkeletonMelee")
                .Select(definition => (
                    definition.SkeletonMeleeDelayChanceBase,
                    definition.SkeletonMeleeDelayChanceIntelligenceScale,
                    definition.SkeletonMeleeAttackChanceBase,
                    definition.SkeletonMeleeAttackChanceIntelligenceScale))
                .Distinct()
                .Single());
        Assert.Equal(
            (4, 13, 2, 3, 2, 50),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "SkeletonRanged")
                .Select(definition => (
                    definition.SkeletonRangedRetreatMinimumDistance,
                    definition.SkeletonRangedRetreatChanceBase,
                    definition.SkeletonRangedRetreatChanceIntelligenceScale,
                    definition.SkeletonRangedAttackChanceBase,
                    definition.SkeletonRangedAttackChanceIntelligenceScale,
                    definition.SkeletonRangedMovementFreshChanceBonus))
                .Distinct()
                .Single());
        Assert.Equal(
            (28, 2, 23, 2, 50, 2, 50),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "GoatMelee")
                .Select(definition => (
                    definition.GoatMeleeMovementChanceBase,
                    definition.GoatMeleeMovementChanceIntelligenceScale,
                    definition.GoatMeleeAttackChanceBase,
                    definition.GoatMeleeAttackChanceIntelligenceScale,
                    definition.GoatMeleeSpecialHealthThresholdPercent,
                    definition.GoatMeleeSpecialChanceGroupPeriod,
                    definition.AiAvoidanceMovementFreshChanceBonus))
                .Distinct()
                .Single());
        Assert.Equal(
            (20, 4, 50, 20, 4, 15, 4),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Fat")
                .Select(definition => (
                    definition.FatMovementChanceBase,
                    definition.FatMovementChanceIntelligenceScale,
                    definition.FatMovementFreshChanceBonus,
                    definition.FatAttackChanceBase,
                    definition.FatAttackChanceIntelligenceScale,
                    definition.FatSpecialChanceBase,
                    definition.FatSpecialChanceIntelligenceScale))
                .Distinct()
                .Single());
        Assert.Equal(
            (10, 2, 20, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Zombie")
                .Select(definition => (
                    definition.ZombieActionChanceBase,
                    definition.ZombieActionChanceIntelligenceScale,
                    definition.ZombieRandomDirectionChanceBase,
                    definition.ZombieRandomDirectionChanceIntelligenceScale))
                .Distinct()
                .Single());
        Assert.Equal(
            (2, 4, 2),
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Zombie")
                .Select(definition => (
                    definition.ZombieMovementDistanceMinimum,
                    definition.ZombieMovementDistanceBase,
                    definition.ZombieMovementDistanceIntelligenceScale))
                .Distinct()
                .Single());
        var toadDemon = monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 54);
        Assert.Equal(("Fat", 40, 8, 20), (
            toadDemon.NativeAi,
            toadDemon.ToHitSpecial,
            toadDemon.MinimumSpecialDamage,
            toadDemon.MaximumSpecialDamage));
        var hornedDemon = monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 64);
        Assert.Equal(("Rhino", 0, 100, 5, 32), (
            hornedDemon.NativeAi,
            hornedDemon.Intelligence,
            hornedDemon.ToHitSpecial,
            hornedDemon.MinimumSpecialDamage,
            hornedDemon.MaximumSpecialDamage));
        Assert.Equal(
            [0, 1, 2, 3],
            monsterTypes.Definitions
                .Where(definition => definition.NativeAi == "Rhino")
                .OrderBy(definition => definition.NativeMonsterId)
                .Select(definition => definition.Intelligence));
        Assert.Equal((5, 43, 2), monsterTypes.Definitions
            .Where(definition => definition.NativeAi == "Rhino")
            .Select(definition => (
                definition.RhinoChargeMinimumDistance,
                definition.RhinoChargeChanceBase,
                definition.RhinoChargeChanceIntelligenceScale))
            .Distinct()
            .Single());
        Assert.Equal((33, 2, 50), monsterTypes.Definitions
            .Where(definition => definition.NativeAi == "Rhino")
            .Select(definition => (
                definition.RhinoMovementChanceBase,
                definition.RhinoMovementChanceIntelligenceScale,
                definition.RhinoMovementFreshChanceBonus))
            .Distinct()
            .Single());
        Assert.Equal(
            "HorkDemon",
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).NativeAi);
        Assert.Equal((28, 2, 3, 43, 2, 33, 2), (
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonAttackChanceBase,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonAttackChanceIntelligenceScale,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonSpawnMinimumDistance,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonSpawnChanceBase,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonSpawnChanceIntelligenceScale,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonMovementChanceBase,
            monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 123).HorkDemonMovementChanceIntelligenceScale));
        Assert.Equal((2, 1, 50, 16, 5, 4), monsterTypes.Definitions
            .Where(definition => definition.NativeAi == "Gargoyle")
            .Select(definition => (
                definition.GargoyleSafeDistanceBase,
                definition.GargoyleSafeDistanceIntelligenceScale,
                definition.GargoyleHealingHealthThresholdPercent,
                definition.GargoyleHealDivisorMultiplier,
                definition.GargoyleHealDivisorRandomRange,
                definition.GargoyleHealDivisorRandomOffset))
            .Distinct()
            .Single());
        Assert.Equal((10, 4, 8, 1, 75, 50), monsterTypes.Definitions
            .Where(definition => definition.NativeAi == "Scavenger")
            .Select(definition => (
                definition.ScavengerHealingDurationTicks,
                definition.ScavengerCorpseSearchRadius,
                definition.ScavengerHellfireHealingDivisor,
                definition.ScavengerBaseHealingAmount,
                definition.ScavengerBaseHealingTargetPercent,
                definition.ScavengerHealingHealthThresholdPercent))
            .Distinct()
            .Single());
        Assert.True(activation.MonsterMissiles!.TryGet("HorkDemon", out var horkDemonMissile));
        Assert.Equal(("HorkSpawn", AuthoritativeProjectileBehavior.HorkSpawn), (
            horkDemonMissile!.MissileId,
            horkDemonMissile!.ProjectileBehavior));
        Assert.Equal(119U, horkDemonMissile.SpawnMonsterId);
        Assert.Equal(8, horkDemonMissile.ProjectileSpeed);
        Assert.Equal(9, horkDemonMissile.ProjectileLifetime);
        Assert.Equal(1, horkDemonMissile.ProjectileImpactLifetime);
        Assert.Equal(48, monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 85).DeathAnimationTicks);
        Assert.Equal(42, monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 129).DeathAnimationTicks);
        Assert.Equal(12, monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 109).DeathAnimationTicks);
        Assert.Equal(7, monsterTypes.Definitions.Single(definition => definition.MonsterId == 5).AggroRange);
        Assert.Equal(73, monsterTypes.Definitions.Count(definition => definition.Demon));
        Assert.Equal(4, monsterTypes.Definitions.Count(definition => definition.Knockback));
        Assert.True(monsterTypes.Definitions.Single(definition => definition.NativeMonsterId == 110).Knockback);
        Assert.Equal("Zombie", monsterTypes.Definitions[1].Name);
        Assert.Equal(0U, monsterTypes.Definitions[1].NativeMonsterId);
        Assert.Equal(100, monsterTypes.Definitions[1].MagicResistance);
        Assert.Equal("Carver", monsterTypes.Definitions[6].Name);
        Assert.Equal("Horror", monsterTypes.Definitions[12].Name);
        Assert.True(monsterTypes.Definitions[12].Undead);
        Assert.Equal(11U, monsterTypes.Definitions[12].NativeMonsterId);
        Assert.Equal("Bone Gasher", monsterTypes.Definitions[20].Name);
        Assert.Equal(19U, monsterTypes.Definitions[20].NativeMonsterId);
        Assert.Equal(75, monsterTypes.Definitions[20].LightningResistance);
        Assert.Equal("Horror", monsterTypes.Definitions[24].Name);
        Assert.Equal(23U, monsterTypes.Definitions[24].NativeMonsterId);
        Assert.Equal("Invisible Lord", monsterTypes.Definitions[29].Name);
        Assert.Equal(28U, monsterTypes.Definitions[29].NativeMonsterId);
        Assert.True(monsterTypes.Definitions[29].Demon);
        Assert.Equal((75, 75, 75), (
            monsterTypes.Definitions[29].FireResistance,
            monsterTypes.Definitions[29].LightningResistance,
            monsterTypes.Definitions[29].MagicResistance));
        Assert.Equal("Stone Clan", monsterTypes.Definitions[36].Name);
        Assert.Equal(35U, monsterTypes.Definitions[36].NativeMonsterId);
        Assert.Equal(100, monsterTypes.Definitions[36].MagicResistance);
        Assert.Equal("Night Clan", monsterTypes.Definitions[38].Name);
        Assert.Equal((30, 30), (
            monsterTypes.Definitions[38].MinimumSpecialDamage,
            monsterTypes.Definitions[38].MaximumSpecialDamage));
        Assert.Equal(100, monsterTypes.Definitions[38].MagicResistance);
        Assert.Equal((100, 75), (
            monsterTypes.Definitions[42].LightningResistance,
            monsterTypes.Definitions[42].MagicResistance));
        Assert.Equal("Poison Spitter", monsterTypes.Definitions[48].Name);
        Assert.Equal(47U, monsterTypes.Definitions[48].NativeMonsterId);
        Assert.Equal(100, monsterTypes.Definitions[48].MagicResistance);
        Assert.Equal(100, monsterTypes.Definitions[48].AcidResistance);
        Assert.Equal("Pit Beast", monsterTypes.Definitions[49].Name);
        Assert.Equal((75, 100), (
            monsterTypes.Definitions[49].LightningResistance,
            monsterTypes.Definitions[49].MagicResistance));
        Assert.Equal("Skeleton King", monsterTypes.Definitions[51].Name);
        Assert.True(monsterTypes.Definitions[51].Undead);
        Assert.Equal((100, 100, 100), (
            monsterTypes.Definitions[51].FireResistance,
            monsterTypes.Definitions[51].LightningResistance,
            monsterTypes.Definitions[51].MagicResistance));
        Assert.Equal((3, 35, 4), (
            monsterTypes.Definitions[51].SkeletonKingSummonMinimumDistance,
            monsterTypes.Definitions[51].SkeletonKingSummonChanceBase,
            monsterTypes.Definitions[51].SkeletonKingSummonChanceIntelligenceScale));
        Assert.Equal((8, 20), (
            monsterTypes.Definitions[55].MinimumSpecialDamage,
            monsterTypes.Definitions[55].MaximumSpecialDamage));
        Assert.Equal("Devourer", monsterTypes.Definitions[60].Name);
        Assert.Equal(59U, monsterTypes.Definitions[60].NativeMonsterId);
        Assert.Equal((100, 100), (
            monsterTypes.Definitions[61].FireResistance,
            monsterTypes.Definitions[61].MagicResistance));
        Assert.Equal((5, 32), (
            monsterTypes.Definitions[65].MinimumSpecialDamage,
            monsterTypes.Definitions[65].MaximumSpecialDamage));
        Assert.Equal("Undead Balrog", monsterTypes.Definitions[72].Name);
        Assert.Equal(71U, monsterTypes.Definitions[72].NativeMonsterId);
        Assert.False(monsterTypes.Definitions[72].Undead);
        Assert.Equal((75, 75, 100), (
            monsterTypes.Definitions[72].FireResistance,
            monsterTypes.Definitions[72].LightningResistance,
            monsterTypes.Definitions[72].MagicResistance));
        Assert.Equal((4, 16), (
            monsterTypes.Definitions[77].MinimumSpecialDamage,
            monsterTypes.Definitions[77].MaximumSpecialDamage));
        Assert.Equal((75, 75, 75), (
            monsterTypes.Definitions[81].FireResistance,
            monsterTypes.Definitions[81].LightningResistance,
            monsterTypes.Definitions[81].MagicResistance));
        Assert.Equal("Blood Claw", monsterTypes.Definitions[84].Name);
        Assert.Equal(83U, monsterTypes.Definitions[84].NativeMonsterId);
        Assert.Equal((100, 75, 100), (
            monsterTypes.Definitions[84].FireResistance,
            monsterTypes.Definitions[84].LightningResistance,
            monsterTypes.Definitions[84].MagicResistance));
        Assert.Equal("Death Wing", monsterTypes.Definitions[85].Name);
        Assert.Equal((75, 100, 100), (
            monsterTypes.Definitions[85].FireResistance,
            monsterTypes.Definitions[85].LightningResistance,
            monsterTypes.Definitions[85].MagicResistance));
        Assert.Equal("Balrog", monsterTypes.Definitions[89].Name);
        Assert.Equal(88U, monsterTypes.Definitions[89].NativeMonsterId);
        Assert.Equal("Steel Lord", monsterTypes.Definitions[96].Name);
        Assert.Equal(95U, monsterTypes.Definitions[96].NativeMonsterId);
        Assert.Equal("Blood Knight", monsterTypes.Definitions[97].Name);
        Assert.Equal((75, 100, 100), (
            monsterTypes.Definitions[97].FireResistance,
            monsterTypes.Definitions[97].LightningResistance,
            monsterTypes.Definitions[97].MagicResistance));
        Assert.True(monsterTypes.Definitions[98].Undead);
        Assert.Equal("Reality Weaver", monsterTypes.Definitions[101].Name);
        Assert.True(monsterTypes.Definitions[101].Undead);
        Assert.Equal((100, 100, 75), (
            monsterTypes.Definitions[101].FireResistance,
            monsterTypes.Definitions[101].LightningResistance,
            monsterTypes.Definitions[101].MagicResistance));
        Assert.Equal("Cabalist", monsterTypes.Definitions[108].Name);
        Assert.Equal(107U, monsterTypes.Definitions[108].NativeMonsterId);
        Assert.Equal("Advocate", monsterTypes.Definitions[109].Name);
        Assert.Equal((100, 100, 100), (
            monsterTypes.Definitions[109].FireResistance,
            monsterTypes.Definitions[109].LightningResistance,
            monsterTypes.Definitions[109].MagicResistance));
        Assert.Equal("The Dark Lord", monsterTypes.Definitions[111].Name);
        Assert.Equal(110U, monsterTypes.Definitions[111].NativeMonsterId);
        Assert.Equal(1666, monsterTypes.Definitions[111].MaximumHitPoints);
        Assert.Equal("Necromorb", monsterTypes.Definitions[120].Name);
        Assert.Equal(119U, monsterTypes.Definitions[120].NativeMonsterId);
        Assert.Equal((100, 75), (
            monsterTypes.Definitions[120].FireResistance,
            monsterTypes.Definitions[120].LightningResistance));
        Assert.Equal((10, 10), (
            monsterTypes.Definitions[121].MinimumSpecialDamage,
            monsterTypes.Definitions[121].MaximumSpecialDamage));
        Assert.Equal((50, 60), (
            monsterTypes.Definitions[125].MinimumSpecialDamage,
            monsterTypes.Definitions[125].MaximumSpecialDamage));
        Assert.True(monsterTypes.Definitions[129].Undead);
        Assert.Equal("Hellbat", monsterTypes.Definitions[132].Name);
        Assert.Equal(131U, monsterTypes.Definitions[132].NativeMonsterId);
        Assert.Equal("Bone Demon", monsterTypes.Definitions[133].Name);
        Assert.True(monsterTypes.Definitions[133].Undead);
        Assert.Equal((50, 50), (
            monsterTypes.Definitions[133].MinimumSpecialDamage,
            monsterTypes.Definitions[133].MaximumSpecialDamage));
        var naKrul = monsterTypes.Definitions.Single(definition => definition.Name == "Na-Krul");
        Assert.Equal(137U, naKrul.NativeMonsterId);
        Assert.Equal(125, naKrul.ArmorClass);
        Assert.Equal((40, 50), (
            naKrul.MinimumSpecialDamage,
            naKrul.MaximumSpecialDamage));
        var questTypes = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            composed.GetRequiredTable("quest_types.tsv").ToCanonicalTsv());
        Assert.Equal(24, questTypes.Definitions.Count);
        Assert.Equal("The Magic Rock", questTypes.Definitions[0].Name);
        Assert.Equal("The Jersey's Jersey", questTypes.Definitions[^1].Name);
        Assert.Equal(23U, questTypes.Definitions[^1].NativeQuestId);
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            composed.GetRequiredTable("quests.tsv").ToCanonicalTsv(),
            questTypes,
            stableIds);
        var anvilQuest = Assert.Single(quests, quest => quest.QuestId == 3011);
        Assert.Equal((88, 90, 0, 0U, 62, 63, 2138U, 1, 1010U, 1, 61, 64), (
            anvilQuest.MessageId,
            anvilQuest.TurnInMessageId,
            anvilQuest.TurnInTownerId,
            anvilQuest.TurnInLevelId,
            anvilQuest.TurnInPositionX,
            anvilQuest.TurnInPositionY,
            anvilQuest.TurnInItemId,
            anvilQuest.TurnInRequiredVariable2,
            anvilQuest.TurnInRewardUniqueId,
            anvilQuest.TurnInRewardItemLevel,
            anvilQuest.TurnInRewardPositionX,
            anvilQuest.TurnInRewardPositionY));
        var magicRockQuest = Assert.Single(quests, quest => quest.QuestId == 3001);
        Assert.Equal((115, 117, 0, 0U, 62, 63, 2135U, 1, 1004U, 5, 61, 64), (
            magicRockQuest.MessageId,
            magicRockQuest.TurnInMessageId,
            magicRockQuest.TurnInTownerId,
            magicRockQuest.TurnInLevelId,
            magicRockQuest.TurnInPositionX,
            magicRockQuest.TurnInPositionY,
            magicRockQuest.TurnInItemId,
            magicRockQuest.TurnInRequiredVariable2,
            magicRockQuest.TurnInRewardUniqueId,
            magicRockQuest.TurnInRewardItemLevel,
            magicRockQuest.TurnInRewardPositionX,
            magicRockQuest.TurnInRewardPositionY));
        var girlQuest = Assert.Single(quests, quest => quest.QuestId == 3019);
        Assert.Equal((283, 285, 11, 0U, 77, 43, 2102U, 171U, 13, 77, 43), (
            girlQuest.MessageId,
            girlQuest.TurnInMessageId,
            girlQuest.TurnInTownerId,
            girlQuest.TurnInLevelId,
            girlQuest.TurnInPositionX,
            girlQuest.TurnInPositionY,
            girlQuest.TurnInItemId,
            girlQuest.TurnInRewardItemId,
            girlQuest.TurnInRewardItemLevel,
            girlQuest.TurnInRewardPositionX,
            girlQuest.TurnInRewardPositionY));
        var hellfireCursors = AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            composed.GetRequiredTable("item_cursor_graphics.tsv").ToCanonicalTsv());
        var hellfireItems = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            composed.GetRequiredTable("items.tsv").ToCanonicalTsv(),
            cursorCatalog: hellfireCursors);
        var hellfireUniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            composed.GetRequiredTable("unique_items.tsv").ToCanonicalTsv(),
            hellfireCursors);
        hellfireItems.AttachUniqueCatalog(hellfireUniques);
        Assert.Equal(2, hellfireItems.GetRequired(1).MinimumMonsterLevel);
        Assert.Equal(0, hellfireItems.GetRequired(1).DropRate);
        Assert.False(hellfireItems.GetRequired(1).Usable);
        var runeExpectations = new[] {
            ("rune_of_fire", 2106U, 161, 193, 47, 12, "fireball", "Rune of Fire"),
            ("rune_of_lightning", 2107U, 162, 195, 48, 50, "lightning_wall", "Rune of Lightning"),
            ("greater_rune_of_fire", 2108U, 163, 194, 50, 45, "immolation", "Greater Rune of Fire"),
            ("greater_rune_of_lightning", 2109U, 164, 196, 49, 18, "nova", "Greater Rune of Lightning"),
            ("rune_of_stone", 2110U, 165, 197, 51, 8, "stone_curse", "Rune of Stone"),
        };
        foreach (var (symbol, itemId, nativeIndex, cursor, miscId, spellId, spellSymbol, name) in runeExpectations) {
            Assert.Equal(itemId, stableIds.Resolve("item", symbol));
            var rune = hellfireItems.GetRequired(itemId);
            Assert.Equal(symbol, rune.StableSymbol);
            Assert.Equal((uint)spellId, stableIds.Resolve("spell", spellSymbol));
            Assert.Equal(nativeIndex, rune.NativeItemIndex);
            Assert.Equal(cursor, rune.NativeCursorGraphic);
            Assert.Equal(miscId, rune.MiscId);
            Assert.Equal(spellId, rune.SpellId);
            Assert.Equal(DiabloItemSpellSourceKind.ConsumableItem, rune.SpellSourceKind);
            Assert.Equal(name, rune.Name);
            Assert.Equal("Rune", rune.IdentifiedName);
            Assert.True(rune.Usable);
            Assert.Contains("Rune", rune.GenerationTags, StringComparer.OrdinalIgnoreCase);
            Assert.True(hellfireItems.CanDropAtMonsterLevel(itemId, rune.MinimumMonsterLevel));
        }
        var scrollExpectations = new[] {
            ("scroll_of_town_portal_fixed", 2111U, 27, 21, 39, 4, 200, 0),
            ("scroll_of_resurrect_fixed", 2112U, 34, 22, 32, 1, 250, 0),
            ("scroll_of_healing", 2113U, 91, 21, 2, 1, 50, 1),
            ("scroll_of_search", 2114U, 92, 21, 46, 1, 50, 1),
            ("scroll_of_lightning", 2115U, 93, 22, 3, 4, 150, 1),
            ("scroll_of_resurrect", 2116U, 95, 22, 32, 1, 250, 1),
            ("scroll_of_fire_wall", 2117U, 96, 22, 6, 4, 400, 1),
            ("scroll_of_inferno", 2118U, 97, 22, 21, 1, 100, 1),
            ("scroll_of_town_portal", 2119U, 98, 21, 39, 4, 200, 1),
            ("scroll_of_flash", 2120U, 99, 22, 4, 6, 500, 1),
            ("scroll_of_infravision", 2121U, 100, 21, 9, 8, 600, 1),
            ("scroll_of_phasing", 2122U, 101, 21, 10, 6, 200, 1),
            ("scroll_of_mana_shield", 2123U, 102, 21, 11, 8, 1200, 1),
            ("scroll_of_flame_wave", 2124U, 103, 22, 15, 10, 650, 1),
            ("scroll_of_fireball", 2125U, 104, 22, 12, 8, 300, 1),
            ("scroll_of_stone_curse", 2126U, 105, 22, 8, 6, 800, 1),
            ("scroll_of_chain_lightning", 2127U, 106, 22, 16, 10, 750, 1),
            ("scroll_of_guardian", 2128U, 107, 22, 7, 12, 950, 1),
            ("scroll_of_nova", 2129U, 109, 21, 18, 14, 1300, 1),
            ("scroll_of_golem", 2130U, 110, 22, 52, 10, 1100, 1),
            ("scroll_of_teleport", 2131U, 112, 21, 23, 14, 3000, 1),
            ("scroll_of_apocalypse", 2132U, 113, 21, 25, 22, 2000, 1),
        };
        foreach (var (symbol, itemId, nativeIndex, miscId, spellId, minimumLevel, value, dropRate) in scrollExpectations) {
            Assert.Equal(itemId, stableIds.Resolve("item", symbol));
            var scroll = hellfireItems.GetRequired(itemId);
            Assert.Equal(symbol, scroll.StableSymbol);
            Assert.Equal(nativeIndex, scroll.NativeItemIndex);
            Assert.Equal(1, scroll.NativeCursorGraphic);
            Assert.Equal(miscId, scroll.MiscId);
            Assert.Equal(spellId, scroll.SpellId);
            Assert.Equal(DiabloItemSpellSourceKind.ConsumableItem, scroll.SpellSourceKind);
            Assert.Equal(minimumLevel, scroll.MinimumMonsterLevel);
            Assert.Equal(dropRate, scroll.DropRate);
            Assert.Equal(value, scroll.Value);
            Assert.True(scroll.Usable);
            Assert.Contains("Scroll", scroll.GenerationTags, StringComparer.OrdinalIgnoreCase);
        }
        var identifyScrollExpectations = new[] {
            ("scroll_of_identify_fixed", 2133U, 26, 200, 0),
            ("scroll_of_identify", 2134U, 94, 100, 1),
        };
        foreach (var (symbol, itemId, nativeIndex, value, dropRate) in identifyScrollExpectations) {
            Assert.Equal(itemId, stableIds.Resolve("item", symbol));
            var scroll = hellfireItems.GetRequired(itemId);
            Assert.Equal(symbol, scroll.StableSymbol);
            Assert.Equal(nativeIndex, scroll.NativeItemIndex);
            Assert.Equal(1, scroll.NativeCursorGraphic);
            Assert.Equal(21, scroll.MiscId);
            Assert.Equal(0, scroll.SpellId);
            Assert.Equal(DiabloItemSpellSourceKind.None, scroll.SpellSourceKind);
            Assert.Equal(DiabloItemActionKind.Identify, scroll.ItemActionKind);
            Assert.Equal(1, scroll.MinimumMonsterLevel);
            Assert.Equal(dropRate, scroll.DropRate);
            Assert.Equal(value, scroll.Value);
            Assert.True(scroll.Usable);
        }
        var hellfireQuestItemExpectations = new[] {
            ("magic_rock", 2135U, 9, 76, 0, false, "Misc,Quest"),
            ("tavern_sign", 2136U, 12, 126, 0, false, "Misc,Quest"),
            ("golden_elixir", 2137U, 15, 17, 15, false, "Misc"),
            ("anvil_of_fury", 2138U, 16, 140, 0, false, "Misc,Quest"),
            ("black_mushroom", 2139U, 17, 89, 0, false, "Misc,Quest"),
            ("brain", 2140U, 18, 40, 0, false, "Misc,Quest"),
            ("fungal_tome", 2141U, 19, 97, 0, false, "Misc,Quest"),
            ("spectral_elixir", 2142U, 20, 15, 15, true, "Misc"),
            ("blood_stone", 2143U, 21, 25, 0, false, "Misc,Quest"),
        };
        foreach (var (symbol, itemId, nativeIndex, cursor, minimumLevel, usable, tags) in hellfireQuestItemExpectations) {
            Assert.Equal(itemId, stableIds.Resolve("item", symbol));
            var item = hellfireItems.GetRequired(itemId);
            Assert.Equal(symbol, item.StableSymbol);
            Assert.Equal(nativeIndex, item.NativeItemIndex);
            Assert.Equal(cursor, item.NativeCursorGraphic);
            Assert.Equal(minimumLevel, item.MinimumMonsterLevel);
            Assert.Equal(usable, item.Usable);
            Assert.Equal(tags.Split(','), item.GenerationTags.OrderBy(tag => tag));
            Assert.Equal(DiabloItemSpellSourceKind.None, item.SpellSourceKind);
            Assert.Equal(
                itemId == 2142
                    ? DiabloItemActionKind.IncreaseAllAttributes
                    : DiabloItemActionKind.None,
                item.ItemActionKind);
            Assert.Equal(itemId == 2142 ? 3 : 1, item.ItemActionAmount);
        }
        var hellfirePotionExpectations = new[] {
            ("potion_of_full_healing_fixed", 2144U, 29, 35, 2, 1, 0, true),
            ("potion_of_full_mana_fixed", 2145U, 30, 0, 7, 1, 0, true),
            ("potion_healing_hellfire", 2146U, 77, 32, 3, 1, 1, true),
            ("potion_of_full_healing", 2147U, 78, 35, 2, 1, 1, true),
            ("potion_mana_hellfire", 2148U, 79, 39, 6, 1, 1, true),
            ("potion_of_full_mana", 2149U, 80, 0, 7, 1, 1, true),
            ("potion_of_rejuvenation", 2150U, 81, 37, 19, 3, 1, true),
            ("potion_of_full_rejuvenation", 2151U, 82, 33, 20, 7, 1, true),
        };
        foreach (var (symbol, itemId, nativeIndex, cursor, miscId, minimumLevel, dropRate, usable) in hellfirePotionExpectations) {
            Assert.Equal(itemId, stableIds.Resolve("item", symbol));
            var potion = hellfireItems.GetRequired(itemId);
            Assert.Equal(nativeIndex, potion.NativeItemIndex);
            Assert.Equal(cursor, potion.NativeCursorGraphic);
            Assert.Equal(miscId, potion.MiscId);
            Assert.Equal(minimumLevel, potion.MinimumMonsterLevel);
            Assert.Equal(dropRate, potion.DropRate);
            Assert.Equal(usable, potion.Usable);
            Assert.Equal(DiabloItemSpellSourceKind.None, potion.SpellSourceKind);
            Assert.Equal(
                miscId switch {
                    3 => DiabloItemActionKind.RestorePartialLife,
                    2 => DiabloItemActionKind.RestoreFullLife,
                    6 => DiabloItemActionKind.RestorePartialMana,
                    7 => DiabloItemActionKind.RestoreFullMana,
                    19 => DiabloItemActionKind.RestorePartialLifeAndMana,
                    20 => DiabloItemActionKind.RestoreFullLifeAndMana,
                    _ => DiabloItemActionKind.None,
                },
                potion.ItemActionKind);
        }
        var hellfireOilAndElixirExpectations = new[] {
            ("oil_of_blacksmith", 2152U, 83, 37, 30, 1, 100),
            ("oil_of_accuracy", 2153U, 84, 32, 30, 1, 500),
            ("oil_of_sharpness", 2154U, 85, 34, 30, 1, 500),
            ("oil_of_fortitude", 2155U, 86, 31, 30, 10, 0),
            ("elixir_of_strength", 2156U, 87, 11, 38, 15, 5000),
            ("elixir_of_magic", 2157U, 88, 12, 34, 15, 5000),
            ("elixir_of_dexterity", 2158U, 89, 13, 36, 15, 5000),
            ("elixir_of_vitality", 2159U, 90, 14, 31, 20, 5000),
        };
        foreach (var (symbol, itemId, nativeIndex, miscId, cursor, minimumLevel, value) in hellfireOilAndElixirExpectations) {
            Assert.Equal(itemId, stableIds.Resolve("item", symbol));
            var item = hellfireItems.GetRequired(itemId);
            Assert.Equal(nativeIndex, item.NativeItemIndex);
            Assert.Equal(miscId, item.MiscId);
            Assert.Equal(cursor, item.NativeCursorGraphic);
            Assert.Equal(minimumLevel, item.MinimumMonsterLevel);
            Assert.Equal(value, item.Value);
            Assert.Equal(1, item.DropRate);
            Assert.True(item.Usable);
            Assert.Equal(DiabloItemSpellSourceKind.None, item.SpellSourceKind);
            Assert.Equal(
                itemId switch {
                    2152 => DiabloItemActionKind.ApplyBlacksmithOil,
                    2153 => DiabloItemActionKind.ApplyAccuracyOil,
                    2154 => DiabloItemActionKind.ApplySharpnessOil,
                    2155 => DiabloItemActionKind.ApplyFortitudeOil,
                    2156 => DiabloItemActionKind.IncreaseStrength,
                    2157 => DiabloItemActionKind.IncreaseMagic,
                    2158 => DiabloItemActionKind.IncreaseDexterity,
                    2159 => DiabloItemActionKind.IncreaseVitality,
                    _ => DiabloItemActionKind.None,
                },
                item.ItemActionKind);
        }
        Assert.Equal(("the_undead_crown", "The Undead Crown", 82), (
            hellfireItems.GetRequired(111).StableSymbol,
            hellfireItems.GetRequired(111).Name,
            hellfireItems.GetRequired(111).NativeCursorGraphic));
        Assert.Equal(("hellfire_ring_base", "Empyrean Band", 12), (
            hellfireItems.GetRequired(113).StableSymbol,
            hellfireItems.GetRequired(113).Name,
            hellfireItems.GetRequired(113).NativeCursorGraphic));
        Assert.Equal(("optic_amulet", "Optic Amulet", 45), (
            hellfireItems.GetRequired(114).StableSymbol,
            hellfireItems.GetRequired(114).Name,
            hellfireItems.GetRequired(114).NativeCursorGraphic));
        Assert.Equal(("ring_of_truth", "Ring of Truth", 12), (
            hellfireItems.GetRequired(115).StableSymbol,
            hellfireItems.GetRequired(115).Name,
            hellfireItems.GetRequired(115).NativeCursorGraphic));
        Assert.Equal(("harlequin_crest", "Harlequin Crest", 81), (
            hellfireItems.GetRequired(116).StableSymbol,
            hellfireItems.GetRequired(116).Name,
            hellfireItems.GetRequired(116).NativeCursorGraphic));
        Assert.Equal(("veil_of_steel", "Veil of Steel", 85), (
            hellfireItems.GetRequired(117).StableSymbol,
            hellfireItems.GetRequired(117).Name,
            hellfireItems.GetRequired(117).NativeCursorGraphic));
        Assert.Equal(("arkaine_s_valor", "Arkaine's Valor", 157), (
            hellfireItems.GetRequired(118).StableSymbol,
            hellfireItems.GetRequired(118).Name,
            hellfireItems.GetRequired(118).NativeCursorGraphic));
        Assert.Equal(("griswold_s_edge", "Griswold's Edge", 61), (
            hellfireItems.GetRequired(119).StableSymbol,
            hellfireItems.GetRequired(119).Name,
            hellfireItems.GetRequired(119).NativeCursorGraphic));
        Assert.Equal(("bovine_plate", "Bovine Plate", 226), (
            hellfireItems.GetRequired(120).StableSymbol,
            hellfireItems.GetRequired(120).Name,
            hellfireItems.GetRequired(120).NativeCursorGraphic));
        Assert.Equal(("butcher_s_cleaver", "The Butcher's Cleaver", 106), (
            hellfireItems.GetRequired(121).StableSymbol,
            hellfireItems.GetRequired(121).Name,
            hellfireItems.GetRequired(121).NativeCursorGraphic));
        Assert.Equal(("hunters_bow", "Hunter's Bow", 102), (
            hellfireItems.GetRequired(122).StableSymbol,
            hellfireItems.GetRequired(122).Name,
            hellfireItems.GetRequired(122).NativeCursorGraphic));
        Assert.Equal(("long_bow", "Long Bow", 102), (
            hellfireItems.GetRequired(123).StableSymbol,
            hellfireItems.GetRequired(123).Name,
            hellfireItems.GetRequired(123).NativeCursorGraphic));
        Assert.Equal(("short_bow", "Short Bow", 118), (
            hellfireItems.GetRequired(124).StableSymbol,
            hellfireItems.GetRequired(124).Name,
            hellfireItems.GetRequired(124).NativeCursorGraphic));
        Assert.Equal(("long_war_bow", "Long War Bow", 120), (
            hellfireItems.GetRequired(125).StableSymbol,
            hellfireItems.GetRequired(125).Name,
            hellfireItems.GetRequired(125).NativeCursorGraphic));
        Assert.Equal(("short_battle_bow", "Short Battle Bow", 167), (
            hellfireItems.GetRequired(126).StableSymbol,
            hellfireItems.GetRequired(126).Name,
            hellfireItems.GetRequired(126).NativeCursorGraphic));
        Assert.Equal(("dagger", "Dagger", 51), (
            hellfireItems.GetRequired(127).StableSymbol,
            hellfireItems.GetRequired(127).Name,
            hellfireItems.GetRequired(127).NativeCursorGraphic));
        Assert.Equal(("sabre", "Sabre", 67), (
            hellfireItems.GetRequired(128).StableSymbol,
            hellfireItems.GetRequired(128).Name,
            hellfireItems.GetRequired(128).NativeCursorGraphic));
        Assert.Equal(("falchion", "Falchion", 62), (
            hellfireItems.GetRequired(129).StableSymbol,
            hellfireItems.GetRequired(129).Name,
            hellfireItems.GetRequired(129).NativeCursorGraphic));
        Assert.Equal(("broad_sword", "Broad Sword", 61), (
            hellfireItems.GetRequired(130).StableSymbol,
            hellfireItems.GetRequired(130).Name,
            hellfireItems.GetRequired(130).NativeCursorGraphic));
        Assert.Equal(("long_sword", "Long Sword", 60), (
            hellfireItems.GetRequired(131).StableSymbol,
            hellfireItems.GetRequired(131).Name,
            hellfireItems.GetRequired(131).NativeCursorGraphic));
        Assert.Equal(("claymore", "Claymore", 65), (
            hellfireItems.GetRequired(132).StableSymbol,
            hellfireItems.GetRequired(132).Name,
            hellfireItems.GetRequired(132).NativeCursorGraphic));
        Assert.Equal(("hellfire_broad_sword", "Broad Sword", 61), (
            hellfireItems.GetRequired(133).StableSymbol,
            hellfireItems.GetRequired(133).Name,
            hellfireItems.GetRequired(133).NativeCursorGraphic));
        Assert.Equal(("scimitar", "Scimitar", 72), (
            hellfireItems.GetRequired(134).StableSymbol,
            hellfireItems.GetRequired(134).Name,
            hellfireItems.GetRequired(134).NativeCursorGraphic));
        Assert.Equal(("bastard_sword", "Bastard Sword", 57), (
            hellfireItems.GetRequired(135).StableSymbol,
            hellfireItems.GetRequired(135).Name,
            hellfireItems.GetRequired(135).NativeCursorGraphic));
        Assert.Equal(("two_handed_sword", "Two-Handed Sword", 110), (
            hellfireItems.GetRequired(136).StableSymbol,
            hellfireItems.GetRequired(136).Name,
            hellfireItems.GetRequired(136).NativeCursorGraphic));
        foreach (var itemId in new uint[] { 137, 138, 139, 140, 141 }) {
            var axe = hellfireItems.GetRequired(itemId);
            Assert.Contains("Axe", axe.GenerationTags);
            Assert.DoesNotContain("Sword", axe.GenerationTags);
        }
        Assert.Equal(("mace", "Mace", 59), (
            hellfireItems.GetRequired(142).StableSymbol,
            hellfireItems.GetRequired(142).Name,
            hellfireItems.GetRequired(142).NativeCursorGraphic));
        Assert.Equal(("maul", "Maul", 122), (
            hellfireItems.GetRequired(143).StableSymbol,
            hellfireItems.GetRequired(143).Name,
            hellfireItems.GetRequired(143).NativeCursorGraphic));
        Assert.Equal(("flail", "Flail", 131), (
            hellfireItems.GetRequired(144).StableSymbol,
            hellfireItems.GetRequired(144).Name,
            hellfireItems.GetRequired(144).NativeCursorGraphic));
        Assert.Equal(("morning_star", "Morning Star", 63), (
            hellfireItems.GetRequired(145).StableSymbol,
            hellfireItems.GetRequired(145).Name,
            hellfireItems.GetRequired(145).NativeCursorGraphic));
        Assert.Equal(("war_hammer", "War Hammer", 121), (
            hellfireItems.GetRequired(146).StableSymbol,
            hellfireItems.GetRequired(146).Name,
            hellfireItems.GetRequired(146).NativeCursorGraphic));
        foreach (var itemId in new uint[] { 142, 143, 144, 145, 146 }) {
            var mace = hellfireItems.GetRequired(itemId);
            Assert.Contains("Mace", mace.GenerationTags);
            Assert.DoesNotContain("Sword", mace.GenerationTags);
        }
        Assert.Equal(("long_staff", "Long Staff", 123), (
            hellfireItems.GetRequired(147).StableSymbol,
            hellfireItems.GetRequired(147).Name,
            hellfireItems.GetRequired(147).NativeCursorGraphic));
        Assert.Equal(("war_staff", "War Staff", 124), (
            hellfireItems.GetRequired(148).StableSymbol,
            hellfireItems.GetRequired(148).Name,
            hellfireItems.GetRequired(148).NativeCursorGraphic));
        Assert.Equal(("short_staff", "Short Staff", 109), (
            hellfireItems.GetRequired(149).StableSymbol,
            hellfireItems.GetRequired(149).Name,
            hellfireItems.GetRequired(149).NativeCursorGraphic));
        Assert.Equal(("composite_staff", "Composite Staff", 166), (
            hellfireItems.GetRequired(150).StableSymbol,
            hellfireItems.GetRequired(150).Name,
            hellfireItems.GetRequired(150).NativeCursorGraphic));
        Assert.Equal(("quarter_staff", "Quarter Staff", 109), (
            hellfireItems.GetRequired(151).StableSymbol,
            hellfireItems.GetRequired(151).Name,
            hellfireItems.GetRequired(151).NativeCursorGraphic));
        foreach (var itemId in new uint[] { 147, 148, 149, 150, 151 })
            Assert.Contains("Staff", hellfireItems.GetRequired(itemId).GenerationTags);
        Assert.Equal(("helm", "Helm", 82), (
            hellfireItems.GetRequired(152).StableSymbol,
            hellfireItems.GetRequired(152).Name,
            hellfireItems.GetRequired(152).NativeCursorGraphic));
        Assert.Equal(("skull_cap", "Skull Cap", 90), (
            hellfireItems.GetRequired(153).StableSymbol,
            hellfireItems.GetRequired(153).Name,
            hellfireItems.GetRequired(153).NativeCursorGraphic));
        Assert.Equal(("great_helm", "Great Helm", 98), (
            hellfireItems.GetRequired(154).StableSymbol,
            hellfireItems.GetRequired(154).Name,
            hellfireItems.GetRequired(154).NativeCursorGraphic));
        Assert.Equal(("crown", "Crown", 95), (
            hellfireItems.GetRequired(155).StableSymbol,
            hellfireItems.GetRequired(155).Name,
            hellfireItems.GetRequired(155).NativeCursorGraphic));
        Assert.Equal(("rags", "Rags", 128), (
            hellfireItems.GetRequired(156).StableSymbol,
            hellfireItems.GetRequired(156).Name,
            hellfireItems.GetRequired(156).NativeCursorGraphic));
        foreach (var itemId in new uint[] { 152, 153, 154, 155 }) {
            var helm = hellfireItems.GetRequired(itemId);
            Assert.Equal(4, helm.EquipLocation);
            Assert.Contains("Helmet", helm.GenerationTags);
        }
        var rags = hellfireItems.GetRequired(156);
        Assert.Equal(3, rags.EquipLocation);
        Assert.Contains("LightArmor", rags.GenerationTags);
        Assert.Contains("BodyArmor", rags.GenerationTags);
        Assert.Equal(("studded_leather_armor", "Studded Leather Armor", 107), (
            hellfireItems.GetRequired(157).StableSymbol,
            hellfireItems.GetRequired(157).Name,
            hellfireItems.GetRequired(157).NativeCursorGraphic));
        Assert.Equal(("cloak", "Cloak", 149), (
            hellfireItems.GetRequired(158).StableSymbol,
            hellfireItems.GetRequired(158).Name,
            hellfireItems.GetRequired(158).NativeCursorGraphic));
        Assert.Equal(("leather_armor", "Leather Armor", 135), (
            hellfireItems.GetRequired(159).StableSymbol,
            hellfireItems.GetRequired(159).Name,
            hellfireItems.GetRequired(159).NativeCursorGraphic));
        Assert.Equal(("robe", "Robe", 137), (
            hellfireItems.GetRequired(160).StableSymbol,
            hellfireItems.GetRequired(160).Name,
            hellfireItems.GetRequired(160).NativeCursorGraphic));
        Assert.Equal(("chain_mail", "Chain Mail", 111), (
            hellfireItems.GetRequired(161).StableSymbol,
            hellfireItems.GetRequired(161).Name,
            hellfireItems.GetRequired(161).NativeCursorGraphic));
        foreach (var itemId in new uint[] { 157, 158, 159, 160 }) {
            var lightArmor = hellfireItems.GetRequired(itemId);
            Assert.Equal(3, lightArmor.EquipLocation);
            Assert.Contains("LightArmor", lightArmor.GenerationTags);
            Assert.Contains("BodyArmor", lightArmor.GenerationTags);
            Assert.DoesNotContain("Helmet", lightArmor.GenerationTags);
        }
        var chainMail = hellfireItems.GetRequired(161);
        Assert.Equal(3, chainMail.EquipLocation);
        Assert.Contains("MediumArmor", chainMail.GenerationTags);
        Assert.Contains("BodyArmor", chainMail.GenerationTags);
        Assert.DoesNotContain("Helmet", chainMail.GenerationTags);
        Assert.Equal(("breast_plate", "Breast Plate", 153), (
            hellfireItems.GetRequired(162).StableSymbol,
            hellfireItems.GetRequired(162).Name,
            hellfireItems.GetRequired(162).NativeCursorGraphic));
        Assert.Equal(("cape", "Cape", 150), (
            hellfireItems.GetRequired(163).StableSymbol,
            hellfireItems.GetRequired(163).Name,
            hellfireItems.GetRequired(163).NativeCursorGraphic));
        Assert.Equal(("plate_mail", "Plate Mail", 103), (
            hellfireItems.GetRequired(164).StableSymbol,
            hellfireItems.GetRequired(164).Name,
            hellfireItems.GetRequired(164).NativeCursorGraphic));
        Assert.Equal(("full_plate_mail", "Full Plate Mail", 151), (
            hellfireItems.GetRequired(165).StableSymbol,
            hellfireItems.GetRequired(165).Name,
            hellfireItems.GetRequired(165).NativeCursorGraphic));
        Assert.Equal(("hellfire_buckler", "Buckler", 83), (
            hellfireItems.GetRequired(166).StableSymbol,
            hellfireItems.GetRequired(166).Name,
            hellfireItems.GetRequired(166).NativeCursorGraphic));
        foreach (var itemId in new uint[] { 162, 164, 165 }) {
            var heavyArmor = hellfireItems.GetRequired(itemId);
            Assert.Equal(3, heavyArmor.EquipLocation);
            Assert.Contains("HeavyArmor", heavyArmor.GenerationTags);
            Assert.Contains("BodyArmor", heavyArmor.GenerationTags);
            Assert.DoesNotContain("Helmet", heavyArmor.GenerationTags);
        }
        var cape = hellfireItems.GetRequired(163);
        Assert.Equal(3, cape.EquipLocation);
        Assert.Contains("LightArmor", cape.GenerationTags);
        Assert.Contains("BodyArmor", cape.GenerationTags);
        var hellfireBuckler = hellfireItems.GetRequired(166);
        Assert.Equal(1, hellfireBuckler.EquipLocation);
        Assert.Contains("Shield", hellfireBuckler.GenerationTags);
        Assert.Contains("OneHanded", hellfireBuckler.GenerationTags);
        Assert.DoesNotContain("Helmet", hellfireBuckler.GenerationTags);
        Assert.Equal(("kite_shield", "Kite Shield", 113), (
            hellfireItems.GetRequired(167).StableSymbol,
            hellfireItems.GetRequired(167).Name,
            hellfireItems.GetRequired(167).NativeCursorGraphic));
        Assert.Equal(("small_shield", "Small Shield", 105), (
            hellfireItems.GetRequired(168).StableSymbol,
            hellfireItems.GetRequired(168).Name,
            hellfireItems.GetRequired(168).NativeCursorGraphic));
        Assert.Equal(("large_shield", "Large Shield", 147), (
            hellfireItems.GetRequired(169).StableSymbol,
            hellfireItems.GetRequired(169).Name,
            hellfireItems.GetRequired(169).NativeCursorGraphic));
        Assert.Equal(("gothic_shield", "Gothic Shield", 148), (
            hellfireItems.GetRequired(170).StableSymbol,
            hellfireItems.GetRequired(170).Name,
            hellfireItems.GetRequired(170).NativeCursorGraphic));
        Assert.Equal(("amulet", "Amulet", 45), (
            hellfireItems.GetRequired(171).StableSymbol,
            hellfireItems.GetRequired(171).Name,
            hellfireItems.GetRequired(171).NativeCursorGraphic));
        foreach (var itemId in new uint[] { 167, 168, 169, 170 }) {
            var shield = hellfireItems.GetRequired(itemId);
            Assert.Equal(1, shield.EquipLocation);
            Assert.Contains("Shield", shield.GenerationTags);
            Assert.Contains("OneHanded", shield.GenerationTags);
            Assert.DoesNotContain("Helmet", shield.GenerationTags);
        }
        var amulet = hellfireItems.GetRequired(171);
        Assert.Equal(6, amulet.EquipLocation);
        Assert.Contains("Amulet", amulet.GenerationTags);
        var crown = hellfireItems.GenerateUnique(1003, 0x12345678, 1);
        Assert.Equal(7, crown.ItemIndex);
        Assert.Equal("The Undead Crown", crown.BaseName);
        Assert.Equal("The Undead Crown", crown.IdentifiedName);
        Assert.Equal(77, crown.NativeCursorGraphic);
        Assert.Equal(16650, crown.Value);
        Assert.Equal(8, crown.ArmorClass);
        Assert.Equal(0x00000002U, crown.Flags);
        var band = hellfireItems.GenerateUnique(1004, 0x12345678, 1);
        Assert.Equal(1004, band.UniqueId);
        Assert.Equal(8000, band.Value);
        Assert.Equal(2, band.PlusStrength);
        Assert.Equal(2, band.PlusMagic);
        Assert.Equal(2, band.PlusDexterity);
        Assert.Equal(2, band.PlusVitality);
        Assert.Equal(2, band.PlusLight);
        Assert.Equal(0x00200000U | 0x10000000U, band.Flags);
        var opticAmulet = hellfireItems.GenerateUnique(1005, 0x12345678, 1);
        Assert.Equal(1005, opticAmulet.UniqueId);
        Assert.Equal(9750, opticAmulet.Value);
        Assert.Equal(2, opticAmulet.PlusLight);
        Assert.Equal(20, opticAmulet.PlusLightningResistance);
        Assert.Equal(-1, opticAmulet.PlusGetHit);
        Assert.Equal(5, opticAmulet.PlusMagic);
        var ringOfTruth = hellfireItems.GenerateUnique(1006, 0x12345678, 1);
        Assert.Equal(1006, ringOfTruth.UniqueId);
        Assert.Equal(9100, ringOfTruth.Value);
        Assert.Equal(10 << 6, ringOfTruth.PlusHitPoints);
        Assert.Equal(-1, ringOfTruth.PlusGetHit);
        Assert.Equal(10, ringOfTruth.PlusMagicResistance);
        var harlequinCrest = hellfireItems.GenerateUnique(1007, 0x12345678, 1);
        Assert.Equal(1007, harlequinCrest.UniqueId);
        Assert.Equal(4000, harlequinCrest.Value);
        Assert.Equal(-3, harlequinCrest.PlusArmorClass);
        Assert.Equal(-1, harlequinCrest.PlusGetHit);
        Assert.Equal(2, harlequinCrest.PlusStrength);
        Assert.Equal(448, harlequinCrest.PlusHitPoints);
        Assert.Equal(448, harlequinCrest.PlusMana);
        var veilOfSteel = hellfireItems.GenerateUnique(1008, 0x12345678, 1);
        Assert.Equal(1008, veilOfSteel.UniqueId);
        Assert.Equal(63800, veilOfSteel.Value);
        Assert.Equal(50, veilOfSteel.PlusFireResistance);
        Assert.Equal(50, veilOfSteel.PlusLightningResistance);
        Assert.Equal(50, veilOfSteel.PlusMagicResistance);
        Assert.Equal(60, veilOfSteel.PlusArmorClass);
        Assert.Equal(-(30 << 6), veilOfSteel.PlusMana);
        Assert.Equal(15, veilOfSteel.PlusStrength);
        Assert.Equal(15, veilOfSteel.PlusVitality);
        var arkainesValor = hellfireItems.GenerateUnique(1009, 0x12345678, 1);
        Assert.Equal(1009, arkainesValor.UniqueId);
        Assert.Equal(42000, arkainesValor.Value);
        Assert.Equal(25, arkainesValor.ArmorClass);
        Assert.Equal(10, arkainesValor.PlusVitality);
        Assert.Equal(-3, arkainesValor.PlusGetHit);
        Assert.NotEqual(0U, arkainesValor.Flags & 0x00800000U);
        var griswoldsEdge = hellfireItems.GenerateUnique(1010, 0x12345678, 1);
        Assert.Equal(1010, griswoldsEdge.UniqueId);
        Assert.Equal(42000, griswoldsEdge.Value);
        Assert.Equal(4, griswoldsEdge.MinDamage);
        Assert.Equal(12, griswoldsEdge.MaxDamage);
        Assert.Equal(1, griswoldsEdge.FireMinDamage);
        Assert.Equal(10, griswoldsEdge.FireMaxDamage);
        Assert.Equal(25, griswoldsEdge.PlusToHit);
        Assert.NotEqual(0U, griswoldsEdge.Flags & 0x00040000U);
        Assert.NotEqual(0U, griswoldsEdge.Flags & 0x00000800U);
        Assert.Equal(20 << 6, griswoldsEdge.PlusMana);
        Assert.Equal(-(20 << 6), griswoldsEdge.PlusHitPoints);
        var bovinePlate = hellfireItems.GenerateUnique(1011, 0x12345678, 1);
        Assert.Equal(1011, bovinePlate.UniqueId);
        Assert.Equal(400, bovinePlate.Value);
        Assert.Equal(150, bovinePlate.ArmorClass);
        Assert.Equal(255, bovinePlate.Durability);
        Assert.Equal(255, bovinePlate.MaxDurability);
        Assert.Equal(50, bovinePlate.MinimumStrength);
        Assert.Equal(30, bovinePlate.PlusFireResistance);
        Assert.Equal(30, bovinePlate.PlusLightningResistance);
        Assert.Equal(30, bovinePlate.PlusMagicResistance);
        Assert.Equal(5, bovinePlate.PlusLight);
        Assert.Equal(-(50 << 6), bovinePlate.PlusMana);
        Assert.Equal(-2, bovinePlate.SpellLevelAdd);
        var riftBow = hellfireItems.GenerateUnique(1012, 0x12345678, 1);
        Assert.Equal(1012, riftBow.UniqueId);
        Assert.Equal(1800, riftBow.Value);
        Assert.Equal(4, riftBow.ItemIndex);
        Assert.Equal(2, riftBow.PlusDamageModifier);
        Assert.Equal(-3, riftBow.PlusDexterity);
        Assert.NotEqual(0U, riftBow.Flags & 0x00000004U);
        var butchersCleaver = hellfireItems.GenerateUnique(1013, 0x12345678, 1);
        Assert.Equal(1013, butchersCleaver.UniqueId);
        Assert.Equal(3650, butchersCleaver.Value);
        Assert.Equal(6, butchersCleaver.ItemIndex);
        Assert.Equal(4, butchersCleaver.MinDamage);
        Assert.Equal(24, butchersCleaver.MaxDamage);
        Assert.Equal(10, butchersCleaver.PlusStrength);
        Assert.Equal(10, butchersCleaver.Durability);
        Assert.Equal(10, butchersCleaver.MaxDurability);
        var needler = hellfireItems.GenerateUnique(1014, 0x12345678, 2);
        Assert.Equal(1014, needler.UniqueId);
        Assert.Equal(8900, needler.Value);
        Assert.Equal(4, needler.ItemIndex);
        Assert.Equal(1, needler.MinDamage);
        Assert.Equal(3, needler.MaxDamage);
        Assert.Equal(50, needler.PlusToHit);
        Assert.NotEqual(0U, needler.Flags & 0x00040000U);
        var celestialBow = hellfireItems.GenerateUnique(1015, 0x12345678, 2);
        Assert.Equal(1015, celestialBow.UniqueId);
        Assert.Equal(1200, celestialBow.Value);
        Assert.Equal(146, celestialBow.ItemIndex);
        Assert.Equal(1, celestialBow.MinDamage);
        Assert.Equal(6, celestialBow.MaxDamage);
        Assert.Equal(0, celestialBow.MinimumStrength);
        Assert.Equal(2, celestialBow.PlusDamageModifier);
        Assert.Equal(5, celestialBow.ArmorClass);
        var deadlyHunter = hellfireItems.GenerateUnique(1016, 0x12345678, 3);
        Assert.Equal(1016, deadlyHunter.UniqueId);
        Assert.Equal(8750, deadlyHunter.Value);
        Assert.Equal(147, deadlyHunter.ItemIndex);
        Assert.Equal(3, deadlyHunter.MinDamage);
        Assert.Equal(6, deadlyHunter.MaxDamage);
        Assert.Equal(20, deadlyHunter.PlusToHit);
        Assert.Equal(-5, deadlyHunter.PlusMagic);
        Assert.NotEqual(0U, deadlyHunter.Flags & 0x40000000U);
        var bowOfTheDead = hellfireItems.GenerateUnique(1017, 0x12345678, 5);
        Assert.Equal(1017, bowOfTheDead.UniqueId);
        Assert.Equal(2500, bowOfTheDead.Value);
        Assert.Equal(147, bowOfTheDead.ItemIndex);
        Assert.Equal(10, bowOfTheDead.PlusToHit);
        Assert.Equal(4, bowOfTheDead.PlusDexterity);
        Assert.Equal(-3, bowOfTheDead.PlusVitality);
        Assert.Equal(-2, bowOfTheDead.PlusLight);
        Assert.Equal(30, bowOfTheDead.Durability);
        Assert.Equal(30, bowOfTheDead.MaxDurability);
        var blackoakBow = hellfireItems.GenerateUnique(1018, 0x12345678, 5);
        Assert.Equal(1018, blackoakBow.UniqueId);
        Assert.Equal(2500, blackoakBow.Value);
        Assert.Equal(146, blackoakBow.ItemIndex);
        Assert.Equal(10, blackoakBow.PlusDexterity);
        Assert.Equal(-10, blackoakBow.PlusVitality);
        Assert.Equal(50, blackoakBow.PlusDamage);
        Assert.Equal(-1, blackoakBow.PlusLight);
        var flamedart = hellfireItems.GenerateUnique(1019, 0x12345678, 10);
        Assert.Equal(1019, flamedart.UniqueId);
        Assert.Equal(14250, flamedart.Value);
        Assert.Equal(145, flamedart.ItemIndex);
        Assert.Equal(2, flamedart.MinDamage);
        Assert.Equal(5, flamedart.MaxDamage);
        Assert.Equal(20, flamedart.PlusToHit);
        Assert.Equal(40, flamedart.PlusFireResistance);
        Assert.Equal(1, flamedart.FireMinDamage);
        Assert.Equal(6, flamedart.FireMaxDamage);
        Assert.NotEqual(0U, flamedart.Flags & 0x00000010U);
        var fleshstinger = hellfireItems.GenerateUnique(1020, 0x12345678, 13);
        Assert.Equal(1020, fleshstinger.UniqueId);
        Assert.Equal(16500, fleshstinger.Value);
        Assert.Equal(146, fleshstinger.ItemIndex);
        Assert.Equal(15, fleshstinger.PlusDexterity);
        Assert.Equal(40, fleshstinger.PlusToHit);
        Assert.Equal(80, fleshstinger.PlusDamage);
        Assert.Equal(37, fleshstinger.Durability);
        Assert.Equal(37, fleshstinger.MaxDurability);
        var windforce = hellfireItems.GenerateUnique(1021, 0x12345678, 17);
        Assert.Equal(1021, windforce.UniqueId);
        Assert.Equal(37750, windforce.Value);
        Assert.Equal(151, windforce.ItemIndex);
        Assert.Equal(1, windforce.MinDamage);
        Assert.Equal(14, windforce.MaxDamage);
        Assert.Equal(5, windforce.PlusStrength);
        Assert.Equal(200, windforce.PlusDamage);
        Assert.NotEqual(0U, windforce.Flags & 0x00000800U);
        var eaglehorn = hellfireItems.GenerateUnique(1022, 0x12345678, 26);
        Assert.Equal(1022, eaglehorn.UniqueId);
        Assert.Equal(42500, eaglehorn.Value);
        Assert.Equal(149, eaglehorn.ItemIndex);
        Assert.Equal(1, eaglehorn.MinDamage);
        Assert.Equal(10, eaglehorn.MaxDamage);
        Assert.Equal(20, eaglehorn.PlusDexterity);
        Assert.Equal(50, eaglehorn.PlusToHit);
        Assert.Equal(100, eaglehorn.PlusDamage);
        Assert.Equal(255, eaglehorn.Durability);
        Assert.Equal(255, eaglehorn.MaxDurability);
        var gonnagalsDirk = hellfireItems.GenerateUnique(1023, 0x12345678, 1);
        Assert.Equal(1023, gonnagalsDirk.UniqueId);
        Assert.Equal(7040, gonnagalsDirk.Value);
        Assert.Equal(119, gonnagalsDirk.ItemIndex);
        Assert.Equal(1, gonnagalsDirk.MinDamage);
        Assert.Equal(4, gonnagalsDirk.MaxDamage);
        Assert.Equal(-5, gonnagalsDirk.PlusDexterity);
        Assert.Equal(4, gonnagalsDirk.PlusDamageModifier);
        Assert.Equal(25, gonnagalsDirk.PlusFireResistance);
        Assert.NotEqual(0U, gonnagalsDirk.Flags & 0x00040000U);
        var defender = hellfireItems.GenerateUnique(1024, 0x12345678, 1);
        Assert.Equal(1024, defender.UniqueId);
        Assert.Equal(2000, defender.Value);
        Assert.Equal(125, defender.ItemIndex);
        Assert.Equal(1, defender.MinDamage);
        Assert.Equal(8, defender.MaxDamage);
        Assert.Equal(5, defender.ArmorClass);
        Assert.Equal(17, defender.MinimumStrength);
        Assert.Equal(5, defender.PlusVitality);
        Assert.Equal(-5, defender.PlusToHit);
        var gryphonsClaw = hellfireItems.GenerateUnique(1025, 0x12345678, 1);
        Assert.Equal(1025, gryphonsClaw.UniqueId);
        Assert.Equal(1000, gryphonsClaw.Value);
        Assert.Equal(121, gryphonsClaw.ItemIndex);
        Assert.Equal(4, gryphonsClaw.MinDamage);
        Assert.Equal(8, gryphonsClaw.MaxDamage);
        Assert.Equal(30, gryphonsClaw.MinimumStrength);
        Assert.Equal(100, gryphonsClaw.PlusDamage);
        Assert.Equal(-2, gryphonsClaw.PlusMagic);
        Assert.Equal(-5, gryphonsClaw.PlusDexterity);
        var blackRazor = hellfireItems.GenerateUnique(1026, 0x12345678, 1);
        Assert.Equal(1026, blackRazor.UniqueId);
        Assert.Equal(2000, blackRazor.Value);
        Assert.Equal(119, blackRazor.ItemIndex);
        Assert.Equal(1, blackRazor.MinDamage);
        Assert.Equal(4, blackRazor.MaxDamage);
        Assert.Equal(150, blackRazor.PlusDamage);
        Assert.Equal(2, blackRazor.PlusVitality);
        Assert.Equal(5, blackRazor.Durability);
        Assert.Equal(5, blackRazor.MaxDurability);
        var gibbousMoon = hellfireItems.GenerateUnique(1027, 0x12345678, 2);
        Assert.Equal(1027, gibbousMoon.UniqueId);
        Assert.Equal(6660, gibbousMoon.Value);
        Assert.Equal(127, gibbousMoon.ItemIndex);
        Assert.Equal(4, gibbousMoon.MinDamage);
        Assert.Equal(12, gibbousMoon.MaxDamage);
        Assert.Equal(40, gibbousMoon.MinimumStrength);
        Assert.Equal(2, gibbousMoon.PlusStrength);
        Assert.Equal(2, gibbousMoon.PlusMagic);
        Assert.Equal(2, gibbousMoon.PlusDexterity);
        Assert.Equal(2, gibbousMoon.PlusVitality);
        Assert.Equal(25, gibbousMoon.PlusDamage);
        Assert.Equal(15 << 6, gibbousMoon.PlusMana);
        Assert.Equal(-3, gibbousMoon.PlusLight);
        var iceShank = hellfireItems.GenerateUnique(1028, 0x12345678, 3);
        Assert.Equal(1028, iceShank.UniqueId);
        Assert.Equal(5250, iceShank.Value);
        Assert.Equal(126, iceShank.ItemIndex);
        Assert.Equal(2, iceShank.MinDamage);
        Assert.Equal(10, iceShank.MaxDamage);
        Assert.Equal(30, iceShank.MinimumStrength);
        Assert.Equal(30, iceShank.MinimumDexterity);
        Assert.Equal(40, iceShank.PlusFireResistance);
        Assert.Equal(15, iceShank.Durability);
        Assert.Equal(15, iceShank.MaxDurability);
        Assert.InRange(iceShank.PlusStrength, 5, 10);
        var executionersBlade = hellfireItems.GenerateUnique(1029, 0x12345678, 3);
        Assert.Equal(1029, executionersBlade.UniqueId);
        Assert.Equal(7080, executionersBlade.Value);
        Assert.Equal(121, executionersBlade.ItemIndex);
        Assert.Equal(4, executionersBlade.MinDamage);
        Assert.Equal(8, executionersBlade.MaxDamage);
        Assert.Equal(30, executionersBlade.MinimumStrength);
        Assert.Equal(150, executionersBlade.PlusDamage);
        Assert.Equal(-(10 << 6), executionersBlade.PlusHitPoints);
        Assert.Equal(-1, executionersBlade.PlusLight);
        Assert.Equal(60, executionersBlade.Durability);
        Assert.Equal(60, executionersBlade.MaxDurability);
        var bonesaw = hellfireItems.GenerateUnique(1030, 0x12345678, 6);
        Assert.Equal(1030, bonesaw.UniqueId);
        Assert.Equal(4400, bonesaw.Value);
        Assert.Equal(123, bonesaw.ItemIndex);
        Assert.Equal(1, bonesaw.MinDamage);
        Assert.Equal(12, bonesaw.MaxDamage);
        Assert.Equal(35, bonesaw.MinimumStrength);
        Assert.Equal(10, bonesaw.PlusDamageModifier);
        Assert.Equal(10, bonesaw.PlusStrength);
        Assert.Equal(-5, bonesaw.PlusMagic);
        Assert.Equal(-5, bonesaw.PlusDexterity);
        Assert.Equal(10 << 6, bonesaw.PlusHitPoints);
        Assert.Equal(-(10 << 6), bonesaw.PlusMana);
        var shadowhawk = hellfireItems.GenerateUnique(1031, 0x12345678, 8);
        Assert.Equal(1031, shadowhawk.UniqueId);
        Assert.Equal(13750, shadowhawk.Value);
        Assert.Equal(127, shadowhawk.ItemIndex);
        Assert.Equal(4, shadowhawk.MinDamage);
        Assert.Equal(12, shadowhawk.MaxDamage);
        Assert.Equal(40, shadowhawk.MinimumStrength);
        Assert.Equal(-2, shadowhawk.PlusLight);
        Assert.Equal(15, shadowhawk.PlusToHit);
        Assert.Equal(5, shadowhawk.PlusFireResistance);
        Assert.Equal(5, shadowhawk.PlusLightningResistance);
        Assert.Equal(5, shadowhawk.PlusMagicResistance);
        Assert.NotEqual(0U, shadowhawk.Flags & 0x00010000U);
        var wizardspike = hellfireItems.GenerateUnique(1032, 0x12345678, 11);
        Assert.Equal(1032, wizardspike.UniqueId);
        Assert.Equal(12920, wizardspike.Value);
        Assert.Equal(119, wizardspike.ItemIndex);
        Assert.Equal(1, wizardspike.MinDamage);
        Assert.Equal(4, wizardspike.MaxDamage);
        Assert.Equal(15, wizardspike.PlusMagic);
        Assert.Equal(35 << 6, wizardspike.PlusMana);
        Assert.Equal(25, wizardspike.PlusToHit);
        Assert.Equal(15, wizardspike.PlusFireResistance);
        Assert.Equal(15, wizardspike.PlusLightningResistance);
        Assert.Equal(15, wizardspike.PlusMagicResistance);
        var lightsabre = hellfireItems.GenerateUnique(1033, 0x12345678, 13);
        Assert.Equal(1033, lightsabre.UniqueId);
        Assert.Equal(19150, lightsabre.Value);
        Assert.Equal(125, lightsabre.ItemIndex);
        Assert.Equal(1, lightsabre.MinDamage);
        Assert.Equal(8, lightsabre.MaxDamage);
        Assert.Equal(17, lightsabre.MinimumStrength);
        Assert.Equal(2, lightsabre.PlusLight);
        Assert.Equal(1, lightsabre.LightningMinDamage);
        Assert.Equal(10, lightsabre.LightningMaxDamage);
        Assert.Equal(20, lightsabre.PlusToHit);
        Assert.Equal(50, lightsabre.PlusLightningResistance);
        var falconsTalon = hellfireItems.GenerateUnique(1034, 0x12345678, 15);
        Assert.Equal(1034, falconsTalon.UniqueId);
        Assert.Equal(7867, falconsTalon.Value);
        Assert.Equal(122, falconsTalon.ItemIndex);
        Assert.Equal(3, falconsTalon.MinDamage);
        Assert.Equal(7, falconsTalon.MaxDamage);
        Assert.Equal(23, falconsTalon.MinimumStrength);
        Assert.Equal(23, falconsTalon.MinimumDexterity);
        Assert.Equal(20, falconsTalon.PlusToHit);
        Assert.Equal(-33, falconsTalon.PlusDamage);
        Assert.Equal(10, falconsTalon.PlusDexterity);
        Assert.NotEqual(0U, falconsTalon.Flags & 0x00100000U);
        var inferno = hellfireItems.GenerateUnique(1035, 0x12345678, 17);
        Assert.Equal(1035, inferno.UniqueId);
        Assert.Equal(34600, inferno.Value);
        Assert.Equal(126, inferno.ItemIndex);
        Assert.Equal(2, inferno.MinDamage);
        Assert.Equal(10, inferno.MaxDamage);
        Assert.Equal(30, inferno.MinimumStrength);
        Assert.Equal(30, inferno.MinimumDexterity);
        Assert.Equal(2, inferno.FireMinDamage);
        Assert.Equal(12, inferno.FireMaxDamage);
        Assert.Equal(3, inferno.PlusLight);
        Assert.Equal(20 << 6, inferno.PlusMana);
        Assert.Equal(80, inferno.PlusFireResistance);
        var doombringer = hellfireItems.GenerateUnique(1036, 0x12345678, 19);
        Assert.Equal(1036, doombringer.UniqueId);
        Assert.Equal(18250, doombringer.Value);
        Assert.Equal(129, doombringer.ItemIndex);
        Assert.Equal(8, doombringer.MinDamage);
        Assert.Equal(16, doombringer.MaxDamage);
        Assert.Equal(65, doombringer.MinimumStrength);
        Assert.Equal(25, doombringer.PlusToHit);
        Assert.Equal(250, doombringer.PlusDamage);
        Assert.Equal(-5, doombringer.PlusStrength);
        Assert.Equal(-5, doombringer.PlusMagic);
        Assert.Equal(-5, doombringer.PlusDexterity);
        Assert.Equal(-5, doombringer.PlusVitality);
        Assert.Equal(-(25 << 6), doombringer.PlusHitPoints);
        Assert.Equal(-2, doombringer.PlusLight);
        var grandfather = hellfireItems.GenerateUnique(1037, 0x12345678, 27);
        Assert.Equal(1037, grandfather.UniqueId);
        Assert.Equal(119800, grandfather.Value);
        Assert.Equal(130, grandfather.ItemIndex);
        Assert.Equal(10, grandfather.MinDamage);
        Assert.Equal(20, grandfather.MaxDamage);
        Assert.Equal(75, grandfather.MinimumStrength);
        Assert.Equal(1, grandfather.EquipLocation);
        Assert.Equal(5, grandfather.PlusStrength);
        Assert.Equal(5, grandfather.PlusMagic);
        Assert.Equal(5, grandfather.PlusDexterity);
        Assert.Equal(5, grandfather.PlusVitality);
        Assert.Equal(20, grandfather.PlusToHit);
        Assert.Equal(70, grandfather.PlusDamage);
        Assert.Equal(20 << 6, grandfather.PlusHitPoints);
        var mangler = hellfireItems.GenerateUnique(1038, 0x12345678, 2);
        Assert.Equal(1038, mangler.UniqueId);
        Assert.Equal(2850, mangler.Value);
        Assert.Equal(133, mangler.ItemIndex);
        Assert.Equal(6, mangler.MinDamage);
        Assert.Equal(16, mangler.MaxDamage);
        Assert.Equal(30, mangler.MinimumStrength);
        Assert.Equal(2, mangler.EquipLocation);
        Assert.Equal(200, mangler.PlusDamage);
        Assert.Equal(-5, mangler.PlusDexterity);
        Assert.Equal(-5, mangler.PlusMagic);
        Assert.Equal(-(10 << 6), mangler.PlusMana);
        var sharpBeak = hellfireItems.GenerateUnique(1039, 0x12345678, 2);
        Assert.Equal(1039, sharpBeak.UniqueId);
        Assert.Equal(2850, sharpBeak.Value);
        Assert.Equal(133, sharpBeak.ItemIndex);
        Assert.Equal(6, sharpBeak.MinDamage);
        Assert.Equal(16, sharpBeak.MaxDamage);
        Assert.Equal(30, sharpBeak.MinimumStrength);
        Assert.Equal(2, sharpBeak.EquipLocation);
        Assert.Equal(20 << 6, sharpBeak.PlusHitPoints);
        Assert.Equal(-10, sharpBeak.PlusMagic);
        Assert.Equal(-(10 << 6), sharpBeak.PlusMana);
        var bloodSlayer = hellfireItems.GenerateUnique(1040, 0x12345678, 3);
        Assert.Equal(1040, bloodSlayer.UniqueId);
        Assert.Equal(2500, bloodSlayer.Value);
        Assert.Equal(134, bloodSlayer.ItemIndex);
        Assert.Equal(8, bloodSlayer.MinDamage);
        Assert.Equal(20, bloodSlayer.MaxDamage);
        Assert.Equal(50, bloodSlayer.MinimumStrength);
        Assert.Equal(2, bloodSlayer.EquipLocation);
        Assert.Equal(100, bloodSlayer.PlusDamage);
        Assert.NotEqual(0U, bloodSlayer.Flags & 0x40000000U);
        Assert.Equal(-5, bloodSlayer.PlusStrength);
        Assert.Equal(-5, bloodSlayer.PlusMagic);
        Assert.Equal(-5, bloodSlayer.PlusDexterity);
        Assert.Equal(-5, bloodSlayer.PlusVitality);
        Assert.Equal(-1, bloodSlayer.SpellLevelAdd);
        var celestialAxe = hellfireItems.GenerateUnique(1041, 0x12345678, 4);
        Assert.Equal(1041, celestialAxe.UniqueId);
        Assert.Equal(14100, celestialAxe.Value);
        Assert.Equal(135, celestialAxe.ItemIndex);
        Assert.Equal(10, celestialAxe.MinDamage);
        Assert.Equal(25, celestialAxe.MaxDamage);
        Assert.Equal(0, celestialAxe.MinimumStrength);
        Assert.Equal(2, celestialAxe.EquipLocation);
        Assert.Equal(15, celestialAxe.PlusToHit);
        Assert.Equal(15 << 6, celestialAxe.PlusHitPoints);
        Assert.Equal(-15, celestialAxe.PlusStrength);
        var wickedAxe = hellfireItems.GenerateUnique(1042, 0x12345678, 5);
        Assert.Equal(1042, wickedAxe.UniqueId);
        Assert.Equal(31150, wickedAxe.Value);
        Assert.Equal(133, wickedAxe.ItemIndex);
        Assert.Equal(6, wickedAxe.MinDamage);
        Assert.Equal(16, wickedAxe.MaxDamage);
        Assert.Equal(30, wickedAxe.MinimumStrength);
        Assert.Equal(2, wickedAxe.EquipLocation);
        Assert.Equal(30, wickedAxe.PlusToHit);
        Assert.Equal(10, wickedAxe.PlusDexterity);
        Assert.Equal(-10, wickedAxe.PlusVitality);
        Assert.Equal(-2, wickedAxe.PlusGetHit);
        Assert.Equal(255, wickedAxe.Durability);
        Assert.Equal(255, wickedAxe.MaxDurability);
        var stonecleaver = hellfireItems.GenerateUnique(1043, 0x12345678, 7);
        Assert.Equal(1043, stonecleaver.UniqueId);
        Assert.Equal(23900, stonecleaver.Value);
        Assert.Equal(134, stonecleaver.ItemIndex);
        Assert.Equal(8, stonecleaver.MinDamage);
        Assert.Equal(20, stonecleaver.MaxDamage);
        Assert.Equal(50, stonecleaver.MinimumStrength);
        Assert.Equal(2, stonecleaver.EquipLocation);
        Assert.Equal(30 << 6, stonecleaver.PlusHitPoints);
        Assert.Equal(20, stonecleaver.PlusToHit);
        Assert.Equal(50, stonecleaver.PlusDamage);
        Assert.Equal(40, stonecleaver.PlusLightningResistance);
        var aguinarasHatchet = hellfireItems.GenerateUnique(1044, 0x12345678, 12);
        Assert.Equal(1044, aguinarasHatchet.UniqueId);
        Assert.Equal(24800, aguinarasHatchet.Value);
        Assert.Equal(131, aguinarasHatchet.ItemIndex);
        Assert.Equal(2, aguinarasHatchet.MinDamage);
        Assert.Equal(10, aguinarasHatchet.MaxDamage);
        Assert.Equal(0, aguinarasHatchet.MinimumStrength);
        Assert.Equal(2, aguinarasHatchet.EquipLocation);
        Assert.Equal(1, aguinarasHatchet.SpellLevelAdd);
        Assert.Equal(10, aguinarasHatchet.PlusMagic);
        Assert.Equal(80, aguinarasHatchet.PlusMagicResistance);
        var hellslayer = hellfireItems.GenerateUnique(1045, 0x12345678, 15);
        Assert.Equal(1045, hellslayer.UniqueId);
        Assert.Equal(26200, hellslayer.Value);
        Assert.Equal(135, hellslayer.ItemIndex);
        Assert.Equal(10, hellslayer.MinDamage);
        Assert.Equal(25, hellslayer.MaxDamage);
        Assert.Equal(65, hellslayer.MinimumStrength);
        Assert.Equal(2, hellslayer.EquipLocation);
        Assert.Equal(8, hellslayer.PlusStrength);
        Assert.Equal(8, hellslayer.PlusVitality);
        Assert.Equal(100, hellslayer.PlusDamage);
        Assert.Equal(25 << 6, hellslayer.PlusHitPoints);
        Assert.Equal(-(25 << 6), hellslayer.PlusMana);
        var messerschmidtsReaver = hellfireItems.GenerateUnique(1046, 0x12345678, 25);
        Assert.Equal(1046, messerschmidtsReaver.UniqueId);
        Assert.Equal(58000, messerschmidtsReaver.Value);
        Assert.Equal(136, messerschmidtsReaver.ItemIndex);
        Assert.Equal(12, messerschmidtsReaver.MinDamage);
        Assert.Equal(30, messerschmidtsReaver.MaxDamage);
        Assert.Equal(80, messerschmidtsReaver.MinimumStrength);
        Assert.Equal(2, messerschmidtsReaver.EquipLocation);
        Assert.Equal(200, messerschmidtsReaver.PlusDamage);
        Assert.Equal(15, messerschmidtsReaver.PlusDamageModifier);
        Assert.Equal(5, messerschmidtsReaver.PlusStrength);
        Assert.Equal(5, messerschmidtsReaver.PlusMagic);
        Assert.Equal(5, messerschmidtsReaver.PlusDexterity);
        Assert.Equal(5, messerschmidtsReaver.PlusVitality);
        Assert.Equal(-(50 << 6), messerschmidtsReaver.PlusHitPoints);
        Assert.Equal(2, messerschmidtsReaver.FireMinDamage);
        Assert.Equal(12, messerschmidtsReaver.FireMaxDamage);
        var crackrust = hellfireItems.GenerateUnique(1047, 0x12345678, 1);
        Assert.Equal(1047, crackrust.UniqueId);
        Assert.Equal(11375, crackrust.Value);
        Assert.Equal(137, crackrust.ItemIndex);
        Assert.Equal(1, crackrust.MinDamage);
        Assert.Equal(8, crackrust.MaxDamage);
        Assert.Equal(16, crackrust.MinimumStrength);
        Assert.Equal(1, crackrust.EquipLocation);
        Assert.Equal(2, crackrust.PlusStrength);
        Assert.Equal(2, crackrust.PlusMagic);
        Assert.Equal(2, crackrust.PlusDexterity);
        Assert.Equal(2, crackrust.PlusVitality);
        Assert.Equal(15, crackrust.PlusFireResistance);
        Assert.Equal(15, crackrust.PlusLightningResistance);
        Assert.Equal(15, crackrust.PlusMagicResistance);
        Assert.Equal(50, crackrust.PlusDamage);
        Assert.Equal(-1, crackrust.SpellLevelAdd);
        Assert.Equal(255, crackrust.Durability);
        Assert.Equal(255, crackrust.MaxDurability);
        var hammerOfJholm = hellfireItems.GenerateUnique(1048, 0x12345678, 1);
        Assert.Equal(1048, hammerOfJholm.UniqueId);
        Assert.Equal(8700, hammerOfJholm.Value);
        Assert.Equal(143, hammerOfJholm.ItemIndex);
        Assert.Equal(4, hammerOfJholm.MinDamage);
        Assert.Equal(10, hammerOfJholm.MaxDamage);
        Assert.Equal(55, hammerOfJholm.MinimumStrength);
        Assert.Equal(2, hammerOfJholm.EquipLocation);
        Assert.Equal(3, hammerOfJholm.PlusStrength);
        Assert.Equal(15, hammerOfJholm.PlusToHit);
        Assert.Equal(255, hammerOfJholm.Durability);
        Assert.Equal(255, hammerOfJholm.MaxDurability);
        var civerbsCudgel = hellfireItems.GenerateUnique(1049, 0x12345678, 1);
        Assert.Equal(1049, civerbsCudgel.UniqueId);
        Assert.Equal(2000, civerbsCudgel.Value);
        Assert.Equal(137, civerbsCudgel.ItemIndex);
        Assert.Equal(1, civerbsCudgel.MinDamage);
        Assert.Equal(8, civerbsCudgel.MaxDamage);
        Assert.Equal(16, civerbsCudgel.MinimumStrength);
        Assert.Equal(1, civerbsCudgel.EquipLocation);
        Assert.NotEqual(0U, civerbsCudgel.Flags & 0x40000000U);
        Assert.Equal(-5, civerbsCudgel.PlusDexterity);
        Assert.Equal(-2, civerbsCudgel.PlusMagic);
        var celestialStar = hellfireItems.GenerateUnique(1050, 0x12345678, 2);
        Assert.Equal(1050, celestialStar.UniqueId);
        Assert.Equal(7810, celestialStar.Value);
        Assert.Equal(142, celestialStar.ItemIndex);
        Assert.Equal(2, celestialStar.MinDamage);
        Assert.Equal(12, celestialStar.MaxDamage);
        Assert.Equal(0, celestialStar.MinimumStrength);
        Assert.Equal(1, celestialStar.EquipLocation);
        Assert.Equal(2, celestialStar.PlusLight);
        Assert.Equal(10, celestialStar.PlusDamageModifier);
        Assert.Equal(-8, celestialStar.PlusArmorClass);
        var baranarsStar = hellfireItems.GenerateUnique(1051, 0x12345678, 5);
        Assert.Equal(1051, baranarsStar.UniqueId);
        Assert.Equal(6850, baranarsStar.Value);
        Assert.Equal(138, baranarsStar.ItemIndex);
        Assert.Equal(1, baranarsStar.MinDamage);
        Assert.Equal(10, baranarsStar.MaxDamage);
        Assert.Equal(26, baranarsStar.MinimumStrength);
        Assert.Equal(1, baranarsStar.EquipLocation);
        Assert.Equal(12, baranarsStar.PlusToHit);
        Assert.Equal(80, baranarsStar.PlusDamage);
        Assert.NotEqual(0U, baranarsStar.Flags & 0x00020000U);
        Assert.Equal(4, baranarsStar.PlusVitality);
        Assert.Equal(-4, baranarsStar.PlusDexterity);
        Assert.Equal(60, baranarsStar.Durability);
        Assert.Equal(60, baranarsStar.MaxDurability);
        var gnarledRoot = hellfireItems.GenerateUnique(1052, 0x12345678, 9);
        Assert.Equal(1052, gnarledRoot.UniqueId);
        Assert.Equal(9820, gnarledRoot.Value);
        Assert.Equal(139, gnarledRoot.ItemIndex);
        Assert.Equal(3, gnarledRoot.MinDamage);
        Assert.Equal(6, gnarledRoot.MaxDamage);
        Assert.Equal(18, gnarledRoot.MinimumStrength);
        Assert.Equal(1, gnarledRoot.EquipLocation);
        Assert.Equal(20, gnarledRoot.PlusToHit);
        Assert.Equal(300, gnarledRoot.PlusDamage);
        Assert.Equal(10, gnarledRoot.PlusDexterity);
        Assert.Equal(5, gnarledRoot.PlusMagic);
        Assert.Equal(10, gnarledRoot.PlusFireResistance);
        Assert.Equal(10, gnarledRoot.PlusLightningResistance);
        Assert.Equal(10, gnarledRoot.PlusMagicResistance);
        Assert.Equal(-10, gnarledRoot.PlusArmorClass);
        var craniumBasher = hellfireItems.GenerateUnique(1053, 0x12345678, 12);
        Assert.Equal(1053, craniumBasher.UniqueId);
        Assert.Equal(36500, craniumBasher.Value);
        Assert.Equal(143, craniumBasher.ItemIndex);
        Assert.Equal(6, craniumBasher.MinDamage);
        Assert.Equal(20, craniumBasher.MaxDamage);
        Assert.Equal(55, craniumBasher.MinimumStrength);
        Assert.Equal(2, craniumBasher.EquipLocation);
        Assert.Equal(20, craniumBasher.PlusDamageModifier);
        Assert.Equal(15, craniumBasher.PlusStrength);
        Assert.Equal(255, craniumBasher.Durability);
        Assert.Equal(255, craniumBasher.MaxDurability);
        Assert.Equal(-(150 << 6), craniumBasher.PlusMana);
        Assert.Equal(5, craniumBasher.PlusFireResistance);
        Assert.Equal(5, craniumBasher.PlusLightningResistance);
        Assert.Equal(5, craniumBasher.PlusMagicResistance);
        var schaefersHammer = hellfireItems.GenerateUnique(1054, 0x12345678, 16);
        Assert.Equal(1054, schaefersHammer.UniqueId);
        Assert.Equal(56125, schaefersHammer.Value);
        Assert.Equal(139, schaefersHammer.ItemIndex);
        Assert.Equal(5, schaefersHammer.MinDamage);
        Assert.Equal(9, schaefersHammer.MaxDamage);
        Assert.Equal(40, schaefersHammer.MinimumStrength);
        Assert.Equal(1, schaefersHammer.EquipLocation);
        Assert.Equal(-100, schaefersHammer.PlusDamage);
        Assert.Equal(1, schaefersHammer.LightningMinDamage);
        Assert.Equal(50, schaefersHammer.LightningMaxDamage);
        Assert.Equal(50 << 6, schaefersHammer.PlusHitPoints);
        Assert.Equal(30, schaefersHammer.PlusToHit);
        Assert.Equal(80, schaefersHammer.PlusLightningResistance);
        Assert.Equal(1, schaefersHammer.PlusLight);
        var dreamflange = hellfireItems.GenerateUnique(1055, 0x12345678, 26);
        Assert.Equal(1055, dreamflange.UniqueId);
        Assert.Equal(26450, dreamflange.Value);
        Assert.Equal(137, dreamflange.ItemIndex);
        Assert.Equal(1, dreamflange.MinDamage);
        Assert.Equal(8, dreamflange.MaxDamage);
        Assert.Equal(16, dreamflange.MinimumStrength);
        Assert.Equal(1, dreamflange.EquipLocation);
        Assert.Equal(30, dreamflange.PlusMagic);
        Assert.Equal(50 << 6, dreamflange.PlusMana);
        Assert.Equal(50, dreamflange.PlusMagicResistance);
        Assert.Equal(2, dreamflange.PlusLight);
        Assert.Equal(1, dreamflange.SpellLevelAdd);
        var staffOfShadows = hellfireItems.GenerateUnique(1056, 0x12345678, 2);
        Assert.Equal(1056, staffOfShadows.UniqueId);
        Assert.Equal(1250, staffOfShadows.Value);
        Assert.Equal(153, staffOfShadows.ItemIndex);
        Assert.Equal(4, staffOfShadows.MinDamage);
        Assert.Equal(8, staffOfShadows.MaxDamage);
        Assert.Equal(0, staffOfShadows.MinimumStrength);
        Assert.Equal(2, staffOfShadows.EquipLocation);
        Assert.Equal(-10, staffOfShadows.PlusMagic);
        Assert.Equal(10, staffOfShadows.PlusToHit);
        Assert.Equal(60, staffOfShadows.PlusDamage);
        Assert.Equal(-2, staffOfShadows.PlusLight);
        Assert.NotEqual(0U, staffOfShadows.Flags & 0x00020000U);
        var immolator = hellfireItems.GenerateUnique(1057, 0x12345678, 4);
        Assert.Equal(1057, immolator.UniqueId);
        Assert.Equal(3900, immolator.Value);
        Assert.Equal(153, immolator.ItemIndex);
        Assert.Equal(4, immolator.MinDamage);
        Assert.Equal(8, immolator.MaxDamage);
        Assert.Equal(0, immolator.MinimumStrength);
        Assert.Equal(2, immolator.EquipLocation);
        Assert.Equal(20, immolator.PlusFireResistance);
        Assert.Equal(4, immolator.FireMinDamage);
        Assert.Equal(4, immolator.FireMaxDamage);
        Assert.NotEqual(0U, immolator.Flags & 0x00000010U);
        Assert.Equal(10 << 6, immolator.PlusMana);
        Assert.Equal(-5, immolator.PlusVitality);
        var stormSpire = hellfireItems.GenerateUnique(1058, 0x12345678, 8);
        Assert.Equal(1058, stormSpire.UniqueId);
        Assert.Equal(22500, stormSpire.Value);
        Assert.Equal(156, stormSpire.ItemIndex);
        Assert.Equal(8, stormSpire.MinDamage);
        Assert.Equal(16, stormSpire.MaxDamage);
        Assert.Equal(30, stormSpire.MinimumStrength);
        Assert.Equal(2, stormSpire.EquipLocation);
        Assert.Equal(50, stormSpire.PlusLightningResistance);
        Assert.Equal(2, stormSpire.LightningMinDamage);
        Assert.Equal(8, stormSpire.LightningMaxDamage);
        Assert.NotEqual(0U, stormSpire.Flags & 0x00000020U);
        Assert.Equal(10, stormSpire.PlusStrength);
        Assert.Equal(-10, stormSpire.PlusMagic);
        var gleamsong = hellfireItems.GenerateUnique(1059, 0x12345678, 8);
        Assert.Equal(1059, gleamsong.UniqueId);
        Assert.Equal(6520, gleamsong.Value);
        Assert.Equal(152, gleamsong.ItemIndex);
        Assert.Equal(1, gleamsong.MinDamage);
        Assert.Equal(4, gleamsong.MaxDamage);
        Assert.Equal(0, gleamsong.MinimumStrength);
        Assert.Equal(2, gleamsong.EquipLocation);
        Assert.Equal(25 << 6, gleamsong.PlusMana);
        Assert.Equal(-3, gleamsong.PlusStrength);
        Assert.Equal(-3, gleamsong.PlusVitality);
        Assert.Equal(10, gleamsong.SpellId);
        Assert.Equal(76, gleamsong.Charges);
        Assert.Equal(76, gleamsong.MaxCharges);
        var thundercall = hellfireItems.GenerateUnique(1060, 0x12345678, 14);
        Assert.Equal(1060, thundercall.UniqueId);
        Assert.Equal(22250, thundercall.Value);
        Assert.Equal(154, thundercall.ItemIndex);
        Assert.Equal(5, thundercall.MinDamage);
        Assert.Equal(10, thundercall.MaxDamage);
        Assert.Equal(0, thundercall.MinimumStrength);
        Assert.Equal(2, thundercall.EquipLocation);
        Assert.Equal(35, thundercall.PlusToHit);
        Assert.Equal(1, thundercall.LightningMinDamage);
        Assert.Equal(10, thundercall.LightningMaxDamage);
        Assert.NotEqual(0U, thundercall.Flags & 0x00000020U);
        Assert.Equal(3, thundercall.SpellId);
        Assert.Equal(76, thundercall.Charges);
        Assert.Equal(76, thundercall.MaxCharges);
        Assert.Equal(30, thundercall.PlusLightningResistance);
        Assert.Equal(2, thundercall.PlusLight);
        var theProtector = hellfireItems.GenerateUnique(1061, 0x12345678, 16);
        Assert.Equal(1061, theProtector.UniqueId);
        Assert.Equal(17240, theProtector.Value);
        Assert.Equal(152, theProtector.ItemIndex);
        Assert.Equal(1, theProtector.MinDamage);
        Assert.Equal(4, theProtector.MaxDamage);
        Assert.Equal(0, theProtector.MinimumStrength);
        Assert.Equal(2, theProtector.EquipLocation);
        Assert.Equal(5, theProtector.PlusVitality);
        Assert.Equal(-5, theProtector.PlusGetHit);
        Assert.Equal(40, theProtector.ArmorClass);
        Assert.Equal(2, theProtector.SpellId);
        Assert.Equal(86, theProtector.Charges);
        Assert.Equal(86, theProtector.MaxCharges);
        Assert.NotEqual(0U, theProtector.Flags & 0x04000000U);
        var najsPuzzler = hellfireItems.GenerateUnique(1062, 0x12345678, 18);
        Assert.Equal(1062, najsPuzzler.UniqueId);
        Assert.Equal(34000, najsPuzzler.Value);
        Assert.Equal(153, najsPuzzler.ItemIndex);
        Assert.Equal(4, najsPuzzler.MinDamage);
        Assert.Equal(8, najsPuzzler.MaxDamage);
        Assert.Equal(0, najsPuzzler.MinimumStrength);
        Assert.Equal(2, najsPuzzler.EquipLocation);
        Assert.Equal(20, najsPuzzler.PlusMagic);
        Assert.Equal(10, najsPuzzler.PlusDexterity);
        Assert.Equal(20, najsPuzzler.PlusFireResistance);
        Assert.Equal(20, najsPuzzler.PlusLightningResistance);
        Assert.Equal(20, najsPuzzler.PlusMagicResistance);
        Assert.Equal(23, najsPuzzler.SpellId);
        Assert.Equal(57, najsPuzzler.Charges);
        Assert.Equal(57, najsPuzzler.MaxCharges);
        Assert.Equal(-(25 << 6), najsPuzzler.PlusHitPoints);
        var mindcry = hellfireItems.GenerateUnique(1063, 0x12345678, 20);
        Assert.Equal(1063, mindcry.UniqueId);
        Assert.Equal(41500, mindcry.Value);
        Assert.Equal(155, mindcry.ItemIndex);
        Assert.Equal(6, mindcry.MinDamage);
        Assert.Equal(12, mindcry.MaxDamage);
        Assert.Equal(20, mindcry.MinimumStrength);
        Assert.Equal(2, mindcry.EquipLocation);
        Assert.Equal(15, mindcry.PlusMagic);
        Assert.Equal(13, mindcry.SpellId);
        Assert.Equal(69, mindcry.Charges);
        Assert.Equal(69, mindcry.MaxCharges);
        Assert.Equal(15, mindcry.PlusFireResistance);
        Assert.Equal(15, mindcry.PlusLightningResistance);
        Assert.Equal(15, mindcry.PlusMagicResistance);
        Assert.Equal(1, mindcry.SpellLevelAdd);
        var rodOfOnan = hellfireItems.GenerateUnique(1064, 0x12345678, 22);
        Assert.Equal(1064, rodOfOnan.UniqueId);
        Assert.Equal(44167, rodOfOnan.Value);
        Assert.Equal(156, rodOfOnan.ItemIndex);
        Assert.Equal(8, rodOfOnan.MinDamage);
        Assert.Equal(16, rodOfOnan.MaxDamage);
        Assert.Equal(30, rodOfOnan.MinimumStrength);
        Assert.Equal(2, rodOfOnan.EquipLocation);
        Assert.Equal(21, rodOfOnan.SpellId);
        Assert.Equal(50, rodOfOnan.Charges);
        Assert.Equal(50, rodOfOnan.MaxCharges);
        Assert.Equal(100, rodOfOnan.PlusDamage);
        Assert.Equal(5, rodOfOnan.PlusStrength);
        var helmOfSpirits = hellfireItems.GenerateUnique(1065, 0x12345678, 1);
        Assert.Equal(1065, helmOfSpirits.UniqueId);
        Assert.Equal(7525, helmOfSpirits.Value);
        Assert.Equal(51, helmOfSpirits.ItemIndex);
        Assert.Equal(4, helmOfSpirits.ArmorClass);
        Assert.Equal(30, helmOfSpirits.Durability);
        Assert.Equal(30, helmOfSpirits.MaxDurability);
        Assert.Equal(25, helmOfSpirits.MinimumStrength);
        Assert.Equal(4, helmOfSpirits.EquipLocation);
        Assert.NotEqual(0U, helmOfSpirits.Flags & 0x00010000U);
        var thinkingCap = hellfireItems.GenerateUnique(1066, 0x12345678, 6);
        Assert.Equal(1066, thinkingCap.UniqueId);
        Assert.Equal(2020, thinkingCap.Value);
        Assert.Equal(50, thinkingCap.ItemIndex);
        Assert.Equal(4, thinkingCap.ArmorClass);
        Assert.Equal(1, thinkingCap.Durability);
        Assert.Equal(1, thinkingCap.MaxDurability);
        Assert.Equal(0, thinkingCap.MinimumStrength);
        Assert.Equal(4, thinkingCap.EquipLocation);
        Assert.Equal(30 << 6, thinkingCap.PlusMana);
        Assert.Equal(2, thinkingCap.SpellLevelAdd);
        Assert.Equal(20, thinkingCap.PlusFireResistance);
        Assert.Equal(20, thinkingCap.PlusLightningResistance);
        Assert.Equal(20, thinkingCap.PlusMagicResistance);
        var overlordsHelm = hellfireItems.GenerateUnique(1067, 0x12345678, 7);
        Assert.Equal(1067, overlordsHelm.UniqueId);
        Assert.Equal(12500, overlordsHelm.Value);
        Assert.Equal(51, overlordsHelm.ItemIndex);
        Assert.Equal(4, overlordsHelm.ArmorClass);
        Assert.Equal(15, overlordsHelm.Durability);
        Assert.Equal(15, overlordsHelm.MaxDurability);
        Assert.Equal(25, overlordsHelm.MinimumStrength);
        Assert.Equal(4, overlordsHelm.EquipLocation);
        Assert.Equal(20, overlordsHelm.PlusStrength);
        Assert.Equal(15, overlordsHelm.PlusDexterity);
        Assert.Equal(5, overlordsHelm.PlusVitality);
        Assert.Equal(-20, overlordsHelm.PlusMagic);
        var foolsCrest = hellfireItems.GenerateUnique(1068, 0x12345678, 12);
        Assert.Equal(1068, foolsCrest.UniqueId);
        Assert.Equal(10150, foolsCrest.Value);
        Assert.Equal(51, foolsCrest.ItemIndex);
        Assert.Equal(4, foolsCrest.ArmorClass);
        Assert.Equal(30, foolsCrest.Durability);
        Assert.Equal(30, foolsCrest.MaxDurability);
        Assert.Equal(25, foolsCrest.MinimumStrength);
        Assert.Equal(4, foolsCrest.EquipLocation);
        Assert.Equal(-4, foolsCrest.PlusStrength);
        Assert.Equal(-4, foolsCrest.PlusMagic);
        Assert.Equal(-4, foolsCrest.PlusDexterity);
        Assert.Equal(-4, foolsCrest.PlusVitality);
        Assert.Equal(100 << 6, foolsCrest.PlusHitPoints);
        Assert.InRange(foolsCrest.PlusGetHit, 1, 6);
        Assert.NotEqual(0U, foolsCrest.Flags & 0x04000000U);
        var gotterdamerung = hellfireItems.GenerateUnique(1069, 0x12345678, 21);
        Assert.Equal(1069, gotterdamerung.UniqueId);
        Assert.Equal(54900, gotterdamerung.Value);
        Assert.Equal(54, gotterdamerung.ItemIndex);
        Assert.Equal(60, gotterdamerung.ArmorClass);
        Assert.Equal(60, gotterdamerung.Durability);
        Assert.Equal(60, gotterdamerung.MaxDurability);
        Assert.Equal(50, gotterdamerung.MinimumStrength);
        Assert.Equal(4, gotterdamerung.EquipLocation);
        Assert.Equal(20, gotterdamerung.PlusStrength);
        Assert.Equal(20, gotterdamerung.PlusMagic);
        Assert.Equal(20, gotterdamerung.PlusDexterity);
        Assert.Equal(20, gotterdamerung.PlusVitality);
        Assert.Equal(-4, gotterdamerung.PlusGetHit);
        Assert.Equal(-4, gotterdamerung.PlusLight);
        Assert.NotEqual(0U, gotterdamerung.Flags & 0x80000000U);
        var royalCirclet = hellfireItems.GenerateUnique(1070, 0x12345678, 27);
        Assert.Equal(1070, royalCirclet.UniqueId);
        Assert.Equal(24875, royalCirclet.Value);
        Assert.Equal(53, royalCirclet.ItemIndex);
        Assert.Equal(40, royalCirclet.ArmorClass);
        Assert.Equal(40, royalCirclet.Durability);
        Assert.Equal(40, royalCirclet.MaxDurability);
        Assert.Equal(0, royalCirclet.MinimumStrength);
        Assert.Equal(4, royalCirclet.EquipLocation);
        Assert.Equal(10, royalCirclet.PlusStrength);
        Assert.Equal(10, royalCirclet.PlusMagic);
        Assert.Equal(10, royalCirclet.PlusDexterity);
        Assert.Equal(10, royalCirclet.PlusVitality);
        Assert.Equal(40 << 6, royalCirclet.PlusMana);
        Assert.Equal(1, royalCirclet.PlusLight);
        var tornFlesh = hellfireItems.GenerateUnique(1071, 0x12345678, 2);
        Assert.Equal(1071, tornFlesh.UniqueId);
        Assert.Equal(4825, tornFlesh.Value);
        Assert.Equal(56, tornFlesh.ItemIndex);
        Assert.Equal(8, tornFlesh.ArmorClass);
        Assert.Equal(255, tornFlesh.Durability);
        Assert.Equal(255, tornFlesh.MaxDurability);
        Assert.Equal(0, tornFlesh.MinimumStrength);
        Assert.Equal(3, tornFlesh.EquipLocation);
        Assert.Equal(10, tornFlesh.PlusVitality);
        Assert.Equal(-1, tornFlesh.PlusGetHit);
        var gladiatorsBane = hellfireItems.GenerateUnique(1072, 0x12345678, 6);
        Assert.Equal(1072, gladiatorsBane.UniqueId);
        Assert.Equal(3450, gladiatorsBane.Value);
        Assert.Equal(62, gladiatorsBane.ItemIndex);
        Assert.Equal(25, gladiatorsBane.ArmorClass);
        Assert.Equal(135, gladiatorsBane.Durability);
        Assert.Equal(135, gladiatorsBane.MaxDurability);
        Assert.Equal(20, gladiatorsBane.MinimumStrength);
        Assert.Equal(3, gladiatorsBane.EquipLocation);
        Assert.Equal(-3, gladiatorsBane.PlusStrength);
        Assert.Equal(-3, gladiatorsBane.PlusMagic);
        Assert.Equal(-3, gladiatorsBane.PlusDexterity);
        Assert.Equal(-3, gladiatorsBane.PlusVitality);
        Assert.Equal(-2, gladiatorsBane.PlusGetHit);
        var rainbowCloak = hellfireItems.GenerateUnique(1073, 0x12345678, 2);
        Assert.Equal(1073, rainbowCloak.UniqueId);
        Assert.Equal(4900, rainbowCloak.Value);
        Assert.Equal(57, rainbowCloak.ItemIndex);
        Assert.Equal(10, rainbowCloak.ArmorClass);
        Assert.Equal(27, rainbowCloak.Durability);
        Assert.Equal(27, rainbowCloak.MaxDurability);
        Assert.Equal(0, rainbowCloak.MinimumStrength);
        Assert.Equal(3, rainbowCloak.EquipLocation);
        Assert.Equal(1, rainbowCloak.PlusStrength);
        Assert.Equal(1, rainbowCloak.PlusMagic);
        Assert.Equal(1, rainbowCloak.PlusDexterity);
        Assert.Equal(1, rainbowCloak.PlusVitality);
        Assert.Equal(10, rainbowCloak.PlusFireResistance);
        Assert.Equal(10, rainbowCloak.PlusLightningResistance);
        Assert.Equal(10, rainbowCloak.PlusMagicResistance);
        Assert.Equal(5 << 6, rainbowCloak.PlusHitPoints);
        var leatherOfAut = hellfireItems.GenerateUnique(1074, 0x12345678, 4);
        Assert.Equal(1074, leatherOfAut.UniqueId);
        Assert.Equal(10550, leatherOfAut.Value);
        Assert.Equal(60, leatherOfAut.ItemIndex);
        Assert.Equal(15, leatherOfAut.ArmorClass);
        Assert.Equal(255, leatherOfAut.Durability);
        Assert.Equal(255, leatherOfAut.MaxDurability);
        Assert.Equal(0, leatherOfAut.MinimumStrength);
        Assert.Equal(3, leatherOfAut.EquipLocation);
        Assert.Equal(5, leatherOfAut.PlusStrength);
        Assert.Equal(-5, leatherOfAut.PlusMagic);
        Assert.Equal(5, leatherOfAut.PlusDexterity);
        var wisdomsWrap = hellfireItems.GenerateUnique(1075, 0x12345678, 5);
        Assert.Equal(1075, wisdomsWrap.UniqueId);
        Assert.Equal(6200, wisdomsWrap.Value);
        Assert.Equal(58, wisdomsWrap.ItemIndex);
        Assert.Equal(15, wisdomsWrap.ArmorClass);
        Assert.Equal(24, wisdomsWrap.Durability);
        Assert.Equal(24, wisdomsWrap.MaxDurability);
        Assert.Equal(0, wisdomsWrap.MinimumStrength);
        Assert.Equal(3, wisdomsWrap.EquipLocation);
        Assert.Equal(5, wisdomsWrap.PlusMagic);
        Assert.Equal(10 << 6, wisdomsWrap.PlusMana);
        Assert.Equal(25, wisdomsWrap.PlusLightningResistance);
        Assert.Equal(-1, wisdomsWrap.PlusGetHit);
        var sparkingMail = hellfireItems.GenerateUnique(1076, 0x12345678, 9);
        Assert.Equal(1076, sparkingMail.UniqueId);
        Assert.Equal(15750, sparkingMail.Value);
        Assert.Equal(64, sparkingMail.ItemIndex);
        Assert.Equal(30, sparkingMail.ArmorClass);
        Assert.Equal(55, sparkingMail.Durability);
        Assert.Equal(55, sparkingMail.MaxDurability);
        Assert.Equal(30, sparkingMail.MinimumStrength);
        Assert.Equal(3, sparkingMail.EquipLocation);
        Assert.Equal(1, sparkingMail.LightningMinDamage);
        Assert.Equal(10, sparkingMail.LightningMaxDamage);
        Assert.NotEqual(0U, sparkingMail.Flags & 0x00000020U);
        var scavengerCarapace = hellfireItems.GenerateUnique(1077, 0x12345678, 13);
        Assert.Equal(1077, scavengerCarapace.UniqueId);
        Assert.Equal(14000, scavengerCarapace.Value);
        Assert.Equal(66, scavengerCarapace.ItemIndex);
        Assert.Equal(22, scavengerCarapace.ArmorClass);
        Assert.Equal(80, scavengerCarapace.Durability);
        Assert.Equal(80, scavengerCarapace.MaxDurability);
        Assert.Equal(40, scavengerCarapace.MinimumStrength);
        Assert.Equal(3, scavengerCarapace.EquipLocation);
        Assert.Equal(-30, scavengerCarapace.PlusArmorClass);
        Assert.Equal(-15, scavengerCarapace.PlusGetHit);
        Assert.Equal(5, scavengerCarapace.PlusDexterity);
        Assert.Equal(40, scavengerCarapace.PlusLightningResistance);
        var nightscape = hellfireItems.GenerateUnique(1078, 0x12345678, 16);
        Assert.Equal(1078, nightscape.UniqueId);
        Assert.Equal(11600, nightscape.Value);
        Assert.Equal(55, nightscape.ItemIndex);
        Assert.Equal(15, nightscape.ArmorClass);
        Assert.Equal(12, nightscape.Durability);
        Assert.Equal(12, nightscape.MaxDurability);
        Assert.Equal(0, nightscape.MinimumStrength);
        Assert.Equal(3, nightscape.EquipLocation);
        Assert.Equal(3, nightscape.PlusDexterity);
        Assert.Equal(20, nightscape.PlusFireResistance);
        Assert.Equal(20, nightscape.PlusLightningResistance);
        Assert.Equal(20, nightscape.PlusMagicResistance);
        Assert.Equal(-4, nightscape.PlusLight);
        Assert.NotEqual(0U, nightscape.Flags & 0x00400000U);
        var najsLightPlate = hellfireItems.GenerateUnique(1079, 0x12345678, 19);
        Assert.Equal(1079, najsLightPlate.UniqueId);
        Assert.Equal(78700, najsLightPlate.Value);
        Assert.Equal(68, najsLightPlate.ItemIndex);
        Assert.Equal(42, najsLightPlate.ArmorClass);
        Assert.Equal(75, najsLightPlate.Durability);
        Assert.Equal(75, najsLightPlate.MaxDurability);
        Assert.Equal(0, najsLightPlate.MinimumStrength);
        Assert.Equal(3, najsLightPlate.EquipLocation);
        Assert.Equal(5, najsLightPlate.PlusMagic);
        Assert.Equal(20 << 6, najsLightPlate.PlusMana);
        Assert.Equal(20, najsLightPlate.PlusFireResistance);
        Assert.Equal(20, najsLightPlate.PlusLightningResistance);
        Assert.Equal(20, najsLightPlate.PlusMagicResistance);
        Assert.Equal(1, najsLightPlate.SpellLevelAdd);
        var demonspikeCoat = hellfireItems.GenerateUnique(1080, 0x12345678, 25);
        Assert.Equal(1080, demonspikeCoat.UniqueId);
        Assert.Equal(251175, demonspikeCoat.Value);
        Assert.Equal(71, demonspikeCoat.ItemIndex);
        Assert.Equal(100, demonspikeCoat.ArmorClass);
        Assert.Equal(255, demonspikeCoat.Durability);
        Assert.Equal(255, demonspikeCoat.MaxDurability);
        Assert.Equal(90, demonspikeCoat.MinimumStrength);
        Assert.Equal(3, demonspikeCoat.EquipLocation);
        Assert.Equal(-6, demonspikeCoat.PlusGetHit);
        Assert.Equal(10, demonspikeCoat.PlusStrength);
        Assert.Equal(50, demonspikeCoat.PlusFireResistance);
        var theDeflector = hellfireItems.GenerateUnique(1081, 0x12345678, 1);
        Assert.Equal(1081, theDeflector.UniqueId);
        Assert.Equal(1500, theDeflector.Value);
        Assert.Equal(72, theDeflector.ItemIndex);
        Assert.Equal(7, theDeflector.ArmorClass);
        Assert.Equal(16, theDeflector.Durability);
        Assert.Equal(16, theDeflector.MaxDurability);
        Assert.Equal(0, theDeflector.MinimumStrength);
        Assert.Equal(1, theDeflector.EquipLocation);
        Assert.Equal(-20, theDeflector.PlusDamage);
        Assert.Equal(-5, theDeflector.PlusToHit);
        Assert.Equal(10, theDeflector.PlusFireResistance);
        Assert.Equal(10, theDeflector.PlusLightningResistance);
        Assert.Equal(10, theDeflector.PlusMagicResistance);
        var splitSkullShield = hellfireItems.GenerateUnique(1082, 0x12345678, 1);
        Assert.Equal(1082, splitSkullShield.UniqueId);
        Assert.Equal(2025, splitSkullShield.Value);
        Assert.Equal(72, splitSkullShield.ItemIndex);
        Assert.Equal(10, splitSkullShield.ArmorClass);
        Assert.Equal(15, splitSkullShield.Durability);
        Assert.Equal(15, splitSkullShield.MaxDurability);
        Assert.Equal(0, splitSkullShield.MinimumStrength);
        Assert.Equal(1, splitSkullShield.EquipLocation);
        Assert.Equal(10 << 6, splitSkullShield.PlusHitPoints);
        Assert.Equal(2, splitSkullShield.PlusStrength);
        Assert.Equal(-1, splitSkullShield.PlusLight);
        var dragonsBreach = hellfireItems.GenerateUnique(1083, 0x12345678, 2);
        Assert.Equal(1083, dragonsBreach.UniqueId);
        Assert.Equal(19200, dragonsBreach.Value);
        Assert.Equal(75, dragonsBreach.ItemIndex);
        Assert.Equal(20, dragonsBreach.ArmorClass);
        Assert.Equal(255, dragonsBreach.Durability);
        Assert.Equal(255, dragonsBreach.MaxDurability);
        Assert.Equal(50, dragonsBreach.MinimumStrength);
        Assert.Equal(1, dragonsBreach.EquipLocation);
        Assert.Equal(25, dragonsBreach.PlusFireResistance);
        Assert.Equal(5, dragonsBreach.PlusStrength);
        Assert.Equal(-5, dragonsBreach.PlusMagic);
        var blackoakShield = hellfireItems.GenerateUnique(1084, 0x12345678, 4);
        Assert.Equal(1084, blackoakShield.UniqueId);
        Assert.Equal(5725, blackoakShield.Value);
        Assert.Equal(73, blackoakShield.ItemIndex);
        Assert.Equal(18, blackoakShield.ArmorClass);
        Assert.Equal(60, blackoakShield.Durability);
        Assert.Equal(60, blackoakShield.MaxDurability);
        Assert.Equal(25, blackoakShield.MinimumStrength);
        Assert.Equal(1, blackoakShield.EquipLocation);
        Assert.Equal(10, blackoakShield.PlusDexterity);
        Assert.Equal(-10, blackoakShield.PlusVitality);
        Assert.Equal(-1, blackoakShield.PlusLight);
        var holyDefender = hellfireItems.GenerateUnique(1085, 0x12345678, 10);
        Assert.Equal(1085, holyDefender.UniqueId);
        Assert.Equal(13800, holyDefender.Value);
        Assert.Equal(74, holyDefender.ItemIndex);
        Assert.Equal(15, holyDefender.ArmorClass);
        Assert.Equal(96, holyDefender.Durability);
        Assert.Equal(96, holyDefender.MaxDurability);
        Assert.Equal(40, holyDefender.MinimumStrength);
        Assert.Equal(1, holyDefender.EquipLocation);
        Assert.Equal(-2, holyDefender.PlusGetHit);
        Assert.Equal(20, holyDefender.PlusFireResistance);
        Assert.NotEqual(0U, holyDefender.Flags & 0x01000000U);
        var stormshield = hellfireItems.GenerateUnique(1086, 0x12345678, 24);
        Assert.Equal(1086, stormshield.UniqueId);
        Assert.Equal(49000, stormshield.Value);
        Assert.Equal(77, stormshield.ItemIndex);
        Assert.Equal(40, stormshield.ArmorClass);
        Assert.Equal(255, stormshield.Durability);
        Assert.Equal(255, stormshield.MaxDurability);
        Assert.Equal(80, stormshield.MinimumStrength);
        Assert.Equal(1, stormshield.EquipLocation);
        Assert.Equal(4, stormshield.PlusGetHit);
        Assert.Equal(10, stormshield.PlusStrength);
        Assert.Equal(50, stormshield.PlusLightningResistance);
        Assert.NotEqual(0U, stormshield.Flags & 0x01000000U);
        var bramble = hellfireItems.GenerateUnique(1087, 0x12345678, 1);
        Assert.Equal(1087, bramble.UniqueId);
        Assert.Equal(1000, bramble.Value);
        Assert.Equal(157, bramble.ItemIndex);
        Assert.Equal(0, bramble.ArmorClass);
        Assert.Equal(5, bramble.EquipLocation);
        Assert.Equal(-2, bramble.PlusStrength);
        Assert.Equal(-2, bramble.PlusMagic);
        Assert.Equal(-2, bramble.PlusDexterity);
        Assert.Equal(-2, bramble.PlusVitality);
        Assert.Equal(3, bramble.PlusDamageModifier);
        Assert.Equal(10 << 6, bramble.PlusMana);
        var ringOfRegha = hellfireItems.GenerateUnique(1088, 0x12345678, 1);
        Assert.Equal(1088, ringOfRegha.UniqueId);
        Assert.Equal(4175, ringOfRegha.Value);
        Assert.Equal(157, ringOfRegha.ItemIndex);
        Assert.Equal(5, ringOfRegha.EquipLocation);
        Assert.Equal(10, ringOfRegha.PlusMagic);
        Assert.Equal(10, ringOfRegha.PlusMagicResistance);
        Assert.Equal(1, ringOfRegha.PlusLight);
        Assert.Equal(-3, ringOfRegha.PlusStrength);
        Assert.Equal(-3, ringOfRegha.PlusDexterity);
        var theBleeder = hellfireItems.GenerateUnique(1089, 0x12345678, 2);
        Assert.Equal(1089, theBleeder.UniqueId);
        Assert.Equal(8500, theBleeder.Value);
        Assert.Equal(157, theBleeder.ItemIndex);
        Assert.Equal(5, theBleeder.EquipLocation);
        Assert.Equal(20, theBleeder.PlusMagicResistance);
        Assert.Equal(30 << 6, theBleeder.PlusMana);
        Assert.Equal(-(10 << 6), theBleeder.PlusHitPoints);
        var constrictingRing = hellfireItems.GenerateUnique(1090, 0x12345678, 5);
        Assert.Equal(1090, constrictingRing.UniqueId);
        Assert.Equal(62000, constrictingRing.Value);
        Assert.Equal(157, constrictingRing.ItemIndex);
        Assert.Equal(5, constrictingRing.EquipLocation);
        Assert.Equal(75, constrictingRing.PlusFireResistance);
        Assert.Equal(75, constrictingRing.PlusLightningResistance);
        Assert.Equal(75, constrictingRing.PlusMagicResistance);
        Assert.NotEqual(0U, constrictingRing.Flags & 0x00000040U);
        var ringOfEngagement = hellfireItems.GenerateUnique(1091, 0x12345678, 11);
        Assert.Equal(1091, ringOfEngagement.UniqueId);
        Assert.Equal(12476, ringOfEngagement.Value);
        Assert.Equal(157, ringOfEngagement.ItemIndex);
        Assert.Equal(5, ringOfEngagement.EquipLocation);
        Assert.Equal(5, ringOfEngagement.ArmorClass);
        Assert.InRange(ringOfEngagement.PlusGetHit, -2, -1);
        Assert.Equal(2, ringOfEngagement.PlusEnemyArmorClass);
        Assert.NotEqual(0U, ringOfEngagement.Flags & 0x04000000U);
        var giantsKnuckle = hellfireItems.GenerateUnique(1092, 0x12345678, 8);
        Assert.Equal(1092, giantsKnuckle.UniqueId);
        Assert.Equal(8000, giantsKnuckle.Value);
        Assert.Equal(157, giantsKnuckle.ItemIndex);
        Assert.Equal(5, giantsKnuckle.EquipLocation);
        Assert.Equal(60, giantsKnuckle.PlusStrength);
        Assert.Equal(-30, giantsKnuckle.PlusDexterity);
        var mercurialRing = hellfireItems.GenerateUnique(1093, 0x12345678, 8);
        Assert.Equal(1093, mercurialRing.UniqueId);
        Assert.Equal(8000, mercurialRing.Value);
        Assert.Equal(157, mercurialRing.ItemIndex);
        Assert.Equal(5, mercurialRing.EquipLocation);
        Assert.Equal(60, mercurialRing.PlusDexterity);
        Assert.Equal(-30, mercurialRing.PlusStrength);
        var xorinesRing = hellfireItems.GenerateUnique(1094, 0x12345678, 8);
        Assert.Equal(1094, xorinesRing.UniqueId);
        Assert.Equal(8000, xorinesRing.Value);
        Assert.Equal(157, xorinesRing.ItemIndex);
        Assert.Equal(5, xorinesRing.EquipLocation);
        Assert.Equal(60, xorinesRing.PlusMagic);
        Assert.Equal(-30, xorinesRing.PlusStrength);
        var kariksRing = hellfireItems.GenerateUnique(1095, 0x12345678, 8);
        Assert.Equal(1095, kariksRing.UniqueId);
        Assert.Equal(8000, kariksRing.Value);
        Assert.Equal(157, kariksRing.ItemIndex);
        Assert.Equal(5, kariksRing.EquipLocation);
        Assert.Equal(60, kariksRing.PlusVitality);
        Assert.Equal(-30, kariksRing.PlusMagic);
        var ringOfMagma = hellfireItems.GenerateUnique(1096, 0x12345678, 8);
        Assert.Equal(1096, ringOfMagma.UniqueId);
        Assert.Equal(8000, ringOfMagma.Value);
        Assert.Equal(157, ringOfMagma.ItemIndex);
        Assert.Equal(5, ringOfMagma.EquipLocation);
        Assert.Equal(60, ringOfMagma.PlusFireResistance);
        Assert.Equal(-30, ringOfMagma.PlusLightningResistance);
        Assert.Equal(-30, ringOfMagma.PlusMagicResistance);
        var ringOfTheMystics = hellfireItems.GenerateUnique(1097, 0x12345678, 8);
        Assert.Equal(1097, ringOfTheMystics.UniqueId);
        Assert.Equal(8000, ringOfTheMystics.Value);
        Assert.Equal(157, ringOfTheMystics.ItemIndex);
        Assert.Equal(5, ringOfTheMystics.EquipLocation);
        Assert.Equal(60, ringOfTheMystics.PlusMagicResistance);
        Assert.Equal(-30, ringOfTheMystics.PlusFireResistance);
        Assert.Equal(-30, ringOfTheMystics.PlusLightningResistance);
        var ringOfThunder = hellfireItems.GenerateUnique(1098, 0x12345678, 8);
        Assert.Equal(1098, ringOfThunder.UniqueId);
        Assert.Equal(8000, ringOfThunder.Value);
        Assert.Equal(157, ringOfThunder.ItemIndex);
        Assert.Equal(5, ringOfThunder.EquipLocation);
        Assert.Equal(60, ringOfThunder.PlusLightningResistance);
        Assert.Equal(-30, ringOfThunder.PlusFireResistance);
        Assert.Equal(-30, ringOfThunder.PlusMagicResistance);
        var amuletOfWarding = hellfireItems.GenerateUnique(1099, 0x12345678, 12);
        Assert.Equal(1099, amuletOfWarding.UniqueId);
        Assert.Equal(30000, amuletOfWarding.Value);
        Assert.Equal(160, amuletOfWarding.ItemIndex);
        Assert.Equal(6, amuletOfWarding.EquipLocation);
        Assert.Equal(27, amuletOfWarding.MiscId);
        Assert.Equal(40, amuletOfWarding.PlusFireResistance);
        Assert.Equal(40, amuletOfWarding.PlusLightningResistance);
        Assert.Equal(40, amuletOfWarding.PlusMagicResistance);
        Assert.Equal(-6400, amuletOfWarding.PlusHitPoints);
        var gnatSting = hellfireItems.GenerateUnique(1100, 0x12345678, 15);
        Assert.Equal(1100, gnatSting.UniqueId);
        Assert.Equal(30000, gnatSting.Value);
        Assert.Equal(145, gnatSting.ItemIndex);
        Assert.Equal(2, gnatSting.EquipLocation);
        Assert.Equal(1, gnatSting.MinDamage);
        Assert.Equal(2, gnatSting.MaxDamage);
        Assert.Equal(255, gnatSting.Durability);
        Assert.Equal(255, gnatSting.MaxDurability);
        Assert.NotEqual(0U, gnatSting.Flags & 0x00000200U);
        Assert.NotEqual(0U, gnatSting.Flags & 0x00020000U);
        var flambeau = hellfireItems.GenerateUnique(1101, 0x12345678, 11);
        Assert.Equal(1101, flambeau.UniqueId);
        Assert.Equal(30000, flambeau.Value);
        Assert.Equal(147, flambeau.ItemIndex);
        Assert.Equal(2, flambeau.EquipLocation);
        Assert.Equal(0, flambeau.MinDamage);
        Assert.Equal(0, flambeau.MaxDamage);
        Assert.Equal(15, flambeau.FireMinDamage);
        Assert.Equal(20, flambeau.FireMaxDamage);
        Assert.Equal(255, flambeau.Durability);
        Assert.Equal(255, flambeau.MaxDurability);
        Assert.NotEqual(0U, flambeau.Flags & 0x00000008U);
        Assert.NotEqual(0U, flambeau.Flags & 0x02000000U);
        var armorOfGloom = hellfireItems.GenerateUnique(1102, 0x12345678, 25);
        Assert.Equal(1102, armorOfGloom.UniqueId);
        Assert.Equal(200000, armorOfGloom.Value);
        Assert.Equal(71, armorOfGloom.ItemIndex);
        Assert.Equal(3, armorOfGloom.EquipLocation);
        Assert.Equal(225, armorOfGloom.ArmorClass);
        Assert.Equal(0, armorOfGloom.MinimumStrength);
        Assert.Equal(-2, armorOfGloom.PlusLight);
        Assert.Equal(90, armorOfGloom.Durability);
        Assert.Equal(90, armorOfGloom.MaxDurability);
        Assert.NotEqual(0U, armorOfGloom.Flags & 0x80000000U);
        var blitzen = hellfireItems.GenerateUnique(1103, 0x12345678, 13);
        Assert.Equal(1103, blitzen.UniqueId);
        Assert.Equal(30000, blitzen.Value);
        Assert.Equal(147, blitzen.ItemIndex);
        Assert.Equal(2, blitzen.EquipLocation);
        Assert.Equal(0, blitzen.MinDamage);
        Assert.Equal(0, blitzen.MaxDamage);
        Assert.Equal(10, blitzen.FireMinDamage);
        Assert.Equal(15, blitzen.FireMaxDamage);
        Assert.Equal(1, blitzen.LightningMinDamage);
        Assert.Equal(0, blitzen.LightningMaxDamage);
        Assert.Equal(255, blitzen.Durability);
        Assert.Equal(255, blitzen.MaxDurability);
        Assert.NotEqual(0U, blitzen.Flags & 0x02000000U);
        Assert.NotEqual(0U, blitzen.Flags & 0x00000008U);
        var thunderclap = hellfireItems.GenerateUnique(1104, 0x12345678, 13);
        Assert.Equal(1104, thunderclap.UniqueId);
        Assert.Equal(30000, thunderclap.Value);
        Assert.Equal(139, thunderclap.ItemIndex);
        Assert.Equal(1, thunderclap.EquipLocation);
        Assert.Equal(20, thunderclap.PlusStrength);
        Assert.Equal(30, thunderclap.PlusLightningResistance);
        Assert.Equal(2, thunderclap.PlusLight);
        Assert.Equal(3, thunderclap.FireMinDamage);
        Assert.Equal(6, thunderclap.FireMaxDamage);
        Assert.Equal(2, thunderclap.LightningMinDamage);
        Assert.Equal(0, thunderclap.LightningMaxDamage);
        Assert.Equal(255, thunderclap.Durability);
        Assert.Equal(255, thunderclap.MaxDurability);
        Assert.NotEqual(0U, thunderclap.Flags & 0x00000020U);
        Assert.NotEqual(0U, thunderclap.Flags & 0x00000010U);
        var shirotachi = hellfireItems.GenerateUnique(1105, 0x12345678, 21);
        Assert.Equal(1105, shirotachi.UniqueId);
        Assert.Equal(36000, shirotachi.Value);
        Assert.Equal(130, shirotachi.ItemIndex);
        Assert.Equal(1, shirotachi.EquipLocation);
        Assert.Equal(10, shirotachi.MinDamage);
        Assert.Equal(20, shirotachi.MaxDamage);
        Assert.Equal(6, shirotachi.LightningMinDamage);
        Assert.Equal(6, shirotachi.LightningMaxDamage);
        Assert.Equal(2, shirotachi.PlusEnemyArmorClass);
        Assert.Equal(100, shirotachi.Durability);
        Assert.Equal(100, shirotachi.MaxDurability);
        Assert.NotEqual(0U, shirotachi.Flags & 0x00100000U);
        Assert.NotEqual(0U, shirotachi.Flags & 0x00000020U);
        var eaterOfSouls = hellfireItems.GenerateUnique(1106, 0x12345678, 23);
        Assert.Equal(1106, eaterOfSouls.UniqueId);
        Assert.Equal(42000, eaterOfSouls.Value);
        Assert.Equal(129, eaterOfSouls.ItemIndex);
        Assert.Equal(1, eaterOfSouls.EquipLocation);
        Assert.Equal(8, eaterOfSouls.MinDamage);
        Assert.Equal(16, eaterOfSouls.MaxDamage);
        Assert.Equal(3200, eaterOfSouls.PlusHitPoints);
        Assert.Equal(255, eaterOfSouls.Durability);
        Assert.Equal(255, eaterOfSouls.MaxDurability);
        Assert.NotEqual(0U, eaterOfSouls.Flags & 0x00010000U);
        Assert.NotEqual(0U, eaterOfSouls.Flags & 0x00004000U);
        Assert.NotEqual(0U, eaterOfSouls.Flags & 0x00000040U);
        var diamondedge = hellfireItems.GenerateUnique(1107, 0x12345678, 17);
        Assert.Equal(1107, diamondedge.UniqueId);
        Assert.Equal(42000, diamondedge.Value);
        Assert.Equal(126, diamondedge.ItemIndex);
        Assert.Equal(1, diamondedge.EquipLocation);
        Assert.Equal(2, diamondedge.MinDamage);
        Assert.Equal(10, diamondedge.MaxDamage);
        Assert.Equal(10, diamondedge.ArmorClass);
        Assert.Equal(50, diamondedge.PlusToHit);
        Assert.Equal(100, diamondedge.PlusDamage);
        Assert.Equal(50, diamondedge.PlusLightningResistance);
        Assert.Equal(10, diamondedge.Durability);
        Assert.Equal(10, diamondedge.MaxDurability);
        var boneChainArmor = hellfireItems.GenerateUnique(1108, 0x12345678, 13);
        Assert.Equal(1108, boneChainArmor.UniqueId);
        Assert.Equal(36000, boneChainArmor.Value);
        Assert.Equal(64, boneChainArmor.ItemIndex);
        Assert.Equal(3, boneChainArmor.EquipLocation);
        Assert.Equal(40, boneChainArmor.ArmorClass);
        Assert.Equal(55, boneChainArmor.Durability);
        Assert.Equal(55, boneChainArmor.MaxDurability);
        Assert.Equal(64, boneChainArmor.HellfireDamageArmorFlags);
        var demonPlateArmor = hellfireItems.GenerateUnique(1109, 0x12345678, 25);
        Assert.Equal(1109, demonPlateArmor.UniqueId);
        Assert.Equal(80000, demonPlateArmor.Value);
        Assert.Equal(71, demonPlateArmor.ItemIndex);
        Assert.Equal(3, demonPlateArmor.EquipLocation);
        Assert.Equal(80, demonPlateArmor.ArmorClass);
        Assert.Equal(90, demonPlateArmor.Durability);
        Assert.Equal(90, demonPlateArmor.MaxDurability);
        Assert.Equal(32, demonPlateArmor.HellfireDamageArmorFlags);
        var acolytesAmulet = hellfireItems.GenerateUnique(1110, 0x12345678, 10);
        Assert.Equal(1110, acolytesAmulet.UniqueId);
        Assert.Equal(10000, acolytesAmulet.Value);
        Assert.Equal(160, acolytesAmulet.ItemIndex);
        Assert.Equal(6, acolytesAmulet.EquipLocation);
        Assert.Equal(3, acolytesAmulet.ItemClass);
        Assert.Equal(50, acolytesAmulet.ManaToLifePercent);
        Assert.Equal(0, acolytesAmulet.LifeToManaPercent);
        Assert.Equal(0, acolytesAmulet.PlusMana);
        Assert.Equal(0, acolytesAmulet.PlusHitPoints);
        var gladiatorsRing = hellfireItems.GenerateUnique(1111, 0x12345678, 10);
        Assert.Equal(1111, gladiatorsRing.UniqueId);
        Assert.Equal(10000, gladiatorsRing.Value);
        Assert.Equal(157, gladiatorsRing.ItemIndex);
        Assert.Equal(5, gladiatorsRing.EquipLocation);
        Assert.Equal(3, gladiatorsRing.ItemClass);
        Assert.Equal(0, gladiatorsRing.ManaToLifePercent);
        Assert.Equal(40, gladiatorsRing.LifeToManaPercent);
        Assert.Equal(0, gladiatorsRing.PlusMana);
        Assert.Equal(0, gladiatorsRing.PlusHitPoints);
        var hellfireSorcerer = activation.PlayerLoadouts.CreateTemplate(
            "hellfire-sorcerer",
            2,
            null,
            null,
            null,
            activation.DefaultStartingLevelId);
        var startingStaff = Assert.Single(
            hellfireSorcerer.Equipment,
            item => item.Slot == 4);
        Assert.Equal(1, startingStaff.State.SpellId);
        Assert.Equal(23, startingStaff.State.MiscId);
        Assert.Equal(166, startingStaff.State.ItemIndex);
        Assert.True(startingStaff.State.StatFlag);
        Assert.Equal(2, composed.Manifest.Packs.Count);
    }

    [Fact]
    public void BaseContentManifestAndAuthoritativeCatalogsLoadDeterministically()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var firstManifest = new ContentManifest("base", "1", [ContentPackLoader.LoadDirectory("base", "1", contentRoot)]);
        var secondManifest = new ContentManifest("base", "1", [ContentPackLoader.LoadDirectory("base", "1", contentRoot)]);

        Assert.Equal(firstManifest.Sha256, secondManifest.Sha256);
        Assert.Equal(64, firstManifest.Sha256.Length);
        Assert.True(firstManifest.Packs.Single().Tables.Count >= 16);
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "content_ids.tsv")));
        var nativeItemEffects = AuthoritativeNativeItemEffectCatalog.LoadTsv(
            "native_item_effects.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "native_item_effects.tsv")));
        Assert.Equal(27, nativeItemEffects.Definitions.Count);
        Assert.Equal(512U, nativeItemEffects.GetRequiredFlag("MULTIPLE ARROWS"));
        var nativeItemHellfireEffects = AuthoritativeNativeItemHellfireEffectCatalog.LoadTsv(
            "native_item_hellfire_effects.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "native_item_hellfire_effects.tsv")));
        Assert.Equal(9, nativeItemHellfireEffects.Definitions.Count);
        Assert.Equal(32, nativeItemHellfireEffects.GetRequiredFlag("ACDEMON"));
        var nativeItemTags = AuthoritativeNativeItemTagCatalog.LoadTsv(
            "native_item_tags.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "native_item_tags.tsv")));
        Assert.Equal(14, nativeItemTags.Definitions.Count);
        Assert.Contains("Weapon", nativeItemTags.GetRequiredTags("SWORD"));
        Assert.Equal(5U, stableIds.Resolve("ability", "identify"));
        Assert.Equal(26U, stableIds.Resolve("ability", "repair"));
        Assert.Equal(27U, stableIds.Resolve("ability", "recharge"));
        Assert.Equal(28U, stableIds.Resolve("ability", "trap_disarm"));
        Assert.Equal(3001U, stableIds.Resolve("quest", "magic_rock"));
        Assert.Equal(3016U, stableIds.Resolve("quest", "lazarus"));
        Assert.Equal(5U, stableIds.Resolve("spell", "holy_bolt"));
        Assert.Equal(30U, stableIds.Resolve("spell", "charged_bolt"));
        Assert.Equal(46U, stableIds.Resolve("spell", "search"));
        Assert.Equal(36U, stableIds.Resolve("spell", "blood_star"));
        Assert.Equal(21U, stableIds.Resolve("spell", "inferno"));
        Assert.Equal(25U, stableIds.Resolve("spell", "apocalypse"));

        var module = new DiabloModule();
        var registry = new GameplayModuleRegistry();
        registry.Load(
            [module],
            new GameplayModuleContext(
                firstManifest,
                new DelegateAuthoritativeClock(() => 0)));
        var activated = registry.GetRequiredCapability<IDiabloContentBootstrap>()
            .Activate(ComposedContent.Compose(firstManifest));
        Assert.Equal(0U, activated.Simulation.CombatRules.TownLevelId);
        Assert.Equal(10U, activated.AdriaStoreId);
        var runtime = activated.Simulation.CreateRuntime(
            activated.PlayerLoadouts.CreateTemplate(
                "player",
                activated.PlayerClasses.Default.ClassId,
                null,
                null,
                null,
                activated.DefaultStartingLevelId));
        var initial = runtime.Capture(
            "player",
            0,
            player => activated.Simulation.CaptureSnapshot(player, 0));
        var opened = runtime.Execute(
            "player",
            1,
            (player, _) => new SimulationStepResult<DiabloIntentResult, DiabloGameplayEvent>(
                activated.Simulation.Execute(player, new DiabloOpenStoreIntent(1), 1)));
        var snapshot = runtime.Capture(
            "player",
            1,
            player => activated.Simulation.CaptureSnapshot(player, 1));
        var moved = runtime.Execute(
            "player",
            2,
            (player, participants) =>
                new SimulationStepResult<DiabloWorldIntentResult, DiabloGameplayEvent>(
                    activated.Simulation.Execute(
                        player,
                        new DiabloMoveIntent(0, 1),
                        2,
                        participants)));
        var moving = runtime.Read(
            "player",
            player => activated.Simulation.CaptureSnapshot(player, 2));

        Assert.True(opened.Succeeded);
        Assert.True(moved.Succeeded);
        Assert.Equal((0, 0), (moving.Player.PositionX, moving.Player.PositionY));
        Assert.Equal(DiabloPlayerCombatMode.WalkSouthwards, moving.Player.CombatMode);
        Assert.Equal(10UL, moving.Player.CombatModeEndTick);
        var moveAction = Assert.IsType<AuthoritativePlayerActionState>(
            moving.Player.CurrentAction);
        Assert.Equal(DiabloPlayerActionKind.Move, moveAction.Kind);
        Assert.Equal((0, 1), (moveAction.DirectionX, moveAction.DirectionY));
        Assert.Equal(6, activated.PlayerClasses.Definitions.Count);
        Assert.Equal(6, activated.PlayerAnimations.Definitions.Count);
        Assert.Same(activated.PlayerAnimations, activated.Simulation.PlayerAnimations);
        Assert.Equal("warrior", activated.PlayerClasses.Default.Symbol);
        Assert.Equal(0U, snapshot.Player.CharacterClassId);
        Assert.Equal(30, snapshot.Player.Attributes.Strength.Base);
        Assert.Equal(10, snapshot.Player.Attributes.Magic.Base);
        Assert.Equal(20, snapshot.Player.Attributes.Dexterity.Base);
        Assert.Equal(25, snapshot.Player.Attributes.Vitality.Base);
        Assert.Equal(4480, initial.Player.Life);
        Assert.Equal(4480, initial.Player.LifeMaximum);
        Assert.Equal(640, initial.Player.Mana);
        Assert.Equal(640, initial.Player.ManaMaximum);
        Assert.Equal(100U, initial.Player.Gold);
        Assert.Equal(26U, initial.Player.AbilitySpellId);
        Assert.Equal(DiabloPlayerAbilityKind.Repair, initial.Player.AbilityKind);
        Assert.Empty(initial.Player.MemorizedSpells);
        Assert.Equal(2, initial.Player.Equipment.Count);
        Assert.Equal(2, initial.Player.Belt.Count);
        Assert.Equal(2, initial.Player.Inventory.Count);
        var startingGold = Assert.Single(
            initial.Player.Inventory,
            item => item.State.ItemType == activated.Simulation.CombatRules.DeathGoldItemType);
        Assert.Equal(100U, startingGold.Price);
        Assert.Equal(1, initial.Player.InventoryGrid[30]);
        Assert.Equal(4U, activated.StoreRules.GetSalePrice(new OwnedStoreItem(
            0,
            0,
            1,
            0,
            0,
            AuthoritativeItemState.Empty with { Value = 16 })));
        Assert.NotEmpty(snapshot.Monsters);
        var store = Assert.IsType<DiabloStoreState>(snapshot.ActiveStore);
        Assert.Equal(2, store.Items.Count);
        var firstStoreItem = store.Items[0];
        Assert.Equal(42U, firstStoreItem.ItemSeed);
        Assert.Equal(1, firstStoreItem.State.ItemIndex);
        Assert.Equal(4, firstStoreItem.State.MinDamage);
        Assert.Equal(8, firstStoreItem.State.MaxDamage);
        Assert.True(firstStoreItem.State.Identified);
        var secondStoreItem = store.Items[1];
        Assert.Equal(43U, secondStoreItem.ItemSeed);
        Assert.Equal(2, secondStoreItem.State.ItemIndex);
        Assert.Equal(5, secondStoreItem.State.ArmorClass);
        Assert.Equal(5, activated.Simulation.Items!.GetRequired(2).MinArmorClass);
        Assert.Equal(5, activated.Simulation.Items.GetRequired(2).MaxArmorClass);
        var legacyItems = AuthoritativeItemCatalog.LoadLegacyTsv("itemdat.tsv", File.ReadAllText(Path.Combine(contentRoot, "itemdat.tsv")));
        Assert.NotEqual(0U, legacyItems.ResolveLegacySymbol("IDI_WARRIOR"));
        Assert.Equal(6, legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_WARRIOR"), 0x12345678).MaxDamage);

        var invalidClass = snapshot with {
            Player = snapshot.Player with { CharacterClassId = 99 },
        };
        var livePlayer = runtime.Read("player", player => player);
        Assert.Throws<InvalidDataException>(() =>
            activated.Simulation.RestoreSnapshot(livePlayer, invalidClass));
        Assert.Equal(0U, livePlayer.CharacterClassId);
    }

    [Fact]
    public void BaseContentActivationRejectsAMissingDirectStartingSpell()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadDirectory("base", "1", contentRoot);
        var spells = basePack.Tables.Single(table => table.SourcePath == "spells.tsv");
        var spellBooks = basePack.Tables.Single(table => table.SourcePath == "spell_books.tsv");
        var withoutChargedBolt = TsvTable.Parse(
            spells.SourcePath,
            string.Join(
                '\n',
                spells.ToCanonicalTsv()
                    .Split('\n')
                    .Where(line => !line.StartsWith("30\t", StringComparison.Ordinal))));
        var withoutChargedBoltBook = TsvTable.Parse(
            spellBooks.SourcePath,
            string.Join(
                '\n',
                spellBooks.ToCanonicalTsv()
                    .Split('\n')
                    .Where(line => !line.StartsWith("30\t", StringComparison.Ordinal))));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == spells.SourcePath
                    ? withoutChargedBolt
                    : table.SourcePath == spellBooks.SourcePath
                        ? withoutChargedBoltBook
                    : table)
                .ToArray());
        var incompleteManifest = new ContentManifest("base", "1", [incompletePack]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(incompleteManifest)));

        Assert.Contains("charged item 104 spell 30", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BaseContentGenerationAndServicePricingCoverTheShippedSources()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var rules = AuthoritativeItemGenerationRules.LoadTsv(
            "item_generation.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_generation.tsv")));
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_prefixes.tsv")),
            "item_suffixes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_suffixes.tsv")),
            rules);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "items.tsv")),
            affixes);
        var uniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "unique_items.tsv")));
        items.AttachUniqueCatalog(uniques);

        var weapon = items.Generate(1, 42, 10);
        Assert.Equal(weapon, items.Generate(1, 42, 10));
        Assert.Equal(1, weapon.ItemIndex);
        Assert.Equal(4, weapon.MinDamage);
        Assert.Equal(8, weapon.MaxDamage);
        var unique = items.GenerateUnique(1001, 42, 1);
        Assert.Equal(1001, unique.UniqueId);
        Assert.Equal(3650, unique.Value);
        Assert.Equal(10, unique.PlusToHit);
        Assert.Equal(1, unique.FireMinDamage);
        Assert.Equal(10, unique.FireMaxDamage);

        var legacyItems = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "itemdat.tsv")));
        var warrior = legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_WARRIOR"), 0x12345678);
        var cleaver = legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_CLEAVER"), 0x12345678);
        Assert.Equal(6, warrior.MaxDamage);
        Assert.Equal(24, cleaver.MaxDamage);
        Assert.Equal(2, cleaver.ItemType);

        var pricing = StoreServicePricing.LoadTsv(
            "store_services.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "store_services.tsv")));
        Assert.Equal(4, pricing.SaleDivisor);
        Assert.Equal(2, pricing.NormalRepairDivisor);
        Assert.Equal(30, pricing.MagicalRepairPercent);
        Assert.Equal(100U, pricing.IdentificationPrice);
        Assert.Equal(64, pricing.ManaChunk);
    }

    [Fact]
    public void BaseContentAffixesMatchNativeDevilutionTables()
    {
        var repositoryRoot = FindRepositoryRoot();
        var baseRoot = Path.Combine(repositoryRoot, "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(baseRoot);

        foreach (var sourcePath in new[] { "item_prefixes.tsv", "item_suffixes.tsv" }) {
            var native = TsvTable.Parse(
                sourcePath,
                File.ReadAllText(Path.Combine(repositoryRoot, "assets", "txtdata", "items", sourcePath)));
            var external = basePack.Tables.Single(table => table.SourcePath == sourcePath);

            Assert.Equal(native.Columns, external.Columns);
            Assert.Equal(native.Rows.Count, external.Rows.Count);
            for (var rowIndex = 0; rowIndex < native.Rows.Count; rowIndex++) {
                foreach (var column in native.Columns)
                    Assert.Equal(native.Rows[rowIndex][column], external.Rows[rowIndex][column]);
            }
        }
    }

    [Fact]
    public void BaseLegacyItemDataMatchesNativeCompatibilityTable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var baseRoot = Path.Combine(repositoryRoot, "server", "content", "base");
        var basePack = ContentPackLoader.LoadManifestDirectory(baseRoot);
        var native = TsvTable.Parse(
            "assets/txtdata/items/itemdat.tsv",
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "assets",
                "txtdata",
                "items",
                "itemdat.tsv")));
        var external = basePack.Tables.Single(table => table.SourcePath == "itemdat.tsv");

        Assert.Equal(native.Columns, external.Columns);
        Assert.Equal(native.Rows.Count, external.Rows.Count);
        for (var rowIndex = 0; rowIndex < native.Rows.Count; rowIndex++)
            foreach (var column in native.Columns)
                Assert.Equal(native.Rows[rowIndex][column], external.Rows[rowIndex][column]);
    }

    [Fact]
    public void HellfireAffixesMatchNativeVariantTables()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));

        foreach (var (sourcePath, keyColumns) in new[] {
            ("item_prefixes.tsv", new[] { "name", "power", "power.value1", "power.value2", "minLevel" }),
            ("item_suffixes.tsv", new[] { "name", "power", "minLevel" }),
        }) {
            var native = TsvTable.Parse(
                $"native/{sourcePath}",
                File.ReadAllText(Path.Combine(
                    repositoryRoot,
                    "mods",
                    "Hellfire",
                    "txtdata",
                    "items",
                    sourcePath)));
            var external = composed.GetRequiredTable(sourcePath);
            Assert.Equal(native.Columns, external.Columns);

            static string Key(TsvRow row, IReadOnlyList<string> columns)
                => string.Join('\u001F', columns.Select(row.Required));

            var nativeRows = native.Rows.ToDictionary(
                row => Key(row, keyColumns),
                StringComparer.Ordinal);
            var externalRows = external.Rows.ToDictionary(
                row => Key(row, keyColumns),
                StringComparer.Ordinal);
            Assert.Equal(
                nativeRows.Keys.Order(StringComparer.Ordinal),
                externalRows.Keys.Order(StringComparer.Ordinal));
            foreach (var key in nativeRows.Keys)
                foreach (var column in native.Columns)
                    Assert.Equal(nativeRows[key][column], externalRows[key][column]);
        }
    }

    [Fact]
    public void NormalizedAffixesPreserveLegacyRowsAndNativeGenerationMetadata()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));

        var legacy = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            composed.GetRequiredTable("item_prefixes.tsv").ToCanonicalTsv(),
            "item_suffixes.tsv",
            composed.GetRequiredTable("item_suffixes.tsv").ToCanonicalTsv());
        var normalized = AuthoritativeItemAffixCatalog.LoadTsv(
            "item_affixes.tsv",
            composed.GetRequiredTable("item_affixes.tsv").ToCanonicalTsv());

        Assert.True(normalized.UsesLegacyRandom);
        Assert.Equal(legacy.Prefixes.Count, normalized.Prefixes.Count);
        Assert.Equal(legacy.Suffixes.Count, normalized.Suffixes.Count);
        foreach (var (expected, actual) in legacy.Prefixes.Zip(normalized.Prefixes))
            AssertEquivalent(expected, actual);
        foreach (var (expected, actual) in legacy.Suffixes.Zip(normalized.Suffixes))
            AssertEquivalent(expected, actual);

        static void AssertEquivalent(
            AuthoritativeItemAffix expected,
            AuthoritativeItemAffix actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Power, actual.Power);
            Assert.Equal(expected.Value1, actual.Value1);
            Assert.Equal(expected.Value2, actual.Value2);
            Assert.Equal(expected.MinimumLevel, actual.MinimumLevel);
            Assert.True(expected.ItemTags.SetEquals(actual.ItemTags));
            Assert.Equal(expected.Chance, actual.Chance);
            Assert.Equal(expected.Useful, actual.Useful);
            Assert.Equal(expected.Alignment, actual.Alignment);
            Assert.Equal(expected.MinimumValue, actual.MinimumValue);
            Assert.Equal(expected.MaximumValue, actual.MaximumValue);
            Assert.Equal(expected.ValueMultiplier, actual.ValueMultiplier);
            Assert.Equal(expected.HasLegacyValueRange, actual.HasLegacyValueRange);
        }
    }

    [Fact]
    public void HellfireOverlayExternalizesTheCrystallineWeaponPrefix()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));

        var prefix = Assert.Single(
            composed.GetRequiredTable("item_prefixes.tsv").Rows,
            row => row.Required("name") == "Crystalline");

        Assert.Equal("CRYSTALLINE", prefix.Required("power"));
        Assert.Equal(30, prefix.RequiredInt32("power.value1"));
        Assert.Equal(70, prefix.RequiredInt32("power.value2"));
        Assert.Equal("Weapon", prefix.Required("itemTypes"));

        var jesters = Assert.Single(
            composed.GetRequiredTable("item_prefixes.tsv").Rows,
            row => row.Required("name") == "Jester's");

        Assert.Equal("JESTERS", jesters.Required("power"));
        Assert.Equal(1, jesters.RequiredInt32("power.value1"));
        Assert.Equal(1, jesters.RequiredInt32("power.value2"));
        Assert.Equal("Weapon", jesters.Required("itemTypes"));
    }

    [Fact]
    public void HellfireOverlayExternalizesTheDoppelgangersWeaponStaffPrefix()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));

        var doppelganger = Assert.Single(
            composed.GetRequiredTable("item_prefixes.tsv").Rows,
            row => row.Required("name") == "Doppelganger's");

        Assert.Equal("DOPPELGANGER", doppelganger.Required("power"));
        Assert.Equal(81, doppelganger.RequiredInt32("power.value1"));
        Assert.Equal(95, doppelganger.RequiredInt32("power.value2"));
        Assert.Equal("Weapon,Staff", doppelganger.Required("itemTypes"));
    }

    [Fact]
    public void HellfireOverlayExternalizesSpecialItemSuffixes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));

        var suffixes = composed.GetRequiredTable("item_suffixes.tsv").Rows
            .Where(row => row.Required("name") is "devastation" or "decay" or "peril")
            .ToArray();

        Assert.Equal(3, suffixes.Length);
        Assert.Equal(
            new[] { "DEVASTATION", "DECAY", "PERIL" },
            suffixes.Select(row => row.Required("power")).ToArray());
        Assert.All(suffixes, row => Assert.Equal("Weapon,Staff,Bow", row.Required("itemTypes")));
    }

    [Fact]
    public void HellfireOverlayExternalizesTargetAccuracySuffixRanges()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));

        var suffixes = composed.GetRequiredTable("item_suffixes.tsv").Rows
            .Where(row => row.Required("name") is "piercing" or "puncturing" or "bashing")
            .ToDictionary(row => row.Required("name"), StringComparer.Ordinal);

        Assert.Equal((1, 1), (
            suffixes["piercing"].RequiredInt32("power.value1"),
            suffixes["piercing"].RequiredInt32("power.value2")));
        Assert.Equal((2, 2), (
            suffixes["puncturing"].RequiredInt32("power.value1"),
            suffixes["puncturing"].RequiredInt32("power.value2")));
        Assert.Equal((3, 3), (
            suffixes["bashing"].RequiredInt32("power.value1"),
            suffixes["bashing"].RequiredInt32("power.value2")));
    }

    [Fact]
    public void HellfireOverlayReplacesNativeAffixEligibilityRows()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));

        var prefixes = composed.GetRequiredTable("item_prefixes.tsv").Rows
            .Where(row => row.Required("name") is "Useless" or "Bent" or "Weak" or "Jagged" or "Deadly"
                or "Heavy" or "Vicious" or "Brutal" or "Massive")
            .ToArray();
        Assert.Equal(9, prefixes.Length);
        Assert.All(prefixes, row => Assert.Equal("Weapon,Staff,Bow", row.Required("itemTypes")));

        var suffixes = composed.GetRequiredTable("item_suffixes.tsv").Rows
            .Where(row => row.Required("name") is "quality" or "maiming" or "weakness" or "strength"
                or "atrophy" or "dexterity" or "illness" or "disease" or "vitality" or "trouble"
                or "the pit" or "the sky" or "the moon" or "sturdiness" or "craftsmanship" or "structure"
                or "the ages")
            .ToArray();
        Assert.Equal(17, suffixes.Length);
        Assert.All(suffixes, row => Assert.Contains("Staff", row.Required("itemTypes"), StringComparison.Ordinal));
    }

    [Fact]
    public void HellfireOverlayExternalizesTheGrizzlyUniqueItem()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));

        var rows = composed.GetRequiredTable("unique_items.tsv").Rows
            .Where(row => row.Required("unique_symbol") == "the_grizzly")
            .ToArray();

        Assert.Equal(5, rows.Length);
        Assert.All(rows, row => {
            Assert.Equal(1112U, row.RequiredUInt32("unique_id"));
            Assert.Equal("The Grizzly", row.Required("name"));
            Assert.Equal(136, row.RequiredInt32("base_item_id"));
            Assert.Equal("two_handed_sword", row.Required("base_item_symbol"));
            Assert.Equal(23, row.RequiredInt32("min_level"));
            Assert.Equal(50000, row.RequiredInt32("value"));
            Assert.Equal("THE_GRIZZLY", row.Required("cursor_graphic"));
        });
        Assert.Equal(
            new[] { "STR", "VIT_CURSE", "DAMP", "KNOCKBACK", "DUR" },
            rows.Select(row => row.Required("power")).ToArray());
    }

    [Fact]
    public void HellfireUniqueCatalogNormalizesNativeBlankPowerValues()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));
        var catalog = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            composed.GetRequiredTable("unique_items.tsv").ToCanonicalTsv());

        // Hellfire's native unique_itemdat.tsv has 110 definitions. Empty
        // numeric cells on flag-style powers are represented as 0,0 in the
        // normalized table, while populated ranges retain their values.
        Assert.Equal(110, catalog.Definitions.Count(definition => definition.UniqueId >= 1003));
        var grandfather = Assert.Single(catalog.Definitions, definition => definition.UniqueId == 1037);
        var oneHand = Assert.Single(grandfather.Powers, power => power.Power == "ONEHAND");
        Assert.Equal((0, 0), (oneHand.Value1, oneHand.Value2));
        var empyreanBand = Assert.Single(catalog.Definitions, definition => definition.UniqueId == 1004);
        var attributes = Assert.Single(empyreanBand.Powers, power => power.Power == "ATTRIBS");
        Assert.Equal((2, 2), (attributes.Value1, attributes.Value2));
        var trapResistance = Assert.Single(empyreanBand.Powers, power => power.Power == "ABSHALFTRAP");
        Assert.Equal((0, 0), (trapResistance.Value1, trapResistance.Value2));

        var nativeUniqueTable = TsvTable.Parse(
            "mods/Hellfire/txtdata/items/unique_itemdat.tsv",
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "mods",
                "Hellfire",
                "txtdata",
                "items",
                "unique_itemdat.tsv")));
        Assert.Equal(110, nativeUniqueTable.Rows.Count);
        foreach (var nativeRow in nativeUniqueTable.Rows) {
            var definition = Assert.Single(
                catalog.Definitions,
                candidate => candidate.Name == nativeRow.Required("name"));
            Assert.Equal(nativeRow.RequiredInt32("minLevel"), definition.MinimumLevel);
            Assert.Equal(nativeRow.RequiredInt32("value"), definition.Value);
            var nativeCursor = nativeRow.TryGet("cursorGraphic", out var cursorGraphic)
                ? cursorGraphic
                : string.Empty;
            Assert.Equal(
                nativeCursor is "-" or "" ? string.Empty : nativeCursor,
                definition.CursorGraphic);

            var nativePowers = new List<(string Power, int Value1, int Value2)>();
            for (var powerIndex = 0; powerIndex < 6; powerIndex++) {
                var powerColumn = $"power{powerIndex}";
                if (!nativeRow.TryGet(powerColumn, out var power)
                    || string.IsNullOrWhiteSpace(power))
                    continue;
                nativePowers.Add((
                    power,
                    nativeRow.OptionalInt32($"{powerColumn}.value1"),
                    nativeRow.OptionalInt32($"{powerColumn}.value2")));
            }
            Assert.Equal(
                nativePowers,
                definition.Powers.Select(power => (
                    power.Power,
                    power.Value1,
                    power.Value2)).ToArray());
        }
    }

    [Fact]
    public void HellfireOverlayPreservesNativeArmorAndSpecialDamage()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]));
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            composed.GetRequiredTable("monster_types.tsv").ToCanonicalTsv());

        var expectedArmor = new Dictionary<uint, int>
        {
            [0] = 5,
            [2] = 15,
            [3] = 20,
            [4] = 0,
            [5] = 5,
            [26] = 15,
            [28] = 60,
            [29] = 25,
            [30] = 30,
            [31] = 30,
            [33] = 70,
            [34] = 40,
            [35] = 40,
            [60] = 45,
            [61] = 45,
            [62] = 50,
            [63] = 60,
            [64] = 40,
            [65] = 45,
            [66] = 50,
            [67] = 55,
            [68] = 50,
            [69] = 60,
            [70] = 45,
            [71] = 65,
        };

        foreach (var (nativeMonsterId, armorClass) in expectedArmor) {
            Assert.Equal(
                armorClass,
                catalog.Definitions.Single(definition => definition.MonsterId >= 2
                    && definition.NativeMonsterId == nativeMonsterId).ArmorClass);
        }

        foreach (var nativeMonsterId in new uint[] { 4, 5, 6, 7 }) {
            var definition = catalog.Definitions.Single(
                candidate => candidate.MonsterId >= 2 && candidate.NativeMonsterId == nativeMonsterId);
            Assert.Equal((0, 0), (definition.MinimumSpecialDamage, definition.MaximumSpecialDamage));
        }
    }

    [Fact]
    public void ComposedHellfireMonsterCatalogCoversEveryNativeIdentity()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var composed = ComposedContent.Compose(new ContentManifest(
            "hellfire-monster-coverage",
            "1",
            [basePack, hellfirePack]));
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            composed.GetRequiredTable("monster_types.tsv").ToCanonicalTsv());

        Assert.Equal(140, catalog.Definitions.Count);
        Assert.Equal(
            Enumerable.Range(1, 140).Select(value => (uint)value),
            catalog.Definitions
                .Select(definition => definition.MonsterId)
                .OrderBy(value => value));
        Assert.Equal(
            Enumerable.Range(0, 138).Select(value => (uint)value),
            catalog.Definitions
                .Select(definition => definition.NativeMonsterId)
                .Distinct()
                .OrderBy(value => value));
        Assert.Equal(
            2,
            catalog.Definitions.Count(definition => definition.NativeMonsterId == 0));
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static byte[] CreateSyntheticDun()
    {
        const int megaWidth = 40;
        const int megaHeight = 40;
        var megaCount = megaWidth * megaHeight;
        var dPieceCount = megaCount * 4;
        var words = new ushort[2 + megaCount + 4 * dPieceCount];
        words[0] = megaWidth;
        words[1] = megaHeight;
        var bytes = new byte[words.Length * sizeof(ushort)];
        for (var index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                words[index]);
        return bytes;
    }

    private static byte[] CreateSyntheticTil()
    {
        var bytes = new byte[sizeof(ushort) * 4];
        for (var index = 0; index < 4; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                (ushort)(20 + index));
        return bytes;
    }

    private static byte[] CreateSyntheticSol()
    {
        var bytes = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        bytes[20] = (byte)AuthoritativeDunTileFlags.Solid;
        return bytes;
    }

    private static string ToQ6(string nativeValue)
    {
        var value = decimal.Parse(nativeValue, System.Globalization.CultureInfo.InvariantCulture);
        return checked((int)(value * 64m)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
