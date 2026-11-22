namespace ApiAggregationService.Models;

public record AggregateResponse(
    IReadOnlyList<AggregatedItem> Items,
    IReadOnlyList<string> FailedSources,
    IReadOnlyList<string> StaleSources);

public record ProviderResult(
    string Source,
    IReadOnlyList<AggregatedItem> Items,
    bool Failed,
    bool Stale);