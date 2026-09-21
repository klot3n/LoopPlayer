using System.IO;
using System.Text.Json;

namespace LoopPlayer.Core;

/// <summary>Состояние между запусками: последний трек, точки A/B, скорость, позиция.</summary>
public sealed class AppSettings
{
    public string? TrackPath { get; set; }
    public double A { get; set; }
    public double B { get; set; }
    public double Position { get; set; }
    public int SpeedSteps { get; set; } = SpeedController.DefaultSteps;

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoopPlayer", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Читает настройки; при любой проблеме возвращает null — приложение стартует с чистого состояния.</summary>
    public static AppSettings? TryLoad()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Сохраняет настройки; ошибки записи не должны мешать закрытию программы.</summary>
    public bool TrySave()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
