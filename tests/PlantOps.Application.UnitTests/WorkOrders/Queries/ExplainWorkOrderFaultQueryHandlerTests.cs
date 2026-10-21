using Moq;
using PlantOps.Application.Abstractions.Ai;
using PlantOps.Application.Abstractions.Persistence;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Queries;

public sealed class ExplainWorkOrderFaultQueryHandlerTests
{
    private readonly Mock<IWorkOrderQueries> _queries = new();
    private readonly Mock<IFaultExplainer> _explainer = new();
    private readonly ExplainWorkOrderFaultQueryHandler _handler;

    public ExplainWorkOrderFaultQueryHandlerTests()
    {
        _explainer.Setup(e => e.IsEnabled).Returns(true);
        _handler = new ExplainWorkOrderFaultQueryHandler(_queries.Object, _explainer.Object);
    }

    [Fact]
    public async Task When_the_feature_is_off_it_returns_Disabled_without_reading_the_work_order()
    {
        _explainer.Setup(e => e.IsEnabled).Returns(false);

        var result = await _handler.Handle(new ExplainWorkOrderFaultQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Should().Be(FaultExplanationErrors.Disabled);
        _queries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Missing_work_order_returns_NotFound_without_calling_the_model()
    {
        var missingId = Guid.NewGuid();

        var result = await _handler.Handle(new ExplainWorkOrderFaultQuery(missingId), TestContext.Current.CancellationToken);

        result.Error.Should().Be(WorkOrderErrors.NotFound(missingId));
        _explainer.Verify(e => e.ExplainAsync(It.IsAny<FaultReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_model_is_given_the_equipment_and_the_reporters_words_and_its_answer_is_returned()
    {
        var details = Stored();
        var explanation = new FaultExplanationDto("Possible causes:\n- Worn seal faces", "phi4-mini", TestWorkOrders.CreatedAtUtc);
        _explainer
            .Setup(e => e.ExplainAsync(It.IsAny<FaultReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(explanation);

        var result = await _handler.Handle(new ExplainWorkOrderFaultQuery(details.Id), TestContext.Current.CancellationToken);

        result.Value.Should().Be(explanation);
        _explainer.Verify(e => e.ExplainAsync(
            new FaultReport("P-101A", "Crude charge pump A", "U-100", "Crude Distillation", WorkOrderPriority.High,
                "Mechanical seal leaking", "Steady drip from the inboard seal."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_model_that_does_not_answer_is_passed_on_as_Unavailable()
    {
        var details = Stored();
        _explainer
            .Setup(e => e.ExplainAsync(It.IsAny<FaultReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FaultExplanationErrors.Unavailable);

        var result = await _handler.Handle(new ExplainWorkOrderFaultQuery(details.Id), TestContext.Current.CancellationToken);

        result.Error.Should().Be(FaultExplanationErrors.Unavailable);
    }

    private WorkOrderDetailsDto Stored()
    {
        var details = new WorkOrderDetailsDto(
            Id: Guid.NewGuid(),
            Title: "Mechanical seal leaking",
            Description: "Steady drip from the inboard seal.",
            Priority: WorkOrderPriority.High,
            Status: WorkOrderStatus.Open,
            PlantUnitId: Guid.NewGuid(),
            PlantUnitCode: "U-100",
            PlantUnitName: "Crude Distillation",
            EquipmentId: Guid.NewGuid(),
            EquipmentTag: "P-101A",
            EquipmentName: "Crude charge pump A",
            ReportedByUserId: Guid.NewGuid(),
            ReportedByName: null,
            ApprovedByUserId: null,
            ApprovedByName: null,
            AssignedToUserId: null,
            AssignedToName: null,
            ResolutionNotes: null,
            CreatedAtUtc: TestWorkOrders.CreatedAtUtc,
            ApprovedAtUtc: null,
            AssignedAtUtc: null,
            StartedAtUtc: null,
            ClosedAtUtc: null,
            Version: [0, 0, 0, 0, 0, 0, 7, 209]);
        _queries.Setup(q => q.GetDetailsAsync(details.Id, It.IsAny<CancellationToken>())).ReturnsAsync(details);
        return details;
    }
}
