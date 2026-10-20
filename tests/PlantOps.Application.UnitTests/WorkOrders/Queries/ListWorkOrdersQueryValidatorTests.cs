using FluentValidation.TestHelper;
using PlantOps.Application.WorkOrders.Queries;
using PlantOps.Domain.WorkOrders;

namespace PlantOps.Application.UnitTests.WorkOrders.Queries;

public sealed class ListWorkOrdersQueryValidatorTests
{
    private readonly ListWorkOrdersQueryValidator _validator = new();

    [Fact]
    public void Default_query_is_valid()
    {
        _validator.TestValidate(new ListWorkOrdersQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(ListWorkOrdersQuery.MaxPage)]
    public void Page_from_1_to_MaxPage_is_valid(int page)
    {
        _validator.TestValidate(new ListWorkOrdersQuery(Page: page)).ShouldNotHaveValidationErrorFor(q => q.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ListWorkOrdersQuery.MaxPage + 1)]
    public void Page_outside_1_to_MaxPage_is_rejected(int page)
    {
        _validator.TestValidate(new ListWorkOrdersQuery(Page: page)).ShouldHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void MaxPage_keeps_the_row_offset_of_a_full_last_page_within_int_range()
    {
        // The list query skips (Page - 1) * PageSize rows; MaxPage exists so that product never overflows an int.
        var largestOffset = (long)ListWorkOrdersQuery.MaxPage * ListWorkOrdersQuery.MaxPageSize;

        largestOffset.Should().BeLessThanOrEqualTo(int.MaxValue);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(ListWorkOrdersQuery.MaxPageSize)]
    public void Page_size_from_1_to_MaxPageSize_is_valid(int pageSize)
    {
        _validator.TestValidate(new ListWorkOrdersQuery(PageSize: pageSize)).ShouldNotHaveValidationErrorFor(q => q.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ListWorkOrdersQuery.MaxPageSize + 1)]
    public void Page_size_outside_1_to_MaxPageSize_is_rejected(int pageSize)
    {
        _validator.TestValidate(new ListWorkOrdersQuery(PageSize: pageSize)).ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void Undefined_status_and_priority_filters_are_rejected()
    {
        var query = new ListWorkOrdersQuery(Status: (WorkOrderStatus)99, Priority: (WorkOrderPriority)0);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Status);
        result.ShouldHaveValidationErrorFor(q => q.Priority);
    }
}
