namespace LoopPlayer.Core;

/// <summary>Правила перемещения текущей позиции: всегда в пределах [0, Duration].</summary>
public static class PositionController
{
    public const double StepSmall = 0.2;
    public const double StepMedium = 1.0;
    public const double StepLarge = 2.0;

    public static double Clamp(double position, double duration)
    {
        if (double.IsNaN(position)) return 0;
        return Math.Clamp(position, 0, Math.Max(0, duration));
    }

    public static double SeekBy(double position, double delta, double duration) =>
        Clamp(position + delta, duration);
}
