using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using System.Text.RegularExpressions;

namespace ACI.Application.Services;

public class SpamCheckService : ISpamCheckService
{
    private static readonly HashSet<string> SpamWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "free", "winner", "won", "urgent", "act now", "limited time", "exclusive deal",
        "congratulations", "claim", "prize", "lottery", "million", "billion",
        "click here", "click below", "unsubscribe", "opt-out", "remove me",
        "100%", "guaranteed", "risk-free", "no obligation", "no cost",
        "credit card", "wire transfer", "bitcoin", "cryptocurrency",
        "make money", "earn money", "extra income", "work from home",
        "viagra", "pharmacy", "medication", "pills", "weight loss",
        "nigerian", "prince", "inheritance", "beneficiary",
        "password", "verify your account", "suspended", "locked",
        "dear friend", "dear customer", "dear sir/madam"
    };
    
    public Task<SpamCheckResponse> CheckAsync(SpamCheckRequest request, CancellationToken ct = default)
    {
        var issues = new List<SpamIssueDto>();
        var score = 0.0;
        
        var combined = $"{request.Subject} {request.Body}";
        
        // Check for spam words
        var foundSpamWords = SpamWords.Where(w => combined.Contains(w, StringComparison.OrdinalIgnoreCase)).ToList();
        if (foundSpamWords.Any())
        {
            score += foundSpamWords.Count * 10;
            issues.Add(new SpamIssueDto(
                "spam_words",
                $"Contains {foundSpamWords.Count} spam trigger words: {string.Join(", ", foundSpamWords.Take(5))}",
                foundSpamWords.Count > 3 ? "high" : "medium"
            ));
        }
        
        // Check for excessive caps
        var capsRatio = GetCapsRatio(combined);
        if (capsRatio > 0.3)
        {
            score += 20;
            issues.Add(new SpamIssueDto(
                "caps",
                $"Excessive use of capital letters ({Math.Round(capsRatio * 100)}%)",
                capsRatio > 0.5 ? "high" : "medium"
            ));
        }
        
        // Check for excessive punctuation
        var exclamationCount = combined.Count(c => c == '!');
        if (exclamationCount > 3)
        {
            score += exclamationCount * 3;
            issues.Add(new SpamIssueDto(
                "punctuation",
                $"Too many exclamation marks ({exclamationCount})",
                exclamationCount > 5 ? "high" : "low"
            ));
        }
        
        // Check for too many links
        var linkCount = Regex.Matches(combined, @"https?://", RegexOptions.IgnoreCase).Count;
        if (linkCount > 2)
        {
            score += linkCount * 5;
            issues.Add(new SpamIssueDto(
                "links",
                $"Too many URLs ({linkCount})",
                linkCount > 4 ? "high" : "medium"
            ));
        }
        
        // Check for all caps subject
        if (!string.IsNullOrWhiteSpace(request.Subject) && 
            request.Subject.Length > 5 && 
            request.Subject.All(c => !char.IsLetter(c) || char.IsUpper(c)))
        {
            score += 15;
            issues.Add(new SpamIssueDto(
                "caps_subject",
                "Subject line is all caps",
                "high"
            ));
        }
        
        // Check for suspicious phrases
        if (Regex.IsMatch(combined, @"\$\d+[,\d]*", RegexOptions.IgnoreCase))
        {
            score += 10;
            issues.Add(new SpamIssueDto(
                "money",
                "Contains monetary amounts which can trigger spam filters",
                "low"
            ));
        }
        
        // Check for missing personalization
        if (!combined.Contains("{{") && !combined.Contains("{name}") && !Regex.IsMatch(combined, @"\bHi\s+\w+"))
        {
            if (combined.Contains("Dear") || combined.Contains("Hello"))
            {
                issues.Add(new SpamIssueDto(
                    "generic",
                    "Consider adding personalization (recipient name)",
                    "low"
                ));
            }
        }
        
        // Clamp score
        score = Math.Min(100, Math.Max(0, score));
        
        var rating = score switch
        {
            < 20 => "Good",
            < 50 => "Warning",
            _ => "Spam Risk"
        };
        
        return Task.FromResult(new SpamCheckResponse(
            Math.Round(score, 1),
            rating,
            issues
        ));
    }
    
    private static double GetCapsRatio(string text)
    {
        var letters = text.Where(char.IsLetter).ToList();
        if (letters.Count == 0) return 0;
        return (double)letters.Count(char.IsUpper) / letters.Count;
    }
}
