using OrderProcessing.Api.DTOs.Common;

namespace OrderProcessing.Api.Tests.Unit;

public sealed class PagedResponseTests
{
    [Fact]
    public void PaginationProperties_AreCalculatedCorrectly()
    {
        // Arrange
        var response = new PagedResponse<int>
        {
            Items = [1, 2, 3],
            Page = 2,
            PageSize = 3,
            TotalCount = 8
        };

        // Assert
        Assert.Equal(3, response.TotalPages);
        Assert.True(response.HasPreviousPage);
        Assert.True(response.HasNextPage);
    }

    [Fact]
    public void TotalPages_WhenNoRecords_ReturnsZero()
    {
        // Arrange
        var response = new PagedResponse<int>
        {
            Items = [],
            Page = 1,
            PageSize = 10,
            TotalCount = 0
        };

        // Assert
        Assert.Equal(0, response.TotalPages);
        Assert.False(response.HasPreviousPage);
        Assert.False(response.HasNextPage);
    }
}