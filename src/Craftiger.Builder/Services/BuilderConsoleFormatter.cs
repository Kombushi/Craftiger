using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Craftiger.Builder.Services;

/// <summary>Prints bare messages, because the builder's log is its user-facing progress report.</summary>
public sealed class BuilderConsoleFormatter() : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "builder";

    public override void Write<TState>(
        in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message is null)
        {
            return;
        }

        if (logEntry.LogLevel >= LogLevel.Warning)
        {
            textWriter.Write($"{logEntry.LogLevel switch
            {
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                _ => "crit"
            }}: ");
        }

        textWriter.WriteLine(message);
        if (logEntry.Exception is { } exception)
        {
            textWriter.WriteLine(exception);
        }
    }
}
