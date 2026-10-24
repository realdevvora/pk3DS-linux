using System;
using pk3DS.Core;

namespace pk3DS.Cli;

/// <summary>
/// Writes log lines to standard error, keeping standard output free for command results.
/// </summary>
public sealed class ConsoleProgressSink : IProgressSink
{
    public void Log(string message) => Console.Error.WriteLine(message);

    public void Reset(int max) { }

    public void Step() { }

    public void Set(int value) { }
}
