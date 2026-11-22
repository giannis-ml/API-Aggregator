using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiAggregationService.Models;
using ApiAggregationService.Services;

namespace ApiAggregationService.Providers;

public class SpotifyProvider : IDataProvider
{
    private readonly HttpClient _http;
    private readonly SpotifyTokenService _tokens;

    public SpotifyProvider(HttpClient http, SpotifyTokenService tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public string Name => "Spotify";

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string query, CancellationToken ct)
    {
        var token = await _tokens.GetTokenAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"v1/search?q={Uri.EscapeDataString(query)}&type=track&limit=10");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<SearchResponse>(ct);

        return body?.Tracks.Items
            .Select(t => new AggregatedItem(
                Source: Name,
                Title: $"{t.Name} - {string.Join(", ", t.Artists.Select(a => a.Name))}",
                Details: $"Album: {t.Album.Name}",
                Date: ParseReleaseDate(t.Album.ReleaseDate),
                Url: t.Links?.Spotify))
            .ToList()
            ?? new List<AggregatedItem>();
    }

    // Spotify release dates can be "2020", "2020-05" or "2020-05-17"
    private static DateTimeOffset ParseReleaseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DateTimeOffset.MinValue;
        if (value.Length == 4) value += "-01-01";
        else if (value.Length == 7) value += "-01";

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var d) ? d : DateTimeOffset.MinValue;
    }

    private record SearchResponse([property: JsonPropertyName("tracks")] TracksPage Tracks);
    private record TracksPage([property: JsonPropertyName("items")] List<Track> Items);

    private record Track(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("artists")] List<Artist> Artists,
        [property: JsonPropertyName("album")] Album Album,
        [property: JsonPropertyName("external_urls")] ExternalLinks? Links);

    private record Artist([property: JsonPropertyName("name")] string Name);

    private record Album(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("release_date")] string? ReleaseDate);

    private record ExternalLinks([property: JsonPropertyName("spotify")] string? Spotify);
}