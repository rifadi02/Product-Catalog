namespace Catalog.UnitTests.Application;

public sealed class PagingTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(20, 20)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(10_000, 100)]
    public void PageSize_is_clamped_into_range(int requested, int expected)
    {
        Paging.ClampPageSize(requested).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(7, 7)]
    public void Page_is_clamped_to_at_least_one(int requested, int expected)
    {
        Paging.ClampPage(requested).Should().Be(expected);
    }

    [Fact]
    public void TotalPages_rounds_up()
    {
        var page = new PagedResult<int>([1, 2, 3], Page: 1, PageSize: 20, TotalCount: 61);

        page.TotalPages.Should().Be(4);
    }

    [Fact]
    public void An_empty_result_set_has_no_pages_and_no_navigation()
    {
        var page = PagedResult<int>.Empty(page: 1, pageSize: 20);

        page.TotalPages.Should().Be(0);
        page.HasNextPage.Should().BeFalse();
        page.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void Navigation_flags_reflect_position_in_the_set()
    {
        var first = new PagedResult<int>([1], 1, 20, 61);
        var middle = new PagedResult<int>([1], 2, 20, 61);
        var last = new PagedResult<int>([1], 4, 20, 61);

        first.HasPreviousPage.Should().BeFalse();
        first.HasNextPage.Should().BeTrue();

        middle.HasPreviousPage.Should().BeTrue();
        middle.HasNextPage.Should().BeTrue();

        last.HasPreviousPage.Should().BeTrue();
        last.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void A_page_size_of_zero_cannot_divide_by_zero()
    {
        var page = new PagedResult<int>([], 1, 0, 10);

        page.TotalPages.Should().Be(0);
    }
}
