using LoopPlayer.Core;
using NAudio.Wave;

namespace LoopPlayer.Audio;

/// <summary>
/// Источник сэмплов поверх <see cref="Track"/> с циклом A–B.
/// Переход B→A выполняется внутри одного вызова Read — без пауз и без сброса буферов.
/// Позиция и границы хранятся в кадрах; все обращения потокобезопасны.
/// </summary>
public sealed class LoopingSourceProvider : ISampleProvider
{
    private const float Scale = 1f / 32768f;

    private readonly Track _track;
    private readonly object _lock = new();
    private long _position;
    private long _a;
    private long _b;
    private int _pendingWraps;

    public LoopingSourceProvider(Track track)
    {
        _track = track;
        _b = track.TotalFrames;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(track.SampleRate, track.Channels);
    }

    public WaveFormat WaveFormat { get; }

    public Track Track => _track;

    /// <summary>Позиция чтения в кадрах.</summary>
    public long PositionFrames
    {
        get { lock (_lock) return _position; }
        set { lock (_lock) _position = Math.Clamp(value, 0, _track.TotalFrames); }
    }

    public void SetLoop(long aFrames, long bFrames)
    {
        lock (_lock)
        {
            _a = Math.Clamp(aFrames, 0, _track.TotalFrames);
            _b = Math.Clamp(bFrames, _a, _track.TotalFrames);
        }
    }

    /// <summary>Возвращает и обнуляет число переходов B→A с прошлого вызова.</summary>
    public int TakeWraps() => Interlocked.Exchange(ref _pendingWraps, 0);

    public int Read(float[] buffer, int offset, int count)
    {
        var channels = _track.Channels;
        var frames = count / channels;
        ReadPlan plan;
        lock (_lock)
        {
            plan = LoopController.Plan(_position, frames, _a, _b, _track.TotalFrames);
            _position = plan.NewPosition;
        }

        var samples = _track.Samples;
        var written = 0;
        foreach (var seg in plan.Segments)
        {
            var src = seg.Start * channels;
            var n = seg.Length * channels;
            for (var i = 0; i < n; i++)
                buffer[offset + written + i] = samples[src + i] * Scale;
            written += n;
        }

        if (plan.Wraps > 0) Interlocked.Add(ref _pendingWraps, plan.Wraps);
        return written;
    }
}
