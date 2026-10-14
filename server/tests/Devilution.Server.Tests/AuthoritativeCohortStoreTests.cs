using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeCohortStoreTests
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact]
    public void RoundTripDetachesAndSortsTheCompleteRosterAndLedgers()
    {
        var root = CreateRoot();
        try {
            var firstReceipts = new List<CommandResult> {
                new() {
                    ClientSequence = 9,
                    ServerReceiptSequence = 11,
                    AppliedTick = 20,
                    Status = CommandStatus.Accepted,
                    RejectReason = CommandRejectReason.Unspecified,
                },
                new() {
                    ClientSequence = 2,
                    ServerReceiptSequence = 4,
                    AppliedTick = 19,
                    Status = CommandStatus.Rejected,
                    RejectReason = CommandRejectReason.InvalidTarget,
                },
            };
            var participants = new List<AuthoritativeCohortParticipantCheckpoint> {
                new(new string('b', 64), 2, firstReceipts),
                new(new string('a', 64), 1, []),
            };
            var document = CreateDocument(1) with { Participants = participants };
            using (var store = new AuthoritativeCohortStore(root))
                store.Save(document);

            firstReceipts[0].ClientSequence = 99;
            participants[0] = new AuthoritativeCohortParticipantCheckpoint(
                new string('b', 64),
                2,
                []);

            using (var reader = new AuthoritativeCohortStore(root)) {
                var loaded = reader.Load(CreateDocument(1).Identity);
                Assert.NotNull(loaded);
                Assert.Equal(1UL, loaded!.Generation);
                Assert.Equal(
                    [new string('a', 64), new string('b', 64)],
                    loaded.Participants.Select(participant => participant.SessionId));
                Assert.Equal(
                    [2UL, 9UL],
                    loaded.Participants[1].CommandReceipts.Select(receipt => receipt.ClientSequence));
                Assert.Equal(
                    [4UL, 11UL],
                    loaded.Participants[1].CommandReceipts.Select(receipt => receipt.ServerReceiptSequence));
            }
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SaveRereadsAndRejectsSameLengthCanonicalCorruptionBeforeUsingTheCache()
    {
        var root = CreateRoot();
        try {
            using var store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1));

            var original = File.ReadAllBytes(store.CheckpointPath);
            var text = Encoding.UTF8.GetString(original);
            const string integrityMarker = "\"IntegritySha256\":\"";
            var integrityOffset = text.IndexOf(integrityMarker, StringComparison.Ordinal);
            Assert.True(integrityOffset >= 0);
            integrityOffset += integrityMarker.Length;
            var changed = text[integrityOffset] == '0' ? '1' : '0';
            var corrupted = Encoding.UTF8.GetBytes(
                text[..integrityOffset] + changed + text[(integrityOffset + 1)..]);
            Assert.Equal(original.Length, corrupted.Length);
            File.WriteAllBytes(store.CheckpointPath, corrupted);

            Assert.Throws<InvalidDataException>(() => store.Save(CreateDocument(2)));
            Assert.Equal(corrupted, File.ReadAllBytes(store.CheckpointPath));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SaveCacheDoesNotShareCallerOrLoadedReceiptInstances()
    {
        var root = CreateRoot();
        try {
            using var store = new AuthoritativeCohortStore(root);
            var first = CreateDocument(1);
            store.Save(first);

            first.Participants[0].CommandReceipts[0].ClientSequence = 99;
            var loaded = store.Load();
            Assert.NotNull(loaded);
            loaded!.Participants[0].CommandReceipts[0].ServerReceiptSequence = 99;

            store.Save(CreateDocument(2) with { Tick = 21 });
            var persisted = store.Load();
            Assert.NotNull(persisted);
            Assert.Equal(2UL, persisted!.Generation);
            Assert.Equal(1UL, persisted.Participants[0].CommandReceipts[0].ClientSequence);
            Assert.Equal(3UL, persisted.Participants[0].CommandReceipts[0].ServerReceiptSequence);
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SaveRetryAfterPostRenameObserverFailureReadsThePublishedGeneration()
    {
        var root = CreateRoot();
        try {
            using var store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1));
            store.PublicationObserver = (phase, _) => {
                if (phase == AuthoritativePublicationPhase.AfterCanonicalRename)
                    throw new IOException("post-rename observer failure");
            };

            Assert.Throws<IOException>(() => store.Save(CreateDocument(2) with { Tick = 21 }));
            store.PublicationObserver = null;
            Assert.Equal(2UL, store.Load()!.Generation);

            store.Save(CreateDocument(3) with { Tick = 22 });
            var retried = store.Load();
            Assert.NotNull(retried);
            Assert.Equal(3UL, retried!.Generation);
            Assert.Equal(22UL, retried.Tick);
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SaveRequiresTheExactNextGenerationAndPreservesThePreviousDocument()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            var first = CreateDocument(1);
            store.Save(first);

            Assert.Throws<InvalidDataException>(() => store.Save(first with { Generation = 3 }));
            Assert.Throws<InvalidDataException>(() => store.Save(first with {
                Generation = 2,
                Identity = first.Identity with { GameMode = AuthoritativeGameMode.Multiplayer },
            }));
            Assert.Equal(1UL, store.Load()!.Generation);

            store.Save(first with { Generation = 2, Tick = 21 });
            Assert.Equal(21UL, store.Load()!.Tick);
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SaveRejectsTickAndCursorRewindsButAllowsEqualTicksAndExhaustion()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            var first = CreateDocument(1);
            store.Save(first);

            Assert.Throws<InvalidDataException>(() => store.Save(first with {
                Generation = 2,
                Tick = first.Tick - 1,
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(first with {
                Generation = 2,
                NextEntityId = first.NextEntityId - 1,
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(first with {
                Generation = 2,
                NextServerReceiptSequence = first.NextServerReceiptSequence - 1,
            }));

            store.Save(first with {
                Generation = 2,
                Tick = first.Tick,
                NextEntityId = 0,
                NextServerReceiptSequence = 0,
            });
            Assert.Throws<InvalidDataException>(() => store.Save(first with {
                Generation = 3,
                Tick = first.Tick + 1,
                NextEntityId = 10,
                NextServerReceiptSequence = 20,
            }));
            store.Save(first with {
                Generation = 3,
                Tick = first.Tick + 1,
                NextEntityId = 0,
                NextServerReceiptSequence = 0,
            });
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SaveRetainsCommittedBindingsAndReceiptsAndRejectsReusedAllocationHoles()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            var first = CreateDocument(1);
            store.Save(first);

            var added = first with {
                Generation = 2,
                Participants = [
                    first.Participants[0] with {
                        CommandReceipts = [
                            first.Participants[0].CommandReceipts[0].Clone(),
                            new CommandResult {
                                ClientSequence = 2,
                                ServerReceiptSequence = 20,
                                AppliedTick = first.Tick,
                                Status = CommandStatus.Accepted,
                                RejectReason = CommandRejectReason.Unspecified,
                            },
                        ],
                    },
                    new(new string('b', 64), 10, []),
                ],
                NextEntityId = 11,
                NextServerReceiptSequence = 21,
            };
            store.Save(added);

            store.Dispose();
            store = new AuthoritativeCohortStore(root);
            Assert.Equal(2UL, store.Load(added.Identity)!.Generation);
            store.Save(added with { Generation = 3, Tick = added.Tick });

            Assert.Throws<InvalidDataException>(() => store.Save(added with {
                Generation = 4,
                Participants = [added.Participants[0]],
            }));
            var committedBytes = File.ReadAllBytes(store.CheckpointPath);
            var bindingError = Assert.Throws<InvalidDataException>(() => store.Save(added with {
                Generation = 4,
                NextEntityId = 13,
                Participants = [added.Participants[0] with { EntityId = 12 }, added.Participants[1]],
            }));
            Assert.Contains("entity binding cannot change", bindingError.Message, StringComparison.Ordinal);
            Assert.Equal(committedBytes, File.ReadAllBytes(store.CheckpointPath));
            Assert.Throws<InvalidDataException>(() => store.Save(added with {
                Generation = 4,
                Participants = [added.Participants[0] with {
                    CommandReceipts = [
                        ReceiptWith(
                            added.Participants[0].CommandReceipts[0],
                            receipt => receipt.AppliedTick = added.Tick - 1),
                        added.Participants[0].CommandReceipts[1],
                    ],
                }, added.Participants[1]],
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(added with {
                Generation = 4,
                Participants = [
                    added.Participants[0],
                    added.Participants[1],
                    new(new string('c', 64), 9, []),
                ],
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(added with {
                Generation = 4,
                Participants = [added.Participants[0] with {
                    CommandReceipts = [
                        added.Participants[0].CommandReceipts[0],
                        ReceiptWith(
                            added.Participants[0].CommandReceipts[1],
                            receipt => receipt.ServerReceiptSequence = 19),
                    ],
                }, added.Participants[1]],
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(added with {
                Generation = 4,
                Participants = [added.Participants[0] with {
                    CommandReceipts = [
                        added.Participants[0].CommandReceipts[0],
                        added.Participants[0].CommandReceipts[1],
                        CreateAcceptedReceipt(3, 19, added.Tick),
                    ],
                }, added.Participants[1]],
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(added with {
                Generation = 4,
                Participants = [
                    added.Participants[0],
                    added.Participants[1],
                    new(new string('c', 64), 11, [CreateAcceptedReceipt(3, 19, added.Tick)]),
                ],
                NextEntityId = 12,
                NextServerReceiptSequence = 21,
            }));

            var receiptExhausted = added with {
                Generation = 4,
                NextServerReceiptSequence = 0,
            };
            store.Save(receiptExhausted);
            Assert.Throws<InvalidDataException>(() => store.Save(receiptExhausted with {
                Generation = 5,
                Participants = [
                    receiptExhausted.Participants[0],
                    receiptExhausted.Participants[1],
                    new(new string('c', 64), 11, [CreateAcceptedReceipt(3, 22, added.Tick)]),
                ],
                NextEntityId = 12,
                NextServerReceiptSequence = 0,
            }));

            var exhausted = added with {
                Generation = 5,
                NextEntityId = 0,
                NextServerReceiptSequence = 0,
            };
            store.Save(exhausted);
            Assert.Throws<InvalidDataException>(() => store.Save(exhausted with {
                Generation = 6,
                Participants = [
                    exhausted.Participants[0],
                    exhausted.Participants[1],
                    new(new string('d', 64), 11, []),
                ],
                NextEntityId = 0,
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(exhausted with {
                Generation = 6,
                Participants = [exhausted.Participants[0] with {
                    CommandReceipts = [
                        exhausted.Participants[0].CommandReceipts[0],
                        exhausted.Participants[0].CommandReceipts[1],
                        CreateAcceptedReceipt(3, 22, exhausted.Tick),
                    ],
                }, exhausted.Participants[1]],
                NextServerReceiptSequence = 0,
            }));
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void VerifiedIdentityTransitionRequiresExactPriorIdentityAndPreservesStableFields()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            var first = CreateDocument(1);
            store.Save(first);
            var transitioned = first with {
                Generation = 2,
                Identity = first.Identity with {
                    ModuleFormatVersion = 2,
                    ContentManifestHash = new string('e', 64),
                    RulesetIdentityHash = new string('f', 64),
                },
            };

            Assert.Throws<InvalidDataException>(() => store.Save(transitioned));
            store.Save(transitioned, first.Identity);
            Assert.Equal(transitioned.Identity, store.Load()!.Identity);

            var wrongPrior = transitioned with {
                Generation = 3,
                Identity = transitioned.Identity with { ContentManifestHash = new string('a', 64) },
            };
            Assert.Throws<InvalidDataException>(() => store.Save(wrongPrior, first.Identity));
            Assert.Equal(2UL, store.Load()!.Generation);

            Assert.Throws<InvalidDataException>(() => store.Save(
                transitioned with {
                    Generation = 3,
                    Identity = transitioned.Identity with { ModuleId = "other" },
                },
                transitioned.Identity));
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void TamperedCheckpointIsRejectedAndCannotBeOverwritten()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1));
            var original = File.ReadAllBytes(store.CheckpointPath);
            original[original.Length - 2] = (byte)(original[original.Length - 2] == (byte)'0' ? '1' : '0');
            File.WriteAllBytes(store.CheckpointPath, original);

            Assert.Throws<InvalidDataException>(() => store.Load());
            Assert.Throws<InvalidDataException>(() => store.Save(CreateDocument(2)));
            Assert.Equal(original, File.ReadAllBytes(store.CheckpointPath));
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void ExpectedIdentityFencesEveryIdentityFieldIncludingMode()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            var document = CreateDocument(1);
            store = new AuthoritativeCohortStore(root);
            store.Save(document);

            Assert.NotNull(store.Load(document.Identity));
            Assert.Throws<InvalidDataException>(() => store.Load(document.Identity with {
                GameMode = AuthoritativeGameMode.Multiplayer,
            }));
            Assert.Throws<InvalidDataException>(() => store.Load(document.Identity with {
                RulesetIdentityHash = new string('f', 64),
            }));
            Assert.Throws<InvalidDataException>(() => store.Load(document.Identity with {
                ModuleId = "other-module",
            }));
            Assert.Throws<InvalidDataException>(() => store.Load(document.Identity with {
                ModuleFormatVersion = 2,
            }));
            Assert.Throws<InvalidDataException>(() => store.Load(document.Identity with {
                ProtocolSchemaVersion = "0.3.0",
            }));
            Assert.Throws<InvalidDataException>(() => store.Load(document.Identity with {
                ContentManifestHash = new string('e', 64),
            }));
            Assert.Throws<InvalidDataException>(() => store.Load(document.Identity with {
                TickRateHz = 30,
            }));
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void EmptyAndExhaustedCursorsRoundTripWithoutInventingRosterLimits()
    {
        var root = CreateRoot();
        try {
            var empty = CreateDocument(1) with {
                NextEntityId = 0,
                NextServerReceiptSequence = 0,
                Participants = [],
            };
            using (var store = new AuthoritativeCohortStore(root)) {
                store.Save(empty);
                var loaded = store.Load();
                Assert.NotNull(loaded);
                Assert.Equal(empty.Generation, loaded!.Generation);
                Assert.Equal(empty.NextEntityId, loaded.NextEntityId);
                Assert.Equal(empty.NextServerReceiptSequence, loaded.NextServerReceiptSequence);
                Assert.Empty(loaded.Participants);
            }

            DeleteRoot(root);
            var exhausted = CreateDocument(1) with {
                NextEntityId = 0,
                NextServerReceiptSequence = 0,
                Participants = [new(
                    new string('a', 64),
                    uint.MaxValue,
                    [new CommandResult {
                        ClientSequence = 1,
                        ServerReceiptSequence = ulong.MaxValue,
                        AppliedTick = 20,
                        Status = CommandStatus.Accepted,
                        RejectReason = CommandRejectReason.Unspecified,
                    }])],
            };
            using (var store = new AuthoritativeCohortStore(root)) {
                store.Save(exhausted);
                var loaded = store.Load();
                Assert.NotNull(loaded);
                Assert.Equal(0U, loaded!.NextEntityId);
                Assert.Equal(0UL, loaded.NextServerReceiptSequence);
            }
            DeleteRoot(root);
            var lowerSurvivingMaximum = CreateDocument(1) with {
                NextEntityId = 0,
                NextServerReceiptSequence = 0,
                Participants = [new(
                    new string('a', 64),
                    7,
                    [new CommandResult {
                        ClientSequence = 1,
                        ServerReceiptSequence = 9,
                        AppliedTick = 20,
                        Status = CommandStatus.Accepted,
                        RejectReason = CommandRejectReason.Unspecified,
                    }])],
            };
            using (var store = new AuthoritativeCohortStore(root)) {
                store.Save(lowerSurvivingMaximum);
                var loaded = store.Load();
                Assert.NotNull(loaded);
                Assert.Equal(0U, loaded!.NextEntityId);
                Assert.Equal(0UL, loaded.NextServerReceiptSequence);
            }
        } finally {
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("malformed")]
    [InlineData("invalid-utf8")]
    public void MalformedCheckpointShapesFailClosed(string kind)
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            switch (kind) {
                case "unknown":
                    File.WriteAllText(store.CheckpointPath, "{\"Unknown\":1}");
                    break;
                case "duplicate":
                    File.WriteAllText(store.CheckpointPath, "{\"FormatVersion\":1,\"FormatVersion\":1}");
                    break;
                case "malformed":
                    File.WriteAllText(store.CheckpointPath, "{not-json");
                    break;
                default:
                    File.WriteAllBytes(store.CheckpointPath, [0xC3, 0x28]);
                    break;
            }

            Assert.Throws<InvalidDataException>(() => store.Load());
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SemanticValidationRejectsDuplicateBindingsReceiptsAndUnsafeCursors()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            var baseline = CreateDocument(1);
            var duplicateEntity = baseline with {
                Participants = [
                    baseline.Participants[0],
                    new(new string('b', 64), baseline.Participants[0].EntityId, []),
                ],
            };
            Assert.Throws<InvalidDataException>(() => store.Save(duplicateEntity));

            var duplicateReceipt = baseline with {
                Participants = [
                    baseline.Participants[0],
                    new(new string('b', 64), 2, [baseline.Participants[0].CommandReceipts[0].Clone()]),
                ],
            };
            Assert.Throws<InvalidDataException>(() => store.Save(duplicateReceipt));

            Assert.Throws<InvalidDataException>(() => store.Save(baseline with { NextEntityId = 1 }));
            Assert.Throws<InvalidDataException>(() => store.Save(baseline with { NextServerReceiptSequence = 1 }));
            Assert.Throws<InvalidDataException>(() => store.Save(baseline with {
                Participants = [baseline.Participants[0] with {
                    CommandReceipts = [new CommandResult {
                        ClientSequence = 1,
                        ServerReceiptSequence = 3,
                        AppliedTick = 21,
                        Status = CommandStatus.Accepted,
                        RejectReason = CommandRejectReason.Unspecified,
                    }],
                }],
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(baseline with {
                Identity = baseline.Identity with { TickRateHz = 0 },
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(baseline with {
                Identity = baseline.Identity with { ModuleFormatVersion = 0 },
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(baseline with {
                Identity = baseline.Identity with { ModuleId = "diablo\uD800" },
            }));
            Assert.Throws<InvalidDataException>(() => store.Save(baseline with {
                Identity = baseline.Identity with { ProtocolSchemaVersion = "0.2.\uD800" },
            }));
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void ExclusiveLeaseRejectsSecondWriterAndCanBeReacquiredAfterDispose()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? first = null;
        AuthoritativeCohortStore? reacquired = null;
        try {
            first = new AuthoritativeCohortStore(root);
            Assert.ThrowsAny<IOException>(() => new AuthoritativeCohortStore(root));
            first.Dispose();
            first = null;

            reacquired = new AuthoritativeCohortStore(root);
            reacquired.Save(CreateDocument(1));
            Assert.NotNull(reacquired.Load());
        } finally {
            first?.Dispose();
            reacquired?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SignedMalformedModulePayloadsFailClosed()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1));
            RewriteSignedRoot(store.CheckpointPath, rootNode =>
                rootNode["SerializedModuleState"] = "%%%");
            Assert.Throws<InvalidDataException>(() => store.Load());

            RewriteSignedRoot(store.CheckpointPath, rootNode =>
                rootNode["SerializedModuleState"] = "AP8=" );
            Assert.Throws<InvalidDataException>(() => store.Load());
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void ReplacementKeepsAnOpenReaderOnTheCompletePreviousGeneration()
    {
        var root = CreateRoot();
        try {
            using var store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1));
            var original = File.ReadAllBytes(store.CheckpointPath);
            using var reader = new FileStream(
                store.CheckpointPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            store.Save(CreateDocument(2) with { Tick = 21 });
            using var captured = new MemoryStream();
            reader.CopyTo(captured);
            Assert.Equal(original, captured.ToArray());
            Assert.Equal(2UL, store.Load()!.Generation);
            Assert.Equal(21UL, store.Load()!.Tick);
            Assert.Equal(
                ["authoritative-cohort.json", "authoritative-cohort.lease"],
                Directory.GetFiles(root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ConcurrentReadersAlwaysObserveAnIntactCanonicalGeneration()
    {
        var root = CreateRoot();
        CancellationTokenSource? finished = null;
        ManualResetEventSlim? ready = null;
        Task? reader = null;
        try {
            using var store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1) with { SerializedModuleState = "module-state-1" });
            finished = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            ready = new ManualResetEventSlim();
            var reads = 0;
            reader = Task.Run(() => {
                do {
                    using var input = new FileStream(
                        store.CheckpointPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var buffer = new MemoryStream();
                    input.CopyTo(buffer);
                    var bytes = buffer.ToArray();
                    using var parsed = JsonDocument.Parse(bytes);
                    var document = parsed.RootElement;
                    var generation = document.GetProperty("Generation").GetUInt64();
                    var module = Encoding.UTF8.GetString(Convert.FromBase64String(
                        document.GetProperty("SerializedModuleState").GetString()!));
                    Assert.Equal($"module-state-{generation}", module);
                    var text = Encoding.UTF8.GetString(bytes);
                    var integrityOffset = text.LastIndexOf(
                        ",\"IntegritySha256\":", StringComparison.Ordinal);
                    Assert.True(integrityOffset > 0);
                    var payload = Encoding.UTF8.GetBytes(text[..integrityOffset] + "}");
                    Assert.Equal(
                        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(),
                        document.GetProperty("IntegritySha256").GetString());
                    Interlocked.Increment(ref reads);
                    ready.Set();
                } while (!finished.IsCancellationRequested);
            }, TestContext.Current.CancellationToken);
            try {
                Assert.True(ready.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
                    "The concurrent reader did not initialize.");
                for (ulong generation = 2; generation <= 100; generation++)
                    store.Save(CreateDocument(generation) with {
                        SerializedModuleState = $"module-state-{generation}",
                    });
            } finally {
                finished.Cancel();
                // Cleanup must join independently of a cancelled xUnit token.
#pragma warning disable xUnit1051
                await reader.WaitAsync(TimeSpan.FromSeconds(5));
#pragma warning restore xUnit1051
            }
            Assert.True(reads > 0);
            Assert.Equal(100UL, store.Load()!.Generation);
        } finally {
            if (reader is null || reader.IsCompleted) {
                ready?.Dispose();
                finished?.Dispose();
                DeleteRoot(root);
            } else {
                TestContext.Current.SendDiagnosticMessage(
                    $"Retained publication fixture while its reader is still live: {root}");
            }
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "File replacement sharing semantics are Windows-specific.")]
    public void FailedReplacementPreservesThePreviousCheckpointAndCleansItsTemporaryFile()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        FileStream? blocker = null;
        try {
            store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1));
            var original = File.ReadAllBytes(store.CheckpointPath);
            blocker = new FileStream(
                store.CheckpointPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            Assert.ThrowsAny<IOException>(() => store.Save(CreateDocument(2) with { Tick = 21 }));
            Assert.Equal(original, File.ReadAllBytes(store.CheckpointPath));
            Assert.Empty(Directory.GetFiles(
                root,
                $".{AuthoritativeCohortStore.CheckpointFileName}.*.tmp"));
        } finally {
            blocker?.Dispose();
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Readonly replacement semantics are Windows-specific.")]
    public void ReadonlyCanonicalRefusesReplacementAndAllowsAnExactRetryAfterUnblocking()
    {
        var root = CreateRoot() + "- \u03a9";
        var checkpoint = Path.Combine(root, AuthoritativeCohortStore.CheckpointFileName);
        try {
            using var store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1));
            var original = File.ReadAllBytes(checkpoint);
            File.SetAttributes(checkpoint, FileAttributes.ReadOnly);
            Assert.ThrowsAny<IOException>(() => store.Save(CreateDocument(2)));
            Assert.Equal(original, File.ReadAllBytes(checkpoint));
            Assert.Equal(FileAttributes.ReadOnly, File.GetAttributes(checkpoint) & FileAttributes.ReadOnly);
            Assert.Empty(Directory.GetFiles(root, $".{AuthoritativeCohortStore.CheckpointFileName}.*.tmp"));

            File.SetAttributes(checkpoint, FileAttributes.Normal);
            store.Save(CreateDocument(2));
            Assert.Equal(2UL, store.Load()!.Generation);
        } finally {
            if (File.Exists(checkpoint))
                File.SetAttributes(checkpoint, FileAttributes.Normal);
            DeleteRoot(root);
        }
    }

    [Fact]
    public void CheckpointDirectoryCollisionFailsClosedAndPreservesTheDirectory()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            Directory.CreateDirectory(store.CheckpointPath);
            Assert.Throws<IOException>(() => store.Load());
            Assert.Throws<IOException>(() => store.Save(CreateDocument(1)));
            Assert.True(Directory.Exists(store.CheckpointPath));
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void GenerationOverflowAndDisposedOrInvalidRootsFailClosed()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthoritativeCohortStore(null!));
        Assert.Throws<ArgumentException>(() => new AuthoritativeCohortStore(string.Empty));
        Assert.Throws<ArgumentException>(() => new AuthoritativeCohortStore(Path.GetPathRoot(Path.GetTempPath())!));

        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1));
            RewriteSignedRoot(store.CheckpointPath, rootNode =>
                rootNode["Generation"] = JsonValue.Create(ulong.MaxValue));
            var original = File.ReadAllBytes(store.CheckpointPath);
            Assert.Throws<InvalidOperationException>(() => store.Save(CreateDocument(2)));
            Assert.Equal(original, File.ReadAllBytes(store.CheckpointPath));

            store.Dispose();
            store = null;
            using var disposed = new AuthoritativeCohortStore(root);
            disposed.Dispose();
            Assert.Throws<ObjectDisposedException>(() => disposed.Load());
            Assert.Throws<ObjectDisposedException>(() => disposed.Save(CreateDocument(1)));
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SignedShapeMutationsFailAtTheIntendedValidationLayer()
    {
        var root = CreateRoot();
        AuthoritativeCohortStore? store = null;
        try {
            store = new AuthoritativeCohortStore(root);
            store.Save(CreateDocument(1));
            var valid = File.ReadAllText(store.CheckpointPath);

            var unknown = JsonNode.Parse(valid)!.AsObject();
            unknown["Unknown"] = 1;
            File.WriteAllText(store.CheckpointPath, unknown.ToJsonString());
            Assert.Throws<InvalidDataException>(() => store.Load());

            File.WriteAllText(
                store.CheckpointPath,
                valid.Replace(
                    "\"FormatVersion\":1,",
                    "\"FormatVersion\":1,\"FormatVersion\":1,",
                    StringComparison.Ordinal));
            Assert.Throws<InvalidDataException>(() => store.Load());

            var wrongNumber = JsonNode.Parse(valid)!.AsObject();
            wrongNumber["FormatVersion"] = "1";
            File.WriteAllText(store.CheckpointPath, wrongNumber.ToJsonString());
            Assert.Throws<InvalidDataException>(() => store.Load());

            var missing = JsonNode.Parse(valid)!.AsObject();
            missing.Remove("Identity");
            File.WriteAllText(store.CheckpointPath, missing.ToJsonString());
            Assert.Throws<InvalidDataException>(() => store.Load());
        } finally {
            store?.Dispose();
            DeleteRoot(root);
        }
    }

    private static AuthoritativeCohortDocument CreateDocument(ulong generation)
    {
        return new AuthoritativeCohortDocument(
            AuthoritativeCohortStore.CurrentFormatVersion,
            generation,
            new AuthoritativeCohortIdentity(
                "diablo",
                1,
                "0.2.0",
                new string('c', 64),
                new string('d', 64),
                20,
                AuthoritativeGameMode.SinglePlayer),
            20,
            10,
            20,
            [new(
                new string('a', 64),
                1,
                [new CommandResult {
                    ClientSequence = 1,
                    ServerReceiptSequence = 3,
                    AppliedTick = 20,
                    Status = CommandStatus.Accepted,
                    RejectReason = CommandRejectReason.Unspecified,
                }])],
            "module-state");
    }

    private static string CreateRoot()
        => Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Authoritative-Cohort-Store-" + Guid.NewGuid().ToString("N"));

    private static void RewriteSignedRoot(
        string checkpointPath,
        Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(File.ReadAllText(checkpointPath))?.AsObject()
            ?? throw new InvalidDataException("The checkpoint root is not an object.");
        mutate(root);
        root.Remove("IntegritySha256");
        using var parsed = JsonDocument.Parse(root.ToJsonString());
        var canonical = JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions {
            PropertyNamingPolicy = null,
            WriteIndented = false,
        });
        root["IntegritySha256"] = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
        File.WriteAllText(
            checkpointPath,
            root.ToJsonString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static CommandResult ReceiptWith(
        CommandResult source,
        Action<CommandResult> mutate)
    {
        var clone = source.Clone();
        mutate(clone);
        return clone;
    }

    private static CommandResult CreateAcceptedReceipt(
        ulong clientSequence,
        ulong serverReceiptSequence,
        ulong appliedTick)
        => new() {
            ClientSequence = clientSequence,
            ServerReceiptSequence = serverReceiptSequence,
            AppliedTick = appliedTick,
            Status = CommandStatus.Accepted,
            RejectReason = CommandRejectReason.Unspecified,
        };

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
