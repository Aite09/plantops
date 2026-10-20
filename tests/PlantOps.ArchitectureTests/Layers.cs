using System.Reflection;
using PlantOps.Application.Abstractions.Messaging;
using PlantOps.Domain.WorkOrders;
using PlantOps.Infrastructure.Persistence;

namespace PlantOps.ArchitectureTests;

/// <summary>Each layer's assembly, found through a type that lives in it, and the root namespace its types share.</summary>
internal static class Layers
{
    public const string DomainNamespace = "PlantOps.Domain";
    public const string ApplicationNamespace = "PlantOps.Application";
    public const string InfrastructureNamespace = "PlantOps.Infrastructure";
    public const string ApiNamespace = "PlantOps.Api";

    public static readonly Assembly Domain = typeof(WorkOrder).Assembly;
    public static readonly Assembly Application = typeof(ICommand).Assembly;
    public static readonly Assembly Infrastructure = typeof(PlantOpsDbContext).Assembly;
    public static readonly Assembly Api = typeof(Program).Assembly;
}
