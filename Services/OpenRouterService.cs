using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HabboGPTer.Config;
using HabboGPTer.Models;

namespace HabboGPTer.Services;

public class OpenRouterService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Logger _logger;
    private readonly AISettings _settings;
    private const string OpenRouterApiUrl = "https://openrouter.ai/api/v1/chat/completions";

    public OpenRouterService(Logger logger, AISettings settings)
    {
        _logger = logger;
        _settings = settings;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string?> GenerateResponseAsync(
        ConversationContext context,
        string characterName)
    {
        if (string.IsNullOrEmpty(_settings.ApiKey))
        {
            _logger.Error("OpenRouter API key not configured");
            return null;
        }

        try
        {
            var userPrompt = BuildUserPrompt(context, characterName);

            // Don't respond if there's no context (never start conversations)
            if (userPrompt == null)
            {
                _logger.Debug("No context to respond to - skipping");
                return null;
            }

            // Use a fast, free model
            var request = new OpenRouterRequest
            {
                Model = "openai/gpt-oss-120b:free",
                Messages = new[]
                {
                    new OpenRouterMessage { Role = "user", Content = userPrompt }
                }
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            _logger.API($"Request: {json}");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenRouterApiUrl);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            httpRequest.Headers.Add("HTTP-Referer", "https://github.com/habbogpter");
            httpRequest.Headers.Add("X-Title", "HabboGPTer");
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.API($"Response status: {response.StatusCode}");
            _logger.Debug($"Response body: {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.Error($"OpenRouter API error: {response.StatusCode} - {responseContent}");
                return null;
            }

            var result = JsonSerializer.Deserialize<OpenRouterResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var aiResponse = result?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrEmpty(aiResponse))
            {
                _logger.Warning("Empty response from OpenRouter");
                return null;
            }

            var sanitizedResponse = SanitizeResponse(aiResponse);
            _logger.AI($"Response: {sanitizedResponse}");

            return sanitizedResponse;
        }
        catch (Exception ex)
        {
            _logger.Error($"OpenRouter API exception: {ex.Message}");
            return null;
        }
    }

    private string? BuildUserPrompt(ConversationContext context, string characterName)
    {
        var contextStr = context.BuildContextString();

        // Never start conversations - only reply when there's context
        if (string.IsNullOrEmpty(contextStr))
            return null;

        return $@"Vc e {characterName} no Habbo Hotel Brasil. Chat:
{contextStr}

Responda algo relevante pro contexto como {characterName}.
- Max 100 caracteres
- SEM emoji SEM virgula
- Escrita de jovem brasileiro (vc td mt oq dms tlgd mano kk etc)
- Curto e direto";
    }

    private string SanitizeResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return string.Empty;

        var sanitized = response;

        // Remove markdown formatting
        sanitized = sanitized.Replace("**", "");
        sanitized = sanitized.Replace("*", "");
        sanitized = sanitized.Replace("_", "");
        sanitized = sanitized.Replace("`", "");
        sanitized = sanitized.Replace("#", "");

        // Remove quotes
        if (sanitized.StartsWith("\"") && sanitized.EndsWith("\""))
            sanitized = sanitized[1..^1];

        // Filter AI self-identification
        var aiIndicators = new[]
        {
            "como uma ia", "como ia", "sou uma ia", "sou um bot",
            "como assistente", "sou um assistente", "inteligencia artificial",
            "modelo de linguagem", "language model", "ai assistant"
        };

        foreach (var indicator in aiIndicators)
        {
            if (sanitized.Contains(indicator, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning($"Response contained AI indicator, filtering: {indicator}");
                return "kkk o que?";
            }
        }

        // Limit length for Habbo chat
        if (sanitized.Length > 200)
            sanitized = sanitized[..197] + "...";

        // Clean up whitespace
        sanitized = sanitized.Replace("\n", " ").Replace("\r", " ");
        while (sanitized.Contains("  "))
            sanitized = sanitized.Replace("  ", " ");

        return sanitized.Trim();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public class OpenRouterRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "openai/gpt-oss-120b:free";

    [JsonPropertyName("messages")]
    public OpenRouterMessage[] Messages { get; set; } = Array.Empty<OpenRouterMessage>();
}

public class OpenRouterMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class OpenRouterResponse
{
    [JsonPropertyName("choices")]
    public OpenRouterChoice[]? Choices { get; set; }
}

public class OpenRouterChoice
{
    [JsonPropertyName("message")]
    public OpenRouterMessage? Message { get; set; }
}
