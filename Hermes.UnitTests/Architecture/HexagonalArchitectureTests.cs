using System.Reflection;
using Hermes.Domain.Entities;
using NetArchTest.Rules;
using Xunit;

namespace Hermes.UnitTests.Architecture;

/// <summary>
/// Verifies architectural constraints and dependency boundaries across the Hexagonal Architecture / Clean Architecture layers.
/// Ensures that domain purity, inbound/outbound port segregation, and layer dependencies remain strictly enforced in CI/CD.
/// </summary>
public sealed class HexagonalArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(User).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Hermes.Application.Ports.Inbound.IUserService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Hermes.Infrastructure.Adapters.Outbound.Persistence.Data.HermesDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Hermes.Api.Controllers.Users.UsersController).Assembly;

    /// <summary>
    /// Tests that the <c>Hermes.Domain</c> layer does not reference any outer layers (Application, Infrastructure, Api, Worker, Presentation).
    /// Enforces absolute domain purity according to DDD and Hexagonal principles.
    /// </summary>
    [Fact]
    public void Domain_Should_Not_Have_Dependency_On_Outer_Layers()
    {
        // Act
        TestResult result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Hermes.Application",
                "Hermes.Infrastructure",
                "Hermes.Api",
                "Hermes.Worker",
                "Hermes.WebFrontend")
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, "Domain layer must remain pure and free from outer layer dependencies.");
    }

    /// <summary>
    /// Tests that the <c>Hermes.Application</c> layer does not reference outer adapters (Infrastructure, Api, Worker, Presentation).
    /// Enforces the Dependency Inversion Principle where Application depends only on Domain and its own Port abstractions.
    /// </summary>
    [Fact]
    public void Application_Should_Not_Have_Dependency_On_Infrastructure_Or_Api()
    {
        // Act
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Hermes.Infrastructure",
                "Hermes.Api",
                "Hermes.Worker",
                "Hermes.WebFrontend")
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, "Application layer must not depend on concrete infrastructure adapters or API presentations.");
    }

    /// <summary>
    /// Tests that API controllers in <c>Hermes.Api</c> do not directly depend on EF Core <c>HermesDbContext</c> or repositories,
    /// ensuring all interactions flow through Inbound application service ports.
    /// </summary>
    [Fact]
    public void Controllers_Should_Not_Depend_On_Infrastructure_Persistence_Directly()
    {
        // Act
        TestResult result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Hermes.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOn("Hermes.Infrastructure.Adapters.Outbound.Persistence.Data")
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, "API Controllers must invoke Application Inbound Ports, never DbContext directly.");
    }

    /// <summary>
    /// Tests that all Domain Event types in <c>Hermes.Domain</c> end with the 'Event' suffix.
    /// </summary>
    [Fact]
    public void DomainEvents_Should_Have_Event_Suffix()
    {
        // Act
        TestResult result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("Hermes.Domain.Events")
            .And()
            .AreClasses()
            .Should()
            .HaveNameEndingWith("Event")
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, "All domain events must end with 'Event' suffix.");
    }

    /// <summary>
    /// Tests that the <see cref="User"/> domain aggregate root inherits from <see cref="AggregateRoot"/>.
    /// </summary>
    [Fact]
    public void AggregateRoots_Should_Inherit_From_AggregateRoot_Base()
    {
        // Act
        TestResult result = Types.InAssembly(DomainAssembly)
            .That()
            .HaveName("User")
            .Should()
            .Inherit(typeof(AggregateRoot))
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, "User aggregate root must inherit from AggregateRoot.");
    }
}
