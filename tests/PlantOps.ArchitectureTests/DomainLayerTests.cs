using System.Reflection;
using NetArchTest.Rules;

namespace PlantOps.ArchitectureTests;

public sealed class DomainLayerTests
{
    [Theory]
    [InlineData(Layers.ApplicationNamespace)]
    [InlineData(Layers.InfrastructureNamespace)]
    [InlineData(Layers.ApiNamespace)]
    public void Domain_does_not_depend_on_an_outer_layer(string layerNamespace)
    {
        var result = Types.InAssembly(Layers.Domain)
            .ShouldNot().HaveDependencyOn(layerNamespace)
            .GetResult();

        result.ShouldPass();
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("MediatR")]
    [InlineData("FluentValidation")]
    [InlineData("Microsoft.AspNetCore")]
    public void Domain_does_not_depend_on_a_framework(string frameworkNamespace)
    {
        var result = Types.InAssembly(Layers.Domain)
            .ShouldNot().HaveDependencyOn(frameworkNamespace)
            .GetResult();

        result.ShouldPass();
    }

    // The rules above only catch the namespaces they name. The compiler records every assembly the domain
    // actually uses, so this also catches a package nobody thought to list. A reference counts as the base class
    // library only if it loads from the same runtime folder as System.Object; a name check would let through
    // third-party packages named System.*, and Microsoft.Extensions.* load from the ASP.NET Core shared framework.
    [Fact]
    public void Domain_references_only_the_base_class_library()
    {
        var baseClassLibraryFolder = Path.GetDirectoryName(typeof(object).Assembly.Location);

        var otherReferences = Layers.Domain.GetReferencedAssemblies()
            .Select(Assembly.Load)
            .Where(assembly => Path.GetDirectoryName(assembly.Location) != baseClassLibraryFolder)
            .Select(assembly => $"{assembly.GetName().Name} ({assembly.Location})")
            .ToList();

        otherReferences.ShouldBeNone("the domain model has no project or package references, by design");
    }
}
