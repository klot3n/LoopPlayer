using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using LoopPlayer.Core;
using Microsoft.Win32;

namespace LoopPlayer.UI;

public partial class MainWindow : Window
{
    private readonly PlayerController _player = new();
    private readonly HotkeyManager _hotkeys;
    private readonly DispatcherTimer _timer;

    private bool _draggingMain;
    private bool _draggingA;
    private bool _draggingB;
    private bool _updatingSliders;

    public MainWindow()
    {
        InitializeComponent();

        _hotkeys = new HotkeyManager(_player, OpenFileDialogAndLoad);
        PreviewKeyDown += (_, e) => _hotkeys.HandleKeyDown(e);

        _player.StateChanged += UpdateAll;
        _player.Error += ShowError;
        _player.Speed.Changed += UpdateSpeed;
        _player.Range.Changed += UpdateRange;
        _player.Counter.Changed += UpdateCounter;

        HookSlider(MainSlider, v => _draggingMain = v, v => _player.SeekTo(v));
        HookSlider(ASlider, v => _draggingA = v, v => _player.MoveA(v));
        HookSlider(BSlider, v => _draggingB = v, v => _player.MoveB(v));

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => OnTimerTick();
        _timer.Start();

        Closed += (_, _) =>
        {
            _timer.Stop();
            _player.Dispose();
        };

        UpdateAll();
        UpdateSpeed();
    }

    // ---------- Слайдеры ----------

    /// <summary>
    /// Слайдер сообщает значение только по действию пользователя: при перетаскивании бегунка
    /// (по отпусканию) и при клике по шкале. Программные обновления игнорируются.
    /// </summary>
    private void HookSlider(Slider slider, Action<bool> setDragging, Action<double> apply)
    {
        slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => setDragging(true)));
        slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
        {
            setDragging(false);
            apply(slider.Value);
        }));
        slider.ValueChanged += (_, e) =>
        {
            if (_updatingSliders) return;
            // Клик по шкале (IsMoveToPointEnabled) меняет значение без перетаскивания.
            if (!IsDragging(slider)) apply(e.NewValue);
        };
    }

    private bool IsDragging(Slider slider) =>
        ReferenceEquals(slider, MainSlider) ? _draggingMain :
        ReferenceEquals(slider, ASlider) ? _draggingA : _draggingB;

    // ---------- Обновление интерфейса ----------

    private void OnTimerTick()
    {
        _player.Tick();
        if (!_player.HasTrack) return;

        var useHours = TimeFormatter.UseHours(_player.Duration);
        var position = _player.Position;
        PositionText.Text = TimeFormatter.Format(position, useHours);

        if (!_draggingMain)
        {
            _updatingSliders = true;
            MainSlider.Value = position;
            _updatingSliders = false;
        }

        PlayPauseButton.Content = _player.IsPlaying ? "❚❚" : "▶";
    }

    private void UpdateAll()
    {
        var hasTrack = _player.HasTrack;
        var enabled = hasTrack && !_player.IsLoading;
        var useHours = TimeFormatter.UseHours(_player.Duration);

        foreach (var button in new[]
                 {
                     SetAButton, AMinus1Button, AMinusSmallButton, APlusSmallButton, APlus1Button,
                     SetBButton, BMinus1Button, BMinusSmallButton, BPlusSmallButton, BPlus1Button,
                     Back2Button, PlayPauseButton, GoToAButton, Forward2Button,
                 })
            button.IsEnabled = enabled;

        MainSlider.IsEnabled = enabled;
        ASlider.IsEnabled = enabled;
        BSlider.IsEnabled = enabled;
        OpenButton.IsEnabled = !_player.IsLoading;

        if (_player.IsLoading)
        {
            FileNameText.Text = "Загрузка…";
            FileNameText.Foreground = SystemColors.GrayTextBrush;
            StatusText.Text = "Декодирование файла в память…";
        }
        else if (hasTrack)
        {
            FileNameText.Text = _player.Track!.FileName;
            FileNameText.Foreground = SystemColors.ControlTextBrush;
            StatusText.Text = "";
        }
        else
        {
            FileNameText.Text = "Трек не выбран";
            FileNameText.Foreground = SystemColors.GrayTextBrush;
        }

        var duration = _player.Duration;
        _updatingSliders = true;
        MainSlider.Maximum = Math.Max(duration, 0.001);
        ASlider.Maximum = MainSlider.Maximum;
        BSlider.Maximum = MainSlider.Maximum;
        MainSlider.Value = _player.Position;
        _updatingSliders = false;

        DurationText.Text = TimeFormatter.Format(duration, useHours);
        PositionText.Text = TimeFormatter.Format(_player.Position, useHours);
        PlayPauseButton.Content = _player.IsPlaying ? "❚❚" : "▶";

        UpdateRange();
        UpdateCounter();
    }

    private void UpdateRange()
    {
        var useHours = TimeFormatter.UseHours(_player.Duration);
        ATimeText.Text = TimeFormatter.Format(_player.Range.A, useHours);
        BTimeText.Text = TimeFormatter.Format(_player.Range.B, useHours);

        _updatingSliders = true;
        if (!_draggingA) ASlider.Value = _player.Range.A;
        if (!_draggingB) BSlider.Value = _player.Range.B;
        _updatingSliders = false;
    }

    private void UpdateCounter() => RepeatText.Text = "Повторений: " + _player.Counter.Count;

    private void UpdateSpeed()
    {
        SpeedText.Text = _player.Speed.Format();
        SpeedDownButton.IsEnabled = _player.Speed.CanDecrease;
        SpeedUpButton.IsEnabled = _player.Speed.CanIncrease;
    }

    private void ShowError(string message)
    {
        StatusText.Text = message.Replace('\n', ' ');
        MessageBox.Show(this, message, "LoopPlayer", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // ---------- Открытие файла ----------

    private async void OpenFileDialogAndLoad()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите аудиофайл",
            Filter = "Аудиофайлы|*.mp3;*.wav;*.m4a;*.aac;*.wma;*.flac;*.aiff;*.aif;*.mp4|Все файлы|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        await LoadFileAsync(dialog.FileName);
    }

    public Task LoadFileAsync(string path) => _player.LoadTrackAsync(path);

    // ---------- Обработчики кнопок ----------

    private void OpenButton_Click(object sender, RoutedEventArgs e) => OpenFileDialogAndLoad();
    private void SpeedDownButton_Click(object sender, RoutedEventArgs e) => _player.SpeedDown();
    private void SpeedUpButton_Click(object sender, RoutedEventArgs e) => _player.SpeedUp();

    private void SetAButton_Click(object sender, RoutedEventArgs e) => _player.SetA();
    private void AMinus1Button_Click(object sender, RoutedEventArgs e) => _player.NudgeA(-AbRange.LargeStep);
    private void AMinusSmallButton_Click(object sender, RoutedEventArgs e) => _player.NudgeA(-AbRange.SmallStep);
    private void APlusSmallButton_Click(object sender, RoutedEventArgs e) => _player.NudgeA(AbRange.SmallStep);
    private void APlus1Button_Click(object sender, RoutedEventArgs e) => _player.NudgeA(AbRange.LargeStep);

    private void SetBButton_Click(object sender, RoutedEventArgs e) => _player.SetB();
    private void BMinus1Button_Click(object sender, RoutedEventArgs e) => _player.NudgeB(-AbRange.LargeStep);
    private void BMinusSmallButton_Click(object sender, RoutedEventArgs e) => _player.NudgeB(-AbRange.SmallStep);
    private void BPlusSmallButton_Click(object sender, RoutedEventArgs e) => _player.NudgeB(AbRange.SmallStep);
    private void BPlus1Button_Click(object sender, RoutedEventArgs e) => _player.NudgeB(AbRange.LargeStep);

    private void Back2Button_Click(object sender, RoutedEventArgs e) => _player.SeekBy(-PositionController.StepLarge);
    private void PlayPauseButton_Click(object sender, RoutedEventArgs e) => _player.TogglePlayPause();
    private void GoToAButton_Click(object sender, RoutedEventArgs e) => _player.GoToA();
    private void Forward2Button_Click(object sender, RoutedEventArgs e) => _player.SeekBy(PositionController.StepLarge);
}
