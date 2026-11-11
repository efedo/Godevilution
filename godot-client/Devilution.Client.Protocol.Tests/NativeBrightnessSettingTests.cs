using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeBrightnessSettingTests
{
    private sealed class MemorySettings : IClientSettingsStore
    {
        public readonly Dictionary<(string, string), int> Values = [];

        public bool TryGetInt(string section, string key, out int value) => Values.TryGetValue((section, key), out value);

        public void SetInt(string section, string key, int value) => Values[(section, key)] = value;
    }

    [Fact]
    public void DefaultIsTheNativePaletteAndTheSliderMatchesTheNativeOption()
    {
        Assert.Equal(0, NativeBrightnessSetting.Default);
        Assert.Equal(0, NativeBrightness.Minimum);
        Assert.Equal(100, NativeBrightness.Maximum);
        Assert.Equal(5, NativeBrightness.Step);
        Assert.Equal(0.0F, NativeBrightness.ToneMappingParameter(NativeBrightnessSetting.Default));
        Assert.Equal(("graphics", "brightness"), (NativeBrightnessSetting.Section, NativeBrightnessSetting.Key));
    }

    [Fact]
    public void EmptyOrOlderSettingsWithoutTheKeyLoadTheDefault()
    {
        var older = new MemorySettings();
        Assert.Equal(NativeBrightnessSetting.Default, NativeBrightnessSetting.Load(older));
        older.SetInt("input", "brightness", 60);
        older.SetInt("graphics", "zoom", 1);
        Assert.Equal(NativeBrightnessSetting.Default, NativeBrightnessSetting.Load(older));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(50)]
    [InlineData(100)]
    public void SavedValuesRoundTrip(int brightness)
    {
        var settings = new MemorySettings();
        NativeBrightnessSetting.Save(settings, brightness);
        Assert.Equal(brightness, settings.Values[("graphics", "brightness")]);
        Assert.Equal(brightness, NativeBrightnessSetting.Load(settings));
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(37, 35)]
    [InlineData(250, 100)]
    [InlineData(int.MinValue, 0)]
    [InlineData(int.MaxValue, 100)]
    public void OutOfRangeOrOffStepValuesAreNormalizedOnLoadAndSave(int stored, int expected)
    {
        var settings = new MemorySettings();
        settings.SetInt("graphics", "brightness", stored);
        Assert.Equal(expected, NativeBrightnessSetting.Load(settings));
        NativeBrightnessSetting.Save(settings, stored);
        Assert.Equal(expected, settings.Values[("graphics", "brightness")]);
    }

    [Fact]
    public void SavingKeepsOtherSettings()
    {
        var settings = new MemorySettings();
        settings.SetInt("input", "walk", 3);
        NativeBrightnessSetting.Save(settings, 45);
        Assert.Equal(3, settings.Values[("input", "walk")]);
        Assert.Equal(45, NativeBrightnessSetting.Load(settings));
    }
}
