namespace ACI.Application.DTOs;

/// <summary>
/// Generic paginated result wrapper for list endpoints.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
public record PagedResult<T>
{
    /// <summary>
    /// The items for the current page.
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    
    /// <summary>
    /// Total number of items matching the query (across all pages).
    /// </summary>
    public int TotalCount { get; init; }
    
    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; init; }
    
    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }
    
    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    
    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;
    
    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
    
    /// <summary>
    /// Creates a paged result from a list of items.
    /// </summary>
    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
    
    /// <summary>
    /// Creates an empty paged result.
    /// </summary>
    public static PagedResult<T> Empty(int page = 1, int pageSize = 20)
    {
        return new PagedResult<T>
        {
            Items = Array.Empty<T>(),
            TotalCount = 0,
            Page = page,
            PageSize = pageSize
        };
    }
}

/// <summary>
/// Query parameters for paginated requests.
/// </summary>
public record PaginationParams
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;
    
    private int _page = 1;
    private int _pageSize = DefaultPageSize;
    
    /// <summary>
    /// Page number (1-based). Defaults to 1.
    /// </summary>
    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }
    
    /// <summary>
    /// Number of items per page. Defaults to 20, max 100.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? DefaultPageSize : (value > MaxPageSize ? MaxPageSize : value);
    }
    
    /// <summary>
    /// Search query string (optional).
    /// </summary>
    public string? Search { get; init; }
    
    /// <summary>
    /// Number of items to skip.
    /// </summary>
    public int Skip => (Page - 1) * PageSize;
    
    /// <summary>
    /// Number of items to take.
    /// </summary>
    public int Take => PageSize;
}
