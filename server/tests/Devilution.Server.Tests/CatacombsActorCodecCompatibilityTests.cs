using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class DiabloCohortCodecTests
{
    [Fact]
    public void EveryCurrentCohortFormatRefusesNondefaultCatacombsActorState()
    {
        for (var formatVersion = 1; formatVersion <= DiabloCohortState.NativeTownProgressionFormatVersion; formatVersion++) {
            foreach (var state in CatacombsActorStates(formatVersion)) {
                var error = Assert.Throws<InvalidDataException>(() => codec.Serialize(state));
                Assert.Equal("Catacombs actor state requires its additive cohort format.", error.Message);
            }
        }
    }

    [Fact]
    public void FormatsOneAndTwoRejectCatacombsActorParentsExplicitly()
    {
        foreach (var baseline in new[] { CreateState(), CathedralDoorState() }) {
            var encoded = codec.Serialize(baseline);
            Assert.Equal(encoded, codec.Serialize(codec.Deserialize(encoded)));
            var document = JsonNode.Parse(encoded)!.AsObject();
            var target = document["CombatTargets"]![0]!.AsObject();
            target["NativeInitialization"] = new JsonObject { [nameof(AuthoritativeNativeMonsterInitialization.CatacombsDepth)] = 0 };
            var initializationError = Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
            Assert.Equal("Unknown or duplicate cohort field 'NativeInitialization'.", initializationError.Message);

            document = JsonNode.Parse(encoded)!.AsObject();
            target = document["CombatTargets"]![0]!.AsObject();
            target["Monster"]!["NativeMeleeCursor"] = new JsonObject {
                ["FrameIndex"] = 0,
                ["TickCounter"] = 0,
                ["HitRecovery"] = false,
                [nameof(AuthoritativeNativeMeleeCursor.UsesSpecialAttack)] = false,
            };
            var cursorError = Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
            Assert.Equal("Unknown or duplicate cohort field 'NativeMeleeCursor'.", cursorError.Message);
        }
    }

    [Fact]
    public void FormatsThreeThroughElevenRejectEachExplicitDefaultInitializationField()
    {
        var fields = new[] {
            nameof(AuthoritativeNativeMonsterInitialization.CatacombsDepth),
            nameof(AuthoritativeNativeMonsterInitialization.NativeSpecialAttackFrameCount),
            nameof(AuthoritativeNativeMonsterInitialization.NativeSpecialAttackTicksPerFrame),
        };
        for (var formatVersion = DiabloCohortState.NativeMonsterFormatVersion;
             formatVersion <= DiabloCohortState.NativeTownProgressionFormatVersion; formatVersion++) {
            foreach (var field in fields) {
                var document = MinimalCohort(formatVersion, new JsonObject {
                    ["NativeInitialization"] = new JsonObject { [field] = 0 },
                });
                var error = Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
                Assert.Equal($"Unknown or duplicate cohort field '{field}'.", error.Message);
            }
        }
    }

    [Fact]
    public void FormatsFiveThroughElevenRejectExplicitDefaultSpecialCursorField()
    {
        for (var formatVersion = DiabloCohortState.NativeCombatFormatVersion;
             formatVersion <= DiabloCohortState.NativeTownProgressionFormatVersion; formatVersion++) {
            var document = MinimalCohort(formatVersion, new JsonObject {
                ["Monster"] = new JsonObject {
                    ["NativeMeleeCursor"] = new JsonObject {
                        ["FrameIndex"] = 0,
                        ["TickCounter"] = 0,
                        ["HitRecovery"] = false,
                        [nameof(AuthoritativeNativeMeleeCursor.UsesSpecialAttack)] = false,
                    },
                },
            });
            var error = Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
            Assert.Equal("Unknown or duplicate cohort field 'UsesSpecialAttack'.", error.Message);
        }
    }

    [Fact]
    public void ValidNativeMonsterAndCombatBaselinesDecodeBeforeDefaultC2FieldsAreRefused()
    {
        var nativeMonster = codec.Serialize(NativeState(0, 1));
        Assert.Equal(nativeMonster, codec.Serialize(codec.Deserialize(nativeMonster)));
        foreach (var field in new[] {
            nameof(AuthoritativeNativeMonsterInitialization.CatacombsDepth),
            nameof(AuthoritativeNativeMonsterInitialization.NativeSpecialAttackFrameCount),
            nameof(AuthoritativeNativeMonsterInitialization.NativeSpecialAttackTicksPerFrame),
        }) {
            var document = JsonNode.Parse(nativeMonster)!.AsObject();
            document["CombatTargets"]![0]!["NativeInitialization"]![field] = 0;
            var error = Assert.Throws<InvalidDataException>(() => codec.Deserialize(document.ToJsonString()));
            Assert.Equal($"Unknown or duplicate cohort field '{field}'.", error.Message);
        }

        var nativeCombat = codec.Serialize(NativeCombatState());
        Assert.Equal(nativeCombat, codec.Serialize(codec.Deserialize(nativeCombat)));
        var cursorDocument = JsonNode.Parse(nativeCombat)!.AsObject();
        cursorDocument["CombatTargets"]![0]!["Monster"]!["NativeMeleeCursor"]![
            nameof(AuthoritativeNativeMeleeCursor.UsesSpecialAttack)] = false;
        var cursorError = Assert.Throws<InvalidDataException>(() => codec.Deserialize(cursorDocument.ToJsonString()));
        Assert.Equal("Unknown or duplicate cohort field 'UsesSpecialAttack'.", cursorError.Message);
    }

    [Fact]
    public void DefaultCatacombsActorSupplementsDoNotChangeLegacyBytes()
    {
        var legacy = codec.Serialize(CreateState());
        var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacy))).ToLowerInvariant();
        Assert.Equal("067bdf7e13316b858887c1ffde3364da249dfde4783f4de5c977c81b0a1a3ed0", legacyHash);

        var native = NativeState(0, 1);
        var nativeBaseline = codec.Serialize(native);
        var target = Assert.Single(native.CombatTargets);
        var defaultSupplements = native with {
            CombatTargets = [target with {
                NativeInitialization = target.NativeInitialization! with {
                    CatacombsDepth = 0,
                    NativeSpecialAttackFrameCount = 0,
                    NativeSpecialAttackTicksPerFrame = 0,
                },
            }],
        };
        var encoded = codec.Serialize(defaultSupplements);

        Assert.DoesNotContain("CatacombsDepth", encoded);
        Assert.DoesNotContain("NativeSpecialAttackFrameCount", encoded);
        Assert.DoesNotContain("NativeSpecialAttackTicksPerFrame", encoded);
        Assert.DoesNotContain("UsesSpecialAttack", encoded);
        Assert.Equal(nativeBaseline, encoded);
        Assert.Equal(encoded, codec.Serialize(codec.Deserialize(encoded)));

        var nativeCombat = NativeCombatState();
        var nativeCombatBaseline = codec.Serialize(nativeCombat);
        var combatTarget = Assert.Single(nativeCombat.CombatTargets);
        var explicitDefault = nativeCombat with {
            CombatTargets = [combatTarget with {
                Monster = combatTarget.Monster with {
                    NativeMeleeCursor = combatTarget.Monster.NativeMeleeCursor! with {
                        UsesSpecialAttack = false,
                    },
                },
                NativeInitialization = combatTarget.NativeInitialization! with {
                    CatacombsDepth = 0,
                    NativeSpecialAttackFrameCount = 0,
                    NativeSpecialAttackTicksPerFrame = 0,
                },
            }],
        };
        Assert.DoesNotContain("UsesSpecialAttack", nativeCombatBaseline);
        Assert.Equal(nativeCombatBaseline, codec.Serialize(explicitDefault));
    }

    private IEnumerable<DiabloCohortState> CatacombsActorStates(int formatVersion)
    {
        var native = NativeState(0, 1) with { FormatVersion = formatVersion };
        var nativeTarget = Assert.Single(native.CombatTargets);
        var initialization = nativeTarget.NativeInitialization!;
        yield return native with {
            CombatTargets = [nativeTarget with {
                NativeInitialization = initialization with { CatacombsDepth = 5 },
            }],
        };
        yield return native with {
            CombatTargets = [nativeTarget with {
                NativeInitialization = initialization with { NativeSpecialAttackFrameCount = 1 },
            }],
        };
        yield return native with {
            CombatTargets = [nativeTarget with {
                NativeInitialization = initialization with { NativeSpecialAttackTicksPerFrame = 1 },
            }],
        };

        var legacy = CreateState() with { FormatVersion = formatVersion };
        var legacyTarget = Assert.Single(legacy.CombatTargets);
        yield return legacy with {
            CombatTargets = [legacyTarget with {
                Monster = legacyTarget.Monster with {
                    NativeMeleeCursor = new AuthoritativeNativeMeleeCursor(0, 0) {
                        UsesSpecialAttack = true,
                    },
                },
            }],
        };
    }

    private DiabloCohortState CathedralDoorState()
    {
        var fingerprint = new string('A', 64);
        return CreateState() with {
            FormatVersion = DiabloCohortState.CathedralDoorFormatVersion,
            CathedralDoors = new DiabloCathedralDoorCheckpoint(
                1, "original-diablo.cathedral-doors", 1, fingerprint,
                new DiabloWorldGeometryState([], []), [], [new(1, [], [], [])]),
        };
    }

    private static JsonObject MinimalCohort(int formatVersion, JsonObject target)
        => new() {
            ["FormatVersion"] = formatVersion,
            ["CombatTargets"] = new JsonArray(target),
        };
}
