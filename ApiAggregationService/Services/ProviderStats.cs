using ApiAggregationService.Models;

namespace ApiAggregationService.Services;

public sealed class ProviderStats
{
    public const int FastBelowMs = 100;    // fast < 100 ms
    public const int SlowAboveMs = 200;    // slow > 200 ms, average in between

    private const int MaxRecentSamples = 10_000;
    private static readonly TimeSpan RecentRetention = TimeSpan.FromMinutes(10);

    private readonly object _lock = new();
    private readonly Queue<(DateTimeOffset At, long Ms)> _recent = new();
    private long _total, _failed, _totalMs, _fast, _average, _slow;

    public void Record(long elapsedMs, bool success, DateTimeOffset now)
    {
        lock (_lock)
        {
            _total++;
            _totalMs += elapsedMs;
            if (!success) _failed++;

            if (elapsedMs < FastBelowMs) _fast++;
            else if (elapsedMs <= SlowAboveMs) _average++;
            else _slow++;

            _recent.Enqueue((now, elapsedMs));
            while (_recent.Count > MaxRecentSamples ||
                   (_recent.Count > 0 && now - _recent.Peek().At > RecentRetention))
                _recent.Dequeue();
        }
    }

    public ProviderStatsSnapshot Snapshot(string api)
    {
        lock (_lock)
        {
            var avg = _total == 0 ? 0 : Math.Round((double)_totalMs / _total, 2);
            return new ProviderStatsSnapshot(api, _total, _failed, avg, new BucketCounts(_fast, _average, _slow));
        }
    }

    public (double Average, int Count) RecentAverage(TimeSpan window, DateTimeOffset now)
    {
        lock (_lock)
        {
            var cutoff = now - window;
            var count = 0;
            long sum = 0;
            foreach (var (at, ms) in _recent)
            {
                if (at < cutoff) continue;
                count++;
                sum += ms;
            }
            return (count == 0 ? 0 : (double)sum / count, count);
        }
    }
}