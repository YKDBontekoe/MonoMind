using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Autonocraft.Ai
{
    public sealed record OpenRouterModelInfo(
        string Id,
        string Name,
        int ContextLength,
        decimal PromptPricePerMillion,
        decimal CompletionPricePerMillion,
        bool SupportsTools,
        bool IsFree);

    public sealed class OpenRouterModelFilter
    {
        public bool FreeOnly { get; init; }
        public int MinContextLength { get; init; } = 8192;
        public bool RequireToolSupport { get; init; } = true;
        public decimal? MaxPromptPricePerMillion { get; init; }
        public decimal? MaxCompletionPricePerMillion { get; init; }
        public int Limit { get; init; } = 50;
    }

    public sealed class OpenRouterModelCatalog
    {
        private static readonly Uri ModelsUri = new("https://openrouter.ai/api/v1/models?sort=pricing-low-to-high");
        private readonly HttpClient _http;

        public OpenRouterModelCatalog(HttpClient? httpClient = null)
        {
            _http = httpClient ?? new HttpClient();
        }

        public async Task<IReadOnlyList<OpenRouterModelInfo>> FetchModelsAsync(
            OpenRouterModelFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            filter ??= new OpenRouterModelFilter();
            var response = await _http.GetFromJsonAsync<OpenRouterModelsResponse>(ModelsUri, cancellationToken)
                .ConfigureAwait(false);

            if (response?.Data == null)
            {
                return Array.Empty<OpenRouterModelInfo>();
            }

            return response.Data
                .Select(ToInfo)
                .Where(model => model.ContextLength >= filter.MinContextLength)
                .Where(model => !filter.FreeOnly || model.IsFree)
                .Where(model => !filter.RequireToolSupport || model.SupportsTools)
                .Where(model => !filter.MaxPromptPricePerMillion.HasValue || model.PromptPricePerMillion <= filter.MaxPromptPricePerMillion.Value)
                .Where(model => !filter.MaxCompletionPricePerMillion.HasValue || model.CompletionPricePerMillion <= filter.MaxCompletionPricePerMillion.Value)
                .OrderBy(model => model.PromptPricePerMillion + model.CompletionPricePerMillion)
                .ThenByDescending(model => model.ContextLength)
                .ThenBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, filter.Limit))
                .ToArray();
        }

        private static OpenRouterModelInfo ToInfo(OpenRouterModelDto dto)
        {
            decimal prompt = ParseTokenPrice(dto.Pricing?.Prompt);
            decimal completion = ParseTokenPrice(dto.Pricing?.Completion);
            bool supportsTools = dto.SupportedParameters?.Any(p =>
                string.Equals(p, "tools", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p, "tool_choice", StringComparison.OrdinalIgnoreCase)) == true;

            return new OpenRouterModelInfo(
                dto.Id ?? string.Empty,
                string.IsNullOrWhiteSpace(dto.Name) ? dto.Id ?? string.Empty : dto.Name!,
                dto.ContextLength,
                prompt,
                completion,
                supportsTools,
                prompt == 0m && completion == 0m);
        }

        private static decimal ParseTokenPrice(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw) ||
                !decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal perToken))
            {
                return 0m;
            }

            return perToken * 1_000_000m;
        }

        private sealed class OpenRouterModelsResponse
        {
            [JsonPropertyName("data")]
            public List<OpenRouterModelDto>? Data { get; set; }
        }

        private sealed class OpenRouterModelDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("context_length")]
            public int ContextLength { get; set; }

            [JsonPropertyName("pricing")]
            public OpenRouterPricingDto? Pricing { get; set; }

            [JsonPropertyName("supported_parameters")]
            public List<string>? SupportedParameters { get; set; }
        }

        private sealed class OpenRouterPricingDto
        {
            [JsonPropertyName("prompt")]
            public string? Prompt { get; set; }

            [JsonPropertyName("completion")]
            public string? Completion { get; set; }
        }
    }
}
