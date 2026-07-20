namespace BuildTrack.Api.Options;

public sealed class AiOptions
{
    public bool Enabled { get; init; }
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; init; } = 30;
    public bool TtsEnabled { get; init; }
    public string TtsModel { get; init; } = "gpt-4o-mini-tts";
    public string TtsVoice { get; init; } = "alloy";
    public string TtsFormat { get; init; } = "mp3";
}
