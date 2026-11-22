using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace ApiAggregationService.Services;

public class SpotifyTokenService
{
    private readonly IHttpClientFactory _factory;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public SpotifyTokenService(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _clientId = config["Spotify:ClientId"]
            ?? throw new InvalidOperationException("Spotify:ClientId is not configured.");
        _clientSecret = config["Spotify:ClientSecret"]
            ?? throw new InvalidOperationException("Spotify:ClientSecret is not configured.");
    }

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt) return _token;

        await _lock.WaitAsync(ct); // only one request refreshes the token at a time
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt) return _token;

            var client = _factory.CreateClient("spotify-auth");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
            {
                Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                })
            };
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
                ?? throw new InvalidOperationException("Empty Spotify token response.");

            _token = body.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(body.ExpiresIn - 60); // refresh a minute early
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}