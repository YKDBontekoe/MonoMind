using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Autonocraft.Ai
{
    public sealed class OpenRouterClient : IOpenRouterClient, IDisposable
    {
        private static readonly Uri ChatCompletionsUri = new("https://openrouter.ai/api/v1/chat/completions");

        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;

        public OpenRouterClient(OpenRouterConfig config, HttpClient? httpClient = null)
        {
            if (!config.IsConfigured)
            {
                throw new InvalidOperationException("OpenRouter API key is not configured.");
            }

            _apiKey = config.ApiKey!;
            _model = string.IsNullOrWhiteSpace(config.Model) ? OpenRouterConfig.DefaultModel : config.Model;
            _http = httpClient ?? new HttpClient();
        }

        public async Task<string> CompleteChatAsync(
            string systemPrompt,
            IReadOnlyList<(string role, string content)> messages,
            string? toolsJson = null)
        {
            var payloadMessages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            foreach (var (role, content) in messages)
            {
                payloadMessages.Add(new { role, content });
            }

            using var doc = JsonDocument.Parse(BuildRequestJson(payloadMessages, toolsJson));
            using var requestContent = new StringContent(doc.RootElement.GetRawText(), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://autonocraft.local");
            request.Headers.TryAddWithoutValidation("X-Title", "Autonocraft");
            request.Content = requestContent;

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"OpenRouter request failed ({(int)response.StatusCode}): {body}");
            }

            using var responseDoc = JsonDocument.Parse(body);
            if (!responseDoc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var message = choices[0].GetProperty("message");
            if (message.TryGetProperty("content", out var contentNode) && contentNode.ValueKind == JsonValueKind.String)
            {
                return contentNode.GetString() ?? string.Empty;
            }

            if (message.TryGetProperty("tool_calls", out var toolCallsNode) &&
                toolCallsNode.ValueKind == JsonValueKind.Array &&
                toolCallsNode.GetArrayLength() > 0)
            {
                return message.GetRawText();
            }

            return message.GetRawText();
        }

        private string BuildRequestJson(List<object> payloadMessages, string? toolsJson)
        {
            if (string.IsNullOrWhiteSpace(toolsJson))
            {
                return JsonSerializer.Serialize(new
                {
                    model = _model,
                    messages = payloadMessages
                });
            }

            using var toolsDoc = JsonDocument.Parse(toolsJson);
            return JsonSerializer.Serialize(new
            {
                model = _model,
                messages = payloadMessages,
                tools = toolsDoc.RootElement,
                tool_choice = "auto"
            });
        }

        public void Dispose() => _http.Dispose();
    }
}
