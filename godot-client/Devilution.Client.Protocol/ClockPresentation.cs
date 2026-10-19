using System.Globalization;

namespace Devilution.Client.Protocol;

/** Formats the optional legacy clock as a client-only presentation value. */
public static class ClockPresentation
{
    public static string Format(DateTimeOffset now)
    {
        return now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
