# LoopPlayer

Настольный аудиоплеер для Windows 10/11, заточенный под многократное прослушивание фрагмента A–B:
точное перемещение на 0,2 / 1 / 2 с, изменение скорости ×0,5–×1,5 без изменения высоты тона,
бесшовный цикл A→B→A со счётчиком повторений и полное управление с клавиатуры.

## Стек и почему он выбран

| Компонент | Выбор | Почему |
|---|---|---|
| Язык | C# 13 / .NET 10 (LTS) | Нативная платформа Windows, один self-contained `.exe`, собирается `dotnet build` без Visual Studio |
| UI | WPF | Гибкие лейауты (`Grid` с `*`-колонками растягивает шкалы под ширину окна), `Slider`, `ToolTip`, единая точка перехвата клавиш `PreviewKeyDown` |
| Аудио | [NAudio 2.2.1](https://github.com/naudio/NAudio) | Декодирование mp3 / wav / aiff (собственные ридеры) и m4a / aac / wma / flac (через Media Foundation), вывод `WaveOutEvent` |
| Скорость | [SoundTouch.Net](https://github.com/owoudenberg/soundtouch.net) | C#-порт SoundTouch: изменение темпа с сохранением высоты тона (WSOLA) |

Ключевое решение: трек целиком декодируется в память (int16). Это даёт сэмпл-точную позицию,
мгновенную перемотку и переход B→A **внутри аудиопотока** — без обращения к декодеру и без паузы.

## Сборка

Требуется .NET SDK 10 (`winget install Microsoft.DotNet.SDK.10`).

```powershell
.\build.ps1
```

Скрипт прогоняет тесты и публикует `dist\LoopPlayer.exe` (self-contained, single-file, win-x64, ~70 МБ,
.NET на целевой машине не нужен). Вручную:

```bash
dotnet test
dotnet publish src/LoopPlayer/LoopPlayer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```

## Запуск

- Собранный файл: двойной клик по `dist\LoopPlayer.exe` или `LoopPlayer.exe "путь\к\треку.mp3"`.
- Из исходников: `dotnet run --project src/LoopPlayer`.

## Горячие клавиши

Работают, когда окно активно, в любой раскладке клавиатуры (A/B определяются по физической клавише).

| Клавиша | Действие |
|---|---|
| `Space` | Play / Pause |
| `A` | Установить A = текущая позиция (если A > B, то B = A) |
| `B` | Установить B = текущая позиция (если B < A, то A = B) |
| `←` / `→` | −2 / +2 сек |
| `Shift + ←` / `→` | −0,2 / +0,2 сек |
| `Ctrl + ←` / `→` | −1 / +1 сек |
| `Home` | В начало трека (Play/Pause не меняется) |
| `Ctrl + Shift + A` | Перейти к A (Play/Pause не меняется) |
| `+` / `−` | Скорость ±0,025 |
| `Ctrl + O` | Выбрать трек |

## Поведение

- После загрузки трека: позиция 0:00, A = 0:00, B = длительность, счётчик = 0.
- Фрагмент A–B играет по кругу; при достижении B — мгновенный переход к A и +1 к счётчику.
- Любое изменение A или B сбрасывает счётчик; изменение скорости — нет.
- Кнопки ±0,2 / ±1 у точек A и B ограничены соседней точкой (A ≤ B); кнопки «A» / «B» подтягивают вторую точку.
- Если позиция правее B — трек играет до конца и останавливается; `▶` из конца начинает с 0:00.
- Если A = B, цикл не активен (пустой фрагмент).
- Время отображается как `MM:SS`; для треков от 60 минут — `HH:MM:SS`. Внутренняя позиция хранится с точностью до сэмпла.
- Ошибки (файл не найден, неподдерживаемый формат, повреждённый файл, нет аудиоустройства, сбой воспроизведения)
  показываются в диалоге; приложение продолжает работать.

## Структура

```
src/LoopPlayer/
  Audio/   Track, TrackLoader, LoopingSourceProvider (цикл A–B), TimeStretchSampleProvider (SoundTouch), AudioEngine
  Core/    SpeedController, AbRange, RepeatCounter, LoopController, PositionController, PlayerController (фасад)
  UI/      MainWindow, HotkeyManager, TimeFormatter
tests/LoopPlayer.Tests/   xunit-тесты логики и аудио-провайдеров (без аудиоустройства)
```
