using NetArchTest.Rules;

namespace PlantOps.ArchitectureTests;

public sealed class InfrastructureLayerTests
{
    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        var result = Types.InAssembly(Layers.Infrastructure)
            .ShouldNot().HaveDependencyOn(Layers.ApiNamespace)
            .GetResult();

        result.ShouldPass();
    }
}
