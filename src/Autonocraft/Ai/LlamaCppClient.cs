using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Autonocraft.Ai
{
    /// <summary>
    /// OpenAI-compatible chat client for llama.cpp <c>llama-server</c> (default http://127.0.0.1:8080).
    /// </summary>
    public sealed class LlamaCppClient : IOpenRouterClient, IDisposable
    {
        private readonly HttpClient _http;
        private readonly LlamaCppConfig _config;

        public LlamaCppClient(LlamaCppConfig config, HttpClient? httpClient = null)
        {
            _config = config;
            _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
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

            string json = JsonSerializer.Serialize(new
            {
                model = _config.Model,
                messages = payloadMessages,
                temperature = 0.7
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, _config.ChatCompletionsUri);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"llama.cpp request failed ({(int)response.StatusCode}): {body}. Is llama-server running at {_config.BaseUrl}?");
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

            return message.GetRawText();
        }

        public void Dispose() => _http.Dispose();
    }
}
