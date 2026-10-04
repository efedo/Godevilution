using System.Text.Json;
using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Actual native Monster methods, independently captured rather than formula goldens. */
public sealed class AuthoritativeCathedralMonsterRuntimeNativeOracleTests
{
    [Fact(Explicit = true)]
    public void CompleteRuntimeMetadataMatchesNativeMonsterMethods()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_ORACLE_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(directory));
        var metadata = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(TsvTable.Parse(
            AuthoritativeCathedralMonsterRuntimeCatalog.TableName,
            File.ReadAllText(Path.Combine(directory!, AuthoritativeCathedralMonsterRuntimeCatalog.TableName))));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!, "native-monster-runtime.json")));
        Assert.Equal(new[] { "caseCount", "cases", "format", "uniqueBaseTypes", "version" },
            document.RootElement.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("godevilution-cathedral-monster-runtime", document.RootElement.GetProperty("format").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(720, document.RootElement.GetProperty("caseCount").GetInt32());
        var rows = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        var uniqueBaseTypes = document.RootElement.GetProperty("uniqueBaseTypes").EnumerateArray()
            .Select(value => value.GetInt32()).ToArray();
        Assert.Equal(100, uniqueBaseTypes.Length);
        Assert.All(uniqueBaseTypes, value => Assert.InRange(value, 0, 137));
        Assert.Equal(720, rows.Length);
        Assert.Equal(Enumerable.Range(0, 138).SelectMany(id => Enumerable.Range(0, 3)
            .Select(difficulty => (id, difficulty))), rows.Take(414).Select(row =>
                (row.GetProperty("nativeId").GetInt32(), row.GetProperty("difficulty").GetInt32())));
        Assert.Equal(Enumerable.Range(0, 100).SelectMany(id => Enumerable.Range(0, 3)
            .Select(difficulty => (id, difficulty))), rows.Skip(414).Take(300).Select(row =>
                (row.GetProperty("uniqueId").GetInt32(), row.GetProperty("difficulty").GetInt32())));
        Assert.All(rows.Take(414), row => {
            Assert.Equal(-1, row.GetProperty("uniqueId").GetInt32());
            Assert.False(row.GetProperty("playerMinion").GetBoolean());
        });
        Assert.All(rows.Skip(414).Take(300), row => {
            Assert.Equal(uniqueBaseTypes[row.GetProperty("uniqueId").GetInt32()], row.GetProperty("nativeId").GetInt32());
            Assert.False(row.GetProperty("playerMinion").GetBoolean());
        });
        Assert.Equal(Enumerable.Range(0, 3).SelectMany(difficulty => new[] {
            (109, -1, difficulty, true), (109, -1, difficulty, false),
        }), rows.Skip(714).Select(row => (row.GetProperty("nativeId").GetInt32(),
            row.GetProperty("uniqueId").GetInt32(), row.GetProperty("difficulty").GetInt32(),
            row.GetProperty("playerMinion").GetBoolean())));
        foreach (var row in rows) {
            Assert.Equal(new[] { "difficulty", "experience", "golemToHit", "level", "nativeId", "playerMinion",
                "toHit", "toHitSpecial", "uniqueCustomToHit", "uniqueDungeonLevel", "uniqueId" },
                row.EnumerateObject().Select(property => property.Name).Order());
            Assert.Equal(65535, row.GetProperty("golemToHit").GetInt32());
            Assert.InRange(row.GetProperty("uniqueDungeonLevel").GetInt32(), 0, 255);
            Assert.InRange(row.GetProperty("uniqueCustomToHit").GetInt32(), 0, ushort.MaxValue);
            var nativeId = row.GetProperty("nativeId").GetInt32();
            var uniqueId = row.GetProperty("uniqueId").GetInt32();
            var difficulty = (DiabloDifficulty)row.GetProperty("difficulty").GetInt32();
            AuthoritativeCathedralUniqueDefinition? unique = uniqueId < 0 ? null : new(
                uniqueId, nativeId, row.GetProperty("uniqueDungeonLevel").GetInt32(),
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, AuthoritativeCathedralUniquePack.None,
                row.GetProperty("uniqueCustomToHit").GetInt32(), 0, -1, false);
            Assert.Equal(row.GetProperty("level").GetUInt32(), metadata.GetEffectiveLevel(nativeId, difficulty, unique));
            Assert.Equal(row.GetProperty("experience").GetUInt32(), metadata.GetEffectiveExperience(nativeId, difficulty, unique is not null));
            Assert.Equal(row.GetProperty("toHit").GetUInt32(), metadata.GetEffectiveToHit(nativeId, difficulty,
                playerMinion: row.GetProperty("playerMinion").GetBoolean(),
                golemToHit: row.GetProperty("golemToHit").GetInt32(), unique: unique));
            Assert.Equal(row.GetProperty("toHitSpecial").GetUInt32(), metadata.GetEffectiveToHit(nativeId, difficulty,
                special: true, playerMinion: row.GetProperty("playerMinion").GetBoolean(),
                golemToHit: row.GetProperty("golemToHit").GetInt32(), unique: unique));
        }
    }
}
