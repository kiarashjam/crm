using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>The deals board, worked out by the server: columns, their cards a page at a time, and their figures.</summary>
public interface IDealBoardService
{
    /// <summary>A pipeline's columns with the first page of cards in each, and the figures for the columns and the pipeline.</summary>
    Task<Result<DealBoardDto>> GetBoardAsync(Guid userId, Guid? organizationId, DealBoardQuery query, CancellationToken ct = default);

    /// <summary>The next cards of one column, after the first <paramref name="skip"/>, with the same filters and order.</summary>
    Task<Result<DealBoardColumnPageDto>> GetColumnAsync(Guid userId, Guid? organizationId, DealBoardQuery query, string column, int skip, CancellationToken ct = default);
}
