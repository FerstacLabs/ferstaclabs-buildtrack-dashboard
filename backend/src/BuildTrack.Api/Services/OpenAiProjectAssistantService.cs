using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuildTrack.Api.Contracts;
using BuildTrack.Api.Options;

namespace BuildTrack.Api.Services;

public interface IOpenAiProjectAssistantService
{
    Task<ProjectAssistantChatResponse> GetAnswerAsync(ProjectAssistantChatRequest request, CancellationToken cancellationToken);
}

public sealed class OpenAiProjectAssistantService(
    HttpClient httpClient,
    AiOptions options,
    ILogger<OpenAiProjectAssistantService> logger) : IOpenAiProjectAssistantService
{
    public async Task<ProjectAssistantChatResponse> GetAnswerAsync(ProjectAssistantChatRequest request, CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return new ProjectAssistantChatResponse(string.Empty, "local-fallback", options.Model, null, "Mesaj boş ola bilməz");
        }

        if (message.Length > 2000)
        {
            return new ProjectAssistantChatResponse(string.Empty, "local-fallback", options.Model, null, "Mesaj 2000 simvoldan uzun ola bilməz");
        }

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new ProjectAssistantChatResponse(string.Empty, "local-fallback", options.Model, null, "OpenAI API key is not configured");
        }

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

            var payload = new JsonObject
            {
                ["model"] = options.Model,
                ["temperature"] = 0.2,
                ["max_tokens"] = 900,
                ["messages"] = BuildOpenAiMessages(message, request.Context, request.History),
            };

            requestMessage.Content = JsonContent.Create(payload);
            using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenAI project assistant request failed. Status={StatusCode}", (int)response.StatusCode);
                return new ProjectAssistantChatResponse(string.Empty, "local-fallback", options.Model, null, "OpenAI sorğusu uğursuz oldu");
            }

            var parsed = JsonNode.Parse(body)?.AsObject();
            var answer = parsed?["choices"]?[0]?["message"]?["content"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(answer))
            {
                return new ProjectAssistantChatResponse(string.Empty, "local-fallback", options.Model, parsed?["usage"]?.DeepClone(), "OpenAI boş cavab qaytardı");
            }

            if (ContainsCyrillic(answer))
            {
                logger.LogWarning("OpenAI project assistant answer failed Azerbaijani language guard");
                return new ProjectAssistantChatResponse(string.Empty, "local-fallback", options.Model, parsed?["usage"]?.DeepClone(), "Cavab dil yoxlamasından keçmədi");
            }

            return new ProjectAssistantChatResponse(answer, "openai", options.Model, parsed?["usage"]?.DeepClone(), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenAI project assistant request failed");
            return new ProjectAssistantChatResponse(string.Empty, "local-fallback", options.Model, null, "AI köməkçi hazırda əlçatan deyil");
        }
    }

    private static JsonArray BuildOpenAiMessages(string message, JsonElement? context, IReadOnlyList<AiChatMessageDto>? history)
    {
        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = """
You are “BuildTrack AI Rəhbər Köməkçisi”.
Always answer in Azerbaijani.
You are an executive assistant for construction project management.
Use only provided BuildTrack context data.
Help with project status, smeta, budget, crews, workers, attendance, payroll, materials, daily reports, risks, delays, audit and export readiness.
Give short professional decision-support answers with numbers and recommendations.
Do not answer in Russian or English unless explicitly asked.
Do not mention demo/mock/fallback/internal implementation.
""",
            },
        };

        foreach (var item in (history ?? Array.Empty<AiChatMessageDto>()).TakeLast(8))
        {
            var role = item.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
            var content = (item.Content ?? string.Empty).Trim();
            if (content.Length == 0) continue;
            messages.Add(new JsonObject
            {
                ["role"] = role,
                ["content"] = content.Length > 1200 ? content[..1200] : content,
            });
        }

        messages.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = $"""
İstifadəçi sualı:
{message}

BuildTrack cari konteksti:
{BuildCompressedAiContext(context)}
""",
        });

        return messages;
    }

    private static string BuildCompressedAiContext(JsonElement? context)
    {
        if (context is null) return "{}";

        var source = context.Value;
        var compressed = new JsonObject();
        CopyJsonProperty(source, compressed, "selectedObjectId");
        CopyJsonProperty(source, compressed, "selectedObjectName");
        CopyJsonProperty(source, compressed, "selectedObject");
        CopyJsonProperty(source, compressed, "summary");
        CopyJsonProperty(source, compressed, "topInsights");
        CopyJsonArraySlice(source, compressed, "objects", 12);
        CopyJsonArraySlice(source, compressed, "stages", 18);
        CopyJsonArraySlice(source, compressed, "workItems", 30);
        CopyJsonArraySlice(source, compressed, "crews", 16);
        CopyJsonArraySlice(source, compressed, "workers", 30);
        CopyJsonArraySlice(source, compressed, "attendance", 30);
        CopyJsonArraySlice(source, compressed, "payroll", 20);
        CopyJsonArraySlice(source, compressed, "materials", 25);
        CopyJsonArraySlice(source, compressed, "dailyReports", 12);
        CopyJsonArraySlice(source, compressed, "risks", 20);
        CopyJsonArraySlice(source, compressed, "delays", 20);
        CopyJsonArraySlice(source, compressed, "audit", 16);
        CopyJsonArraySlice(source, compressed, "exportRows", 16);

        return compressed.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void CopyJsonProperty(JsonElement source, JsonObject target, string name)
    {
        if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value))
        {
            target[name] = JsonNode.Parse(value.GetRawText());
        }
    }

    private static void CopyJsonArraySlice(JsonElement source, JsonObject target, string name, int maxItems)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var array = new JsonArray();
        foreach (var item in value.EnumerateArray().Take(maxItems))
        {
            array.Add(JsonNode.Parse(item.GetRawText()));
        }

        target[name] = array;
    }

    private static bool ContainsCyrillic(string value) => value.Any(ch => ch is >= '\u0400' and <= '\u04FF');
}
