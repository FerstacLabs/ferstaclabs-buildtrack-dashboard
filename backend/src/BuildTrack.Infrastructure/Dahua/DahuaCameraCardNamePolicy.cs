using System.Globalization;
using System.Text;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaCameraCardNamePolicy
{
    private static readonly HashSet<string> KnownBadCandidates = new(StringComparer.OrdinalIgnoreCase)
    {
        "bx",
        "fj",
        "pp",
        "cj",
        "kf",
        "p1x",
        "j4myh"
    };

    public static bool TryValidate(string? value, int minLength, out string displayName, out string normalizedName, out string? reason)
    {
        displayName = NormalizeDisplayName(value);
        normalizedName = NormalizeForMatching(displayName);
        reason = null;

        var effectiveMinLength = Math.Clamp(minLength, 2, 64);
        if (displayName.Length < effectiveMinLength)
        {
            reason = "card name is shorter than configured minimum";
            return false;
        }

        if (displayName.Length > 64)
        {
            reason = "card name is longer than 64 characters";
            return false;
        }

        if (displayName.Any(char.IsControl))
        {
            reason = "card name contains control characters";
            return false;
        }

        if (KnownBadCandidates.Contains(normalizedName))
        {
            reason = "card name matches known corrupted candidate";
            return false;
        }

        var letterCount = 0;
        var digitCount = 0;
        var symbolCount = 0;
        foreach (var ch in displayName)
        {
            if (char.IsLetter(ch))
            {
                letterCount++;
                continue;
            }

            if (char.IsDigit(ch))
            {
                digitCount++;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '-' or '_' or '\'')
            {
                continue;
            }

            symbolCount++;
        }

        if (letterCount == 0)
        {
            reason = "card name does not contain a letter";
            return false;
        }

        if (symbolCount > 0)
        {
            reason = "card name contains unsupported symbols";
            return false;
        }

        if (digitCount > 0 && normalizedName.Length <= 6)
        {
            reason = "short mixed letter/digit card name looks like a corrupted binary candidate";
            return false;
        }

        var asciiLetters = normalizedName.Count(ch => ch is >= 'a' and <= 'z');
        var vowels = normalizedName.Count(ch => "aeiouaeiou".Contains(ch, StringComparison.OrdinalIgnoreCase));
        if (normalizedName.Length <= 3 && asciiLetters == normalizedName.Length && vowels == 0)
        {
            reason = "short consonant-only card name looks suspicious";
            return false;
        }

        return true;
    }

    public static string NormalizeDisplayName(string? value) =>
        string.Join(' ', (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static string NormalizeForMatching(string? value)
    {
        var displayName = NormalizeDisplayName(value).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(displayName.Length);
        foreach (var ch in displayName)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
