namespace LoopPlayer.Core;

/// <summary>Непрерывный отрезок источника, который нужно прочитать.</summary>
public readonly record struct ReadSegment(long Start, int Length);

/// <summary>Результат планирования чтения: отрезки, новая позиция и число переходов B→A.</summary>
public readonly record struct ReadPlan(IReadOnlyList<ReadSegment> Segments, long NewPosition, int Wraps);

/// <summary>
/// Чистая логика цикла A–B в единицах кадров (сэмплов на канал).
/// Не зависит от аудиобиблиотеки, поэтому покрывается тестами.
/// </summary>
public static class LoopController
{
    /// <summary>
    /// Планирует чтение <paramref name="frames"/> кадров начиная с <paramref name="position"/>.
    /// Если позиция ≤ B и фрагмент не пустой — читаем до B и переходим к A (столько раз, сколько нужно).
    /// Переход выполняется сразу по достижении B, поэтому каждый переход — это полное прохождение
    /// фрагмента; старт ровно с B (после перемотки) переводит на A без увеличения счётчика.
    /// Если позиция правее B — читаем до конца трека без зацикливания.
    /// Возвращает меньше кадров, чем запрошено, только при достижении конца трека.
    /// </summary>
    public static ReadPlan Plan(long position, int frames, long a, long b, long total)
    {
        var segments = new List<ReadSegment>(2);
        var pos = Math.Clamp(position, 0, total);
        var wraps = 0;
        var remaining = frames;
        var loopActive = b > a && a < total;
        var readAnything = false;

        while (remaining > 0)
        {
            var insideLoop = loopActive && pos <= b;
            var limit = insideLoop ? b : total;

            if (pos >= limit)
            {
                if (!insideLoop) break; // конец трека
                if (readAnything) wraps++;
                pos = a;
                continue;
            }

            var n = (int)Math.Min(remaining, limit - pos);
            segments.Add(new ReadSegment(pos, n));
            pos += n;
            remaining -= n;
            readAnything = true;
        }

        if (loopActive && readAnything && pos == b)
        {
            pos = a; // достигли B в конце чтения — переходим сразу
            wraps++;
        }

        return new ReadPlan(segments, pos, wraps);
    }
}
