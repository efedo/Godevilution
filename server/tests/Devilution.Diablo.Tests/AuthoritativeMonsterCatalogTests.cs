using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeMonsterCatalogTests
{
    [Fact]
    public void ReturnsTypesAvailableAtTheRequestedDungeonLevelInStableIdOrder()
    {
        var catalog = new AuthoritativeMonsterTypeCatalog([
            CreateType(20, minimumLevel: 3, maximumLevel: 8),
            CreateType(10, minimumLevel: 1, maximumLevel: 4),
            CreateType(30, minimumLevel: 8, maximumLevel: 16),
        ]);

        Assert.Equal(
            [10U, 20U],
            catalog.GetAvailableForDungeonLevel(3)
                .Select(definition => definition.MonsterId));
        Assert.Equal(
            [20U, 30U],
            catalog.GetAvailableForDungeonLevel(8)
                .Select(definition => definition.MonsterId));
        Assert.Empty(catalog.GetAvailableForDungeonLevel(17));
    }

    [Fact]
    public void RejectsNegativeDungeonLevels()
    {
        var catalog = new AuthoritativeMonsterTypeCatalog([CreateType(1, 1, 1)]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => catalog.GetAvailableForDungeonLevel(-1));
    }

    [Fact]
    public void RejectsOutOfRangeAggroRadius()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with { AggroRange = 65 },
        ]));
    }

    private static AuthoritativeMonsterTypeDefinition CreateType(
        uint monsterId,
        int minimumLevel,
        int maximumLevel)
    {
        return new AuthoritativeMonsterTypeDefinition(
            monsterId,
            monsterId,
            $"Monster {monsterId}",
            minimumLevel,
            maximumLevel,
            1,
            1,
            1,
            0,
            1,
            1,
            0,
            0,
            false,
            false,
            false);
    }

    [Fact]
    public void LoadsNativeMonsterTypesInStableIdentityOrder()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\n"
                + "2\t0\tZombie\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t1\t1\t0\n"
                + "3\t1\tGhoul\t2\t3\t2\t7\t11\t10\t3\t10\t0\t0\t1\t1\t0\n");

        Assert.Equal([2U, 3U], catalog.Definitions.Select(definition => definition.MonsterId));
        var zombie = catalog.Definitions[0];
        Assert.Equal(0U, zombie.NativeMonsterId);
        Assert.Equal("Zombie", zombie.Name);
        Assert.Equal((4, 7, 10, 2, 5), (
            zombie.MinimumHitPoints,
            zombie.MaximumHitPoints,
            zombie.ArmorClass,
            zombie.MinimumDamage,
            zombie.MaximumDamage));
        Assert.Equal(6, zombie.AggroRange);
        Assert.True(zombie.StoneCurseImmune);
        Assert.True(zombie.Undead);
        Assert.False(zombie.Demon);
        Assert.True(catalog.TryGetNative(0, out var nativeZombie));
        Assert.Same(zombie, nativeZombie);
    }

    [Fact]
    public void LoadsAndInheritsDemonCapability()
    {
        var types = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tdemon\n"
                + "2\t0\tDemon Beast\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\t1\n");

        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.True(Assert.Single(catalog.Targets).IsDemon);
    }

    [Fact]
    public void LoadsAndInheritsAnimalCapability()
    {
        var types = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tanimal\n"
                + "2\t0\tFallen One\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\t1\n");

        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.True(Assert.Single(catalog.Targets).IsAnimal);
    }

    [Fact]
    public void LoadsAndInheritsKnockbackCapability()
    {
        var types = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tknockback\n"
                + "2\t0\tKnockback Beast\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\t1\n");

        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.True(Assert.Single(catalog.Targets).HasKnockback);
    }

    [Fact]
    public void DoesNotReverseResolveAmbiguousNativeMonsterIdentities()
    {
        var catalog = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, true, true, false),
            new AuthoritativeMonsterTypeDefinition(
                3, 0, "Ghoul", 2, 3, 2, 7, 11, 10, 3, 10, 0, 0, true, true, false),
        ]);

        Assert.False(catalog.TryGetNative(0, out _));
    }

    [Fact]
    public void LoadsNativeMonsterTypeResistances()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tfire_resistance\tlightning_resistance\tmagic_resistance\n"
                + "2\t0\tZombie\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t1\t1\t0\t0\t0\t100\n");

        var zombie = Assert.Single(catalog.Definitions);
        Assert.Equal((0, 0, 100), (
            zombie.FireResistance,
            zombie.LightningResistance,
            zombie.MagicResistance));
    }

    [Fact]
    public void LoadsAndInheritsAcidResistance()
    {
        var types = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tacid_resistance\n"
                + "2\t0\tAcid Beast\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\t100\n");

        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.Equal(100, Assert.Single(catalog.Targets).AcidResistance);
    }

    [Fact]
    public void EncounterUsesTypeResistanceWhenNoInstanceOverrideExists()
    {
        var types = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, true, true, false) {
                MagicResistance = 100,
            },
        ]);
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.Equal(100, Assert.Single(catalog.Targets).MagicResistance);
    }

    [Fact]
    public void ExplicitEncounterResistanceOverridesTypeResistance()
    {
        var types = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, true, true, false) {
                MagicResistance = 100,
            },
        ]);
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tmagic_resistance\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t2\t1\t1\t0\t10\t10\t2\t25\n",
            monsterTypes: types);

        Assert.Equal(25, Assert.Single(catalog.Targets).MagicResistance);
    }

    [Fact]
    public void RejectsMonsterTypeResistanceOutsideNativeRange()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, true, true, false) {
                MagicResistance = 101,
            },
        ]));
    }

    [Fact]
    public void EncounterUsesTypeCombatAndCapabilityDefaultsWhenInstanceFieldsAreOmitted()
    {
        var types = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 3, 4, 7, 10, 2, 5, 6, 9, true, true, true),
        ]);
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t2\t1\t1\t0\t10\t10\n",
            monsterTypes: types);

        var target = Assert.Single(catalog.Targets);
        Assert.Equal(10, target.ArmorClass);
        Assert.Equal(3, target.MonsterLevel);
        Assert.Equal((2, 5, 6, 9), (
            target.MinDamage,
            target.MaxDamage,
            target.MinDamageSpecial,
            target.MaxDamageSpecial));
        Assert.True(target.StoneCurseImmune);
        Assert.True(target.IsUndead);
        Assert.True(target.BerserkImmune);
    }

    [Fact]
    public void EncounterRowsCanRequireAnExternalMonsterType()
    {
        var types = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, true, true, false),
        ]);
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.Equal(2U, Assert.Single(catalog.Targets).MonsterId);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeMonsterCatalog.LoadTsv(
                "monsters.tsv",
                header + "9\t3\t1\t1\t0\t10\t10\t2\n",
                monsterTypes: types));
    }

    [Fact]
    public void LoadsExternalEncounterDefinitionsInStableEntityOrder()
    {
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t1\t2\t4\t5\t20\t25\t3\n");

        var target = Assert.Single(catalog.Targets);
        Assert.Equal(9U, target.EntityId);
        Assert.Equal(1U, target.MonsterId);
        Assert.Equal(2U, target.LevelId);
        Assert.Equal(25, target.MaxHitPoints);
        Assert.False(target.StoneCurseImmune);
    }

    [Fact]
    public void LoadsAndValidatesStoneCurseImmunity()
    {
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tstone_curse_immune\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t1\t2\t4\t5\t20\t25\t3\t1\n");

        Assert.True(Assert.Single(catalog.Targets).StoneCurseImmune);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeMonsterCatalog.LoadTsv(
                "monsters.tsv",
                header + "9\t1\t2\t4\t5\t20\t25\t3\t2\n"));
    }

    [Fact]
    public void LoadsAndValidatesUndeadCapability()
    {
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tundead\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t1\t2\t4\t5\t20\t25\t3\t1\n");

        Assert.True(Assert.Single(catalog.Targets).IsUndead);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeMonsterCatalog.LoadTsv(
                "monsters.tsv",
                header + "9\t1\t2\t4\t5\t20\t25\t3\t2\n"));
    }

    [Fact]
    public void RejectsDuplicateMonsterEntityIds()
    {
        var targets = new[] {
            new AuthoritativeCombatTarget(9, 0, 0, 10),
            new AuthoritativeCombatTarget(9, 1, 0, 10),
        };

        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterCatalog(targets));
    }

    [Fact]
    public void LoadsOptionalDropDefinitions()
    {
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tdrop_item_entity_id\tdrop_item_seed\tdrop_item_price\n"
                + "9\t1\t1\t1\t0\t10\t10\t2\t1009\t6001\t25\n");

        var drop = Assert.Single(catalog.Targets).Drop;
        Assert.NotNull(drop);
        Assert.Equal(6001U, drop.ItemSeed);
    }

    [Fact]
    public void GeneratesCatalogBackedDropState()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ]);
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tdrop_item_entity_id\tdrop_item_seed\tdrop_item_price\tdrop_item_id\tdrop_item_level\n"
                + "9\t1\t1\t1\t0\t10\t10\t2\t1009\t6001\t25\t7\t1\n",
            items);

        Assert.Equal(7, Assert.Single(catalog.Targets).Drop!.State.ItemIndex);
        Assert.Equal(4, catalog.Targets[0].Drop!.State.MinDamage);
    }

    [Fact]
    public void GeneratesUniqueCatalogBackedDropState()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Ember Blade", 7, 1, 3650, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tdrop_item_entity_id\tdrop_item_seed\tdrop_item_price\tdrop_unique_item_id\tdrop_item_level\n"
                + "9\t1\t1\t1\t0\t10\t10\t2\t1009\t6001\t3650\t1001\t1\n",
            items,
            uniques);

        Assert.Equal(1001, Assert.Single(catalog.Targets).Drop!.State.UniqueId);
    }
}
