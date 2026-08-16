using FluentResults;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hermes.Application.DTOs.Email;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Newsletter;
using Hermes.Application.Services.NotificationLogs;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Repositories;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Hermes.IntegrationTests.News;

/// <summary>
/// Contains integration tests for the background newsletter execution workflow,
/// testing scheduling resolution, HTML rendering, and email dispatch against MySQL database state.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class NewsletterExecutionIntegrationTests(MySqlApiFixture fixture)
{
    /// <summary>
    /// Tests that <see cref="NewsletterSchedulerWorkerService"/> resolves due subscriptions from MySQL
    /// and enqueues Hangfire jobs for execution.
    /// </summary>
    [Fact]
    public async Task Worker_NewsletterScheduler_Enqueues_Job_For_Due_Subscription()
    {
        // Arrange
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();

        var user = new User
        {
            Id = new UserId(0),
            Name = "Scheduler Execution User",
            Email = Email.Parse($"sched-{Guid.NewGuid():N}@integration.hermes"),
            PasswordHash = "$2a$11$hash"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        DateTime nowUtc = DateTime.UtcNow;
        DateTime currentSlotUtc = new(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, nowUtc.Minute, 0, DateTimeKind.Utc);

        var sub = NewsletterSubscription.CreateForUser(user.Id);
        sub.UpdateFilters(["ai"], [NewsCategory.Technology], [Language.English], [Country.Germany]);
        sub.AssignDigestSchedule(ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday, Weekdays.Tuesday, Weekdays.Wednesday, Weekdays.Thursday, Weekdays.Friday, Weekdays.Saturday, Weekdays.Sunday], [TimeOnly.FromDateTime(currentSlotUtc)]));

        db.NewsletterSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        Mock<IBackgroundJobClient> jobClientMock = new();
        Mock<INewsletterScheduleService> scheduleServiceMock = new();
        scheduleServiceMock.Setup(s => s.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([(sub.Id, user.Id)]);

        var options = Options.Create(new NewsletterOptions { TimeZoneId = "UTC" });
        var worker = new Hermes.Worker.Services.Scheduling.NewsletterSchedulerWorkerService(
            scheduleServiceMock.Object,
            jobClientMock.Object,
            NullLogger<Hermes.Worker.Services.Scheduling.NewsletterSchedulerWorkerService>.Instance,
            options);

        // Act
        await worker.RunAsync(CancellationToken.None);

        // Assert
        jobClientMock.Verify(j => j.Create(
            It.Is<Job>(job => job.Type == typeof(NotificationJobService) && job.Method.Name == nameof(NotificationJobService.SendNewsDigestAsync)),
            It.IsAny<EnqueuedState>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterDigestService"/> queries user and subscription from MySQL,
    /// formats articles, renders the HTML template, and dispatches the email via <see cref="IEmailProvider"/>.
    /// </summary>
    [Fact]
    public async Task NewsletterDigestService_EndToEnd_GeneratesHtml_And_Calls_EmailProvider()
    {
        // Arrange
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();

        var user = new User
        {
            Id = new UserId(0),
            Name = "Digest Recipient",
            Email = Email.Parse($"digest-{Guid.NewGuid():N}@integration.hermes"),
            PasswordHash = "$2a$11$hash"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sub = NewsletterSubscription.CreateForUser(user.Id);
        sub.UpdateFilters(["dotnet"], [NewsCategory.Technology], [Language.English], [Country.Germany]);
        sub.AssignDigestSchedule(ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(8, 0)]));

        db.NewsletterSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        IUserRepository userRepo = new UserRepository(db);
        INewsletterSubscriptionRepository newsRepo = new NewsletterSubscriptionRepository(db);

        Mock<IArticleFetchingService> articleFetchingMock = new();
        articleFetchingMock.Setup(f => f.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new NewsArticle("art-101", "https://news.example.com/101", "Release of .NET 10", "A new major release.", ["Technology"], null),
                new NewsArticle("art-102", "https://news.example.com/102", "Cloud Innovations", "Cloud platform updates.", ["Business"], null)
            ]);

        Mock<IEmailProvider> emailSenderMock = new();
        EmailMessageDto? capturedEmail = null;
        emailSenderMock.Setup(e => e.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessageDto, CancellationToken>((dto, _) => capturedEmail = dto)
            .Returns(Task.CompletedTask);

        Mock<INewsletterHtmlService> htmlRendererMock = new();
        htmlRendererMock.Setup(h => h.RenderNewsletterAsync(It.IsAny<NewsletterRenderRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html><body><h1>Hello Digest Recipient</h1></body></html>");

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero));

        NewsletterDigestService digestService = new(
            userRepo,
            newsRepo,
            articleFetchingMock.Object,
            emailSenderMock.Object,
            htmlRendererMock.Object,
            timeProvider);

        // Act
        Result<bool> result = await digestService.SendAsync(user.Id, sub.Id, DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.NotNull(capturedEmail);
        Assert.Equal(user.Email.Value, capturedEmail!.To.Address);
        Assert.Contains("Hermes Newsletter", capturedEmail.Subject);
        Assert.Contains("Hello Digest Recipient", capturedEmail.Body);
    }
}
