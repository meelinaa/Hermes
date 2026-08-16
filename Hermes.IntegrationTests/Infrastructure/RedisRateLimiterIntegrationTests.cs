using System;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using Testcontainers.Redis;
using Xunit;
using StackExchange.Redis;
using Hermes.Infrastructure.Adapters.Outbound.RateLimiting;

namespace Hermes.IntegrationTests.Infrastructure;

/// <summary>
/// Contains integration tests for <see cref="RedisRateLimiter"/> using a Testcontainers Redis instance,
/// verifying distributed rate limiting, quota exhaustion, key isolation, and window expiration.
/// </summary>
[Trait("Integration", "Docker")]
public sealed class RedisRateLimiterIntegrationTests : IAsyncLifetime
{
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _redisConnection;

    /// <summary>
    /// Starts the Redis Testcontainers instance and establishes a connection multiplexer.
    /// </summary>
    public async Task InitializeAsync()
    {
        _redisContainer = new RedisBuilder("redis:7-alpine")
            .Build();

        await _redisContainer.StartAsync();
        _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
    }

    /// <summary>
    /// Disposes the connection multiplexer and stops the Redis container.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_redisConnection is not null)
            await _redisConnection.DisposeAsync();

        if (_redisContainer is not null)
            await _redisContainer.DisposeAsync();
    }

    /// <summary>
    /// Tests that the rate limiter permits requests up to the configured limit,
    /// blocks subsequent requests within the window, and allows requests again after the window expires.
    /// </summary>
    [Fact]
    public async Task RedisRateLimiter_Should_AllowRequests_UpToLimit_And_BlockAfter()
    {
        // Arrange
        int limit = 5;
        TimeSpan window = TimeSpan.FromSeconds(2);
        string key = Guid.NewGuid().ToString("N");
        var limiter = new RedisRateLimiter(_redisConnection!, key, limit, window);

        // Act & Assert
        // First 5 should succeed
        for (int i = 0; i < limit; i++)
        {
            var lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired, $"Request {i + 1} should be acquired.");
            lease.Dispose();
        }

        // 6th should fail
        var blockedLease = await limiter.AcquireAsync();
        Assert.False(blockedLease.IsAcquired, "Request 6 should be blocked.");
        blockedLease.Dispose();

        // Wait for the window to pass
        await Task.Delay(2100);

        // Next request should succeed again
        var nextWindowLease = await limiter.AcquireAsync();
        Assert.True(nextWindowLease.IsAcquired, "Request in new window should be acquired.");
        nextWindowLease.Dispose();
    }

    /// <summary>
    /// Tests that rate limits are tracked independently per key, ensuring one user's quota exhaustion
    /// does not impact other users.
    /// </summary>
    [Fact]
    public async Task RedisRateLimiter_Should_Isolate_Different_Keys()
    {
        // Arrange
        int limit = 2;
        TimeSpan window = TimeSpan.FromSeconds(5);
        string keyA = $"user-a-{Guid.NewGuid():N}";
        string keyB = $"user-b-{Guid.NewGuid():N}";

        var limiterA = new RedisRateLimiter(_redisConnection!, keyA, limit, window);
        var limiterB = new RedisRateLimiter(_redisConnection!, keyB, limit, window);

        // Act: Exhaust user A quota
        var leaseA1 = await limiterA.AcquireAsync();
        var leaseA2 = await limiterA.AcquireAsync();
        var leaseABlocked = await limiterA.AcquireAsync();

        // Assert: User A is blocked
        Assert.True(leaseA1.IsAcquired);
        Assert.True(leaseA2.IsAcquired);
        Assert.False(leaseABlocked.IsAcquired);

        // Act & Assert: User B can still acquire leases freely
        var leaseB1 = await limiterB.AcquireAsync();
        var leaseB2 = await limiterB.AcquireAsync();
        Assert.True(leaseB1.IsAcquired);
        Assert.True(leaseB2.IsAcquired);

        leaseA1.Dispose();
        leaseA2.Dispose();
        leaseABlocked.Dispose();
        leaseB1.Dispose();
        leaseB2.Dispose();
    }

    /// <summary>
    /// Tests that the synchronous fallback AttemptAcquire permits requests up to the limit.
    /// </summary>
    [Fact]
    public void RedisRateLimiter_AttemptAcquire_SynchronousFallback_Works()
    {
        // Arrange
        int limit = 2;
        TimeSpan window = TimeSpan.FromSeconds(5);
        string key = $"sync-{Guid.NewGuid():N}";
        var limiter = new RedisRateLimiter(_redisConnection!, key, limit, window);

        // Act & Assert
        var lease1 = limiter.AttemptAcquire(1);
        var lease2 = limiter.AttemptAcquire(1);
        var leaseBlocked = limiter.AttemptAcquire(1);

        Assert.True(lease1.IsAcquired);
        Assert.True(lease2.IsAcquired);
        Assert.False(leaseBlocked.IsAcquired);

        lease1.Dispose();
        lease2.Dispose();
        leaseBlocked.Dispose();
    }
}
