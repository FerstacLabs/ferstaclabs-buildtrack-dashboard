using System.Text.Json;
using System.Text.Json.Nodes;

namespace BuildTrack.Api.Contracts;

public sealed record AiChatMessageDto(string Role, string Content);

public sealed record ProjectAssistantChatRequest(
    string Message,
    JsonElement? Context,
    IReadOnlyList<AiChatMessageDto>? History);

public sealed record ProjectAssistantChatResponse(
    string Answer,
    string Source,
    string Model,
    JsonNode? Usage,
    string? Error);

public sealed record ProjectAssistantStatusResponse(
    bool Enabled,
    bool Configured,
    string Model);
