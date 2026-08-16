using System.Security.Claims;
using Hangfire;
using Hangfire.Dashboard;
using Hermes.Api.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Security;

public sealed class HangfireDashboardAuthorizationFilterTests
{
    private sealed class FakeEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Hermes.Api";
        public string ContentRootPath { get; set; } = "C:/Hermes";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static DefaultHttpContext CreateHttpContext(ClaimsPrincipal? user = null)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        if (user != null)
        {
            context.User = user;
        }
        return context;
    }

    [Fact]
    public void Authorize_Returns_True_In_Development()
    {
        var env = new FakeEnvironment(Environments.Development);
        var filter = new HangfireDashboardAuthorizationFilter(env);

        var httpContext = CreateHttpContext();
        var storage = Mock.Of<JobStorage>();
        var dashboardContext = new AspNetCoreDashboardContext(storage, new DashboardOptions(), httpContext);

        bool result = filter.Authorize(dashboardContext);
        Assert.True(result);
    }

    [Fact]
    public void Authorize_Returns_False_In_Production_When_Unauthenticated()
    {
        var env = new FakeEnvironment(Environments.Production);
        var filter = new HangfireDashboardAuthorizationFilter(env);

        var httpContext = CreateHttpContext();
        var storage = Mock.Of<JobStorage>();
        var dashboardContext = new AspNetCoreDashboardContext(storage, new DashboardOptions(), httpContext);

        bool result = filter.Authorize(dashboardContext);
        Assert.False(result);
    }

    [Fact]
    public void Authorize_Returns_True_In_Production_When_Authenticated()
    {
        var env = new FakeEnvironment(Environments.Production);
        var filter = new HangfireDashboardAuthorizationFilter(env);

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "Bearer");
        var httpContext = CreateHttpContext(new ClaimsPrincipal(identity));
        var storage = Mock.Of<JobStorage>();
        var dashboardContext = new AspNetCoreDashboardContext(storage, new DashboardOptions(), httpContext);

        bool result = filter.Authorize(dashboardContext);
        Assert.True(result);
    }
}
