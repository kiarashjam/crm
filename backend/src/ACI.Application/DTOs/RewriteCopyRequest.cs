using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to rewrite existing copy.
/// </summary>
public record RewriteCopyRequest
{
    /// <summary>
    /// Original copy to rewrite.
    /// </summary>
    [Required(ErrorMessage = "OriginalCopy is required")]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "OriginalCopy must be between 1 and 10000 characters")]
    public required string OriginalCopy { get; init; }

    /// <summary>
    /// Adjustment instructions.
    /// </summary>
    [Required(ErrorMessage = "Adjustment is required")]
    [StringLength(500, ErrorMessage = "Adjustment cannot exceed 500 characters")]
    public required string Adjustment { get; init; }
}

/// <summary>
/// Response containing rewritten copy.
/// </summary>
public record RewriteCopyResponse(string Copy);
