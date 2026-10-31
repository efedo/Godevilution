using System.Text.Json;
using Devilution.Data;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Free-running native L5-L8 melee AI: one production-admitted actor against a passive Warrior, compared every tick. */
public sealed class AuthoritativeCatacombsMeleeAiNativeOracleTests
{
    private const string Policy = "prepared-original-diablo-catacombs-melee-ai-free-run";
    private const string Fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const uint EntityId = 501;
    private const int MonsterFlagHidden = 1;
    private static readonly string[] LaneNames =
        ["zombie", "fallen", "skeleton", "scavenger", "sneak", "goat-melee", "fat", "bat", "gargoyle", "rhino"];
    private static readonly Lazy<JsonDocument> Corpus = new(ReadCorpus);
    private static readonly JsonSerializerOptions StateOptions = new() { PropertyNameCaseInsensitive = true };

    public static TheoryData<string> Lanes => new(LaneNames);

    [Theory(Explicit = true)]
    [MemberData(nameof(Lanes))]
    public void NativeMeleeAiMatchesEveryTick(string lane)
    {
        var row = Corpus.Value.RootElement.GetProperty("cases").EnumerateArray().Single(value => S(value, "name") == lane);
        var run = Arena.Create(row);
        var ticks = row.GetProperty("ticks").EnumerateArray().ToArray();
        Assert.Equal(N(Corpus.Value.RootElement, "tickCount"), ticks.Length);
        var previousRandom = U(row, "initialRandom");
        for (var index = 0; index < ticks.Length; index++) {
            run.Processor.AdvanceTick((ulong)index + 1, [run.Player]);
            var tick = ticks[index];
            var actor = tick.GetProperty("actor");
            var expected = Observe(U(tick, "random"), N(tick, "playerLife"), N(tick, "playerLifeMaximum"),
                N(actor.GetProperty("tile"), "x"), N(actor.GetProperty("tile"), "y"), N(actor, "hitPoints"),
                (N(actor, "flags") & MonsterFlagHidden) != 0);
            var target = run.World.CombatTargets.GetValueOrDefault(EntityId);
            var observed = Observe(run.Random.State, run.Player.Life, run.Player.LifeMaximum,
                target?.PositionX ?? -1, target?.PositionY ?? -1, target?.HitPointsQ6 ?? -1, target?.SneakHidden ?? false);
            Assert.True(expected == observed,
                $"{lane} diverges at tick {index} (native mode {N(actor, "mode")}, "
                + $"draws native {Draws(previousRandom, U(tick, "random"))} managed {Draws(previousRandom, run.Random.State)}): "
                + $"native {expected}, managed {observed}");
            previousRandom = U(tick, "random");
        }
    }

    /** LCG steps from one state to another, or -1 beyond a single tick's plausible draws. */
    private static int Draws(uint from, uint to)
    {
        var random = new DeterministicLegacyRandom(from);
        for (var count = 0; count <= 64; count++) {
            if (random.State == to) return count;
            random.NextUInt32();
        }
        return -1;
    }

    private static string Observe(uint random, int life, int lifeMaximum, int x, int y, int hitPoints, bool hidden)
        => $"rng={random} life={life}/{lifeMaximum} tile=({x},{y}) hp={hitPoints} hidden={hidden}";

    private sealed record Arena(DiabloWorldState World, DiabloCombatCommandProcessor Processor,
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
    private static string ReadShipped(string table)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "server", "content", "base", table)))
            directory = directory.Parent;
        if (directory is null)
            throw new InvalidDataException($"The shipped {table} was not found above the test output.");
        return File.ReadAllText(Path.Combine(directory.FullName, "server", "content", "base", table));
    }

    private static JsonDocument ReadCorpus()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_CATACOMBS_MELEE_AI_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidDataException("An explicit native Catacombs melee AI reference directory is required.");
        var file = new FileInfo(Path.Combine(directory, "native-catacombs-melee-ai.json"));
        if (!file.Exists || file.Length is <= 0 or > 32 * 1024 * 1024)
            throw new InvalidDataException("The native Catacombs melee AI reference is missing or outside its size limit.");
        var document = JsonDocument.Parse(File.ReadAllBytes(file.FullName), new JsonDocumentOptions { MaxDepth = 32 });
        var root = document.RootElement;
        if (S(root, "format") != "godevilution-catacombs-melee-ai" || N(root, "version") != 1
            || S(root, "policy") != Policy || N(root, "caseCount") != LaneNames.Length
            || !root.GetProperty("cases").EnumerateArray().Select(value => S(value, "name")).SequenceEqual(LaneNames))
            throw new InvalidDataException("The native Catacombs melee AI reference has an unsupported schema or policy.");
        return document;
    }

    private static bool IsSkeleton(int id) => id is >= 8 and <= 11 or >= 20 and <= 27;

    private static int N(JsonElement value, string property) => value.GetProperty(property).GetInt32();
    private static uint U(JsonElement value, string property) => value.GetProperty(property).GetUInt32();
    private static string S(JsonElement value, string property)
        => value.GetProperty(property).GetString() ?? throw new InvalidDataException("A melee AI oracle string is null.");
}
