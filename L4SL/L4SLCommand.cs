using System;
using System.Linq;
using CommandSystem;

namespace L4SL.Commands;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class L4SLCommand : ParentCommand
{
    public L4SLCommand() => LoadGeneratedCommands();

    public override string Command => "l4sl";
    public override string[] Aliases => Array.Empty<string>();
    public override string Description => "동적 이벤트 로거 (L4SL)";

    public override void LoadGeneratedCommands()
    {
        RegisterCommand(new AddCommand());
        RegisterCommand(new RemoveCommand());
        RegisterCommand(new ListCommand());
        RegisterCommand(new ConfigureCommand());
    }

    protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        response = "사용법: l4sl <add|remove|list|configure> ...";
        return false;
    }
}

internal sealed class AddCommand : ICommand
{
    public string Command => "add";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "l4sl add <ClassName.EventName> <포맷>";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (arguments.Count < 2)
        {
            response = Description;
            return false;
        }

        string address = arguments.At(0);
        string format = string.Join(" ", arguments.Skip(1));

        response = Main.Instance.LoggerManager.Add(address, format);
        return true;
    }
}

internal sealed class RemoveCommand : ICommand
{
    public string Command => "remove";
    public string[] Aliases => new[] { "rm" };
    public string Description => "l4sl remove <ClassName.EventName>";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (arguments.Count < 1)
        {
            response = Description;
            return false;
        }

        response = Main.Instance.LoggerManager.Remove(arguments.At(0));
        return true;
    }
}

internal sealed class ListCommand : ICommand
{
    public string Command => "list";
    public string[] Aliases => new[] { "ls" };
    public string Description => "l4sl list";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        response = Main.Instance.LoggerManager.List();
        return true;
    }
}

internal sealed class ConfigureCommand : ICommand
{
    public string Command => "configure";
    public string[] Aliases => new[] { "config", "cfg" };
    public string Description => "l4sl configure <ClassName.EventName> <새 포맷>";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (arguments.Count < 2)
        {
            response = Description;
            return false;
        }

        string address = arguments.At(0);
        string newFormat = string.Join(" ", arguments.Skip(1));

        response = Main.Instance.LoggerManager.Configure(address, newFormat);
        return true;
    }
}
