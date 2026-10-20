using FluentValidation;
using MediatR;
using PlantOps.Application.Behaviors;
using PlantOps.Application.PlantUnits;
using PlantOps.Application.WorkOrders.Commands;
using PlantOps.Domain.Common;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.Behaviors;

public sealed class ValidationBehaviorTests
{
    private static readonly CreateWorkOrderCommand ValidCreate =
        new("Seal leak", "Dripping from the inboard seal.", WorkOrderPriority.High, Guid.NewGuid());

    private int _nextCalls;

    [Fact]
    public async Task Invalid_request_returns_a_failed_result_with_errors_grouped_by_property_and_never_calls_next()
    {
        // 201 spaces break two Title rules at once (not empty, max length), so both must land under one key.
        var command = ValidCreate with { Title = new string(' ', 201), EquipmentId = Guid.Empty };
        var behavior = new ValidationBehavior<CreateWorkOrderCommand, Result<Guid>>([new CreateWorkOrderCommandValidator()]);

        var result = await behavior.Handle(command, Next(Result.Success(Guid.NewGuid())), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        var errors = result.Error.Should().BeOfType<ValidationError>().Subject.Errors;
        errors.Keys.Should().BeEquivalentTo(nameof(CreateWorkOrderCommand.Title), nameof(CreateWorkOrderCommand.EquipmentId));
        errors[nameof(CreateWorkOrderCommand.Title)].Should().HaveCount(2);
        errors[nameof(CreateWorkOrderCommand.EquipmentId)].Should().ContainSingle();
        _nextCalls.Should().Be(0);
    }

    [Fact]
    public async Task Failures_from_several_validators_for_the_same_property_are_merged_under_that_property()
    {
        var extraRule = new InlineValidator<CreateWorkOrderCommand>();
        extraRule.RuleFor(c => c.Title).Must(t => t.Contains('-')).WithMessage("Title must include the equipment tag.");
        var behavior = new ValidationBehavior<CreateWorkOrderCommand, Result<Guid>>([new CreateWorkOrderCommandValidator(), extraRule]);

        var result = await behavior.Handle(
            ValidCreate with { Title = "" }, Next(Result.Success(Guid.NewGuid())), TestContext.Current.CancellationToken);

        var errors = result.Error.Should().BeOfType<ValidationError>().Subject.Errors;
        errors.Keys.Should().Equal(nameof(CreateWorkOrderCommand.Title));
        errors[nameof(CreateWorkOrderCommand.Title)].Should().HaveCount(2).And.Contain("Title must include the equipment tag.");
    }

    [Fact]
    public async Task The_same_message_from_two_validators_is_reported_once()
    {
        var behavior = new ValidationBehavior<CreateWorkOrderCommand, Result<Guid>>(
            [new CreateWorkOrderCommandValidator(), new CreateWorkOrderCommandValidator()]);

        var result = await behavior.Handle(
            ValidCreate with { Title = "" }, Next(Result.Success(Guid.NewGuid())), TestContext.Current.CancellationToken);

        var errors = result.Error.Should().BeOfType<ValidationError>().Subject.Errors;
        errors[nameof(CreateWorkOrderCommand.Title)].Should().ContainSingle();
    }

    [Fact]
    public async Task Valid_request_calls_next_once_and_returns_its_response_unchanged()
    {
        var handlerResponse = Result.Success(Guid.NewGuid());
        var behavior = new ValidationBehavior<CreateWorkOrderCommand, Result<Guid>>([new CreateWorkOrderCommandValidator()]);

        var result = await behavior.Handle(ValidCreate, Next(handlerResponse), TestContext.Current.CancellationToken);

        result.Should().BeSameAs(handlerResponse);
        _nextCalls.Should().Be(1);
    }

    [Fact]
    public async Task Invalid_command_without_a_return_value_gets_a_failed_plain_Result()
    {
        // Result (no value) takes a different branch of the failure factory than Result<T>.
        var command = new UpdateWorkOrderCommand(Guid.NewGuid(), "Seal leak", "Dripping.", WorkOrderPriority.High, Version: [1, 2, 3]);
        var behavior = new ValidationBehavior<UpdateWorkOrderCommand, Result>([new UpdateWorkOrderCommandValidator()]);

        var result = await behavior.Handle(command, Next(Result.Success()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>()
            .Which.Errors.Keys.Should().Equal(nameof(UpdateWorkOrderCommand.Version));
        _nextCalls.Should().Be(0);
    }

    [Fact]
    public async Task Request_without_validators_goes_straight_to_next()
    {
        var handlerResponse = Result.Success<IReadOnlyList<PlantUnitDto>>([]);
        var behavior = new ValidationBehavior<ListPlantUnitsQuery, Result<IReadOnlyList<PlantUnitDto>>>([]);

        var result = await behavior.Handle(new ListPlantUnitsQuery(), Next(handlerResponse), TestContext.Current.CancellationToken);

        result.Should().BeSameAs(handlerResponse);
        _nextCalls.Should().Be(1);
    }

    /// <summary>Stands in for the handler: counts calls and returns a fixed response.</summary>
    private RequestHandlerDelegate<TResponse> Next<TResponse>(TResponse response) => _ =>
    {
        _nextCalls++;
        return Task.FromResult(response);
    };
}
