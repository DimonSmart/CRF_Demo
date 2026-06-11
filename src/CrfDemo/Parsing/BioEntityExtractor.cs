using CrfDemo.Inference;

namespace CrfDemo.Parsing;

public sealed class BioEntityExtractionResult
{
    public IReadOnlyList<BioEntity> Entities { get; init; } = Array.Empty<BioEntity>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class BioEntityExtractor
{
    public BioEntityExtractionResult Extract(IReadOnlyList<TaggedToken> tokens)
    {
        var entities = new List<BioEntity>();
        var warnings = new List<string>();
        string? currentType = null;
        var currentTokens = new List<string>();

        foreach (var token in tokens)
        {
            if (token.Label == "O")
            {
                Flush();
                continue;
            }

            var prefix = token.Label.Length >= 2 ? token.Label[..2] : "";
            var type = token.Entity;
            if (prefix == "B-")
            {
                Flush();
                currentType = type;
                currentTokens.Add(token.Token);
                continue;
            }

            if (prefix == "I-")
            {
                if (currentType != type)
                {
                    warnings.Add($"Token {token.Index} has invalid BIO transition: {token.Label}.");
                    Flush();
                    currentType = type;
                }

                currentTokens.Add(token.Token);
            }
        }

        Flush();
        return new BioEntityExtractionResult { Entities = entities, Warnings = warnings };

        void Flush()
        {
            if (currentType is null || currentTokens.Count == 0)
                return;

            entities.Add(new BioEntity(currentType, currentTokens.ToArray()));
            currentType = null;
            currentTokens.Clear();
        }
    }
}
