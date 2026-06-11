using System.Text.RegularExpressions;

namespace CrfDemo.Tokenization;

public sealed class PharmaTokenizer
{
    private static readonly Regex DecimalPattern = new(@"\d+[,\.]\d+", RegexOptions.Compiled);
    private static readonly Regex UnitRatioPattern = new(
        @"\b(?:mg|mcg|µg|g|ml|UI|U|mEq|mmol|microgramos?|MICROGRAMOS?)\/(?:ml|g|dl|L|h|hora)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CompactStrengthPattern = new(
        @"(\d+(?:[,\.]\d+)?)(mg|mcg|µg|g|ml|UI|U|mEq|mmol|microgramos?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public TokenizedLine Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new TokenizedLine(text, Array.Empty<TokenizedToken>());

        var expanded = ExpandCompactStrengths(text);
        var rawTokens = SplitIntoRawTokens(expanded.Text);
        var tokens = MapToSourceTokens(rawTokens, expanded.OffsetMap, text);
        return new TokenizedLine(text, tokens);
    }

    private static (string Text, int[] OffsetMap) ExpandCompactStrengths(string original)
    {
        var builder = new System.Text.StringBuilder();
        var offsetMap = new List<int>();
        var i = 0;

        while (i < original.Length)
        {
            var match = CompactStrengthPattern.Match(original, i);
            if (match.Success && match.Index == i)
            {
                var number = match.Groups[1].Value;
                var unit = match.Groups[2].Value;
                foreach (var _ in number)
                {
                    builder.Append(original[i]);
                    offsetMap.Add(i++);
                }

                builder.Append(' ');
                offsetMap.Add(i);

                foreach (var _ in unit)
                {
                    builder.Append(original[i]);
                    offsetMap.Add(i++);
                }

                continue;
            }

            builder.Append(original[i]);
            offsetMap.Add(i);
            i++;
        }

        return (builder.ToString(), offsetMap.ToArray());
    }

    private static List<(string Text, int Start, int End)> SplitIntoRawTokens(string text)
    {
        var tokens = new List<(string, int, int)>();
        var i = 0;

        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
                continue;
            }

            var unitMatch = UnitRatioPattern.Match(text, i);
            if (unitMatch.Success && unitMatch.Index == i)
            {
                tokens.Add((unitMatch.Value, i, i + unitMatch.Length));
                i += unitMatch.Length;
                continue;
            }

            var decimalMatch = DecimalPattern.Match(text, i);
            if (decimalMatch.Success && decimalMatch.Index == i)
            {
                tokens.Add((decimalMatch.Value, i, i + decimalMatch.Length));
                i += decimalMatch.Length;
                continue;
            }

            var c = text[i];
            if (c is ',' or '.' or ';' or ':' or '(' or ')' or '+' or '/' or '-')
            {
                tokens.Add((c.ToString(), i, i + 1));
                i++;
                continue;
            }

            var start = i;
            while (i < text.Length
                   && !char.IsWhiteSpace(text[i])
                   && text[i] is not (',' or '.' or ';' or ':' or '(' or ')' or '+' or '/' or '-'))
            {
                i++;
            }

            if (i > start)
                tokens.Add((text[start..i], start, i));
        }

        return tokens;
    }

    private static IReadOnlyList<TokenizedToken> MapToSourceTokens(
        IReadOnlyList<(string Text, int Start, int End)> rawTokens,
        int[] offsetMap,
        string originalText)
    {
        var result = new List<TokenizedToken>(rawTokens.Count);

        for (var index = 0; index < rawTokens.Count; index++)
        {
            var raw = rawTokens[index];
            var originalStart = raw.Start < offsetMap.Length ? offsetMap[raw.Start] : raw.Start;
            var originalEnd = raw.End - 1 < offsetMap.Length ? offsetMap[raw.End - 1] + 1 : raw.End;
            var text = originalStart < originalEnd && originalEnd <= originalText.Length
                ? originalText[originalStart..originalEnd]
                : raw.Text;

            result.Add(new TokenizedToken(index, text, originalStart, originalEnd));
        }

        return result;
    }
}
