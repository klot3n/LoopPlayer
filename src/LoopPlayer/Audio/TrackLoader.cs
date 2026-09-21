using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace LoopPlayer.Audio;

/// <summary>Открывает аудиофайл через NAudio и декодирует его целиком в память.</summary>
public static class TrackLoader
{
    private const long MaxSamples = 1_500_000_000; // ~3 ГБ int16 — защита от переполнения массива

    public static Track Load(string path, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new TrackLoadException("Файл не найден или недоступен:\n" + path);

        try
        {
            using var reader = OpenReader(path);
            return Decode(reader, path, cancellation);
        }
        catch (TrackLoadException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (UnauthorizedAccessException ex)
        {
            throw new TrackLoadException("Нет доступа к файлу:\n" + path, ex);
        }
        catch (IOException ex)
        {
            throw new TrackLoadException("Не удалось прочитать файл:\n" + ex.Message, ex);
        }
        catch (Exception ex) when (ex is COMException or InvalidDataException or FormatException
                                    or InvalidOperationException or NotSupportedException
                                    or ArgumentException or NAudio.MmException)
        {
            throw new TrackLoadException(
                "Формат файла не поддерживается или файл повреждён:\n" + Path.GetFileName(path), ex);
        }
    }

    private static WaveStream OpenReader(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".wav" => new WaveFileReader(path),
            ".aiff" or ".aif" => new AiffFileReader(path),
            ".mp3" => new Mp3FileReader(path),
            // m4a, aac, wma, flac и прочее — через Media Foundation (Windows 10/11)
            _ => new MediaFoundationReader(path),
        };
    }

    private static Track Decode(WaveStream reader, string path, CancellationToken cancellation)
    {
        // Приводим любой входной формат к IEEE float, затем сохраняем как int16.
        ISampleProvider samples = reader.ToSampleProvider();
        var format = samples.WaveFormat;
        var channels = format.Channels;
        var sampleRate = format.SampleRate;

        var estimatedFrames = reader.TotalTime.TotalSeconds > 0
            ? (long)(reader.TotalTime.TotalSeconds * sampleRate) + sampleRate
            : sampleRate * 60L;
        if (estimatedFrames * channels > MaxSamples)
            throw new TrackLoadException("Файл слишком длинный для загрузки в память.");

        var buffer = new float[sampleRate * channels]; // ~1 секунда за итерацию
        var chunks = new List<short[]>();
        long total = 0;

        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            var read = samples.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;

            var chunk = new short[read];
            for (var i = 0; i < read; i++)
            {
                var v = buffer[i];
                if (float.IsNaN(v)) v = 0;
                chunk[i] = (short)Math.Clamp((int)MathF.Round(v * 32767f), short.MinValue, short.MaxValue);
            }
            chunks.Add(chunk);
            total += read;
            if (total > MaxSamples)
                throw new TrackLoadException("Файл слишком длинный для загрузки в память.");
        }

        // Отбрасываем неполный последний кадр, если декодер вернул не кратное каналам число сэмплов.
        total -= total % channels;
        if (total == 0)
            throw new TrackLoadException("Файл не содержит аудиоданных или повреждён:\n" + Path.GetFileName(path));

        var all = new short[total];
        long offset = 0;
        foreach (var chunk in chunks)
        {
            var n = (int)Math.Min(chunk.Length, total - offset);
            if (n <= 0) break;
            Array.Copy(chunk, 0, all, offset, n);
            offset += n;
        }

        return new Track(path, all, sampleRate, channels);
    }
}
