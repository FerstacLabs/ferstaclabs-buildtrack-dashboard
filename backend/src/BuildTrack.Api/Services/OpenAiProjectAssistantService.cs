using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuildTrack.Api.Contracts;
using BuildTrack.Api.Options;

namespace BuildTrack.Api.Services;

public interface IOpenAiProjectAssistantService
{
    Task<ProjectAssistantChatResponse> GetAnswerAsync(
        ProjectAssistantChatRequest request,
        JsonObject serverContext,
        IReadOnlyList<string> sourceModules,
        CancellationToken cancellationToken);

    Task<ProjectAssistantSpeechResult> CreateSpeechAsync(ProjectAssistantTtsRequest request, CancellationToken cancellationToken);
}

public sealed record ProjectAssistantSpeechResult(
    bool Success,
    byte[] Audio,
    string ContentType,
    int StatusCode,
    string? Error);

public sealed class OpenAiProjectAssistantService(
    HttpClient httpClient,
    AiOptions options,
    ILogger<OpenAiProjectAssistantService> logger) : IOpenAiProjectAssistantService
{
    public async Task<ProjectAssistantChatResponse> GetAnswerAsync(
        ProjectAssistantChatRequest request,
        JsonObject serverContext,
        IReadOnlyList<string> sourceModules,
        CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return new ProjectAssistantChatResponse(string.Empty, "server-fallback", options.Model, null, "Mesaj boş ola bilməz", sourceModules);
        }

        if (message.Length > 2000)
        {
            return new ProjectAssistantChatResponse(string.Empty, "server-fallback", options.Model, null, "Mesaj 2000 simvoldan uzun ola bilməz", sourceModules);
        }

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return BuildServerFallbackResponse(serverContext, sourceModules, "OpenAI API key is not configured");
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
                ["messages"] = BuildOpenAiMessages(message, serverContext, request.History),
            };

            requestMessage.Content = JsonContent.Create(payload);
            using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenAI project assistant request failed. Status={StatusCode}", (int)response.StatusCode);
                return BuildServerFallbackResponse(serverContext, sourceModules, "OpenAI sorğusu uğursuz oldu");
            }

            var parsed = JsonNode.Parse(body)?.AsObject();
            var answer = parsed?["choices"]?[0]?["message"]?["content"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(answer))
            {
                return new ProjectAssistantChatResponse(
                    BuildServerFallbackAnswer(serverContext),
                    "server-fallback",
                    options.Model,
                    parsed?["usage"]?.DeepClone(),
                    "OpenAI boş cavab qaytardı",
                    sourceModules);
            }

            if (ContainsCyrillic(answer))
            {
                logger.LogWarning("OpenAI project assistant answer failed Azerbaijani language guard");
                return new ProjectAssistantChatResponse(
                    BuildServerFallbackAnswer(serverContext),
                    "server-fallback",
                    options.Model,
                    parsed?["usage"]?.DeepClone(),
                    "Cavab dil yoxlamasından keçmədi",
                    sourceModules);
            }

            return new ProjectAssistantChatResponse(answer, "openai", options.Model, parsed?["usage"]?.DeepClone(), null, sourceModules);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenAI project assistant request failed");
            return BuildServerFallbackResponse(serverContext, sourceModules, "AI köməkçi hazırda əlçatan deyil");
        }
    }

    public async Task<ProjectAssistantSpeechResult> CreateSpeechAsync(ProjectAssistantTtsRequest request, CancellationToken cancellationToken)
    {
        var text = request.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ProjectAssistantSpeechResult(false, [], "application/json", StatusCodes.Status400BadRequest, "Text is required");
        }

        if (text.Length > 4000)
        {
            return new ProjectAssistantSpeechResult(false, [], "application/json", StatusCodes.Status400BadRequest, "Text must be 4000 characters or less");
        }

        if (!options.TtsEnabled || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new ProjectAssistantSpeechResult(false, [], "application/json", StatusCodes.Status503ServiceUnavailable, "OpenAI TTS is not configured");
        }

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "audio/speech");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

            var voice = string.IsNullOrWhiteSpace(request.Voice) ? options.TtsVoice : request.Voice.Trim();
            var payload = new JsonObject
            {
                ["model"] = options.TtsModel,
                ["voice"] = voice,
                ["input"] = text,
                ["response_format"] = options.TtsFormat,
                ["speed"] = 1.0,
                ["instructions"] = "Azərbaycan dilində təbii, aydın və sakit peşəkar köməkçi tonu ilə danış. Tikinti layihəsi rəhbərinə hesabat verirmiş kimi danış. İngilis və rus aksentindən uzaq, mümkün qədər təbii Azərbaycan tələffüzü istifadə et.",
            };

            requestMessage.Content = JsonContent.Create(payload);
            using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
            var audio = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenAI TTS request failed. Status={StatusCode}", (int)response.StatusCode);
                return new ProjectAssistantSpeechResult(false, [], "application/json", StatusCodes.Status502BadGateway, "OpenAI TTS request failed");
            }

            return new ProjectAssistantSpeechResult(true, audio, ResolveTtsContentType(options.TtsFormat), StatusCodes.Status200OK, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenAI TTS request failed");
            return new ProjectAssistantSpeechResult(false, [], "application/json", StatusCodes.Status502BadGateway, "OpenAI TTS request failed");
        }
    }

    private static JsonArray BuildOpenAiMessages(string message, JsonObject serverContext, IReadOnlyList<AiChatMessageDto>? history)
    {
        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = """
You are "BuildTrack AI Rəhbər Köməkçisi".
Always answer in Azerbaijani.
You are an executive assistant for construction project management.
Use only canonical BuildTrack server context data provided in the request.
Never rely on browser snapshots or user-provided context as facts.
Help with project status, smeta, budget, crews, workers, attendance, payroll, materials, warehouse, procurement, daily reports, risks, delays, audit, camera and export readiness.
Give short professional decision-support answers with numbers and recommendations.
Do not answer in Russian or English unless explicitly asked.
Do not mention demo/mock/fallback/internal implementation.
Do not expose passwords, tokens, hashes, API keys, device credentials or private file paths.
Clearly distinguish planned smeta quantities, warehouse stock, procurement needs, purchased quantities and received quantities.
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

BuildTrack cari server konteksti:
{BuildServerContext(serverContext)}
""",
        });

        return messages;
    }

    private ProjectAssistantChatResponse BuildServerFallbackResponse(
        JsonObject serverContext,
        IReadOnlyList<string> sourceModules,
        string? error) =>
        new(BuildServerFallbackAnswer(serverContext), "server-fallback", options.Model, null, error, sourceModules);

    private static string BuildServerContext(JsonObject serverContext)
    {
        var json = serverContext.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return json.Length <= 32000 ? json : string.Concat(json.AsSpan(0, 32000), "\n...context truncated...");
    }

    private static string BuildServerFallbackAnswer(JsonObject serverContext)
    {
        var metadata = serverContext["metadata"] as JsonObject;
        var summary = serverContext["executiveSummary"] as JsonObject;
        var siteName = GetString(metadata?["selectedSiteName"]) ?? "Bütün obyektlər";
        var progress = GetNumber(summary?["projectProgressPercent"]);
        var workerCount = GetNumber(summary?["workerCount"]);
        var present = GetNumber(summary?["todayPresentCount"]);
        var workedHours = GetNumber(summary?["todayWorkedHours"]);
        var criticalStock = GetNumber(summary?["criticalWarehouseItems"]);
        var openRequests = GetNumber(summary?["openWarehouseRequests"]);
        var openProcurement = GetNumber(summary?["openProcurementNeeds"]);
        var security = GetNumber(summary?["unreviewedSecurityEvents"]);

        return $"""
{siteName} üzrə qısa idarəetmə xülasəsi:

• Layihə gedişatı: {progress:0.#}%.
• İşçi heyəti: {workerCount:0} nəfər, bugün görünən: {present:0}, işlənmiş vaxt: {workedHours:0.##} saat.
• Anbar: kritik qalıq sayı {criticalStock:0}, açıq anbar sorğusu {openRequests:0}.
• Satınalma: açıq ehtiyac sayı {openProcurement:0}.
• Kamera/risk: baxılmamış təhlükəsizlik hadisəsi {security:0}.

Tövsiyə: əvvəlcə kritik anbar qalıqlarını, açıq satınalma ehtiyaclarını və bugün işçi davamiyyətini yoxlayın; gecikən mərhələlər varsa prorab hesabatı ilə səbəbi təsdiqləyin.
""";
    }

    private static string? GetString(JsonNode? node)
    {
        if (node is null) return null;
        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static decimal GetNumber(JsonNode? node)
    {
        if (node is null) return 0;
        try
        {
            return node.GetValue<decimal>();
        }
        catch
        {
            return 0;
        }
    }

    private static bool ContainsCyrillic(string value) => value.Any(ch => ch is >= '\u0400' and <= '\u04FF');

    private static string ResolveTtsContentType(string format) =>
        format.Equals("mp3", StringComparison.OrdinalIgnoreCase) ? "audio/mpeg" :
        format.Equals("wav", StringComparison.OrdinalIgnoreCase) ? "audio/wav" :
        format.Equals("aac", StringComparison.OrdinalIgnoreCase) ? "audio/aac" :
        format.Equals("opus", StringComparison.OrdinalIgnoreCase) ? "audio/opus" :
        "application/octet-stream";
}
