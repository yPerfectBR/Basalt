namespace Basalt.Server.Commands.List.Operator;

using Basalt.Server.Commands;

public class ListCommand : Command
{
    public ListCommand() : base("list", "Get a list of players on the server") { }

    public override CommandResult Execute(CommandExecutionState state)
    {
        List<Player.Player> onlinePlayers = state.Server.Sessions.Values
            .Select(static session => session.ActiveEntity)
            .OfType<Player.Player>()
            .ToList();

        var playerCount = onlinePlayers.Count;

        var message = $"§r§7There are (§a{playerCount}§7) Players Online.";
        if (playerCount > 0) message += "\n";

        for (int i = 0; i < onlinePlayers.Count; i++)
        {
            Player.Player player = onlinePlayers[i];
            if (i < onlinePlayers.Count - 1)
            {
                message += $"§a{player.Username}, \n";
            }
            else
            {
                message += $"§a{player.Username}";
            }
        }

        return CommandResult.Message(message, true);
    }
}







