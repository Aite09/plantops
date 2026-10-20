using System.Reflection;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.ArchitectureTests;

public sealed class DomainEntityTests
{
    // NetArchTest cannot inspect property accessors, so this rule uses reflection. A public set (or init) would let
    // any caller change state directly, e.g. jump a work order to Closed, bypassing the methods that enforce the rules.
    [Fact]
    public void Entity_properties_have_no_public_setters()
    {
        var entityTypes = Layers.Domain.GetTypes()
            .Where(type => typeof(Entity).IsAssignableFrom(type))
            .ToList();
        // Guards against a scan that silently finds nothing and so passes without checking anything.
        entityTypes.Should().Contain(typeof(WorkOrder));

        var publiclySettable = entityTypes
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(property => property.SetMethod is { IsPublic: true })
                .Select(property => $"{type.FullName}.{property.Name}"))
            .ToList();

        publiclySettable.ShouldBeNone("entities change state only through their own methods");
    }
}
