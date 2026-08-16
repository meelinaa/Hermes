using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace Hermes.Infrastructure.Adapters.Outbound.RateLimiting;

/// <summary>
/// A distributed rate limiter backed by Redis using a Fixed Window algorithm via a Lua script.
/// Drops requests (returns false) if the limit is exceeded.
/// </summary>
public sealed class RedisRateLimiter : RateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _key;
    private readonly int _limit;
    private readonly TimeSpan _window;
    
    // Lua script for Fixed Window Rate Limiting.
    // Keys[1]: rate limit key
    // ARGV[1]: window size in milliseconds
    // ARGV[2]: max requests per window
    private const string Script = @"
        local current = redis.call('GET', KEYS[1])
        if current and tonumber(current) >= tonumber(ARGV[2]) then
            return 0
        end
        current = redis.call('INCR', KEYS[1])
        if tonumber(current) == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return 1
    ";

    public override TimeSpan? IdleDuration => null;

    public RedisRateLimiter(IConnectionMultiplexer redis, string key, int limit, TimeSpan window)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _limit = limit > 0 ? limit : throw new ArgumentOutOfRangeException(nameof(limit));
        _window = window > TimeSpan.Zero ? window : throw new ArgumentOutOfRangeException(nameof(window));
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        // Redis rate limiting should ideally be async. 
        // Sync acquisition is not recommended for distributed limiters.
        // We do a blocking call here as fallback, but users should use AcquireAsync.
        var db = _redis.GetDatabase();
        var allowed = (int)db.ScriptEvaluate(
            Script,
            new RedisKey[] { _key },
            new RedisValue[] { _window.TotalMilliseconds, _limit }) == 1;

        return allowed ? new RedisRateLimitLease(true) : new RedisRateLimitLease(false);
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var allowed = (int)await db.ScriptEvaluateAsync(
            Script,
            new RedisKey[] { _key },
            new RedisValue[] { _window.TotalMilliseconds, _limit }).ConfigureAwait(false) == 1;

        return allowed ? new RedisRateLimitLease(true) : new RedisRateLimitLease(false);
    }

    public override RateLimiterStatistics? GetStatistics() => null;

    private sealed class RedisRateLimitLease(bool isAcquired) : RateLimitLease
    {
        public override bool IsAcquired => isAcquired;
        public override IEnumerable<string> MetadataNames => Array.Empty<string>();
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
        protected override void Dispose(bool disposing) { }
    }
}
