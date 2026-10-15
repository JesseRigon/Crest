using Grove.Workflows.Common.Models;

namespace Grove.Workflows.Models;

public record PagedListResponse<T>: LinkedResource
{
    public PagedListResponse()
    {
    }
    
    public PagedListResponse(Page<T> page)
    {
        Items = page.Items;
        TotalCount = page.TotalCount;
    }

    public ICollection<T> Items { get; set; } = default!;
    public long TotalCount { get; set; }
    
    public static PagedListResponse<T> From(Page<T> page) => new(page);
}