using System;
using System.Linq;

namespace pk3DS.Cli;

public static class Program
{
    private const int Success = 0;
    private const int Failure = 1;
    private const int Usage = 2;

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return args.Length == 0 ? Usage : Success;
        }
        if (args.Length < 2)
        {
            Console.Error.WriteLine($"Missing command for group '{args[0]}'.");
            PrintHelp(args[0]);
            return Usage;
        }

        var command = Commands.All.FirstOrDefault(c => c.Group == args[0] && c.Name == args[1]);
        if (command == null)
        {
            Console.Error.WriteLine($"Unknown command '{args[0]} {args[1]}'.");
            PrintHelp();
            return Usage;
        }
        if (args.Skip(2).Any(a => a is "-h" or "--help"))
        {
            PrintCommandHelp(command);
            return Success;
        }

        try
        {
            var parsed = new CommandArgs(args.Skip(2), command.ValueOptions.ToHashSet());
            if (command.Run(parsed))
                return Success;
            Console.Error.WriteLine($"{command.Group} {command.Name} failed.");
            return Failure;
        }
        catch (UsageException e)
        {
            Console.Error.WriteLine(e.Message);
            PrintCommandHelp(command);
            return Usage;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"{command.Group} {command.Name} failed: {e.Message}");
            return Failure;
        }
    }

    private static void PrintHelp(string group = null)
    {
        Console.Error.WriteLine("Usage: pk3ds <group> <command> [arguments]");
        Console.Error.WriteLine();
        foreach (var c in Commands.All.Where(c => group == null || c.Group == group))
            Console.Error.WriteLine($"  {c.Group,-13} {c.Name,-10} {c.Summary}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Run 'pk3ds <group> <command> --help' for a command's arguments.");
    }

    private static void PrintCommandHelp(Command c)
    {
        Console.Error.WriteLine($"Usage: pk3ds {c.Group} {c.Name} {c.Usage}");
        Console.Error.WriteLine(c.Summary);
    }
}
