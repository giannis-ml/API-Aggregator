using ApiAggregationService.Providers;
using ApiAggregationService.Services;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Core services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<AggregatorOptions>(builder.Configuration.GetSection(AggregatorOptions.SectionName));
builder.Services.AddSingleton<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<IAggregatorService, AggregatorService>();

// Retry / timeout / circuit breaker for every external API
Action<HttpStandardResilienceOptions> resilience = o =>
{
    o.Retry.MaxRetryAttempts = 3;
    o.Retry.Delay = TimeSpan.FromMilliseconds(300);
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
};

// External API clients
builder.Services.AddHttpClient<OpenWeatherProvider>(c =>
        c.BaseAddress = new Uri("https://api.openweathermap.org/"))
    .AddStandardResilienceHandler(resilience);

builder.Services.AddHttpClient<NewsApiProvider>(c =>
{
    c.BaseAddress = new Uri("https://newsapi.org/");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregationService/1.0"); // NewsAPI rejects requests without one
    c.DefaultRequestHeaders.Add("X-Api-Key", builder.Configuration["NewsApi:ApiKey"] ?? "");
})
    .AddStandardResilienceHandler(resilience);

builder.Services.AddHttpClient<SpotifyProvider>(c =>
        c.BaseAddress = new Uri("https://api.spotify.com/"))
    .AddStandardResilienceHandler(resilience);
builder.Services.AddHttpClient("spotify-auth");
builder.Services.AddSingleton<SpotifyTokenService>();

// Register providers under the common interface.
// Adding a new API = write one provider class + one line here.
builder.Services.AddTransient<IDataProvider>(sp => sp.GetRequiredService<OpenWeatherProvider>());
builder.Services.AddTransient<IDataProvider>(sp => sp.GetRequiredService<NewsApiProvider>());
builder.Services.AddTransient<IDataProvider>(sp => sp.GetRequiredService<SpotifyProvider>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();