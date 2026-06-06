namespace Basalt.Server.Commands.List.Operator;

using Basalt.Protocol.Enums;
using Basalt.Server.Commands;
using Basalt.Server.Player;
using Basalt.Server.Scheduling;
using Vec3f = Basalt.Protocol.Types.Vec3f;
using Basalt.Server.World.Dimension;
using Player = global::Basalt.Server.Player.Player;
using WorldInstance = Basalt.Server.World.World;

public class TpCommand : Command
{
    public TpCommand() : base("tp", "Teleport entities", ["teleport"], [])
    {
        Permissions.Add("basalt.op");
        CreateOverload();

        AddOverload()
            .Set<TargetEnum>("destination", true)
            .Set<StringEnum>("dimension", false);

        AddOverload()
            .Set<TargetEnum>("victim", true)
            .Set<TargetEnum>("destination", true)
            .Set<StringEnum>("dimension", false);

        AddOverload()
            .Set<PositionEnum>("destination", true)
            .Set<StringEnum>("dimension", false);

        AddOverload()
            .Set<TargetEnum>("victim", true)
            .Set<PositionEnum>("destination", true)
            .Set<StringEnum>("dimension", false);
    }

    public override string? GetHelpMessage() =>
        """
        §cUsage:
        §7/tp <world> [--carry inventory] [--carry position]
        §7/tp <destination> [dimension] | /tp <x> <y> <z> [dimension]
        §7/tp <victim> <destination> [dimension] | /tp <victim> <x> <y> <z> [dimension]
        §7Cross-world default: target world's saved state (position from LevelDB or spawn).
        §7--carry inventory: merge inventory from source world.
        §7--carry position: use current position instead of target save.
        """;

    public override CommandResult? ExecuteManual(CommandExecutionState state, string[] tokens, int argumentOffset)
    {
        string[] args = tokens[argumentOffset..];
        Player? executor = GetExecutor(state);
        WorldInstance contextWorld = executor?.Dimension?.World ?? state.Server.GetWorld();

        if (!TryParseCarryFlags(ref args, out TransferCarryFlags carryFlags, out CommandResult? carryError))
        {
            return carryError;
        }

        StripTrailingDimension(contextWorld, args, out args, out string? explicitDimensionId);

        if (args.Length >= 4 && PositionEnum.Parse(args, 1, new Vec3f(), out _))
        {
            return TeleportVictimsToPosition(state, executor, contextWorld, explicitDimensionId, args[0], args, positionStart: 1, carryFlags);
        }

        if (args.Length == 3 && TryParsePosition(executor, args, 0, null, out Vec3f selfCoords))
        {
            CommandResult? executorError = RequireExecutor(executor);
            if (executorError is not null)
            {
                return executorError;
            }

            Dimension? dimension = ResolveCoordsDimension(contextWorld, executor!, explicitDimensionId);
            PlayerWorldTransfer.PlayerTransform transform = new(selfCoords, executor!.Pitch, executor.Yaw, executor.HeadYaw);
            return TeleportPlayers(state, [executor!], transform, dimension, destinationName: null, carryFlags, explicitCoordinates: true);
        }

        if (args.Length == 2)
        {
            return TeleportVictimsToPlayer(state, executor, contextWorld, explicitDimensionId, args[0], args[1], carryFlags);
        }

        if (args.Length == 1)
        {
            if (TryResolveWorldTeleport(state, args[0], out WorldInstance targetWorld, out Dimension? targetDimension, out CommandResult? worldError))
            {
                CommandResult? executorError = RequireExecutor(executor);
                if (executorError is not null)
                {
                    return executorError;
                }

                PlayerWorldTransfer.PlayerTransform transform = PlayerWorldTransfer.ResolveDestinationTransform(
                    executor!,
                    targetWorld,
                    explicitCoords: null,
                    carryFlags);

                return TeleportPlayers(state, [executor!], transform, targetDimension, destinationName: targetWorld.Name, carryFlags, explicitCoordinates: false);
            }

            if (worldError is not null)
            {
                return worldError;
            }

            return TeleportExecutorToPlayer(state, executor, contextWorld, explicitDimensionId, args[0], carryFlags);
        }

        return CommandResult.Message(GetHelpMessage()!, false);
    }

    static bool TryParseCarryFlags(ref string[] args, out TransferCarryFlags carryFlags, out CommandResult? error)
    {
        carryFlags = TransferCarryFlags.None;
        error = null;
        List<string> remaining = [];

        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--carry", StringComparison.OrdinalIgnoreCase))
            {
                remaining.Add(args[i]);
                continue;
            }

            if (i + 1 >= args.Length)
            {
                error = CommandResult.Message("§c--carry requires a value (inventory, position, or inventory,position).", false);
                return false;
            }

            string value = args[++i];
            if (!TryParseCarryValues(value, out TransferCarryFlags parsed, out string? parseError))
            {
                error = CommandResult.Message($"§c{parseError}", false);
                return false;
            }

            carryFlags |= parsed;
        }

        args = remaining.ToArray();
        return true;
    }

    static bool TryParseCarryValues(string value, out TransferCarryFlags flags, out string? error)
    {
        flags = TransferCarryFlags.None;
        error = null;

        foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "inventory":
                    flags |= TransferCarryFlags.Inventory;
                    break;
                case "position":
                    flags |= TransferCarryFlags.Position;
                    break;
                default:
                    error = $"Unknown carry flag '{part}'. Use inventory and/or position.";
                    return false;
            }
        }

        if (flags == TransferCarryFlags.None)
        {
            error = "At least one carry flag is required after --carry.";
            return false;
        }

        return true;
    }

    static CommandResult TeleportExecutorToPlayer(
        CommandExecutionState state,
        Player? executor,
        WorldInstance contextWorld,
        string? explicitDimensionId,
        string destinationToken,
        TransferCarryFlags carryFlags)
    {
        CommandResult? executorError = RequireExecutor(executor);
        if (executorError is not null)
        {
            return executorError;
        }

        if (!TryGetSinglePlayer(state, executor, destinationToken, out Player destination, out CommandResult? targetError))
        {
            return targetError!;
        }

        Dimension? dimension = ResolvePlayerDestinationDimension(contextWorld, destination, explicitDimensionId);
        PlayerWorldTransfer.PlayerTransform transform = new(destination.Position, destination.Pitch, destination.Yaw, destination.HeadYaw);
        return TeleportPlayers(state, [executor!], transform, dimension, destinationName: destination.Username, carryFlags, explicitCoordinates: true);
    }

    static CommandResult TeleportVictimsToPlayer(
        CommandExecutionState state,
        Player? executor,
        WorldInstance contextWorld,
        string? explicitDimensionId,
        string victimToken,
        string destinationToken,
        TransferCarryFlags carryFlags)
    {
        if (!TryGetPlayers(state, executor, victimToken, out List<Player> victims, out CommandResult? victimError))
        {
            return victimError!;
        }

        if (!TryGetSinglePlayer(state, executor, destinationToken, out Player destination, out CommandResult? destinationError))
        {
            return destinationError!;
        }

        Dimension? dimension = ResolvePlayerDestinationDimension(contextWorld, destination, explicitDimensionId);
        PlayerWorldTransfer.PlayerTransform transform = new(destination.Position, destination.Pitch, destination.Yaw, destination.HeadYaw);
        return TeleportPlayers(state, victims, transform, dimension, destinationName: destination.Username, carryFlags, explicitCoordinates: true);
    }

    static CommandResult TeleportVictimsToPosition(
        CommandExecutionState state,
        Player? executor,
        WorldInstance contextWorld,
        string? explicitDimensionId,
        string victimToken,
        string[] args,
        int positionStart,
        TransferCarryFlags carryFlags)
    {
        if (!TryGetPlayers(state, executor, victimToken, out List<Player> victims, out CommandResult? victimError))
        {
            return victimError!;
        }

        Player originPlayer = victims[0];
        if (!TryParsePosition(executor, args, positionStart, originPlayer, out Vec3f position))
        {
            return CommandResult.Message("§cInvalid coordinates.", false);
        }

        Dimension? dimension = ResolveCoordsDimension(contextWorld, executor ?? originPlayer, explicitDimensionId);
        PlayerWorldTransfer.PlayerTransform transform = new(position, originPlayer.Pitch, originPlayer.Yaw, originPlayer.HeadYaw);
        return TeleportPlayers(state, victims, transform, dimension, destinationName: null, carryFlags, explicitCoordinates: true);
    }

    static Player? GetExecutor(CommandExecutionState state) =>
        state.Executor is PlayerExecutor executor ? executor.Player : null;

    static CommandResult? RequireExecutor(Player? executor)
    {
        if (executor is not null)
        {
            return null;
        }

        return CommandResult.Message("You must specify a target, or be a player!", false);
    }

    static bool TryGetSinglePlayer(
        CommandExecutionState state,
        Player? context,
        string token,
        out Player player,
        out CommandResult? error)
    {
        player = null!;
        error = TargetEnum.ResolveSinglePlayerTarget(
            state.Server,
            context,
            token,
            "No online players matched the target selector",
            "Multiple entities matched the target selector, please be more specific");

        if (error is not null)
        {
            return false;
        }

        List<Player> players = TargetEnum.ResolvePlayers(state.Server, context, token);
        if (players.Count == 0)
        {
            error = CommandResult.Message("The target selector must be a player!", false);
            return false;
        }

        player = players[0];
        return true;
    }

    static bool TryGetPlayers(
        CommandExecutionState state,
        Player? context,
        string token,
        out List<Player> players,
        out CommandResult? error)
    {
        players = TargetEnum.ResolvePlayers(state.Server, context, token);
        if (players.Count == 0)
        {
            error = CommandResult.Message("No online players matched the target selector", false);
            return false;
        }

        error = null;
        return true;
    }

    static Dimension? ResolvePlayerDestinationDimension(
        WorldInstance contextWorld,
        Player destination,
        string? explicitDimensionId)
    {
        if (!string.IsNullOrWhiteSpace(explicitDimensionId))
        {
            return contextWorld.GetDimension(explicitDimensionId);
        }

        return destination.Dimension;
    }

    static Dimension? ResolveCoordsDimension(
        WorldInstance contextWorld,
        Player contextPlayer,
        string? explicitDimensionId)
    {
        if (!string.IsNullOrWhiteSpace(explicitDimensionId))
        {
            return contextWorld.GetDimension(explicitDimensionId);
        }

        return contextPlayer.Dimension ?? contextWorld.GetDimension(DimensionType.Overworld);
    }

    static bool TryParsePosition(Player? executor, string[] args, int start, Player? originPlayer, out Vec3f position)
    {
        Vec3f origin = originPlayer?.Position ?? executor?.Position ?? new Vec3f();
        return PositionEnum.Parse(args, start, origin, out position);
    }

    static bool StripTrailingDimension(
        WorldInstance world,
        string[] args,
        out string[] stripped,
        out string? dimensionIdentifier)
    {
        stripped = args;
        dimensionIdentifier = null;

        if (args.Length == 0)
        {
            return false;
        }

        string last = args[^1];
        if (world.GetDimension(last) is null)
        {
            return false;
        }

        dimensionIdentifier = last;
        stripped = args[..^1];
        return true;
    }

    static bool TryResolveWorldTeleport(
        CommandExecutionState state,
        string token,
        out WorldInstance targetWorld,
        out Dimension? targetDimension,
        out CommandResult? error)
    {
        targetWorld = null!;
        targetDimension = null;
        error = null;

        if (!state.Server.TryGetWorld(token, out WorldInstance? world) || world is null)
        {
            return false;
        }

        targetWorld = world;
        targetDimension = world.GetDimension(DimensionType.Overworld) ?? world.GetDimension("overworld");
        if (targetDimension is null)
        {
            error = CommandResult.Message($"§cWorld §a{token}§c has no overworld dimension.", false);
            return false;
        }

        return true;
    }

    static bool NeedsCrossWorkerTransfer(Server server, WorldInstance sourceWorld, WorldInstance targetWorld)
    {
        if (!server.Properties.WorldSchedulerEnabled || server.Scheduler is not WorldScheduler)
        {
            return false;
        }

        if (ReferenceEquals(sourceWorld, targetWorld))
        {
            return false;
        }

        if (!targetWorld.IsAttached)
        {
            return true;
        }

        return sourceWorld.AttachedWorkerId != targetWorld.AttachedWorkerId;
    }

    static CommandResult TeleportPlayers(
        CommandExecutionState state,
        List<Player> players,
        PlayerWorldTransfer.PlayerTransform transform,
        Dimension? dimension,
        string? destinationName,
        TransferCarryFlags carryFlags,
        bool explicitCoordinates)
    {
        Player? executor = GetExecutor(state);
        List<string> messages = [];
        int successCount = 0;

        for (int i = 0; i < players.Count; i++)
        {
            Player player = players[i];
            try
            {
                if (dimension?.World is not WorldInstance targetWorld)
                {
                    throw new InvalidOperationException("Target dimension is required.");
                }

                WorldInstance? sourceWorld = player.Dimension?.World;
                PlayerWorldTransfer.PlayerTransform resolvedTransform = explicitCoordinates || sourceWorld is null
                    ? transform
                    : PlayerWorldTransfer.ResolveDestinationTransform(player, targetWorld, explicitCoords: null, carryFlags);

                if (sourceWorld is not null && PlayerWorldTransfer.IsCrossWorld(sourceWorld, targetWorld))
                {
                    if (NeedsCrossWorkerTransfer(state.Server, sourceWorld, targetWorld))
                    {
                        if (player.Session is null)
                        {
                            throw new InvalidOperationException("Player has no session.");
                        }

                        WorldScheduler scheduler = (WorldScheduler)state.Server.Scheduler;
                        scheduler.BeginCrossWorldTransfer(player.Session, targetWorld, dimension, resolvedTransform, carryFlags);
                        successCount++;

                        string label = destinationName ?? targetWorld.Name;
                        if (ReferenceEquals(executor, player))
                        {
                            messages.Add($"§7Transferindo para §a{label}§7.");
                        }
                        else
                        {
                            messages.Add($"§7Transferindo §a{player.Username} §7para §a{label}§7.");
                            player.SendMessage($"§7Transferindo para §a{label}§7.");
                        }

                        continue;
                    }

                    PlayerWorldTransfer.ApplySameWorker(state.Server, player, targetWorld, dimension, resolvedTransform, carryFlags);
                    successCount++;

                    string sameWorkerLabel = destinationName ?? targetWorld.Name;
                    if (ReferenceEquals(executor, player))
                    {
                        messages.Add($"§7Transferindo para §a{sameWorkerLabel}§7.");
                    }
                    else
                    {
                        messages.Add($"§7Transferindo §a{player.Username} §7para §a{sameWorkerLabel}§7.");
                        player.SendMessage($"§7Transferindo para §a{sameWorkerLabel}§7.");
                    }

                    continue;
                }

                player.Teleport(resolvedTransform.Position, dimension);
                successCount++;

                if (ReferenceEquals(executor, player))
                {
                    if (destinationName is not null)
                    {
                        messages.Add($"§7Teleported you to §a{destinationName}§7.");
                    }
                    else
                    {
                        messages.Add($"§7Teleported you to §a{resolvedTransform.Position.X:0.##} {resolvedTransform.Position.Y:0.##} {resolvedTransform.Position.Z:0.##}§7.");
                    }
                }
                else if (destinationName is not null)
                {
                    messages.Add($"§7Teleported §a{player.Username} §7to §a{destinationName}§7.");
                    player.SendMessage($"§7You were teleported to §a{destinationName}§7.");
                }
                else
                {
                    messages.Add($"§7Teleported §a{player.Username} §7to §a{resolvedTransform.Position.X:0.##} {resolvedTransform.Position.Y:0.##} {resolvedTransform.Position.Z:0.##}§7.");
                    player.SendMessage($"§7You were teleported to §a{resolvedTransform.Position.X:0.##} {resolvedTransform.Position.Y:0.##} {resolvedTransform.Position.Z:0.##}§7.");
                }
            }
            catch (Exception exception)
            {
                messages.Add($"§cCould not teleport §a{player.Username}§c: {exception.Message}");
            }
        }

        if (successCount == 0)
        {
            return new CommandResult
            {
                Success = false,
                Messages = messages.Count == 0 ? ["§cNo players were teleported."] : messages
            };
        }

        return new CommandResult
        {
            Success = true,
            Messages = messages
        };
    }
}
