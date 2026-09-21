namespace LoopPlayer.UI;

public static class TimeFormatter
{
    /// <summary>Длительность, начиная с которой все таймеры показываются как HH:MM:SS.</summary>
    public const double HoursThresholdSeconds = 3600;

    public static bool UseHours(double duration) => duration >= HoursThresholdSeconds;

    /// <summary>MM:SS, либо HH:MM:SS если <paramref name="useHours"/>. Секунды округляются вниз.</summary>
    public static string Format(double seconds, bool useHours)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        var total = (long)Math.Floor(seconds);
        var h = total / 3600;
        var m = total % 3600 / 60;
        var s = total % 60;
        return useHours ? $"{h:00}:{m:00}:{s:00}" : $"{h * 60 + m:00}:{s:00}";
    }
}
