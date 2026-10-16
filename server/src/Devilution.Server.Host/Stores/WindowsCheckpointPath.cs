namespace Devilution.Server.Stores;

internal static class WindowsCheckpointPath
{
    // Callers supply absolute paths. Direct Win32 calls do not inherit .NET's
    // long-path normalization; preserve their exact local or UNC spelling.
    internal static string ForNativeApi(string path)
    {
        if (path.Length < 248 || path.StartsWith(@"\\?\", StringComparison.Ordinal))
            return path;
        return path.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + path[2..]
            : @"\\?\" + path;
    }
}
