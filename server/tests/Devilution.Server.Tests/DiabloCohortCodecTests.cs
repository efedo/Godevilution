using System.Text.Json.Nodes;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class DiabloCohortCodecTests
{
    private readonly DiabloCohortCodec codec = new();

    [Fact]
    public void LegacyVersionOneRetainsFrozenBaselineBytes()
    {
        // Captured independently from clean 06c0eb5e8, not this serializer.
        var bytes = System.Text.Encoding.UTF8.GetBytes(codec.Serialize(CreateState()));
        Assert.Equal(15991, bytes.Length);
        Assert.Equal("067bdf7e13316b858887c1ffde3364da249dfde4783f4de5c977c81b0a1a3ed0",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(3, 1)]
    public void HorkDemonMoveGoalRoundTripsWithOptionalLegacyDefaults(int ticks, int turn)
    {
        var baseline = CreateState();
        var target = Assert.Single(baseline.CombatTargets);
        var source = baseline with {
            CombatTargets = [target with { Monster = target.Monster with {
                HorkDemonMoveTicks = ticks, HorkDemonMoveTurn = turn,
            } }],
        };
        var encoded = codec.Serialize(source);
        Assert.Equal(ticks != -1 || turn != 0, encoded.Contains("\"HorkDemonMoveTicks\"", StringComparison.Ordinal));
        var monster = Assert.Single(codec.Deserialize(encoded).CombatTargets).Monster;
        Assert.Equal((ticks, turn), (monster.HorkDemonMoveTicks, monster.HorkDemonMoveTurn));
        Assert.Equal(encoded, codec.Serialize(codec.Deserialize(encoded)));
    }

    [Theory]
    [InlineData("HorkDemonMoveTicks")]
    [InlineData("HorkDemonMoveTurn")]
    public void HorkDemonMoveGoalRejectsPartialCohortFields(string field)
    {
        var document = ParseState();
        document["CombatTargets"]![0]!["Monster"]![field] = 0;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void FullQuestPoliciesRoundTripThroughTheHostCodec()
    {
        var quest = new AuthoritativeQuestState(9, 0, 3, progress: 1,
            active: true, logged: true, dialogueVariable1: 7,
            dialogueVariable2: 8, dialogueCompletionVariable1: 9,
            completionRewardItemId: 44, completionRewardItemLevel: 12,
            turnInRequiredVariable1: 13, turnInRewardUniqueId: 14,
            turnInRewardItemLevel: 15, turnInCompletionMessageId: 16,
            dialogueRewardOffsetX: 1, dialogueRewardOffsetY: -1) {
            StableSymbol = "quest.complete-cohort",
        };
        var (options, simulation, runtime, _) = CreateLiveFixture(false, [quest]);
        var checkpoint = simulation.CaptureCohortState(runtime);
        var encoded = codec.Serialize(checkpoint);
        var decoded = codec.Deserialize(encoded);
        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(options, decoded);
        var restoredRuntime = candidate.CreateRuntime();
        var originalQuest = simulation.WorldState.Quests[9];
        var restoredQuest = candidate.Simulation.WorldState.Quests[9];

        foreach (var property in typeof(AuthoritativeQuestState).GetProperties()) {
            var storedProperty = typeof(DiabloCohortQuestState).GetProperty(property.Name);
            Assert.NotNull(storedProperty);
            Assert.Equal(property.GetValue(originalQuest), storedProperty.GetValue(Assert.Single(decoded.Quests)));
            Assert.Equal(property.GetValue(originalQuest), property.GetValue(restoredQuest));
        }
        Assert.Equal(encoded, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        Assert.Equal(encoded, codec.Serialize(simulation.CaptureCohortState(runtime)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EncodedCohortCreatesAFreshCandidateWithExactRecaptureAndGameplayContinuation(bool worldBacked)
    {
        var (options, simulation, runtime, _) = CreateLiveFixture(worldBacked);
        var sourcePlayers = runtime.CaptureCohort((_, sessions) => sessions.Select(session => session.State).ToArray());
        runtime.AdvanceTo(1);
        var pickup = simulation.Execute(sourcePlayers[0], new DiabloPickupWorldItemIntent(100), 1, sourcePlayers);
        Assert.True(pickup.Succeeded, pickup.Failure?.ToString());
        options.StoreCatalog.ReplaceStore(7, []);
        var checkpoint = simulation.CaptureCohortState(runtime);
        var encoded = codec.Serialize(checkpoint);

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(options, codec.Deserialize(encoded));
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Throws<InvalidOperationException>(() => candidate.CreateRuntime());

        Assert.Equal(encoded, codec.Serialize(simulation.CaptureCohortState(runtime)));
        Assert.Equal(encoded, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        Assert.Equal(["participant-a", "participant-b"], candidate.Participants.Select(participant => participant.Id));
        Assert.Empty(candidate.Simulation.WorldState.WorldItems);
        Assert.Single(candidate.Participants[0].State.Inventory);
        Assert.Empty(candidate.Participants[1].State.Inventory);
        Assert.Empty(candidate.Simulation.CaptureCohortState(restoredRuntime).Stores.Single(store => store.StoreId == 7).Items!);

        runtime.AdvanceTo(2);
        restoredRuntime.AdvanceTo(2);
        var restoredPlayers = candidate.Participants.Select(participant => participant.State).ToArray();
        var originalMove = simulation.Execute(sourcePlayers[0], new DiabloMoveIntent(1, 0), 2, sourcePlayers);
        var restoredMove = candidate.Simulation.Execute(restoredPlayers[0], new DiabloMoveIntent(1, 0), 2, restoredPlayers);
        Assert.True(originalMove.Succeeded, originalMove.Failure?.ToString());
        Assert.Equal(originalMove.Succeeded, restoredMove.Succeeded);
        Assert.Equal(originalMove.Failure, restoredMove.Failure);
        Assert.Equal(codec.Serialize(simulation.CaptureCohortState(runtime)),
            codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
    }

    [Theory]
    [InlineData("random-seed")]
    [InlineData("fallback-width")]
    [InlineData("store-id")]
    [InlineData("store-role")]
    [InlineData("last-participant-position")]
    [InlineData("last-participant-life-over-maximum")]
    [InlineData("last-participant-mana-over-maximum")]
    [InlineData("last-participant-light-radius")]
    [InlineData("last-participant-character-level")]
    [InlineData("last-participant-maximum-attribute")]
    [InlineData("last-participant-grid-reference")]
    [InlineData("last-participant-applied-tick")]
    [InlineData("monster-whole-q6-disagreement")]
    [InlineData("monster-max-whole-q6-disagreement")]
    [InlineData("last-object-position")]
    [InlineData("cross-collection-entity")]
    [InlineData("cast-without-intent")]
    [InlineData("uneffected-death")]
    [InlineData("uneffected-restart")]
    [InlineData("uneffected-transition")]
    public void DecodedInvalidCandidateDoesNotMutateTheLiveSource(string defect)
    {
        var (options, simulation, runtime, _) = CreateLiveFixture(true);
        runtime.AdvanceTo(1);
        var state = simulation.CaptureCohortState(runtime);
        var original = codec.Serialize(state);
        var document = JsonNode.Parse(original)!;
        if (defect is "cast-without-intent" or "uneffected-death"
            or "uneffected-restart" or "uneffected-transition") {
            var kind = defect switch {
                "cast-without-intent" => DiabloPlayerActionKind.Cast,
                "uneffected-death" => DiabloPlayerActionKind.Death,
                "uneffected-restart" => DiabloPlayerActionKind.RestartTown,
                _ => DiabloPlayerActionKind.LevelTransition,
            };
            var altered = state with { Participants = [state.Participants[0], state.Participants[1] with {
                Player = state.Participants[1].Player with {
                    CurrentAction = new AuthoritativePlayerActionState(kind, 1, 1, 2, 2),
                },
            }] };
            document = JsonNode.Parse(codec.Serialize(altered))!;
        }
        switch (defect) {
        case "random-seed":
            document["Configuration"]!["InitialRandomSeed"] = options.RandomSeed + 1;
            break;
        case "fallback-width":
            document["WorldInfrastructure"]!["FallbackWidth"] = options.FallbackWidth + 1;
            break;
        case "store-id":
            document["Stores"]![1]!["StoreId"] = 99;
            break;
        case "store-role":
            document["Stores"]![0]!["Role"] = (int)AuthoritativeStoreRole.Witch;
            break;
        case "last-participant-position":
            document["Participants"]![1]!["Player"]!["PositionX"] = 99;
            break;
        case "last-participant-life-over-maximum":
            document["Participants"]![1]!["Player"]!["Life"] = 2561;
            break;
        case "last-participant-mana-over-maximum":
            document["Participants"]![1]!["Player"]!["Mana"] = 1281;
            break;
        case "last-participant-light-radius":
            var player = document["Participants"]![1]!["Player"]!;
            player["LightRadius"] = player["LightRadius"]!.GetValue<int>() + 1;
            break;
        case "last-participant-character-level":
            document["Participants"]![1]!["Player"]!["CharacterLevel"] = 0;
            break;
        case "last-participant-maximum-attribute":
            document["Participants"]![1]!["MaximumAttribute"] = 0;
            break;
        case "last-participant-grid-reference":
            document["Participants"]![1]!["Player"]!["InventoryGrid"]![0] = 0;
            break;
        case "last-participant-applied-tick":
            document["Participants"]![1]!["LastAppliedTick"] = 2;
            break;
        case "monster-whole-q6-disagreement":
            document["CombatTargets"]![0]!["Monster"]!["HitPoints"] = 11;
            break;
        case "monster-max-whole-q6-disagreement":
            document["CombatTargets"]![0]!["Monster"]!["MaxHitPoints"] = 13;
            break;
        case "last-object-position":
            document["Objects"]![0]!["PositionX"] = 99;
            break;
        case "cross-collection-entity":
            document["Objects"]![0]!["EntityId"] = 21;
            break;
        case "cast-without-intent":
        case "uneffected-death":
        case "uneffected-restart":
        case "uneffected-transition":
            break;
        default:
            throw new InvalidOperationException("Unknown test defect.");
        }
        var malformed = codec.Deserialize(document.ToJsonString());

        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.CreateCohortCandidate(options, malformed));

        Assert.Equal(original, codec.Serialize(simulation.CaptureCohortState(runtime)));
        Assert.Equal(1UL, runtime.LastAdvancedTick);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecodedNegativePositionCannotBypassFreshCandidateValidation(bool worldBacked)
    {
        var (options, simulation, runtime, _) = CreateLiveFixture(worldBacked);
        var original = codec.Serialize(simulation.CaptureCohortState(runtime));
        var document = JsonNode.Parse(original)!;
        document["Participants"]![1]!["Player"]!["PositionX"] = -1;
        var malformed = codec.Deserialize(document.ToJsonString());

        Assert.Throws<InvalidDataException>(() =>
            DiabloSimulationFactory.Instance.CreateCohortCandidate(options, malformed));

        Assert.Equal(original, codec.Serialize(simulation.CaptureCohortState(runtime)));
    }

    [Theory]
    [InlineData("Participants")]
    [InlineData("Stores")]
    public void ReorderedCanonicalCollectionsAreRejectedWithoutSourceMutation(string collection)
    {
        var (_, simulation, runtime, _) = CreateLiveFixture(true);
        var original = codec.Serialize(simulation.CaptureCohortState(runtime));
        var document = JsonNode.Parse(original)!;
        var array = document[collection]!.AsArray();
        document[collection] = new JsonArray(array.Reverse().Select(item => item!.DeepClone()).ToArray());

        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
        Assert.Equal(original, codec.Serialize(simulation.CaptureCohortState(runtime)));
    }

    [Fact]
    public void CompleteRecordsRoundTripWithoutDependingOnConvenienceConstructors()
    {
        var state = CreateState();

        var serialized = codec.Serialize(state);
        var restored = codec.Deserialize(serialized);

        Assert.Equal(serialized, codec.Serialize(restored));
        Assert.Equal(42UL, restored.Tick);
        Assert.Equal(7U, restored.RandomState);
        Assert.Null(restored.LastPortalTick);
        Assert.Equal(0U, restored.Allocators.WorldCommand.NextAuthoredProjectileEntityId);
        Assert.Equal(uint.MaxValue, restored.Allocators.CombatCommand.NextMonsterEntityId);
        var participant = Assert.Single(restored.Participants);
        Assert.Equal("opaque-participant", participant.SessionId);
        Assert.Equal(5, participant.BaseArmorClass);
        Assert.Equal(255, participant.MaximumAttribute);
        Assert.False(participant.GoldTopologyEnabled);
        Assert.Equal("Hero", participant.Player.CharacterName);
        Assert.Equal(19UL, Assert.Single(participant.Player.Inventory).PurchasedAtTick);
        Assert.Equal(23U, Assert.Single(participant.Player.Equipment).ItemSeed);
        Assert.Equal(29U, Assert.Single(participant.Player.Belt).ItemSeed);
        Assert.Equal(31U, Assert.Single(participant.Player.Stash).ItemSeed);
        Assert.Equal(37U, participant.Player.HeldItem!.ItemSeed);
        Assert.Equal(DiabloPlayerActionKind.Move, participant.Player.CurrentAction!.Kind);
        Assert.Equal(641, Assert.Single(restored.CombatTargets).Monster.HitPointsQ6);
        Assert.True(Assert.Single(restored.CombatTargets).FallenRallyAttackPending);
        Assert.Equal(41U, Assert.Single(restored.Stores[0].Items!).ItemSeed);
        Assert.Empty(restored.Stores[1].Items!);
        Assert.False(JsonNode.Parse(serialized)!.AsObject().ContainsKey("LastPortalTick"));
        Assert.False(JsonNode.Parse(serialized)!.AsObject().ContainsKey("ActiveWorldMutationIds"));
    }

    [Fact]
    public void RoundTripDetachesMutableActionAndRosterCollections()
    {
        var source = CreateState();
        var restored = codec.Deserialize(codec.Serialize(source));

        restored.Participants[0].Player.CurrentAction!.EffectApplied = true;
        ((IList<DiabloCohortParticipantState>)restored.Participants)[0] = restored.Participants[0] with {
            Player = restored.Participants[0].Player with { CharacterName = "Changed" },
        };

        Assert.False(source.Participants[0].Player.CurrentAction!.EffectApplied);
        Assert.Equal("Hero", source.Participants[0].Player.CharacterName);
    }

    [Fact]
    public void ConstructorBoundReadOnlyGeneratedIdentityFieldsRemainInTheFormat()
    {
        var fingerprint = new string('A', 64);
        var identity = new DiabloNativeGeneratedWorldIdentity([
            new AuthoritativeCathedralLevel1GenerationIdentity(
                AuthoritativeCathedralLevel1RuntimeCatalog.CatalogId,
                AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId,
                AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorRevision,
                1, "cathedral-1", 23, (AuthoritativeCathedralEntry)0, 29, 31,
                fingerprint, fingerprint, fingerprint, fingerprint),
        ], fingerprint);
        var original = CreateState();
        var state = original with { Configuration = original.Configuration with {
            NativeGeneratedWorldIdentity = identity,
        } };

        var serialized = codec.Serialize(state);
        var restored = codec.Deserialize(serialized);

        Assert.Equal(serialized, codec.Serialize(restored));
        Assert.Equal(fingerprint, restored.Configuration.NativeGeneratedWorldIdentity!.RouteFingerprint);
        Assert.Equal(identity.Levels[0], restored.Configuration.NativeGeneratedWorldIdentity.Levels[0]);
        var document = JsonNode.Parse(serialized)!;
        Assert.True(document["Configuration"]!["NativeGeneratedWorldIdentity"]!["Levels"]![0]!
            .AsObject().Remove("AttemptSeed"));
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Theory]
    [InlineData("FormatVersion")]
    [InlineData("Geometry")]
    [InlineData("WorldInfrastructure")]
    [InlineData("RandomState")]
    public void MissingTopLevelFieldsAreNotReplacedWithDefaults(string field)
    {
        var document = ParseState();
        Assert.True(document.Remove(field));

        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Theory]
    [InlineData("LastAppliedTick")]
    [InlineData("BaseArmorClass")]
    [InlineData("GoldTopologyEnabled")]
    [InlineData("PlayerClassBound")]
    [InlineData("ItemCatalogBound")]
    [InlineData("PlayerClassDefinition")]
    public void MissingParticipantSupplementsAreRejected(string field)
    {
        var document = ParseState();
        Assert.True(document["Participants"]![0]!.AsObject().Remove(field));

        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Theory]
    [InlineData("PlayerClassBound")]
    [InlineData("ItemCatalogBound")]
    public void NullPolicyBindingsCannotSelectImplicitRestoreDefaults(string field)
    {
        var document = ParseState();
        document["Participants"]![0]![field] = null;

        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void FullCohortsRequireExplicitEffectiveGeometryEvenWhenTheFieldIsNullableInTheHelper()
    {
        var document = ParseState();
        Assert.True(document["WorldInfrastructure"]!.AsObject().Remove("EffectiveLevels"));
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
        document = ParseState();
        document["WorldInfrastructure"]!["EffectiveLevels"] = null;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
        var state = CreateState();
        Assert.Throws<InvalidDataException>(() => codec.Serialize(state with {
            WorldInfrastructure = state.WorldInfrastructure with { EffectiveLevels = null },
        }));
    }

    [Theory]
    [InlineData("HeldItem")]
    [InlineData("CurrentAction")]
    [InlineData("Inventory")]
    [InlineData("BaseLifeMaximum")]
    [InlineData("FriendlyMode")]
    public void MissingPlayerBackingFieldsAreRejectedIncludingNullableFields(string field)
    {
        var document = ParseState();
        Assert.True(document["Participants"]![0]!["Player"]!.AsObject().Remove(field));

        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void UnknownFieldsAndRedundantDerivedAliasesAreRejected()
    {
        foreach (var field in new[] { "Obsolete", "LastPortalTick", "ActiveWorldMutationIds" }) {
            var document = ParseState();
            document[field] = 0;
            Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
        }
        var nested = ParseState();
        nested["Participants"]![0]!["Player"]!["Obsolete"] = 0;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(nested.ToJsonString()));
    }

    [Fact]
    public void DuplicateFieldsAreRejectedEvenWithEscapedEquivalentNames()
    {
        var serialized = codec.Serialize(CreateState());
        foreach (var name in new[] { "Tick", "\\u0054ick" }) {
            Assert.Throws<InvalidDataException>(() => codec.Deserialize(
                serialized.Insert(1, $"\"{name}\":42,")));
        }
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(serialized.Replace(
            "\"CharacterName\":",
            "\"CharacterName\":\"Other\",\"CharacterName\":",
            StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"FormatVersion\":1,}")]
    [InlineData("/* comment */{}")]
    public void InvalidRootAndNonStrictJsonAreRejected(string text)
    {
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(text));
    }

    [Fact]
    public void UnsupportedFormatWrongScalarTypeAndOverflowAreRejected()
    {
        var document = ParseState();
        document["FormatVersion"] = 2;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
        document = ParseState();
        document["Tick"] = "42";
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(codec.Serialize(CreateState()).Replace(
            "\"Tick\":42", "\"Tick\":18446744073709551616", StringComparison.Ordinal)));
    }

    [Fact]
    public void LegacyCohortsRejectCathedralDoorExtensions()
    {
        var document = ParseState();
        document["CathedralDoors"] = new JsonObject();

        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void CathedralDoorCohortsRequireNonNullCheckpoint()
    {
        var document = ParseState();
        document["FormatVersion"] = 2;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));

        document["CathedralDoors"] = null;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void SerializationRejectsMismatchedCathedralDoorVersionsBeforeOmission()
    {
        var incomplete = new DiabloCathedralDoorCheckpoint(
            0, "", 0, "", null!, null!, null!);
        Assert.Throws<InvalidDataException>(() => codec.Serialize(CreateState() with {
            CathedralDoors = incomplete,
        }));
        Assert.Throws<InvalidDataException>(() => codec.Serialize(CreateState() with {
            FormatVersion = 2,
        }));
    }

    [Fact]
    public void FormatVersionMustBeOneUniqueCanonicalSupportedInteger()
    {
        var serialized = codec.Serialize(CreateState());
        foreach (var duplicate in new[] { "\"FormatVersion\":1,", "\"\\u0046ormatVersion\":1," })
            Assert.Throws<InvalidDataException>(() => codec.Deserialize(serialized.Insert(1, duplicate)));

        foreach (var raw in new[] { "\"1\"", "1.0", "1e0", "true" }) {
            var malformed = serialized.Replace(
                "\"FormatVersion\":1", $"\"FormatVersion\":{raw}", StringComparison.Ordinal);
            Assert.Throws<InvalidDataException>(() => codec.Deserialize(malformed));
        }
    }

    [Fact]
    public void UndefinedEnumValuesAreRejectedOnBothSidesOfTheBoundary()
    {
        var state = CreateState();
        var malformed = state with {
            Configuration = state.Configuration with { GameMode = (DiabloGameMode)123 },
        };
        Assert.Throws<InvalidDataException>(() => codec.Serialize(malformed));
        var document = ParseState();
        document["Configuration"]!["GameMode"] = 123;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void NullRequiredObjectsStringsAndArrayElementsAreRejected()
    {
        var document = ParseState();
        document["Participants"] = null;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
        document = ParseState();
        document["Participants"]![0] = null;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
        document = ParseState();
        document["Participants"]![0]!["Player"]!["CharacterName"] = null;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => codec.Serialize(CreateState() with { Participants = null! }));
    }

    [Theory]
    [InlineData("01")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("65536")]
    public void NumericGeometryDictionaryKeysCannotAliasOrOverflow(string key)
    {
        var document = ParseState();
        document["Geometry"]!["Levels"]![0]!["NativePieceProperties"]![key] = 0;

        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void DefinedFlagCombinationsRoundTripButUnknownBitsAreRejected()
    {
        var document = ParseState();
        document["Geometry"]!["Levels"]![0]!["NativePieceProperties"]!["1"] = 3;
        var restored = codec.Deserialize(document.ToJsonString());
        Assert.Equal(AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockLight,
            restored.Geometry!.Levels![0].NativePieceProperties![1]);
        Assert.Equal(codec.Serialize(restored), codec.Serialize(codec.Deserialize(codec.Serialize(restored))));

        document["Geometry"]!["Levels"]![0]!["NativePieceProperties"]!["1"] = 64;
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
        var malformed = restored with { Geometry = restored.Geometry with {
            Levels = [restored.Geometry.Levels[0] with {
                NativePieceProperties = new Dictionary<ushort, AuthoritativeDunTileFlags> { [1] = (AuthoritativeDunTileFlags)64 },
            }],
        } };
        Assert.Throws<InvalidDataException>(() => codec.Serialize(malformed));
    }

    [Fact]
    public void EscapedPairedUnicodeSurrogatesRoundTrip()
    {
        var state = WithName(CreateState(), "Hero \uD834\uDD1E");

        Assert.Equal(state.Participants[0].Player.CharacterName,
            codec.Deserialize(codec.Serialize(state)).Participants[0].Player.CharacterName);
    }

    [Theory]
    [InlineData("\"\\uD800\"")]
    [InlineData("\"\\uDC00\"")]
    [InlineData("\"\\uD800x\"")]
    [InlineData("\"\\uD800\\uD800\"")]
    public void EscapedUnpairedUnicodeSurrogatesAreRejected(string literal)
    {
        var text = codec.Serialize(WithName(CreateState(), "NAME_TOKEN"))
            .Replace("\"NAME_TOKEN\"", literal, StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => codec.Deserialize(text));
    }

    [Fact]
    public void LiteralUnpairedUnicodeSurrogatesAreRejectedBeforeReplacementEncoding()
    {
        var name = "Hero " + (char)0xD800;
        Assert.Throws<InvalidDataException>(() => codec.Serialize(WithName(CreateState(), name)));
        var text = codec.Serialize(WithName(CreateState(), "NAME_TOKEN"))
            .Replace("\"NAME_TOKEN\"", "\"" + name + "\"", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(text));
    }

    private JsonObject ParseState() => JsonNode.Parse(codec.Serialize(CreateState()))!.AsObject();

    private static (
        DiabloSimulationOptions Options,
        DiabloSimulation Simulation,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> Runtime,
        DiabloPlayerTemplate Template) CreateLiveFixture(
            bool worldBacked,
            IReadOnlyList<AuthoritativeQuestState>? quests = null)
    {
        var stores = new StoreCatalog();
        stores.AddStore(7, [new StoreItem(0, 41, 10)], AuthoritativeStoreRole.Smith);
        stores.AddStore(8, [], AuthoritativeStoreRole.Witch);
        AuthoritativeWorld? world = null;
        if (worldBacked) {
            world = new AuthoritativeWorld();
            world.AddLevel(new AuthoritativeLevel(0, 6, 6));
        }
        var options = new DiabloSimulationOptions(stores, DiabloModule.Instance.Rules) {
            World = world,
            FallbackWidth = 6,
            FallbackHeight = 6,
            GameMode = DiabloGameMode.Multiplayer,
            RandomSeed = 12345,
            Quests = quests,
            CombatTargets = [new AuthoritativeCombatTarget(21, 3, 3, 10, maxHitPoints: 12, aggroRange: 0)],
            Objects = [new AuthoritativeWorldObject(22, 1, 0, 4, 4)],
            WorldItems = [new AuthoritativeWorldItem(100, 0, 1, 0, 123, 0,
                AuthoritativeItemState.Empty with { ItemType = 1, ItemIndex = 1 })],
        };
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var template = DiabloPlayerTemplate.CreateDefault(0, 2560, 1280, 0);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("participant-a", player => {
            player.EntityId = 1;
            player.CharacterName = "A";
        });
        runtime.Write("participant-b", player => {
            player.EntityId = 2;
            player.CharacterName = "B";
            player.PositionY = 2;
        });
        return (options, simulation, runtime, template);
    }

    private static DiabloCohortState WithName(DiabloCohortState state, string name) => state with {
        Participants = [state.Participants[0] with {
            Player = state.Participants[0].Player with { CharacterName = name },
        }],
    };

    private static DiabloCohortState CreateState()
    {
        var item = AuthoritativeItemState.Empty with {
            BaseName = "Checkpoint item",
            IdentifiedName = "Checkpoint item +1",
            Identified = true,
            PlusStrength = 1,
        };
        var player = new StorePlayerSnapshot(
            100, 7, [new OwnedStoreItem(7, 1, 17, 10, 19, item)],
            11, 25, 9, 15,
            new PlayerAttributesState(new(10, 11), new(12, 12), new(13, 13), new(14, 14)),
            [new EquippedStoreItem(0, 23, item)], [1, 0, 0, 0], [new BeltStoreItem(0, 29, item)]) {
            CharacterName = "Hero",
            EntityId = 1,
            LifeMaximum = 25,
            BaseLifeMaximum = 25,
            BaseManaMaximum = 15,
            HeldItem = new OwnedStoreItem(7, 3, 37, 10, 19, item),
            Stash = [new StashStoreItem(0, 0, 7, 2, 31, 10, 19, item)],
            CurrentAction = new AuthoritativePlayerActionState(DiabloPlayerActionKind.Move, 20, 21, 22, 23) {
                DirectionX = 1,
            },
            CombatMode = DiabloPlayerCombatMode.WalkSideways,
            CombatModeEndTick = 23,
        };
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(0, 4, 4));
        var backing = geometry.CaptureGeometryState();
        backing = backing with {
            Levels = [backing.Levels![0] with {
                NativePieceProperties = new Dictionary<ushort, AuthoritativeDunTileFlags> { [1] = 0 },
            }],
        };
        return new DiabloCohortState(
            DiabloCohortState.CurrentFormatVersion,
            42,
            new DiabloCohortConfigurationState(DiabloGameMode.SinglePlayer, (DiabloGameVariant)0,
                DiabloDifficulty.Normal, true, 3, 0, 0, 0, false, null, null),
            [new DiabloCohortParticipantState("opaque-participant", 1, player, 19, 5, 255, false,
                PlayerClassBound: false, ItemCatalogBound: false)],
            [new DiabloCohortCombatTargetState(
                new DiabloMonsterState(2, 1, 0, 2, 2, 10, 12, 0, 1, 4, 0, 0, 0) { HitPointsQ6 = 641 },
                new DiabloWorldItemState(3, 0, 2, 2, 43, 10, item), true)],
            [new DiabloWorldItemState(3, 0, 2, 2, 43, 10, item)],
            [], [], [],
            [new DiabloCohortPortalState(1, 0, 1, 1, 0, 2, 2, -1, false, "", "portal", 7, 1, true)],
            [], [], [], [], [],
            [new DiabloStoreCatalogState(7, AuthoritativeStoreRole.Smith, [new StoreItem(4, 41, 10, item)]),
                new DiabloStoreCatalogState(8, AuthoritativeStoreRole.Witch, [])],
            new DiabloCohortAllocatorState(new(0x40000017, 0, 0x10000031), new(0x40000000, uint.MaxValue, 0x80000000), 0x10000001),
            7, true, backing, new DiabloWorldInfrastructureState(4, 4, [], [], [], null, backing.Levels));
    }
}
