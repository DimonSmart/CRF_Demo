namespace CrfDemo.Tokenization;

public sealed record TokenizedToken(int Index, string Text, int Start, int End);

public sealed record TokenizedLine(string Text, IReadOnlyList<TokenizedToken> Tokens);
