namespace CrfDemo.Parsing;

public sealed record BioEntity(string Type, IReadOnlyList<string> Tokens)
{
    public string Text => string.Join(' ', Tokens);
}
