using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiAggregationService.Models;

namespace ApiAggregationService.Providers;

public class NewsApiProvider : IDataProvider
{
    private readonly HttpClient _http;

    public NewsApiProvider(HttpClient http) => _http = http;

    public string Name => "NewsAPI";

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string query, CancellationToken ct)
    {
        var url = $"v2/everything?q={Uri.EscapeDataString(query)}&pageSize=10&sortBy=publishedAt&language=en";
        var response = await _http.GetFromJsonAsync<NewsResponse>(url, ct);

        return response?.Articles
            .Where(a => !string.IsNullOrWhiteSpace(a.Title))
            .Select(a => new AggregatedItem(
                Source: Name,
                Title: a.Title!,
                Details: a.Description,
                Date: a.PublishedAt,
                Url: a.Url))
            .ToList()
            ?? new List<AggregatedItem>();
    }

    private record NewsResponse(
        [property: JsonPropertyName("articles")] List<Article> Articles);

    private record Article(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset PublishedAt);
}