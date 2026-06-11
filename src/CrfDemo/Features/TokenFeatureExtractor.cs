using CrfDemo.Tokenization;

namespace CrfDemo.Features;

public sealed class TokenFeatureExtractor
{
    public IReadOnlyList<TokenFeatures> Extract(IReadOnlyList<string> tokens)
    {
        var tokenized = tokens.Select((t, i) => new TokenizedToken(i, t, 0, 0)).ToArray();
        return Extract(tokenized);
    }

    public IReadOnlyList<TokenFeatures> Extract(IReadOnlyList<TokenizedToken> tokens)
    {
        var result = new List<TokenFeatures>(tokens.Count);

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i].Text;
            var lower = token.ToLowerInvariant();
            var features = new List<string>
            {
                "bias",
                $"token={token}",
                $"lower={lower}",
                $"shape={TokenShape.Build(token)}",
                $"isUpper={IsUpper(token)}",
                $"isLower={IsLower(token)}",
                $"isTitle={IsTitle(token)}",
                $"containsDigit={token.Any(char.IsDigit)}",
                $"isNumeric={IsNumeric(token)}",
                $"isInteger={IsInteger(token)}",
                $"hasCommaDecimal={HasCommaDecimal(token)}",
                $"hasSlash={token.Contains('/')}",
                $"hasHyphen={token.Contains('-')}",
                $"isPunctuation={token.All(char.IsPunctuation)}",
                $"prefix1={Prefix(token, 1)}",
                $"prefix2={Prefix(token, 2)}",
                $"prefix3={Prefix(token, 3)}",
                $"suffix1={Suffix(token, 1)}",
                $"suffix2={Suffix(token, 2)}",
                $"suffix3={Suffix(token, 3)}",
                $"length={LengthBucket(token)}",
                $"position={PositionBucket(i, tokens.Count)}"
            };

            AddNeighbor(features, "prev", tokens, i - 1);
            AddNeighbor(features, "next", tokens, i + 1);
            result.Add(new TokenFeatures(token, features));
        }

        return result;
    }

    private static void AddNeighbor(List<string> features, string name, IReadOnlyList<TokenizedToken> tokens, int index)
    {
        if (index < 0)
        {
            features.Add($"{name}=BOS");
            return;
        }

        if (index >= tokens.Count)
        {
            features.Add($"{name}=EOS");
            return;
        }

        var token = tokens[index].Text;
        features.Add($"{name}Lower={token.ToLowerInvariant()}");
        features.Add($"{name}Shape={TokenShape.Build(token)}");
    }

    private static bool IsUpper(string token) => token.Any(char.IsLetter) && token.Where(char.IsLetter).All(char.IsUpper);
    private static bool IsLower(string token) => token.Any(char.IsLetter) && token.Where(char.IsLetter).All(char.IsLower);
    private static bool IsTitle(string token) => token.Length > 0 && char.IsUpper(token[0]) && token.Skip(1).All(c => !char.IsLetter(c) || char.IsLower(c));
    private static bool IsNumeric(string token) => token.All(c => char.IsDigit(c) || c is ',' or '.');
    private static bool IsInteger(string token) => token.Length > 0 && token.All(char.IsDigit);
    private static bool HasCommaDecimal(string token) => token.Any(char.IsDigit) && token.Contains(',');
    private static string Prefix(string token, int length) => token.Length <= length ? token.ToLowerInvariant() : token[..length].ToLowerInvariant();
    private static string Suffix(string token, int length) => token.Length <= length ? token.ToLowerInvariant() : token[^length..].ToLowerInvariant();

    private static string LengthBucket(string token) => token.Length switch
    {
        <= 1 => "1",
        <= 3 => "2-3",
        <= 6 => "4-6",
        <= 10 => "7-10",
        _ => "11+"
    };

    private static string PositionBucket(int index, int count)
    {
        if (index == 0) return "first";
        if (index == count - 1) return "last";
        if (index < count / 3) return "early";
        if (index > count * 2 / 3) return "late";
        return "middle";
    }
}
