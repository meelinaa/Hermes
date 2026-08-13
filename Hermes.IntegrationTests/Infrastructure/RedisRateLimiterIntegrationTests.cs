using System;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using Testcontainers.Redis;
using Xunit;
using StackExchange.Redis;
using Hermes.Infrastructure.Adapters.Outbound.RateLimiting;

namespace Hermes.IntegrationTests.Infrastructure;

[Trait("Integration", "Docker")]
public sealed class RedisRateLimiterIntegrationTests : IAsyncLifetime
{
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _redisConnection;

    public async Task InitializeAsync()
    {
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redisContainer.StartAsync();
        _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_redisConnection is not null)
            await _redisConnection.DisposeAsync();

        if (_redisContainer is not null)
            await _redisContainer.DisposeAsync();
    }

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
}
