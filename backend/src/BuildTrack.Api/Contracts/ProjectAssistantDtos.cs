using System.Text.Json;
using System.Text.Json.Nodes;

namespace BuildTrack.Api.Contracts;

public sealed record AiChatMessageDto(string Role, string Content);

public sealed record ProjectAssistantChatRequest(
    string Message,
    string? SelectedProjectId,
    Guid? SelectedSiteId,
    JsonElement? Context,
    IReadOnlyList<AiChatMessageDto>? History);

public sealed record ProjectAssistantChatResponse(
    string Answer,
    string Source,
    string Model,
    JsonNode? Usage,
    string? Error,
    IReadOnlyList<string>? SourceModules = null);

public sealed record ProjectAssistantStatusResponse(
    bool Enabled,
    bool Configured,
    string Model,
    bool TtsEnabled,
    bool TtsConfigured,
    string TtsModel,
    string TtsVoice);

public sealed record ProjectAssistantTtsRequest(
    string Text,
    string? Language,
    string? Voice);
