using ApiAggregationService.Models;

namespace ApiAggregationService.Providers
{
    public interface IDataProvider
    {
        string Name { get; }
        Task<IReadOnlyList<AggregatedItem>> FetchAsync(string query, CancellationToken ct);
    }
}