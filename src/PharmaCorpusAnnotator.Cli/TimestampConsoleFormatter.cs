using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace PharmaCorpusAnnotator.Cli;

internal sealed class TimestampConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "timestamp";

    public TimestampConsoleFormatter()
        : base(FormatterName)
    {
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message) && logEntry.Exception is null)
            return;

        textWriter.Write(DateTime.Now.ToString("HH:mm:ss ", CultureInfo.InvariantCulture));
        textWriter.Write(GetLevelName(logEntry.LogLevel));
        textWriter.Write(": ");
        textWriter.Write(message);

        if (logEntry.Exception is not null)
        {
            textWriter.WriteLine();
            textWriter.Write(logEntry.Exception);
        }

        textWriter.WriteLine();
    }

    private static string GetLevelName(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            LogLevel.None => "none",
            _ => logLevel.ToString().ToLowerInvariant(),
        };
}
