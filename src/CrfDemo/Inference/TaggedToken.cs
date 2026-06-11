namespace CrfDemo.Inference;

public sealed record TaggedToken(int Index, string Token, string Label)
{
    public string Entity
    {
        get
        {
            if (Label == "O")
                return "";

            var index = Label.IndexOf('-', StringComparison.Ordinal);
            return index < 0 ? "" : Label[(index + 1)..];
        }
    }
}
