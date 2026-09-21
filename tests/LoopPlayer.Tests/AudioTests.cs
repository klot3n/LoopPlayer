using System.IO;
using LoopPlayer.Audio;
using NAudio.Wave;
using Xunit;

namespace LoopPlayer.Tests;

public class AudioTests
{
    /// <summary>Стерео-трек, где значение сэмпла равно номеру кадра — удобно проверять, что именно прочитано.</summary>
    private static Track MakeTrack(int frames, int sampleRate = 1000)
    {
        var samples = new short[frames * 2];
        for (var i = 0; i < frames; i++)
        {
            samples[i * 2] = (short)i;
            samples[i * 2 + 1] = (short)-i;
        }
        return new Track("test.wav", samples, sampleRate, 2);
    }

    [Fact]
    public void TrackReportsDurationAndFrameConversion()
    {
        var track = MakeTrack(2500, 1000);
        Assert.Equal(2.5, track.DurationSeconds, 9);
        Assert.Equal(1200, track.SecondsToFrames(1.2));
        Assert.Equal(2500, track.SecondsToFrames(99));
        Assert.Equal(0.25, track.FramesToSeconds(250), 9);
    }

    [Fact]
    public void LoopingSourceWrapsAndCountsRepeats()
    {
        var track = MakeTrack(100);
        var source = new LoopingSourceProvider(track);
        source.SetLoop(10, 15);
        source.PositionFrames = 12;

        var buffer = new float[8 * 2];
        var read = source.Read(buffer, 0, buffer.Length);

        Assert.Equal(16, read);
        // 12,13,14 затем 10,11,12,13,14
        var expected = new[] { 12, 13, 14, 10, 11, 12, 13, 14 };
        for (var i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i] / 32768f, buffer[i * 2], 6);
        Assert.Equal(2, source.TakeWraps());
        Assert.Equal(0, source.TakeWraps());
        Assert.Equal(10, source.PositionFrames);
    }

    [Fact]
    public void LoopingSourceStopsAtTrackEndOutsideFragment()
    {
        var track = MakeTrack(100);
        var source = new LoopingSourceProvider(track);
        source.SetLoop(10, 15);
        source.PositionFrames = 97;

        var buffer = new float[20];
        Assert.Equal(6, source.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, source.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, source.TakeWraps());
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void TimeStretchProducesLengthProportionalToTempo(double tempo)
    {
        const int frames = 44100 * 4;
        var track = MakeTrack(frames, 44100);
        var source = new LoopingSourceProvider(track);
        source.SetLoop(0, 0); // пустой фрагмент — без цикла, читаем до конца
        var stretch = new TimeStretchSampleProvider(source) { Tempo = tempo };

        var buffer = new float[4096 * 2];
        long total = 0;
        while (true)
        {
            var n = stretch.Read(buffer, 0, buffer.Length);
            if (n == 0) break;
            total += n / 2;
            Assert.True(total < frames * 3, "бесконечное чтение");
        }

        var expected = frames / tempo;
        Assert.InRange(total, expected * 0.95, expected * 1.05);
    }

    [Fact]
    public void TimeStretchKeepsLoopingIndefinitely()
    {
        var track = MakeTrack(44100, 44100);
        var source = new LoopingSourceProvider(track);
        source.SetLoop(0, 11025); // 0,25 с
        var stretch = new TimeStretchSampleProvider(source) { Tempo = 1.0 };

        var buffer = new float[2048 * 2];
        for (var i = 0; i < 100; i++)
            Assert.Equal(buffer.Length, stretch.Read(buffer, 0, buffer.Length));

        Assert.True(source.TakeWraps() >= 15);
    }

    [Fact]
    public void TrackLoaderRejectsMissingAndGarbageFiles()
    {
        Assert.Throws<TrackLoadException>(() => TrackLoader.Load(@"C:\definitely\missing\file.mp3"));

        var garbage = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp3");
        File.WriteAllText(garbage, "this is not audio at all");
        try
        {
            Assert.Throws<TrackLoadException>(() => TrackLoader.Load(garbage));
        }
        finally
        {
            File.Delete(garbage);
        }
    }

    [Fact]
    public void TrackLoaderDecodesWav()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
        var format = new WaveFormat(8000, 16, 1);
        using (var writer = new WaveFileWriter(path, format))
        {
            var data = new short[8000 * 2]; // 2 секунды
            for (var i = 0; i < data.Length; i++) data[i] = (short)(i % 100);
            writer.WriteSamples(data, 0, data.Length);
        }

        try
        {
            var track = TrackLoader.Load(path);
            Assert.Equal(8000, track.SampleRate);
            Assert.Equal(1, track.Channels);
            Assert.Equal(2.0, track.DurationSeconds, 3);
            Assert.Equal(42, track.Samples[42]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
