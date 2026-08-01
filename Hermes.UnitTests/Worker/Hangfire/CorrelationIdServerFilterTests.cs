using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using Hermes.Worker.Hangfire;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Worker.Hangfire;

public sealed class CorrelationIdServerFilterTests
{
    private static PerformingContext CreatePerformingContext(string? correlationIdValue)
    {
        Mock<IStorageConnection> connectionMock = new();

        if (correlationIdValue != null)
        {
            // Hangfire job parameters are stored as JSON strings.
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OnPerforming_Should_NotPushLogScope_WhenCorrelationIdIsNullOrWhitespace(string? invalidCorrelationId)
    {
        // Arrange
        CorrelationIdServerFilter sut = new();
        PerformingContext context = CreatePerformingContext(invalidCorrelationId);

        // Act
        sut.OnPerforming(context);

        // Assert
        Assert.False(context.Items.ContainsKey("CorrelationIdLogScope"));
    }

    [Fact]
    public void OnPerformed_Should_DisposeScope_WhenPresentInItems()
    {
        // Arrange
        CorrelationIdServerFilter sut = new();
        Mock<IDisposable> scopeMock = new();

        // We use a dummy context and inject our mock scope manually
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
