using System.Text.RegularExpressions;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Tokenization;

/// <summary>
/// Tokenizes pharmaceutical product line text.
/// Rules:
/// - Keep mg/ml, mg/g, mcg/ml as single tokens (slash-unit combinations)
/// - Split compact strength forms: 600mg -> 600, mg
/// - Keep decimal numbers (2,95 or 3.10) as single tokens
/// - Split standalone punctuation (comma, period) as separate tokens
/// - Slash between two words may split: AMOXICILINA/ACIDO -> AMOXICILINA, /, ACIDO
/// </summary>
public sealed class PharmaTokenizer
{
    // Matches: decimal number with comma (2,95) or dot (3.10), or plain integer
    private static readonly Regex DecimalPattern =
        new(@"\d+[,\.]\d+", RegexOptions.Compiled);

    // Matches concentration ratio units: mg/ml, mg/g, mcg/ml, UI/ml, etc.
    private static readonly Regex UnitRatioPattern =
        new(@"\b(?:mg|mcg|µg|g|ml|UI|U|mEq|mmol|microgramos?|MICROGRAMOS?)\/(?:ml|g|dl|L|h|hora)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Compact strength: digits immediately followed by unit letters (600mg, 25MG, 1g, 500mcg)
    private static readonly Regex CompactStrengthPattern =
        new(@"(\d+(?:[,\.]\d+)?)(mg|mcg|µg|g|ml|UI|U|mEq|mmol|microgramos?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<SourceToken> Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<SourceToken>();

        // First expand compact strengths (600mg -> 600 mg) in a work copy
        // We expand them in-place while tracking offset adjustments
        var expanded = ExpandCompactStrengths(text);
        var rawTokens = SplitIntoRawTokens(expanded.Text);
        return MapToSourceTokens(rawTokens, expanded.OffsetMap, text);
    }

    private (string Text, int[] OffsetMap) ExpandCompactStrengths(string original)
    {
        // Build expanded string and a mapping from expanded offset -> original offset
        var sb = new System.Text.StringBuilder();
        var offsetMap = new List<int>();
        int i = 0;

        while (i < original.Length)
        {
            // Try to match compact strength at position i
            var match = CompactStrengthPattern.Match(original, i);
            if (match.Success && match.Index == i)
            {
                string num = match.Groups[1].Value;
                string unit = match.Groups[2].Value;
                // Insert num
                foreach (char c in num)
                {
                    sb.Append(c);
                    offsetMap.Add(i++);
                }
                // Insert space separator (maps to the same position as start of unit)
                sb.Append(' ');
                offsetMap.Add(i);
                // Insert unit
                foreach (char c in unit)
                {
                    sb.Append(c);
                    offsetMap.Add(i++);
                }
            }
            else
            {
                sb.Append(original[i]);
                offsetMap.Add(i);
                i++;
            }
        }

        return (sb.ToString(), offsetMap.ToArray());
    }

    private List<(string text, int start, int end)> SplitIntoRawTokens(string text)
    {
        var tokens = new List<(string, int, int)>();
        int i = 0;
        int len = text.Length;

        while (i < len)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
                continue;
            }

            // Try unit ratio pattern (mg/ml, etc.) - keep as single token
            var unitMatch = UnitRatioPattern.Match(text, i);
            if (unitMatch.Success && unitMatch.Index == i)
            {
                tokens.Add((unitMatch.Value, unitMatch.Index, unitMatch.Index + unitMatch.Length));
                i = unitMatch.Index + unitMatch.Length;
                continue;
            }

            // Try decimal number (2,95 or 3.10)
            var decMatch = DecimalPattern.Match(text, i);
            if (decMatch.Success && decMatch.Index == i)
            {
                tokens.Add((decMatch.Value, decMatch.Index, decMatch.Index + decMatch.Length));
                i = decMatch.Index + decMatch.Length;
                continue;
            }

            char c = text[i];

            // Standalone punctuation
            if (c == ',' || c == '.' || c == ';' || c == ':' || c == '(' || c == ')')
            {
                tokens.Add((c.ToString(), i, i + 1));
                i++;
                continue;
            }

            // Slash: check if it separates two word tokens or is part of a unit ratio
            if (c == '/')
            {
                tokens.Add((c.ToString(), i, i + 1));
                i++;
                continue;
            }

            // Word/number token: read until whitespace or standalone punctuation or slash
            int start = i;
            while (i < len && !char.IsWhiteSpace(text[i]) &&
                   text[i] != ',' && text[i] != ';' && text[i] != '(' && text[i] != ')')
            {
                // Check if next chars form unit ratio (mg/ml) — stop before if we hit '/'
                // but only if it's not part of a unit ratio already matched above
                if (text[i] == '/')
                {
                    // Check for unit ratio starting here
                    var ur = UnitRatioPattern.Match(text, start);
                    if (ur.Success && ur.Index == start && ur.Length > (i - start))
                    {
                        // The whole token including '/' is a unit ratio, handled earlier;
                        // but since we started a word, just break here and let next iteration handle slash
                        break;
                    }
                    break;
                }
                if (text[i] == '.')
                {
                    // Check if it's a decimal (already handled above, so this is trailing dot)
                    // Keep dots that are part of abbreviations like "comp." as standalone
                    break;
                }
                i++;
            }

            if (i > start)
                tokens.Add((text[start..i], start, i));
        }

        return tokens;
    }

    private IReadOnlyList<SourceToken> MapToSourceTokens(
        List<(string text, int start, int end)> rawTokens,
        int[] offsetMap,
        string originalText)
    {
        var result = new List<SourceToken>(rawTokens.Count);

        for (int idx = 0; idx < rawTokens.Count; idx++)
        {
            var (rawText, rawStart, rawEnd) = rawTokens[idx];

            // Map back to original offsets
            int origStart = rawStart < offsetMap.Length ? offsetMap[rawStart] : rawStart;
            int origEnd = (rawEnd - 1) < offsetMap.Length
                ? offsetMap[rawEnd - 1] + 1
                : rawEnd;

            // The actual token text is from the original string using the mapped offsets
            string tokenText = origStart < origEnd && origEnd <= originalText.Length
                ? originalText[origStart..origEnd]
                : rawText;

            result.Add(new SourceToken(idx, tokenText, origStart, origEnd));
        }

        return result;
    }
}
