using FluentAssertions;
using PharmaCorpusAnnotator.Core.Tokenization;

namespace PharmaCorpusAnnotator.Tests;

public class TokenizerTests
{
    private readonly PharmaTokenizer _sut = new();

    [Fact]
    public void CompactStrength_600mg_SplitsToTwoTokens()
    {
        var tokens = _sut.Tokenize("Ibuprofeno 600mg comp");
        var texts = tokens.Select(t => t.Text).ToList();
        texts.Should().Contain("600");
        texts.Should().Contain("mg");
        texts.IndexOf("600").Should().BeLessThan(texts.IndexOf("mg"));
    }

    [Fact]
    public void CompactStrength_25MG_SplitsToTwoTokens()
    {
        var tokens = _sut.Tokenize("something 25MG tablet");
        var texts = tokens.Select(t => t.Text).ToList();
        texts.Should().Contain("25");
        texts.Should().Contain("MG");
    }

    [Fact]
    public void UnitRatio_50MgMl_KeepsAsOneToken()
    {
        var tokens = _sut.Tokenize("captopril 4 mg/ml suspension");
        var texts = tokens.Select(t => t.Text).ToList();
        texts.Should().Contain("mg/ml");
        texts.Should().NotContain("/");
    }

    [Fact]
    public void DecimalComma_2Comma95_RemainsSingleToken()
    {
        var tokens = _sut.Tokenize("price 2,95 EUR");
        var texts = tokens.Select(t => t.Text).ToList();
        texts.Should().Contain("2,95");
    }

    [Fact]
    public void DecimalDot_3Dot10_RemainsSingleToken()
    {
        var tokens = _sut.Tokenize("price 3.10 EUR");
        var texts = tokens.Select(t => t.Text).ToList();
        texts.Should().Contain("3.10");
    }

    [Fact]
    public void Comma_AfterEfg_IsSeperateToken()
    {
        var tokens = _sut.Tokenize("EFG, 56 comprimidos");
        var texts = tokens.Select(t => t.Text).ToList();
        texts.Should().ContainInOrder("EFG", ",", "56", "comprimidos");
    }

    [Fact]
    public void Offsets_AreCorrect_ForSimpleInput()
    {
        var text = "captopril 4 mg/ml";
        var tokens = _sut.Tokenize(text);
        foreach (var t in tokens)
        {
            text[t.StartOffset..t.EndOffset].Should().Be(t.Text,
                because: $"token '{t.Text}' offset [{t.StartOffset}..{t.EndOffset}] should match original text");
        }
    }

    [Fact]
    public void Offsets_AreCorrect_AfterCompactExpansion()
    {
        var text = "Ibuprofeno 600mg comp";
        var tokens = _sut.Tokenize(text);
        // Tokens that are pure substrings of the original should have correct offsets
        var ibuprofeno = tokens.First(t => t.Text == "Ibuprofeno");
        text[ibuprofeno.StartOffset..ibuprofeno.EndOffset].Should().Be("Ibuprofeno");
    }

    [Fact]
    public void Indexes_AreSequentialFromZero()
    {
        var tokens = _sut.Tokenize("aceite salicilado 50 mg/ml solucion cutanea 100 ml 1 frasco");
        for (int i = 0; i < tokens.Count; i++)
            tokens[i].Index.Should().Be(i);
    }

    [Fact]
    public void CaptoprilExample_ProducesExpectedTokens()
    {
        var tokens = _sut.Tokenize("captopril 4 mg/ml suspension oral 100 ml 1 frasco");
        var texts = tokens.Select(t => t.Text).ToList();
        texts.Should().ContainInOrder("captopril", "4", "mg/ml", "suspension", "oral", "100", "ml", "1", "frasco");
    }

    [Fact]
    public void DonezilExample_ProducesExpectedTokens()
    {
        var tokens = _sut.Tokenize("DONEPEZILO NORMON 5 mg COMPRIMIDOS BUCODISPERSABLES EFG, 56 comprimidos");
        var texts = tokens.Select(t => t.Text).ToList();
        texts.Should().ContainInOrder(
            "DONEPEZILO", "NORMON", "5", "mg", "COMPRIMIDOS", "BUCODISPERSABLES", "EFG", ",", "56", "comprimidos");
    }

    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        _sut.Tokenize("").Should().BeEmpty();
    }
}
