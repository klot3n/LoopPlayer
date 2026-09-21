using System.Globalization;

namespace LoopPlayer.Core;

/// <summary>
/// Скорость воспроизведения ×0,500…×1,500 с шагом 0,025.
/// Хранится как целое число шагов, чтобы ×0,500 → ×0,525 → … не накапливало ошибку float.
/// </summary>
public sealed class SpeedController
{
    public const double Step = 0.025;
    public const int MinSteps = 20; // ×0,500
    public const int MaxSteps = 60; // ×1,500
    public const int DefaultSteps = 40; // ×1,000

    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    private int _steps = DefaultSteps;

    public event Action? Changed;

    public double Speed => _steps * Step;

    public bool CanIncrease => _steps < MaxSteps;
    public bool CanDecrease => _steps > MinSteps;

    public void Increase() => SetSteps(_steps + 1);
    public void Decrease() => SetSteps(_steps - 1);
    public void Reset() => SetSteps(DefaultSteps);

    private void SetSteps(int steps)
    {
        steps = Math.Clamp(steps, MinSteps, MaxSteps);
        if (steps == _steps) return;
        _steps = steps;
        Changed?.Invoke();
    }

    /// <summary>Формат отображения: «×1,000» (десятичная запятая).</summary>
    public string Format() => Format(Speed);

    public static string Format(double speed) => "×" + speed.ToString("F3", RuCulture);
}
