using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using Hermes.Worker.Filters.Hangfire;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Worker.Hangfire;

/// <summary>
/// Contains unit tests for <see cref="CorrelationIdServerFilter"/>,
/// verifying Serilog LogContext scope attachment during Hangfire background job execution.
/// </summary>
public sealed class CorrelationIdServerFilterTests
{
    private static PerformingContext CreatePerformingContext(string? correlationIdValue)
    {
        Mock<IStorageConnection> connectionMock = new();

        if (correlationIdValue != null)
        {
            string jsonValue = System.Text.Json.JsonSerializer.Serialize(correlationIdValue);
            connectionMock.Setup(x => x.GetJobParameter(It.IsAny<string>(), CorrelationIdServerFilter.JOB_PARAMETER_NAME))
                .Returns(jsonValue);
        }

        BackgroundJob backgroundJob = new(
            "job-id",
            Job.FromExpression(() => Console.WriteLine()),
            DateTime.UtcNow);

        PerformContext performContext = new(
            Mock.Of<JobStorage>(),
            connectionMock.Object,
            backgroundJob,
            Mock.Of<IJobCancellationToken>());

        return new PerformingContext(performContext);
    }

    /// <summary>
    /// Tests that <see cref="CorrelationIdServerFilter.OnPerforming"/> pushes a log scope into context items
    /// when the job parameter contains a valid CorrelationId.
    /// </summary>
    [Fact]
    public void OnPerforming_Should_PushLogScope_WhenCorrelationIdIsPresent()
    {
        // Arrange
        CorrelationIdServerFilter sut = new();
        PerformingContext context = CreatePerformingContext("test-correlation-id");

        // Act
        sut.OnPerforming(context);

        // Assert
        Assert.True(context.Items.ContainsKey("CorrelationIdLogScope"));
        Assert.IsAssignableFrom<IDisposable>(context.Items["CorrelationIdLogScope"]);
    }

    /// <summary>
    /// Tests that <see cref="CorrelationIdServerFilter.OnPerforming"/> attaches a log scope
    /// even when the CorrelationId parameter is null or whitespace, generating a fallback identifier.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OnPerforming_Should_GenerateFallbackScope_WhenCorrelationIdIsNullOrWhitespace(string? invalidCorrelationId)
    {
        // Arrange
        CorrelationIdServerFilter sut = new();
        PerformingContext context = CreatePerformingContext(invalidCorrelationId);

        // Act
        sut.OnPerforming(context);

        // Assert
        Assert.True(context.Items.ContainsKey("CorrelationIdLogScope"));
        Assert.IsAssignableFrom<IDisposable>(context.Items["CorrelationIdLogScope"]);
    }

    /// <summary>
    /// Tests that <see cref="CorrelationIdServerFilter.OnPerformed"/> cleanly disposes the active log scope
    /// stored in filter items.
    /// </summary>
    [Fact]
    public void OnPerformed_Should_DisposeScope_WhenPresentInItems()
    {
        // Arrange
        CorrelationIdServerFilter sut = new();
        Mock<IDisposable> scopeMock = new();

        PerformedContext context = new(
            CreatePerformingContext(null),
            null,
            false,
            null);

        context.Items["CorrelationIdLogScope"] = scopeMock.Object;

        // Act
        sut.OnPerformed(context);

        // Assert
        scopeMock.Verify(x => x.Dispose(), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="CorrelationIdServerFilter.OnPerformed"/> completes without exception
    /// when no log scope is present in items.
    /// </summary>
    [Fact]
    public void OnPerformed_Should_NotThrow_WhenScopeIsMissing()
    {
        // Arrange
        CorrelationIdServerFilter sut = new();
        PerformedContext context = new(
            CreatePerformingContext(null),
            null,
            false,
            null);

        // Act & Assert
        Exception? exception = Record.Exception(() => sut.OnPerformed(context));
        Assert.Null(exception);
    }
}
