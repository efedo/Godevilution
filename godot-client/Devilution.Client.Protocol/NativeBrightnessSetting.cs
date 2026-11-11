namespace Devilution.Client.Protocol;

/** The per-user client settings file (Godot ConfigFile in the client, a dictionary in tests). */
public interface IClientSettingsStore
{
    bool TryGetInt(string section, string key, out int value);
    void SetInt(string section, string key, int value);
}

/** The persisted brightness option: graphics/brightness in user://settings.cfg. */
public static class NativeBrightnessSetting
{
    public const string Section = "graphics";
    public const string Key = "brightness";
    public const int Default = NativeBrightness.Minimum;

    /** Older settings files without the key load the native default; stored values are normalized. */
    public static int Load(IClientSettingsStore store)
        => store.TryGetInt(Section, Key, out var stored) ? NativeBrightness.Normalize(stored) : Default;

    public static void Save(IClientSettingsStore store, int brightness)
        => store.SetInt(Section, Key, NativeBrightness.Normalize(brightness));
}
