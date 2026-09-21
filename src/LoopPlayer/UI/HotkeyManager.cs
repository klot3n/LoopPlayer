using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LoopPlayer.Core;

namespace LoopPlayer.UI;

/// <summary>
/// Горячие клавиши окна. Вызывают те же методы <see cref="PlayerController"/>, что и кнопки.
/// Не перехватывают ввод, если фокус находится в текстовом поле.
/// Клавиши A/B определяются по виртуальному коду, поэтому работают в любой раскладке.
/// </summary>
public sealed class HotkeyManager
{
    private readonly PlayerController _player;
    private readonly Action _openFile;

    public HotkeyManager(PlayerController player, Action openFile)
    {
        _player = player;
        _openFile = openFile;
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt);

        if (mods == ModifierKeys.Alt) return; // Alt-комбинации оставляем системе

        var handled = (key, mods) switch
        {
            (Key.Space, ModifierKeys.None) => Do(_player.TogglePlayPause),

            (Key.A, ModifierKeys.Control | ModifierKeys.Shift) => Do(_player.GoToA),
            (Key.A, ModifierKeys.None) => Do(_player.SetA),
            (Key.B, ModifierKeys.None) => Do(_player.SetB),

            (Key.Left, ModifierKeys.None) => Do(() => _player.SeekBy(-PositionController.StepLarge)),
            (Key.Right, ModifierKeys.None) => Do(() => _player.SeekBy(PositionController.StepLarge)),
            (Key.Left, ModifierKeys.Shift) => Do(() => _player.SeekBy(-PositionController.StepSmall)),
            (Key.Right, ModifierKeys.Shift) => Do(() => _player.SeekBy(PositionController.StepSmall)),
            (Key.Left, ModifierKeys.Control) => Do(() => _player.SeekBy(-PositionController.StepMedium)),
            (Key.Right, ModifierKeys.Control) => Do(() => _player.SeekBy(PositionController.StepMedium)),

            (Key.Home, ModifierKeys.None) => Do(_player.GoToStart),
            (Key.R, ModifierKeys.None) => Do(_player.ResetCounter),
            (Key.Escape, ModifierKeys.None) => Do(_player.ExitLoop),

            (Key.O, ModifierKeys.Control) => Do(_openFile),
            (Key.OemPlus, ModifierKeys.None) or (Key.Add, ModifierKeys.None) => Do(_player.SpeedUp),
            (Key.OemMinus, ModifierKeys.None) or (Key.Subtract, ModifierKeys.None) => Do(_player.SpeedDown),

            _ => false,
        };

        if (handled) e.Handled = true;
    }

    private static bool Do(Action action)
    {
        action();
        return true;
    }
}
