namespace LoopPlayer.Core;

/// <summary>Счётчик полных прохождений фрагмента A–B.</summary>
public sealed class RepeatCounter
{
    public int Count { get; private set; }

    public event Action? Changed;

    public void Add(int repeats)
    {
        if (repeats <= 0) return;
        Count += repeats;
        Changed?.Invoke();
    }

    public void Reset()
    {
        if (Count == 0) return;
        Count = 0;
        Changed?.Invoke();
    }
}
