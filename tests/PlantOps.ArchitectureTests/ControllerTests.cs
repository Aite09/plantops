using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace PlantOps.ArchitectureTests;

/// <summary>Controllers stay thin: HTTP in, a command or query sent through MediatR, a Result mapped back out.</summary>
public sealed class ControllerTests
{
    [Fact]
    public void Controllers_send_requests_through_the_MediatR_sender()
    {
        var result = Controllers()
            .Should().HaveDependencyOn("MediatR.ISender")
            .GetResult();

        result.ShouldPass();
    }

    // IMediator extends ISender and inherits its Send method, so a controller that takes IMediator still passes the
    // rule above. ISender is the narrower contract: a controller sends requests and never publishes notifications.
    [Fact]
    public void Controllers_do_not_take_the_wider_IMediator()
    {
        var result = Controllers()
            .ShouldNot().HaveDependencyOn("MediatR.IMediator")
            .GetResult();

        result.ShouldPass();
    }

    // The commands, queries and DTOs a controller sends and returns all live in the Application layer.
    [Fact]
    public void Controllers_depend_on_the_Application_layer()
    {
        var result = Controllers()
            .Should().HaveDependencyOn(Layers.ApplicationNamespace)
            .GetResult();

        result.ShouldPass();
    }

    [Fact]
    public void Controllers_do_not_depend_on_Infrastructure()
    {
        var result = Controllers()
            .ShouldNot().HaveDependencyOn(Layers.InfrastructureNamespace)
            .GetResult();

        result.ShouldPass();
    }

    // Reading or saving data from a controller would bypass the use cases and their rules. That covers EF Core
    // (which the Api layer may use: the exception handler maps concurrency conflicts to 409) and the Application's
    // own repository and query interfaces, which only handlers should call.
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("PlantOps.Application.Abstractions.Persistence")]
    public void Controllers_do_not_access_data_directly(string dataAccessNamespace)
    {
        var result = Controllers()
            .ShouldNot().HaveDependencyOn(dataAccessNamespace)
            .GetResult();

        result.ShouldPass();
    }

    private static PredicateList Controllers()
    {
        var controllers = Types.InAssembly(Layers.Api)
            .That().Inherit(typeof(ControllerBase))
            .And().AreNotAbstract();

        // A rule over no types always passes, so make sure the filter still finds the controllers.
        controllers.GetTypes().Should().NotBeEmpty();
        return controllers;
    }
}
