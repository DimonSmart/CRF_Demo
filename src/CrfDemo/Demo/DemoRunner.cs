using CrfDemo.ConsoleUi;
using CrfDemo.Inference;
using CrfDemo.Parsing;

namespace CrfDemo.Demo;

public sealed class DemoRunner
{
    private readonly ConsoleRenderer _renderer = new();
    private readonly PharmaLineAssembler _assembler = new();

    public void Run(TrainedSequenceLabeler model)
    {
        var predictor = new CrfPredictor(model);
        foreach (var scenario in Scenarios())
        {
            Console.WriteLine();
            Console.WriteLine($"=== {scenario.Name} ===");
            var tokens = predictor.Predict(scenario.Text);
            var parsed = _assembler.Assemble(scenario.Text, tokens);
            _renderer.RenderParseResult(parsed, verbose: false);
        }
    }

    private static IReadOnlyList<DemoScenario> Scenarios() =>
    [
        new("Simple", "CITALOPRAM 20MG 28 COMPRIMIDOS EFG"),
        new("Brand or manufacturer remains O", "CITALOPRAM NORMON 20MG 28 COMPRIMIDOS EFG"),
        new("Compact strength", "IBUPROFENO 600MG 40 COMPRIMIDOS"),
        new("Decimal strength", "DUTASTERIDA 0,5 mg 30 CAPSULAS"),
        new("Combined strength", "LOSARTAN HIDROCLOROTIAZIDA 20MG/12,5MG 28 COMPRIMIDOS"),
        new("Likely error case", "OXYCONTIN 20MG 28 COMPRIMIDOS LIBERACION MODIFIC"),
        new("Regulatory marker", "PARACETAMOL 1 g 40 COMPRIMIDOS EFG")
    ];
}
