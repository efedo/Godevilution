using System.Text.Json;
using Devilution.Data;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Shared arena for the free-running native L5-L8 AI corpora: one production-admitted actor against a passive Warrior. */
internal static class CatacombsAiNativeOracle
{
    private const string Fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    internal const uint EntityId = 501;
    internal const int MonsterFlagHidden = 1;
    private static readonly JsonSerializerOptions StateOptions = new() { PropertyNameCaseInsensitive = true };

    internal sealed record Arena(DiabloWorldState World, DiabloCombatCommandProcessor Processor,
        DeterministicLegacyRandom Random, AuthoritativePlayerState Player)
    {
        public static Arena Create(JsonElement row)
        {
            var depth = N(row, "depth");
            var levelId = (uint)depth;
            var nativeType = N(row, "nativeType");
            var data = row.GetProperty("monsterData");
            var initial = row.GetProperty("initialActor");
            var state = JsonSerializer.Deserialize<AuthoritativeCathedralPopulationMonsterState>(initial.GetRawText(), StateOptions)!
                with { Slot = 0 };
            var monsterTypes = AuthoritativeMonsterTypeCatalog.LoadTsv("monster_types.tsv", ReadShipped("monster_types.tsv"));
            var monsterMissiles = AuthoritativeMonsterMissileCatalog.LoadTsv("monster_missiles.tsv", ReadShipped("monster_missiles.tsv"));
            var rules = AuthoritativeCombatRules.LoadTsv("combat.tsv", ReadShipped("combat.tsv"), AuthoritativePortalRules.TownLevelId);
            var content = monsterTypes.Definitions.Where(definition => definition.NativeMonsterId == nativeType)
                .OrderBy(definition => definition.MonsterId).First();
            var runtime = new AuthoritativeCathedralMonsterRuntimeCatalog(
                Enumerable.Range(0, AuthoritativeCathedralMonsterRuntimeCatalog.NativeMonsterCount)
                    .Select(id => id == nativeType
                        ? new AuthoritativeCathedralMonsterRuntimeDefinition(id, $"MONSTER_{id:000}", S(data, "name"),
                            N(data, "level"), N(data, "toHit"), N(data, "animFrameNum"), N(data, "toHitSpecial"),
                            N(data, "animFrameNumSpecial"), N(data, "monsterClass"), N(data, "selectionRegion"),
                            N(data, "treasure"), N(data, "exp"), N(data, "resistanceHell"))
                        : new AuthoritativeCathedralMonsterRuntimeDefinition(id, $"MONSTER_{id:000}", $"Monster {id}",
                            1, 1, 1, 0, 0, 1, 1, 0, 1, 0)));
            var frames = data.GetProperty("frames").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var rates = data.GetProperty("rates").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var bestiary = Enumerable.Range(0, AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount)
                .Select(id => id == nativeType
                    ? new AuthoritativeCathedralRawMonsterDefinition(id, AuthoritativeCathedralMonsterAvailability.Retail,
                        0, 0, sbyte.MaxValue, IsSkeleton(id),
                        state.MaxHitPoints >> 6, state.MaxHitPoints >> 6, N(data, "ai"), N(data, "intelligence"),
                        U(data, "abilityFlags"), N(data, "minDamage"), N(data, "maxDamage"),
                        N(data, "minDamageSpecial"), N(data, "maxDamageSpecial"), N(data, "armorClass"),
                        N(data, "resistance"), state.ReducePlayerStrength, state.ReducePlayerMagic,
                        state.ReducePlayerDexterity, state.ReducePlayerVitality, state.ReducePlayerMaxHP,
                        state.ReducePlayerMaxMana, data.GetProperty("hasSpecial").GetBoolean(), N(data, "width"),
                        frames, rates)
                    : new AuthoritativeCathedralRawMonsterDefinition(id, AuthoritativeCathedralMonsterAvailability.Retail,
                        0, 0, sbyte.MaxValue, IsSkeleton(id),
                        1, 2, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 1,
                        [0, 0, 4, 0, 0, 0], [0, 0, 2, 0, 0, 0]))
                .ToArray();
            var admission = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(depth, levelId,
                AuthoritativeCatacombsMonsterRuntimeAdapterTests.CreateState(state), runtime, Fingerprint,
                [new(0, EntityId, content.MonsterId, levelId)], bestiary, monsterTypeCatalog: monsterTypes);
            var target = Assert.IsType<AuthoritativeCombatTarget>(Assert.Single(admission.SharedRuntime.Records).Target);
            // An open floor with no vision geometry: every tile is walkable and visible, as in the native arena.
            var level = new AuthoritativeWorld();
            level.AddLevel(new AuthoritativeLevel(levelId, 112, 112, nativeVisionBlockedCells: []));
            var world = new DiabloWorldState(world: level, combatTargets: [target]);
            world.ConfigureNativeCatacombsMonsterPhases(admission);
            var random = new DeterministicLegacyRandom(U(row, "initialRandom"));
            var processor = new DiabloCombatCommandProcessor(world,
                new AuthoritativeSpellCatalog([new AuthoritativeSpellDefinition(1, 1, 1, 0, 0, 0)]),
                rules, random, monsterMissiles: monsterMissiles, monsterTypes: monsterTypes);
            var life = N(row, "initialPlayerLife");
            var playerTile = row.GetProperty("initialPlayer").GetProperty("tile");
            var player = new AuthoritativePlayerState(0, 0, life, 0, 0, PlayerAttributesState.Zero, [],
                Enumerable.Repeat(-1, 40).ToArray(), [], N(playerTile, "x"), N(playerTile, "y"), life, 1, levelId,
                N(row, "playerArmor"), characterClassId: 0) { EntityId = 1 };
            return new(world, processor, random, player);
        }
    }

    /** One shipped base-content table, loaded as production does without the retail-derived packs. */
    internal static string ReadShipped(string table)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "server", "content", "base", table)))
            directory = directory.Parent;
        if (directory is null)
            throw new InvalidDataException($"The shipped {table} was not found above the test output.");
        return File.ReadAllText(Path.Combine(directory.FullName, "server", "content", "base", table));
    }

    /** Reads one capture by its environment directory, file, format, policy and lane order. */
    internal static JsonDocument ReadCorpus(string environment, string file, string format, string policy, string[] lanes)
    {
        var directory = Environment.GetEnvironmentVariable(environment);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidDataException($"An explicit native Catacombs AI reference directory ({environment}) is required.");
        var info = new FileInfo(Path.Combine(directory, file));
        if (!info.Exists || info.Length is <= 0 or > 32 * 1024 * 1024)
            throw new InvalidDataException("The native Catacombs AI reference is missing or outside its size limit.");
        var document = JsonDocument.Parse(File.ReadAllBytes(info.FullName), new JsonDocumentOptions { MaxDepth = 32 });
        var root = document.RootElement;
        if (S(root, "format") != format || N(root, "version") != 1
            || S(root, "policy") != policy || N(root, "caseCount") != lanes.Length
            || !root.GetProperty("cases").EnumerateArray().Select(value => S(value, "name")).SequenceEqual(lanes))
            throw new InvalidDataException("The native Catacombs AI reference has an unsupported schema or policy.");
        return document;
    }

    /** LCG steps from one state to another, or -1 beyond a single tick's plausible draws. */
    internal static int Draws(uint from, uint to)
    {
        var random = new DeterministicLegacyRandom(from);
        for (var count = 0; count <= 64; count++) {
            if (random.State == to) return count;
            random.NextUInt32();
        }
        return -1;
    }

    private static bool IsSkeleton(int id) => id is >= 8 and <= 11 or >= 20 and <= 27;

    internal static int N(JsonElement value, string property) => value.GetProperty(property).GetInt32();
    internal static uint U(JsonElement value, string property) => value.GetProperty(property).GetUInt32();
    internal static string S(JsonElement value, string property)
        => value.GetProperty(property).GetString() ?? throw new InvalidDataException("A Catacombs AI oracle string is null.");
}
