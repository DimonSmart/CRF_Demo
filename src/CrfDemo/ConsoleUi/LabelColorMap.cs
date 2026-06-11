namespace CrfDemo.ConsoleUi;

public static class LabelColorMap
{
    public static ConsoleColor ColorForEntity(string entity) => entity switch
    {
        "ACTIVE_INGREDIENT" => ConsoleColor.Green,
        "STRENGTH" => ConsoleColor.Cyan,
        "DOSE_FORM" => ConsoleColor.Yellow,
        "ROUTE" => ConsoleColor.Magenta,
        "PACKAGE_VOLUME" => ConsoleColor.Blue,
        "PACKAGE_QUANTITY" => ConsoleColor.DarkCyan,
        "PACKAGE_UNIT" => ConsoleColor.DarkYellow,
        "REGULATORY_MARKER" => ConsoleColor.Red,
        _ => ConsoleColor.Gray
    };
}
