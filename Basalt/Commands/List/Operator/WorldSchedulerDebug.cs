namespace Basalt.Server.Commands.List.Operator;

using Basalt.Server.Commands;
using Basalt.Server.Scheduling;

public sealed class WorldSchedulerDebugCommand : Command
{
    public WorldSchedulerDebugCommand() : base("worldscheduler", "Show world scheduler worker metrics", ["scheddebug"], [])
    {
        Permissions.Add("basalt.op");
    }

    public override CommandResult Execute(CommandExecutionState state)
    {
        IReadOnlyList<WorkerLoadMetrics> metrics = state.Server.Scheduler.GetMetrics();
        if (metrics.Count == 0)
        {
            return CommandResult.Message("§7No scheduler metrics available.", true);
        }

        bool multiWorker = state.Server.Properties.WorldSchedulerEnabled;
        string header = multiWorker
            ? $"§7World Scheduler ({metrics.Count} workers)\n"
            : "§7World Scheduler (single-thread mode)\n";

        System.Text.StringBuilder builder = new(header);
        builder.Append(FormatRow(
            Cell("W", 2, "§7"),
            Cell("Wrlds", 5, "§7"),
            Cell("Plrs", 5, "§7"),
            Cell("TPS", 5, "§7"),
            Cell("WorkMs", 6, "§7"),
            Cell("LagMs", 5, "§7")));
        builder.Append('\n');

        for (int i = 0; i < metrics.Count; i++)
        {
            WorkerLoadMetrics metric = metrics[i];
            string tpsColor = metric.Tps < 10 ? "§c" : metric.Tps < 15 ? "§6" : "§a";
            builder.Append(FormatRow(
                Cell(metric.WorkerId.ToString(), 2, "§7"),
                Cell(metric.ActiveWorldCount.ToString(), 5, "§a"),
                Cell(metric.TotalPresentPlayers.ToString(), 5, "§a"),
                Cell(metric.Tps.ToString("0.0"), 5, tpsColor),
                Cell(metric.LastTickWorkMs.ToString("0.0"), 6, "§a"),
                Cell(metric.TickLagMs.ToString("0.0"), 5, "§a")));
            builder.Append('\n');
        }

        return CommandResult.Message(builder.ToString(), true);
    }

    static string Cell(string text, int width, string color)
        => color + text.PadLeft(width);

    static string FormatRow(params string[] cells)
    {
        System.Text.StringBuilder row = new();
        for (int i = 0; i < cells.Length; i++)
        {
            if (i > 0)
            {
                row.Append(" §8│ ");
            }

            row.Append(cells[i]);
        }

        return row.ToString();
    }
}
