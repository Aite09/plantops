using MediatR;
using Microsoft.AspNetCore.Mvc;
using PlantOps.Api.Contracts;
using PlantOps.Application.Common;
using PlantOps.Application.WorkOrders.Commands;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Api.Controllers;

/// <summary>
/// Thin controller: translate HTTP into a command or query, send it through MediatR, translate the Result back.
/// No business logic lives here.
/// </summary>
[Route("api/work-orders")]
public sealed class WorkOrdersController(ISender sender) : ApiControllerBase
{
    /// <summary>Lists work orders, highest priority and oldest first.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<WorkOrderSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<WorkOrderSummaryDto>>> List(
        [FromQuery] WorkOrderStatus? status,
        [FromQuery] Guid? plantUnitId,
        [FromQuery] WorkOrderPriority? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ListWorkOrdersQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListWorkOrdersQuery(status, plantUnitId, priority, page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>Gets one work order with its unit, equipment and lifecycle timestamps.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<WorkOrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkOrderDetailsDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWorkOrderByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>Reports a new equipment fault. The work order starts in the Open status.</summary>
    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateWorkOrderCommand(request.Title, request.Description, request.Priority, request.EquipmentId);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new CreatedResponse(result.Value))
            : Problem(result.Error);
    }

    /// <summary>
    /// Edits title, description and priority. Not allowed once the work order is closed. Send the <c>version</c>
    /// from the details response; if someone else changed the work order since, the result is 409 Conflict.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(Guid id, UpdateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateWorkOrderCommand(id, request.Title, request.Description, request.Priority, request.Version), cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }

    /// <summary>Supervisor approves an open work order.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ApproveWorkOrderCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }

    /// <summary>Supervisor assigns or reassigns the work order to a technician.</summary>
    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Assign(Guid id, AssignWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AssignWorkOrderCommand(id, request.TechnicianId), cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }

    /// <summary>The assigned technician starts work.</summary>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StartWorkOrderCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }

    /// <summary>The assigned technician closes the work order with resolution notes.</summary>
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Close(Guid id, CloseWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CloseWorkOrderCommand(id, request.ResolutionNotes), cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }
}
