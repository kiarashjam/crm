namespace ACI.Application.DTOs;

/// <summary>A workspace's tag, with how many records carry it.</summary>
public record TagDto(Guid Id, string Name, string Color, int Uses);

public record CreateTagRequest(string Name, string? Color);

/// <summary>A new name or colour; what is left out stays.</summary>
public record UpdateTagRequest(string? Name, string? Color);

/// <summary>The tags one record carries, all of them: tags left out are taken off.</summary>
public record SetRecordTagsRequest(string EntityType, Guid RecordId, IReadOnlyList<Guid> TagIds);

/// <summary>The records of one kind a list shows, whose tags it wants.</summary>
public record RecordTagsBatchRequest(string EntityType, IReadOnlyList<Guid> RecordIds);

/// <summary>Tags to put on, and to take off, many records of one kind at once.</summary>
public record BulkTagRequest(string EntityType, IReadOnlyList<Guid> RecordIds, IReadOnlyList<Guid>? Add, IReadOnlyList<Guid>? Remove);

/// <param name="Records">The records changed: those the caller can see.</param>
/// <param name="Added">Tags put on records that did not have them.</param>
/// <param name="Removed">Tags taken off records that had them.</param>
/// <param name="Skipped">Records asked for that are not the caller's to see.</param>
public record BulkTagResult(int Records, int Added, int Removed, int Skipped);
