namespace ACI.Application.Common;

/// <summary>
/// The record changed between this request reading it and saving it, so the save was
/// refused rather than written over the other change.
/// </summary>
/// <remarks>
/// Thrown by a repository, which is the layer that can tell. A service that has a
/// better sentence for its case catches it and returns an error; anywhere else the
/// global handler answers 409, so a lost race never reads as a success.
/// </remarks>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("Someone else changed this record at the same moment.")
    {
    }
}
