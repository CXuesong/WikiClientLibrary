using System.Reflection;
using System.Runtime.InteropServices;
using WikiClientLibrary.Client;
using WikiClientLibrary.Scribunto;
using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Samples.ScribuntoInteractive;

internal static class Program
{

    internal static async Task Main(string[] args)
    {
        var endPoint = args.Length > 0 ? args[0] : "https://test2.wikipedia.org/w/api.php";
        using var client = new WikiClient { ClientUserAgent = "ScribuntoConsoleTestApplication1/0.1" };
        var site = new WikiSite(client, endPoint);
        await site.Initialization;
        var sc = new ScribuntoConsole(site);
        await ResetSessionAsync(sc);
        var eofShortcut = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Ctrl + Z" : "Ctrl + D";
        Console.WriteLine("* Enter any Lua expression to evaluate. EOF ({0}) to exit.", eofShortcut);
        Console.WriteLine("* Precede a line with '=' to evaluate it as an expression or use \x1b[36mprint()\x1b[0m. Use \x1b[36mmw.logObject()\x1b[0m for tables.");
        Console.WriteLine("* Use \x1b[36mmw.log()\x1b[0m and \x1b[36mmw.logObject()\x1b[0m in module code to send messages to this console.");
        Console.WriteLine("* Enter .help for a list of local commands.");
        while (true)
        {
            Console.Write("> ");
            var l = Console.ReadLine();
            if (l == null)
                break;
            if (string.IsNullOrWhiteSpace(l))
                continue;
            if (l.StartsWith("."))
            {
                if (string.Equals(l, ".exit", StringComparison.OrdinalIgnoreCase))
                    break;
                await ExecuteCommandAsync(l[1..], sc);
                continue;
            }
            try
            {
                var result = await sc.EvaluateAsync(l);
                if (result.IsNewSession)
                {
                    Console.WriteLine("---------- Session Cleared ----------");
                }
                if (!string.IsNullOrEmpty(result.Output))
                {
                    Console.WriteLine(result.Output);
                }
                if (!string.IsNullOrEmpty(result.ReturnValue))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(result.ReturnValue);
                    Console.ResetColor();
                }
            }
            catch (ScribuntoConsoleException ex)
            {
                if (!string.IsNullOrEmpty(ex.EvaluationResult?.Output))
                {
                    Console.WriteLine(ex.EvaluationResult.Output);
                }
                WriteError($"{ex.ErrorCode}: {ex.ErrorMessage}");
            }
        }
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }


    private static async Task ExecuteCommandAsync(string command, ScribuntoConsole sc)
    {
        var method = typeof(Program).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .FirstOrDefault(m => string.Equals(m.GetCustomAttribute<ConsoleCommandAttribute>()?.Command, command, StringComparison.OrdinalIgnoreCase));
        if (method == null)
        {
            WriteError("Invalid command: " + command + ".");
            return;
        }
        var result = method.Invoke(null, new object[] { sc });
        if (result is Task t)
            await t;
    }

    [ConsoleCommand("reset", "Clears the Lua evaluation session.")]
    private static async Task ResetSessionAsync(ScribuntoConsole sc)
    {
        await sc.ResetAsync();
        Console.WriteLine("Initialized Scribunto console on {0} with session ID {1}.", sc.Site.SiteInfo.SiteName, sc.SessionId);
    }

    [ConsoleCommand("help", "Shows the command list.")]
    private static void ShowHelp(ScribuntoConsole sc)
    {
        var commands = typeof(Program).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Select(m => (method: m, attr: m.GetCustomAttribute<ConsoleCommandAttribute>()))
            .Where(t => t.attr != null)
            .Select(t => (command: t.attr!.Command, desc: t.attr.Description, method: t.method))
            .OrderBy(t => t.command);
        foreach ((string command, string desc, _) in commands)
        {
            Console.WriteLine(".{0,-15} {1}", command, desc);
        }
    }

    [ConsoleCommand("memory", "Shows the server-side memory usage.")]
    private static void ShowMemory(ScribuntoConsole sc)
    {
        Console.WriteLine("Memory used / maximum allowed: {0}/{1}", sc.SessionSize, sc.SessionMaxSize);
    }

    [ConsoleCommand("exit", "Exits the interactive console.")]
    private static void Exit(ScribuntoConsole sc)
    {
        // Stub
    }

}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
sealed class ConsoleCommandAttribute : Attribute
{

    public string Command { get; }

    public string? Description { get; }

    public ConsoleCommandAttribute(string command) : this(command, null)
    {
    }

    public ConsoleCommandAttribute(string command, string? description)
    {
        Command = command;
        Description = description;
    }

}