using NAudio.Wave;

namespace LoopPlayer.Audio;

public enum EngineState
{
    NoTrack,
    Stopped,
    Playing,
    Paused,
}

/// <summary>Ошибка аудиосистемы с сообщением, пригодным для показа пользователю.</summary>
public sealed class AudioEngineException : Exception
{
    public AudioEngineException(string userMessage, Exception? inner = null) : base(userMessage, inner) { }
}

/// <summary>
/// Цепочка воспроизведения: Track → LoopingSourceProvider → TimeStretchSampleProvider → WaveOutEvent.
/// Позиция в секундах, с компенсацией латентности SoundTouch и выходных буферов.
/// Все методы вызываются из UI-потока; события WaveOut приходят туда же.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    private const int OutputLatencyMs = 60;
    private const int OutputBufferCount = 3;

    private Track? _track;
    private LoopingSourceProvider? _source;
    private TimeStretchSampleProvider? _stretch;
    private WaveOutEvent? _output;
    private double _speed = 1.0;
    private bool _disposed;

    /// <summary>Трек доиграл до конца (вне цикла A–B).</summary>
    public event Action? Ended;

    /// <summary>Ошибка аудиосистемы во время воспроизведения.</summary>
    public event Action<string>? Error;

    public EngineState State { get; private set; } = EngineState.NoTrack;

    public Track? Track => _track;

    public bool IsPlaying => State == EngineState.Playing;

    public double Duration => _track?.DurationSeconds ?? 0;

    /// <summary>Позиция, которую слышит пользователь, в секундах.</summary>
    public double Position
    {
        get
        {
            if (_track is null || _source is null || _stretch is null) return 0;
            var frames = _source.PositionFrames;
            if (State is EngineState.Playing or EngineState.Paused)
            {
                var queuedOutputFrames = _output is null
                    ? 0
                    : (long)(_track.SampleRate * (OutputLatencyMs - OutputLatencyMs / (2.0 * OutputBufferCount)) / 1000.0);
                frames -= _stretch.BufferedSourceFrames + (long)(queuedOutputFrames * _speed);
            }
            return _track.FramesToSeconds(Math.Clamp(frames, 0, _track.TotalFrames));
        }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            _speed = value;
            if (_stretch is not null) _stretch.Tempo = value;
        }
    }

    /// <summary>Загружает трек, полностью сбрасывая состояние предыдущего.</summary>
    public void Load(Track track)
    {
        ThrowIfDisposed();
        Unload();

        _track = track;
        _source = new LoopingSourceProvider(track);
        _stretch = new TimeStretchSampleProvider(_source) { Tempo = _speed };
        State = EngineState.Stopped;
    }

    public void Unload()
    {
        DisposeOutput();
        _stretch = null;
        _source = null;
        _track = null;
        State = EngineState.NoTrack;
    }

    public void SetLoop(double aSeconds, double bSeconds)
    {
        if (_track is null || _source is null) return;
        _source.SetLoop(_track.SecondsToFrames(aSeconds), _track.SecondsToFrames(bSeconds));
    }

    /// <summary>Число переходов B→A с прошлого вызова.</summary>
    public int TakeWraps() => _source?.TakeWraps() ?? 0;

    public void Play()
    {
        ThrowIfDisposed();
        if (_track is null || _source is null || _stretch is null) return;
        if (State == EngineState.Playing) return;

        if (State == EngineState.Stopped && _source.PositionFrames >= _track.TotalFrames)
        {
            _source.PositionFrames = 0;
            _stretch.Clear();
        }

        if (_output is null)
        {
            _output = CreateOutput(_stretch);
        }

        try
        {
            _output.Play();
        }
        catch (NAudio.MmException ex)
        {
            DisposeOutput();
            State = EngineState.Stopped;
            throw new AudioEngineException("Не удалось начать воспроизведение:\n" + ex.Message, ex);
        }

        State = EngineState.Playing;
    }

    public void Pause()
    {
        if (State != EngineState.Playing || _output is null) return;
        _output.Pause();
        State = EngineState.Paused;
    }

    public void TogglePlayPause()
    {
        if (State == EngineState.Playing) Pause();
        else Play();
    }

    /// <summary>Перемотка на позицию в секундах с сохранением состояния Play/Pause.</summary>
    public void Seek(double seconds)
    {
        ThrowIfDisposed();
        if (_track is null || _source is null || _stretch is null) return;

        var wasPlaying = State == EngineState.Playing;

        // Выходные буферы уже содержат старое аудио — пересоздаём вывод, чтобы оно не прозвучало.
        DisposeOutput();

        _source.PositionFrames = _track.SecondsToFrames(seconds);
        _stretch.Clear();

        if (wasPlaying)
        {
            State = EngineState.Stopped;
            Play();
        }
        else if (State != EngineState.NoTrack)
        {
            State = _source.PositionFrames >= _track.TotalFrames ? EngineState.Stopped : EngineState.Paused;
        }
    }

    private WaveOutEvent CreateOutput(ISampleProvider provider)
    {
        WaveOutEvent? output = null;
        try
        {
            if (WaveOut.DeviceCount == 0)
                throw new AudioEngineException("Аудиоустройство не найдено. Подключите динамики или наушники.");

            output = new WaveOutEvent
            {
                DesiredLatency = OutputLatencyMs,
                NumberOfBuffers = OutputBufferCount,
            };
            output.Init(provider);
            output.PlaybackStopped += OnPlaybackStopped;
            return output;
        }
        catch (AudioEngineException)
        {
            output?.Dispose();
            throw;
        }
        catch (Exception ex) when (ex is NAudio.MmException or InvalidOperationException or ArgumentException)
        {
            output?.Dispose();
            throw new AudioEngineException("Ошибка аудиосистемы:\n" + ex.Message, ex);
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!ReferenceEquals(sender, _output)) return; // событие от уже выброшенного вывода

        DisposeOutput();
        if (_track is null || _source is null || _stretch is null) return;

        if (e.Exception is not null)
        {
            State = EngineState.Paused;
            Error?.Invoke("Ошибка воспроизведения:\n" + e.Exception.Message);
            return;
        }

        // Естественное окончание трека: позиция = длительность.
        _source.PositionFrames = _track.TotalFrames;
        _stretch.Clear();
        State = EngineState.Stopped;
        Ended?.Invoke();
    }

    private void DisposeOutput()
    {
        var output = _output;
        if (output is null) return;
        _output = null;
        output.PlaybackStopped -= OnPlaybackStopped;
        try { output.Dispose(); }
        catch (NAudio.MmException) { /* устройство уже могло исчезнуть */ }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioEngine));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unload();
    }
}
