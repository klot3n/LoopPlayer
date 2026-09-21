namespace LoopPlayer.Audio;

/// <summary>Ошибка загрузки трека с сообщением, пригодным для показа пользователю.</summary>
public sealed class TrackLoadException : Exception
{
    public TrackLoadException(string userMessage, Exception? inner = null) : base(userMessage, inner) { }
}
