namespace Basalt.Server.Plugins;

using Basalt.Server.Events;
using Basalt.Server.Player;
using WorldInstance = Basalt.Server.World.World;

public abstract class Plugin
{
    public Server Server = null!;
    public PluginDescription Description = null!;
    public string AssemblyPath = string.Empty;

    public virtual void OnLoad()
    {
    }

    public virtual void OnStart()
    {
    }

    public virtual void OnDisable()
    {
    }

    /// <summary>Registers a server event handler.</summary>
    protected void Listen<TSignal>(ServerEvent @event, Action<TSignal> handler) where TSignal : ISignal
    {
        Server.On(@event, handler);
    }

    /// <summary>Runs an action on the simulation thread that owns the world.</summary>
    protected void RunOnWorld(WorldInstance world, Action action)
    {
        Server.RunOnWorldThread(world, action);
    }

    /// <summary>Resolves the world the player entity is currently in.</summary>
    protected bool TryGetPlayerWorld(Player player, out WorldInstance? world)
    {
        world = player.Dimension?.World as WorldInstance;
        return world is not null;
    }

    /// <summary>Iterates connected sessions (identity only; safe from any thread for read-only use).</summary>
    protected void ForEachOnlineSession(Action<PlayerSession> action)
    {
        foreach (PlayerSession session in Server.Sessions.Values)
        {
            action(session);
        }
    }

    /// <summary>Logs with a plugin name prefix.</summary>
    protected void Log(string message)
    {
        Logger.Info("[Plugin:{0}] {1}", Description.Name, message);
    }
}
