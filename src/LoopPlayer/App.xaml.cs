using System.Windows;
using System.Windows.Threading;

namespace LoopPlayer;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        var window = new UI.MainWindow();
        window.Show();

        // Файл можно передать аргументом командной строки: LoopPlayer.exe "track.mp3"
        if (e.Args.Length > 0) _ = window.LoadFileAsync(e.Args[0]);
    }

    /// <summary>Последний рубеж: любая необработанная ошибка показывается пользователю, приложение продолжает работать.</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Произошла ошибка:\n" + e.Exception.Message,
            "LoopPlayer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
