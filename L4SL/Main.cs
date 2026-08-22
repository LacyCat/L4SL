using Exiled.API.Enums;
using Exiled.API.Features;
using L4SL.Feature;

namespace L4SL;

public sealed class Main : Plugin<Config>
{
    public override string Author { get; } = "@LacyCat";
    public override PluginPriority Priority { get; } = PluginPriority.Last;
    public static Main Instance { get; private set; } = null!;

    internal LoggerManager LoggerManager { get; private set; } = null!;

    public override void OnEnabled()
    {
        Instance = this;
        LoggerManager = new LoggerManager();
        LoggerManager.RestoreFromConfig();

        base.OnEnabled();
    }

    public override void OnDisabled()
    {
        LoggerManager?.RemoveAll();

        base.OnDisabled();
    }
}
