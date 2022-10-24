using Microsoft.Extensions.Logging;
using System;

namespace Unlimitedinf.Tom
{
    internal enum Verbosity
    {
        Dbg,
        Info,
        Warn,
        Err
    }

    internal sealed class CLILogger<T> : CLILogger, ILogger<T>
    {
        public CLILogger(Verbosity verbosity)
            : base(verbosity)
        { }
    }

    internal class CLILogger : ILogger
    {
        private readonly LogLevel minimumLogLevel;

        public CLILogger(Verbosity verbosity)
        {
            this.minimumLogLevel = verbosity switch
            {
                Verbosity.Info => LogLevel.Information,
                Verbosity.Warn => LogLevel.Warning,
                Verbosity.Err => LogLevel.Error,
                _ => LogLevel.Trace,
            };
        }

        public IDisposable BeginScope<TState>(TState state) => throw new NotImplementedException();

        public bool IsEnabled(LogLevel logLevel) => logLevel >= this.minimumLogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel < this.minimumLogLevel)
            {
                return;
            }

            // If an exception is passed in here, it is currently ignored by the LoggerExtensions default formatter
            // Line 509: https://github.com/aspnet/Logging/blob/dev/src/Microsoft.Extensions.Logging.Abstractions/LoggerExtensions.cs
            // Because people may expect it to be logged, go ahead and log it here as an error (but as a concatenation to the current message)
            string message = formatter(state, exception);
            if (exception != null)
            {
                message += Environment.NewLine + exception.ToString();
            }

            string prefix = logLevel switch
            {
                LogLevel.Trace => "TRAC",
                LogLevel.Debug => "DEBG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERRO",
                LogLevel.Critical => "CRIT",
                _ => logLevel.ToString().ToUpper()
            };
            message = $"[{prefix}] {message}";

            if (logLevel >= LogLevel.Warning)
            {
                Console.Error.WriteLine(message);
            }
            else
            {
                Console.Out.WriteLine(message);
            }
        }
    }
}
