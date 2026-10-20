using NetArchTest.Rules;

namespace PlantOps.ArchitectureTests;

public sealed class ApiLayerTests
{
    // The Api project references Infrastructure only so Program.cs, the composition root, can register its services.
    [Fact]
    public void Only_the_composition_root_depends_on_Infrastructure()
    {
        var result = Types.InAssembly(Layers.Api)
            .That().DoNotHaveName(nameof(Program))
            .ShouldNot().HaveDependencyOn(Layers.InfrastructureNamespace)
            .GetResult();

        result.ShouldPass();
    }
}
