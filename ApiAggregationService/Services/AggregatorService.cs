using System.Diagnostics;
using ApiAggregationService.Models;
using ApiAggregationService.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApiAggregationService.Services;

public class AggregatorOptions
{
    public const string SectionName = "Aggregator";
    public int FreshCacheSeconds { get; set; } = 180;   // serve from cache without calling the API
    public int StaleCacheMinutes { get; set; } = 60;    // last known good data, used only when an API fails
}

public interface IAggregatorService
{
    Task<AggregateResponse> GetAsync(AggregateRequest request, CancellationToken ct);
}

public class AggregatorService : IAggregatorService
{
    private readonly IEnumerable<IDataProvider> _providers;
    private readonly IMemoryCache _cache;
    private readonly IStatisticsService _stats;
    private readonly ILogger<AggregatorService> _logger;
    private readonly TimeSpan _freshTtl;
    private readonly TimeSpan _staleTtl;

    public AggregatorService(
        IEnumerable<IDataProvider> providers,
        IMemoryCache cache,
        IStatisticsService stats,
        IOptions<AggregatorOptions> options,
        ILogger<AggregatorService> logger)
    {
        _providers = providers;
        _cache = cache;
        _stats = stats;
        _logger = logger;
        _freshTtl = TimeSpan.FromSeconds(options.Value.FreshCacheSeconds);
        _staleTtl = TimeSpan.FromMinutes(options.Value.StaleCacheMinutes);
    }

    public async Task<AggregateResponse> GetAsync(AggregateRequest req, CancellationToken ct)
    {
        // All providers run in parallel
        var results = await Task.WhenAll(_providers.Select(p => FetchProviderAsync(p, req.Query, ct)));

        IEnumerable<AggregatedItem> items = results.SelectMany(r => r.Items);
        items = ApplySort(ApplyFilters(items, req), req);

        return new AggregateResponse(
            items.ToList(),
            results.Where(r => r.Failed).Select(r => r.Source).ToList(),
            results.Where(r => r.Stale).Select(r => r.Source).ToList());
    }

    private async Task<ProviderResult> FetchProviderAsync(IDataProvider provider, string query, CancellationToken ct)
    {
        var normalized = query.Trim().ToLowerInvariant();
        var freshKey = $"fresh:{provider.Name}:{normalized}";
        var staleKey = $"stale:{provider.Name}:{normalized}";

        if (_cache.TryGetValue(freshKey, out IReadOnlyList<AggregatedItem>? cached) && cached is not null)
            return new ProviderResult(provider.Name, cached, Failed: false, Stale: false);

        var sw = Stopwatch.StartNew();
        try
        {
            var items = await provider.FetchAsync(query, ct);
            sw.Stop();
            _stats.Record(provider.Name, sw.ElapsedMilliseconds, success: true);

            if (_freshTtl > TimeSpan.Zero) _cache.Set(freshKey, items, _freshTtl);
            _cache.Set(staleKey, items, _staleTtl);

            return new ProviderResult(provider.Name, items, Failed: false, Stale: false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // the caller cancelled the request, so don't treat it as a provider failure
        }
        catch (Exception ex)
        {
            sw.Stop();
            _stats.Record(provider.Name, sw.ElapsedMilliseconds, success: false);
            _logger.LogWarning(ex, "Provider {Provider} failed after {Elapsed} ms", provider.Name, sw.ElapsedMilliseconds);

            // Fallback: serve the last known good data if we have any
            if (_cache.TryGetValue(staleKey, out IReadOnlyList<AggregatedItem>? stale) && stale is not null)
                return new ProviderResult(provider.Name, stale, Failed: true, Stale: true);

            return new ProviderResult(provider.Name, [], Failed: true, Stale: false);
        }
    }

    private static IEnumerable<AggregatedItem> ApplyFilters(IEnumerable<AggregatedItem> items, AggregateRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.Category))
            items = items.Where(i => string.Equals(i.Category, req.Category, StringComparison.OrdinalIgnoreCase));
        if (req.From is not null) items = items.Where(i => i.Date >= req.From);
        if (req.To is not null) items = items.Where(i => i.Date <= req.To);
        return items;
    }

    private static IEnumerable<AggregatedItem> ApplySort(IEnumerable<AggregatedItem> items, AggregateRequest req)
    {
        Func<AggregatedItem, object?> key = req.SortBy.ToLowerInvariant() switch
        {
            "title" => i => i.Title,
            "source" => i => i.Source,
            "category" => i => i.Category,
            _ => i => i.Date
        };

        return req.Order.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? items.OrderBy(key)
            : items.OrderByDescending(key);
    }
}