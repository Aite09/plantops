using NetArchTest.Rules;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Application.WorkOrders.Commands;

namespace PlantOps.ArchitectureTests;

/// <summary>The suffix says whether a request changes state (Command) or only reads (Query).</summary>
public sealed class CommandAndQueryNamingTests
{
    [Fact]
    public void Command_names_end_with_Command()
    {
        var commands = Types.InAssembly(Layers.Application)
            .That().ImplementInterface(typeof(ICommand))
            .Or().ImplementInterface(typeof(ICommand<>));
        // A rule over no types always passes. One command of each kind (ICommand, ICommand<Guid>) proves that both
        // halves of the filter still match; a check for "not empty" would pass with either half broken.
        commands.GetTypes().Should()
            .Contain(typeof(ApproveWorkOrderCommand))
            .And.Contain(typeof(CreateWorkOrderCommand));

        var result = commands.Should().HaveNameEndingWith("Command").GetResult();

        result.ShouldPass();
    }

    [Fact]
    public void Query_names_end_with_Query()
    {
        var queries = Types.InAssembly(Layers.Application)
            .That().ImplementInterface(typeof(IQuery<>));
        // A rule over no types always passes, so make sure the filter still finds the queries.
        queries.GetTypes().Should().NotBeEmpty();

        var result = queries.Should().HaveNameEndingWith("Query").GetResult();

        result.ShouldPass();
    }
}
