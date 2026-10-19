using System.Text;
using Devilution.Protocol.V1;
using Google.Protobuf;

namespace Devilution.Client.Protocol;

/** Small line-oriented capture format for deterministic client-session fixtures. */
public sealed class AuthoritativeSessionCapture
{
    private readonly List<byte[]> frames = [];

    public IReadOnlyList<byte[]> Frames => frames;

    public void Record(Envelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        frames.Add(envelope.ToByteArray());
    }

    public void Write(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var writer = new StreamWriter(destination, new UTF8Encoding(false), leaveOpen: true);
        foreach (var frame in frames)
            writer.WriteLine(Convert.ToBase64String(frame));
        writer.Flush();
    }

    public static AuthoritativeSessionCapture Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, Encoding.UTF8, leaveOpen: true);
        var capture = new AuthoritativeSessionCapture();
        while (reader.ReadLine() is { } line) {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            capture.frames.Add(Convert.FromBase64String(line));
        }
        return capture;
    }

    public IEnumerable<Envelope> Replay()
    {
        foreach (var frame in frames)
            yield return Envelope.Parser.ParseFrom(frame);
    }
}
