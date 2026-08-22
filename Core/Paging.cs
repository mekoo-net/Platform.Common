namespace Platform.Common;

/// <summary>分页查询参数。用 <see cref="Normalize"/> 从原始 page/pageSize 收口合法范围。</summary>
public readonly record struct PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int DefaultMaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;

    public static PageRequest Normalize(
        int page,
        int pageSize,
        int maxPageSize = DefaultMaxPageSize,
        int defaultPageSize = DefaultPageSize)
    {
        var p = page < 1 ? 1 : page;
        var size = pageSize < 1 ? defaultPageSize : Math.Min(pageSize, maxPageSize);
        return new PageRequest(p, size);
    }
}

/// <summary>仓储分页结果：当前页 + 符合条件的总数。</summary>
public sealed class PagedList<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Total { get; init; }

    public static PagedList<T> Create(IReadOnlyList<T> items, int total)
        => new() { Items = items, Total = total };
}
