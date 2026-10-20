using Google.Protobuf;
using Devilution.Diablo;
using Devilution.Domain.Inventory;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeTownResidentProjectionTests
{
    private const string CatalogFingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly (int TownerId, int X, int Y, int Direction)[] Residents = [
        (0, 62, 63, 1),
        (1, 55, 79, 7),
        (3, 55, 62, 1),
        (4, 62, 71, 0),
        (5, 71, 84, 0),
        (6, 80, 20, 0),
        (7, 43, 66, 0),
        (8, 11, 53, 0),
        (9, 58, 16, 1),
        (9, 56, 14, 3),
        (9, 59, 20, 4),
    ];

    [Fact]
    public void MapsCanonicalRosterAndCatalogIdentityThroughWireRoundTrip()
    {
        var source = CreateSource();
        var townLevel = new AuthoritativeLevel(
            0,
            112,
            112,
            blockedCells: [63 * 112 + 62],
            staticObjectBlockedCells: OccupiedCells());

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source, levelGeometry: [townLevel]);
        var restored = Snapshot.Parser.ParseFrom(projected.ToByteArray());

        Assert.Equal(CatalogFingerprint, restored.NativeTownCatalogFingerprint);
        Assert.Equal(11, restored.NativeTownResidents.Count);
        Assert.Equal(Enumerable.Range(0, 11), restored.NativeTownResidents.Select(row => row.ResidentSlot));
        Assert.Equal(Residents.Select(row => row.TownerId), restored.NativeTownResidents.Select(row => row.TownerId));
        Assert.Equal(Residents.Select(row => row.Direction), restored.NativeTownResidents.Select(row => row.NativeDirection));
        Assert.Equal(new[] { 9, 9, 9 }, restored.NativeTownResidents.Skip(8).Select(row => row.TownerId));
        Assert.Equal(projected, restored);
        var withoutPresentation = projected.Clone();
        withoutPresentation.NativeTownCatalogFingerprint = string.Empty;
        withoutPresentation.NativeTownResidents.Clear();
        Assert.Equal(SnapshotStateHasher.Compute(projected), SnapshotStateHasher.Compute(withoutPresentation));
        AuthoritativeSnapshotValidator.Validate(restored);
    }

    [Fact]
    public void GenericSnapshotsKeepLegacyOmittedProjectionFieldsAndBytes()
    {
        var generic = DiabloProtocolMapper.ToProtocolSnapshot(CreateSource(nativeTown: false));
        var legacyGolden = new Snapshot {
            Tick = 1,
            Difficulty = GameDifficulty.Normal,
        };
        legacyGolden.Players.Add(new PlayerSnapshot {
            EntityId = 1,
            PositionX = 75,
            PositionY = 68,
            Life = 64,
            CharacterLevel = 1,
            Attributes = new PlayerAttributesSnapshot {
                Strength = new AttributeSnapshot(),
                Magic = new AttributeSnapshot(),
                Dexterity = new AttributeSnapshot(),
                Vitality = new AttributeSnapshot(),
            },
        });
        legacyGolden.StateSha256 = SnapshotStateHasher.Compute(legacyGolden);

        Assert.Equal(string.Empty, generic.NativeTownCatalogFingerprint);
        Assert.Empty(generic.NativeTownResidents);
        Assert.Equal(legacyGolden.ToByteArray(), generic.ToByteArray());
        AuthoritativeSnapshotValidator.Validate(generic);
    }

    [Fact]
    public void LegacySchemaReaderPreservesTownProjectionAsUnknownWireFields()
    {
        var legacyBaseline = DiabloProtocolMapper.ToProtocolSnapshot(
            CreateSource(nativeTown: false, includeTownGreetings: true));
        var townSnapshot = DiabloProtocolMapper.ToProtocolSnapshot(CreateSource());
        var townBytes = townSnapshot.ToByteArray();
        var legacyReader = LegacySnapshotReader.Read(townBytes);

        Assert.Equal(1UL, legacyReader.Tick);
        Assert.Equal(
            legacyBaseline.ToByteArray(),
            LegacySnapshotReader.Write(legacyReader.KnownFields));
        Assert.Equal(new[] { 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 25 },
            legacyReader.UnknownFields.Select(field => field.Number));

        var rewritten = LegacySnapshotReader.Write(
            legacyReader.KnownFields.Concat(legacyReader.UnknownFields));
        Assert.Equal(townBytes, rewritten);
        Assert.Equal(townSnapshot, Snapshot.Parser.ParseFrom(rewritten));
    }

    [Fact]
    public void LegacySchemaReaderRejectsTruncatedFutureFieldsWithinItsBound()
    {
        Assert.Throws<InvalidDataException>(() => LegacySnapshotReader.Read([0xC2, 0x01, 0x7F]));
        Assert.Throws<InvalidDataException>(() => LegacySnapshotReader.Read([
            0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00,
        ]));
    }

    [Fact]
    public void ProtocolImportValidatesButDoesNotPromoteResidentPresentationToSimulationState()
    {
        var projected = DiabloProtocolMapper.ToProtocolSnapshot(CreateSource());

        var imported = DiabloProtocolMapper.FromProtocolSnapshot(projected);

        Assert.Empty(imported.NativeTownCatalogFingerprint);
        Assert.Empty(imported.NativeTownResidents);
    }

    [Fact]
    public void MapperRejectsPartialOrMalformedNativeTownProjection()
    {
        var source = CreateSource();
        var wrongSlot = source.NativeTownResidents.ToArray();
        wrongSlot[1] = wrongSlot[1] with { ResidentSlot = 0 };
        var wrongCowType = source.NativeTownResidents.ToArray();
        wrongCowType[9] = wrongCowType[9] with { TownerId = 8 };
        var wrongDirection = source.NativeTownResidents.ToArray();
        wrongDirection[10] = wrongDirection[10] with { NativeDirection = 8 };
        var wrongPosition = source.NativeTownResidents.ToArray();
        wrongPosition[8] = wrongPosition[8] with { PositionX = 57 };
        var wrongLevel = source.NativeTownResidents.ToArray();
        wrongLevel[0] = wrongLevel[0] with { LevelId = 1 };

        var malformed = new[] {
            source with { NativeTownCatalogFingerprint = string.Empty },
            source with { NativeTownResidents = [] },
            source with { NativeTownResidents = source.NativeTownResidents.Take(10).ToArray() },
            source with { NativeTownResidents = wrongSlot },
            source with { NativeTownResidents = wrongCowType },
            source with { NativeTownResidents = wrongDirection },
            source with { NativeTownResidents = wrongPosition },
            source with { NativeTownResidents = wrongLevel },
            source with { NativeTownCatalogFingerprint = new string('A', 64) },
            source with { TownerGreetings = source.TownerGreetings.Take(7).ToArray() },
            source with {
                TownerGreetings = source.TownerGreetings.Select(greeting => greeting.TownerId == 1
                    ? greeting with { PositionX = greeting.PositionX + 1 }
                    : greeting).ToArray(),
            },
        };

        foreach (var invalid in malformed) {
            Assert.Throws<InvalidDataException>(() => DiabloProtocolMapper.ToProtocolSnapshot(invalid));
        }
    }

    [Fact]
    public void ValidatorAcceptsThreeCowSlotsAndDoesNotRequireSmithCellToBeWalkable()
    {
        var snapshot = CreateWireTownSnapshot();
        var smithCell = Residents[0].Y * 112 + Residents[0].X;
        snapshot.LevelGeometry[0].BlockedCells.Add(smithCell);

        AuthoritativeSnapshotValidator.Validate(snapshot);
    }

    [Fact]
    public void ValidatorRejectsMalformedRosterBindingsAndMissingOccupancy()
    {
        var wrongHash = CreateWireTownSnapshot();
        wrongHash.NativeTownCatalogFingerprint = new string('A', 64);
        var missingRows = CreateWireTownSnapshot();
        missingRows.NativeTownResidents.RemoveAt(10);
        var duplicateSlot = CreateWireTownSnapshot();
        duplicateSlot.NativeTownResidents[1].ResidentSlot = 0;
        var duplicateCowAnchor = CreateWireTownSnapshot();
        duplicateCowAnchor.NativeTownResidents[9].PositionX = Residents[8].X;
        duplicateCowAnchor.NativeTownResidents[9].PositionY = Residents[8].Y;
        var cowAsHuman = CreateWireTownSnapshot();
        cowAsHuman.NativeTownResidents[8].TownerId = 8;
        var missingGreeting = CreateWireTownSnapshot();
        missingGreeting.TownerGreetings.RemoveAt(1);
        var mismatchedGreeting = CreateWireTownSnapshot();
        mismatchedGreeting.TownerGreetings[0].PositionY++;
        var missingOccupancy = CreateWireTownSnapshot();
        missingOccupancy.LevelGeometry[0].StaticObjectBlockedCells.RemoveAt(0);
        var wrongTownShape = CreateWireTownSnapshot();
        wrongTownShape.LevelGeometry[0].Width = 111;
        var wrongLevel = CreateWireTownSnapshot();
        wrongLevel.NativeTownResidents[0].LevelId = 1;

        foreach (var invalid in new[] {
            wrongHash,
            missingRows,
            duplicateSlot,
            duplicateCowAnchor,
            cowAsHuman,
            missingGreeting,
            mismatchedGreeting,
            missingOccupancy,
            wrongTownShape,
            wrongLevel,
        }) {
            Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(invalid));
        }
    }

    [Fact]
    public void ValidatorRejectsNativeTownResidentsWithoutFingerprintAndAcceptsLegacyEmptyFields()
    {
        var partial = CreateWireTownSnapshot();
        partial.NativeTownCatalogFingerprint = string.Empty;
        var noRowsWithFingerprint = new Snapshot { NativeTownCatalogFingerprint = CatalogFingerprint };
        var legacy = new Snapshot();

        Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(partial));
        Assert.Throws<InvalidDataException>(() => AuthoritativeSnapshotValidator.Validate(noRowsWithFingerprint));
        AuthoritativeSnapshotValidator.Validate(legacy);
    }

    private static DiabloSimulationSnapshot CreateSource(
        bool nativeTown = true,
        bool includeTownGreetings = false)
    {
        var player = new StorePlayerSnapshot(
            0, null, [], 0, 64, 0, 0, PlayerAttributesState.Zero, [], [], []) {
            EntityId = 1,
            LevelId = 0,
            PositionX = 75,
            PositionY = 68,
        };
        return new DiabloSimulationSnapshot(1, player, [], [], [], [], [], null) {
            NativeTownCatalogFingerprint = nativeTown ? CatalogFingerprint : string.Empty,
            NativeTownResidents = nativeTown
                ? Array.AsReadOnly(Residents.Select((row, slot) => new DiabloNativeTownResidentState(
                    slot, row.TownerId, 0, row.X, row.Y, row.Direction)).ToArray())
                : [],
            TownerGreetings = nativeTown || includeTownGreetings
                ? Residents.Take(8).Select(row => new AuthoritativeTownerGreetingDefinition(
                    row.TownerId, 0, row.X, row.Y, row.TownerId + 1)).ToArray()
                : [],
        };
    }

    private static Snapshot CreateWireTownSnapshot()
    {
        var snapshot = new Snapshot { NativeTownCatalogFingerprint = CatalogFingerprint };
        for (var slot = 0; slot < Residents.Length; slot++) {
            var row = Residents[slot];
            snapshot.NativeTownResidents.Add(new NativeTownResidentSnapshot {
                ResidentSlot = slot,
                TownerId = row.TownerId,
                LevelId = 0,
                PositionX = row.X,
                PositionY = row.Y,
                NativeDirection = row.Direction,
            });
            if (slot < 8) {
                snapshot.TownerGreetings.Add(new TownerGreetingSnapshot {
                    TownerId = row.TownerId,
                    LevelId = 0,
                    PositionX = row.X,
                    PositionY = row.Y,
                    MessageId = row.TownerId + 1,
                });
            }
        }
        var level = new LevelGeometrySnapshot { LevelId = 0, Width = 112, Height = 112 };
        level.StaticObjectBlockedCells.Add(OccupiedCells());
        snapshot.LevelGeometry.Add(level);
        return snapshot;
    }

    private static int[] OccupiedCells()
    {
        var cells = new HashSet<int>();
        foreach (var resident in Residents) {
            Add(resident.X, resident.Y);
            if (resident.TownerId == 9) {
                Add(resident.X - 1, resident.Y);
                Add(resident.X, resident.Y - 1);
                Add(resident.X - 1, resident.Y - 1);
            }
        }
        return cells.Order().ToArray();

        void Add(int x, int y) => cells.Add(y * 112 + x);
    }

    private static class LegacySnapshotReader
    {
        private const int MaximumSnapshotBytes = 1024 * 1024;
        private const int HighestLegacyFieldNumber = 23;

        public static LegacySnapshotWire Read(byte[] payload)
        {
            if (payload.Length > MaximumSnapshotBytes)
                throw new InvalidDataException("Legacy snapshot exceeds the bounded reader limit.");

            var knownFields = new List<LegacySnapshotField>();
            var unknownFields = new List<LegacySnapshotField>();
            ulong? tick = null;
            var offset = 0;
            while (offset < payload.Length) {
                var start = offset;
                var tag = ReadVarint(payload, ref offset);
                var fieldNumber = tag >> 3;
                var wireType = (int)(tag & 7);
                if (fieldNumber is 0 or > 0x1FFFFFFF)
                    throw new InvalidDataException("Legacy snapshot contains an invalid field number.");
                var number = (int)fieldNumber;

                switch (wireType) {
                    case 0:
                        var value = ReadVarint(payload, ref offset);
                        if (number == 1) {
                            if (tick is not null)
                                throw new InvalidDataException("Legacy snapshot contains duplicate tick fields.");
                            tick = value;
                        }
                        break;
                    case 1:
                        EnsureAvailable(payload, offset, sizeof(ulong));
                        offset += sizeof(ulong);
                        break;
                    case 2:
                        var length = ReadVarint(payload, ref offset);
                        if (length > MaximumSnapshotBytes || length > (ulong)(payload.Length - offset))
                            throw new InvalidDataException("Legacy snapshot contains an invalid field length.");
                        offset += checked((int)length);
                        break;
                    case 5:
                        EnsureAvailable(payload, offset, sizeof(uint));
                        offset += sizeof(uint);
                        break;
                    default:
                        throw new InvalidDataException("Legacy snapshot uses an unsupported protobuf wire type.");
                }

                var field = new LegacySnapshotField(number, payload[start..offset]);
                if (number <= HighestLegacyFieldNumber)
                    knownFields.Add(field);
                else
                    unknownFields.Add(field);
            }

            if (tick is null)
                throw new InvalidDataException("Legacy snapshot is missing its required tick field.");
            return new LegacySnapshotWire(tick.Value, knownFields, unknownFields);
        }

        public static byte[] Write(IEnumerable<LegacySnapshotField> fields)
        {
            using var stream = new MemoryStream();
            foreach (var field in fields)
                stream.Write(field.Bytes);
            return stream.ToArray();
        }

        private static ulong ReadVarint(byte[] payload, ref int offset)
        {
            ulong value = 0;
            for (var index = 0; index < 10; index++) {
                EnsureAvailable(payload, offset, 1);
                var next = payload[offset++];
                if (index == 9 && next > 1)
                    throw new InvalidDataException("Legacy snapshot contains an overflowing varint.");
                value |= (ulong)(next & 0x7F) << (index * 7);
                if ((next & 0x80) == 0)
                    return value;
            }
            throw new InvalidDataException("Legacy snapshot contains an unterminated varint.");
        }

        private static void EnsureAvailable(byte[] payload, int offset, int count)
        {
            if (count < 0 || offset < 0 || offset > payload.Length - count)
                throw new InvalidDataException("Legacy snapshot contains a truncated field.");
        }
    }

    private sealed record LegacySnapshotWire(
        ulong Tick,
        IReadOnlyList<LegacySnapshotField> KnownFields,
        IReadOnlyList<LegacySnapshotField> UnknownFields);

    private sealed record LegacySnapshotField(int Number, byte[] Bytes);
}
