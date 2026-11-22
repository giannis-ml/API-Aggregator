using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiAggregationService.Models;

namespace ApiAggregationService.Providers;

public class OpenWeatherProvider : IDataProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OpenWeatherProvider(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["OpenWeatherMap:ApiKey"]
            ?? throw new InvalidOperationException("OpenWeatherMap:ApiKey is not configured.");
    }

    public string Name => "OpenWeatherMap";

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string query, CancellationToken ct)
    {
        var url = $"data/2.5/weather?q={Uri.EscapeDataString(query)}&units=metric&appid={_apiKey}";
        using var response = await _http.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return []; // the query is not a known city, so there is no weather result

        response.EnsureSuccessStatusCode();
        var w = await response.Content.ReadFromJsonAsync<WeatherResponse>(ct);
        if (w is null) return [];

        var condition = w.Weather.FirstOrDefault();
        return
        [
            new AggregatedItem(
                Source: Name,
                Title: $"Weather in {w.Name}: {w.Main.Temp:0.#}°C, {condition?.Description}",
                Details: $"Humidity {w.Main.Humidity}%",
                Date: DateTimeOffset.FromUnixTimeSeconds(w.Dt),
                Url: $"https://openweathermap.org/city/{w.Id}")
        ];
    }

    private record WeatherResponse(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("dt")] long Dt,
        [property: JsonPropertyName("main")] MainInfo Main,
        [property: JsonPropertyName("weather")] List<Condition> Weather);

    private record MainInfo(
        [property: JsonPropertyName("temp")] double Temp,
        [property: JsonPropertyName("humidity")] int Humidity);

    private record Condition(
        [property: JsonPropertyName("description")] string Description);
}