using NAudio.Wave;
using SoundTouch;

namespace LoopPlayer.Audio;

/// <summary>
/// Изменение темпа с сохранением высоты тона (SoundTouch).
/// Тянет сэмплы из источника, пока не наберётся запрошенное количество на выходе.
/// </summary>
public sealed class TimeStretchSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly SoundTouchProcessor _processor;
    private readonly object _lock = new();
    private readonly float[] _inputBuffer;
    private bool _sourceExhausted;
    private double _tempo = 1.0;

    public TimeStretchSampleProvider(ISampleProvider source)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
        _processor = new SoundTouchProcessor
        {
            SampleRate = WaveFormat.SampleRate,
            Channels = WaveFormat.Channels,
            Tempo = 1.0,
        };
        // Параметры, подходящие для речи и музыки при темпе 0,5–1,5.
        _processor.SetSetting(SettingId.UseQuickSeek, 0);
        _processor.SetSetting(SettingId.SequenceDurationMs, 40);
        _processor.SetSetting(SettingId.SeekWindowDurationMs, 15);
        _processor.SetSetting(SettingId.OverlapDurationMs, 8);
        _inputBuffer = new float[WaveFormat.SampleRate / 10 * WaveFormat.Channels]; // 100 мс
    }

    public WaveFormat WaveFormat { get; }

    public double Tempo
    {
        get { lock (_lock) return _tempo; }
        set
        {
            lock (_lock)
            {
                _tempo = value;
                _processor.Tempo = value;
            }
        }
    }

    /// <summary>Сколько кадров источника уже прочитано, но ещё не выдано наружу (латентность SoundTouch).</summary>
    public long BufferedSourceFrames
    {
        get
        {
            lock (_lock)
                return _processor.UnprocessedSampleCount + (long)(_processor.AvailableSamples * _tempo);
        }
    }

    /// <summary>Сброс внутренних буферов (после перемотки).</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _processor.Clear();
            _sourceExhausted = false;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var channels = WaveFormat.Channels;
        var wantedFrames = count / channels;
        var producedFrames = 0;

        lock (_lock)
        {
            while (producedFrames < wantedFrames)
            {
                var got = _processor.ReceiveSamples(
                    buffer.AsSpan(offset + producedFrames * channels, (wantedFrames - producedFrames) * channels),
                    wantedFrames - producedFrames);
                producedFrames += got;
                if (producedFrames >= wantedFrames) break;

                if (_sourceExhausted)
                {
                    if (got == 0) break;
                    continue;
                }

                var read = _source.Read(_inputBuffer, 0, _inputBuffer.Length);
                if (read <= 0)
                {
                    _sourceExhausted = true;
                    _processor.Flush();
                    continue;
                }

                _processor.PutSamples(_inputBuffer.AsSpan(0, read), read / channels);
            }
        }

        return producedFrames * channels;
    }
}
