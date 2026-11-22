namespace ApiAggregationService.Models;

public record BucketCounts(long Fast, long Average, long Slow);

public record ProviderStatsSnapshot(
    string Api,
    long TotalRequests,
    long FailedRequests,
    double AverageResponseMs,
    BucketCounts Buckets);