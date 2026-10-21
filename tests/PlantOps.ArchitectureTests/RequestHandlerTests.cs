using MediatR;
using NetArchTest.Rules;
using PlantOps.Application.Abstractions.Authentication;

namespace PlantOps.ArchitectureTests;

public sealed class RequestHandlerTests
{
    [Fact]
    public void Request_handlers_are_sealed()
    {
        var result = RequestHandlers()
            .Should().BeSealed()
            .GetResult();

        result.ShouldPass();
    }

    // Only MediatR should call a handler. A public handler could be called directly from another layer,
    // skipping the validation and logging behaviors in the pipeline.
    [Fact]
    public void Request_handlers_are_not_public()
    {
        var result = RequestHandlers()
            .Should().NotBePublic()
            .GetResult();

        result.ShouldPass();
    }

    [Fact]
    public void Request_handler_names_end_with_Handler()
    {
        var result = RequestHandlers()
            .Should().HaveNameEndingWith("Handler")
            .GetResult();

        result.ShouldPass();
    }

    // GET /api/dashboard is output-cached, and one copy is served to every Supervisor and Admin. That is only safe while
    // its handler cannot tell who is asking; if it ever needs to, the cache policy must vary by user first.
    [Fact]
    public void The_cached_dashboard_handler_does_not_read_the_current_user()
    {
        var handler = Types.InAssembly(Layers.Application).That().HaveName("GetDashboardQueryHandler");
        handler.GetTypes().Should().ContainSingle();

        var result = handler.ShouldNot().HaveDependencyOn(typeof(ICurrentUser).FullName!).GetResult();

        result.ShouldPass();
    }

    // ICommandHandler and IQueryHandler both extend MediatR's IRequestHandler<TRequest, TResponse>. MediatR's
    // IRequestHandler<TRequest> (for a request with no response) does not, so it is matched separately; together
    // they find every handler, including one that skips the project's own interfaces.
    // NetArchTest ANDs the conditions inside each Or() group, so AreClasses() is repeated: written once at the end
    // it would apply only to the last group, and the handler interfaces themselves would be selected too.
    private static PredicateList RequestHandlers()
    {
        var handlers = Types.InAssembly(Layers.Application)
            .That().AreClasses().And().ImplementInterface(typeof(IRequestHandler<,>))
            .Or().AreClasses().And().ImplementInterface(typeof(IRequestHandler<>));

        // A rule over no types always passes, so make sure the filter still finds the handlers.
        handlers.GetTypes().Should().NotBeEmpty();
        return handlers;
    }
}
