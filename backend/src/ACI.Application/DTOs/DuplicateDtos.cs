namespace ACI.Application.DTOs;

/// <summary>Records that look like one, and why: "Same phone · Same name".</summary>
/// <param name="Key">The records' ids, sorted and joined by "|", as the browser keys a group.</param>
public record DuplicateGroupDto<T>(string Key, string Reason, IReadOnlyList<T> Records);

/// <summary>A group the team's "not the same" decisions hid, by its records' ids, to offer it again.</summary>
public record NotSameGroupDto(string Key, IReadOnlyList<Guid> RecordIds);

/// <summary>A workspace's duplicates of one kind, found on the server.</summary>
/// <param name="Groups">Still to deal with, most records first.</param>
/// <param name="NotSame">Hidden because someone said they are not the same.</param>
public record DuplicateScanDto<T>(IReadOnlyList<DuplicateGroupDto<T>> Groups, IReadOnlyList<NotSameGroupDto> NotSame);

/// <summary>Records someone says are not the same: every pair among them is kept apart.</summary>
public record DuplicateDecisionRequest
{
    public IReadOnlyList<Guid> RecordIds { get; init; } = [];
}

/// <summary>Two records someone said are not the same.</summary>
public record DuplicatePairDto(Guid RecordIdA, Guid RecordIdB, DateTime DecidedAtUtc);
