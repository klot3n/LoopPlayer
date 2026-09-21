namespace LoopPlayer.Audio;

/// <summary>Полностью декодированный трек: int16-сэмплы, interleaved по каналам.</summary>
public sealed class Track
{
    public Track(string path, short[] samples, int sampleRate, int channels)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));

        Path = path;
        Samples = samples;
        SampleRate = sampleRate;
        Channels = channels;
    }

    public string Path { get; }
    public string FileName => System.IO.Path.GetFileName(Path);
    public short[] Samples { get; }
    public int SampleRate { get; }
    public int Channels { get; }

    /// <summary>Число кадров (сэмплов на канал).</summary>
    public long TotalFrames => Samples.Length / Channels;

    public double DurationSeconds => (double)TotalFrames / SampleRate;

    public long SecondsToFrames(double seconds) =>
        Math.Clamp((long)Math.Round(seconds * SampleRate), 0, TotalFrames);

    public double FramesToSeconds(long frames) => (double)frames / SampleRate;
}
