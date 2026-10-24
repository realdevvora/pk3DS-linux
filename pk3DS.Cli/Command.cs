using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Cli;

/// <summary>
/// One independently runnable operation, addressed as <c>pk3ds &lt;group&gt; &lt;name&gt;</c>.
/// </summary>
/// <param name="Group">Command group, e.g. romfs</param>
/// <param name="Name">Operation within the group, e.g. extract</param>
/// <param name="Usage">Argument synopsis shown in help</param>
/// <param name="Summary">One-line description shown in help</param>
/// <param name="ValueOptions">Names of the <c>--option value</c> pairs this command accepts; every other <c>--name</c> is a flag</param>
/// <param name="Run">Executes the operation and returns true on success</param>
public sealed record Command(string Group, string Name, string Usage, string Summary, string[] ValueOptions, Func<CommandArgs, bool> Run);

/// <summary>
/// Positional arguments and <c>--flag</c> / <c>--option value</c> pairs for a single command invocation.
/// </summary>
public sealed class CommandArgs
{
    private readonly Dictionary<string, string> options = [];
    private readonly List<string> positional = [];

    public IReadOnlyList<string> Positional => positional;

    public CommandArgs(IEnumerable<string> args, ISet<string> valueOptions)
    {
        using var e = args.GetEnumerator();
        while (e.MoveNext())
        {
            string arg = e.Current;
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }
            string key = arg[2..];
            if (!valueOptions.Contains(key))
            {
                options[key] = string.Empty;
                continue;
            }
            if (!e.MoveNext())
                throw new UsageException($"Option --{key} requires a value.");
            options[key] = e.Current;
        }
    }

    public bool Has(string flag) => options.ContainsKey(flag);

    public string Get(string option) => options.GetValueOrDefault(option);

    public string Require(string option) =>
        Get(option) ?? throw new UsageException($"Missing required option --{option}.");

    public string[] Expect(int count)
    {
        if (positional.Count != count)
            throw new UsageException($"Expected {count} argument(s), got {positional.Count}.");
        return [.. positional];
    }

    public string[] ExpectAtLeast(int count)
    {
        if (positional.Count < count)
            throw new UsageException($"Expected at least {count} argument(s), got {positional.Count}.");
        return [.. positional];
    }

    public void RejectUnknown(params string[] known)
    {
        string unknown = options.Keys.FirstOrDefault(k => !known.Contains(k));
        if (unknown != null)
            throw new UsageException($"Unknown option --{unknown}.");
    }
}

public sealed class UsageException(string message) : Exception(message);
