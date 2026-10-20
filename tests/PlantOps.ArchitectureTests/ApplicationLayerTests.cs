using NetArchTest.Rules;

namespace PlantOps.ArchitectureTests;

public sealed class ApplicationLayerTests
{
    [Theory]
    [InlineData(Layers.InfrastructureNamespace)]
    [InlineData(Layers.ApiNamespace)]
    public void Application_does_not_depend_on_an_outer_layer(string layerNamespace)
    {
        var result = Types.InAssembly(Layers.Application)
            .ShouldNot().HaveDependencyOn(layerNamespace)
            .GetResult();

        result.ShouldPass();
    }

    // Use cases reach the database and HTTP only through interfaces they own (repositories, ICurrentUser),
    // so they can be unit tested without either and the implementations can change underneath them.
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    public void Application_does_not_depend_on_data_access_or_web_frameworks(string frameworkNamespace)
    {
        var result = Types.InAssembly(Layers.Application)
            .ShouldNot().HaveDependencyOn(frameworkNamespace)
            .GetResult();

        result.ShouldPass();
    }
}
