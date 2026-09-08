using System.Text;

namespace Devilution.Server.Stores;

/** Provides fail-closed UTF-8 file I/O for server-owned persistence. */
internal static class StrictUtf8Text
{
    private static readonly UTF8Encoding Encoding = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static byte[] Encode(string value, string description)
    {
        ArgumentNullException.ThrowIfNull(value);
        try {
            return Encoding.GetBytes(value);
        } catch (EncoderFallbackException exception) {
            throw new InvalidDataException(
                $"The {description} contains invalid UTF-16 text.",
                exception);
        }
    }

    public static string ReadAllText(string path, string description)
    {
        try {
            return Encoding.GetString(File.ReadAllBytes(path));
        } catch (DecoderFallbackException exception) {
            throw new InvalidDataException(
                $"The {description} is not valid UTF-8.",
                exception);
        }
    }
}
