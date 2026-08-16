using System.Diagnostics;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using Hermes.Infrastructure.Adapters.Outbound.Hangfire;
using Hermes.Worker.Filters.Hangfire;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Worker.Hangfire;

/// <summary>
/// Contains unit tests for <see cref="HangfireTraceContextClientFilter"/> and <see cref="HangfireTraceContextServerFilter"/>,
/// verifying W3C trace context extraction, parent-child activity linkage, and diagnostic tagging.
/// </summary>
public sealed class HangfireTraceContextFilterTests
{
    private static CreatingContext CreateCreatingContext()
    {
        Mock<IStorageConnection> connectionMock = new();
        Job job = Job.FromExpression(() => Console.WriteLine());
        CreateContext createContext = new(Mock.Of<JobStorage>(), connectionMock.Object, job, new global::Hangfire.States.EnqueuedState());
        return new CreatingContext(createContext);
    }

    private static PerformingContext CreatePerformingContext(string? traceParentValue, string? traceStateValue = null)
    {
        Mock<IStorageConnection> connectionMock = new();

        if (traceParentValue != null)
        {
            string jsonValue = System.Text.Json.JsonSerializer.Serialize(traceParentValue);
            connectionMock.Setup(x => x.GetJobParameter(It.IsAny<string>(), HangfireTraceContextServerFilter.TRACE_PARENT_PARAMETER_NAME))
                .Returns(jsonValue);
        }

        if (traceStateValue != null)
        {
            string jsonState = System.Text.Json.JsonSerializer.Serialize(traceStateValue);
            connectionMock.Setup(x => x.GetJobParameter(It.IsAny<string>(), HangfireTraceContextServerFilter.TRACE_STATE_PARAMETER_NAME))
                .Returns(jsonState);
        }

        BackgroundJob backgroundJob = new(
            "job-42",
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
    /// Tests that <see cref="HangfireTraceContextClientFilter.OnCreating"/> attaches W3C TraceParent
    /// when an active <see cref="Activity.Current"/> is present on the thread.
    /// </summary>
    [Fact]
    public void ClientFilter_OnCreating_Should_AttachTraceParent_WhenActivityIsActive()
    {
        // Arrange
        HangfireTraceContextClientFilter sut = new();
        CreatingContext context = CreateCreatingContext();

        using Activity sourceActivity = new Activity("test-operation")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        // Act
        sut.OnCreating(context);

        // Assert
        Assert.True(context.Parameters.ContainsKey(HangfireTraceContextClientFilter.TRACE_PARENT_PARAMETER_NAME));
        Assert.Equal(sourceActivity.Id, context.Parameters[HangfireTraceContextClientFilter.TRACE_PARENT_PARAMETER_NAME]);
    }

    /// <summary>
    /// Tests that <see cref="HangfireTraceContextClientFilter.OnCreating"/> does not attach parameters
    /// when <see cref="Activity.Current"/> is null.
    /// </summary>
    [Fact]
    public void ClientFilter_OnCreating_Should_NotAttachParameters_WhenActivityIsNull()
    {
        // Arrange
        HangfireTraceContextClientFilter sut = new();
        CreatingContext context = CreateCreatingContext();

        // Ensure no ambient activity
        Activity.Current = null;

        // Act
        sut.OnCreating(context);

        // Assert
        Assert.False(context.Parameters.ContainsKey(HangfireTraceContextClientFilter.TRACE_PARENT_PARAMETER_NAME));
    }

    /// <summary>
    /// Tests that <see cref="HangfireTraceContextServerFilter.OnPerforming"/> extracts TraceParent
    /// and starts a child Activity when an activity listener is active.
    /// </summary>
    [Fact]
    public void ServerFilter_OnPerforming_Should_ExtractTraceParent_AndCreateActivity()
    {
        // Arrange
        HangfireTraceContextServerFilter sut = new();
        string validTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        PerformingContext context = CreatePerformingContext(validTraceParent);

        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == HangfireTraceContextServerFilter.ACTIVITY_SOURCE_NAME,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        sut.OnPerforming(context);

        // Assert
        Assert.True(context.Items.ContainsKey("HangfireTraceActivity"));
        Activity? activity = context.Items["HangfireTraceActivity"] as Activity;
        Assert.NotNull(activity);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", activity.TraceId.ToHexString());
        Assert.Equal("job-42", activity.GetTagItem("hangfire.job.id"));

        activity.Dispose();
    }

    /// <summary>
    /// Tests that <see cref="HangfireTraceContextServerFilter.OnPerformed"/> records exceptions
    /// on failing jobs and disposes the activity.
    /// </summary>
    [Fact]
    public void ServerFilter_OnPerformed_Should_HandleException_AndDisposeActivity()
    {
        // Arrange
        HangfireTraceContextServerFilter sut = new();

        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == HangfireTraceContextServerFilter.ACTIVITY_SOURCE_NAME,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        ActivitySource source = new(HangfireTraceContextServerFilter.ACTIVITY_SOURCE_NAME);
        Activity? activity = source.StartActivity("Hangfire TestJob");
        Assert.NotNull(activity);

        PerformingContext performing = CreatePerformingContext(null);
        InvalidOperationException jobException = new("Database connection timeout");
        PerformedContext performed = new(performing, null, false, jobException);
        performed.Items["HangfireTraceActivity"] = activity;

        // Act
        sut.OnPerformed(performed);

        // Assert
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }
}
