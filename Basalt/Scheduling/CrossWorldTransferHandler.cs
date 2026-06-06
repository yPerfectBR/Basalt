namespace Basalt.Server.Scheduling;

using Basalt.Protocol.Enums;
using Basalt.Protocol.Nbt;
using Basalt.Protocol.Packets;
using Basalt.Protocol.Types;
using Basalt.Server.Entity.Traits.Types;
using Basalt.Server.Player;
using Basalt.Server.Player.Traits;
using Basalt.Server.Scheduling.Messages;
using Basalt.Server.World.Dimension;
using PlayerInstance = Basalt.Server.Player.Player;
using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Cross-worker transfer steps executed on source and target workers.
/// </summary>
internal static class CrossWorldTransferHandler
{
    public static PlayerEntitySnapshot CaptureSnapshot(
        PlayerInstance player,
        WorldInstance targetWorld,
        string targetDimensionId,
        PlayerWorldTransfer.PlayerTransform transform,
        TransferCarryFlags carryFlags,
        CompoundTag sourceEntityNbt)
    {
        return new PlayerEntitySnapshot
        {
            Username = player.Username,
            Xuid = player.Xuid,
            Uuid = player.Uuid,
            RuntimeId = player.RuntimeId,
            Position = transform.Position,
            Pitch = transform.Pitch,
            Yaw = transform.Yaw,
            HeadYaw = transform.HeadYaw,
            SourceWorldId = player.Dimension!.World!.Name,
            SourceDimensionType = player.Dimension.Type,
            TargetWorldId = targetWorld.Name,
            TargetDimensionId = targetDimensionId,
            CarryFlags = carryFlags,
            SourceEntityNbt = sourceEntityNbt
        };
    }

    public static void HandlePrepareTransfer(Server server, WorldWorker sourceWorker, PrepareTransferMessage message)
    {
        PlayerSession session = message.Session;
        PlayerInstance? player = session.ActiveEntity;
        if (player?.Dimension?.World is not WorldInstance sourceWorld)
        {
            AbortTransfer(server, session, "PrepareTransfer: no active entity.");
            return;
        }

        if (server.Properties.WorldSchedulerDebug)
        {
            Logger.Debug(
                "[Transfer] PrepareTransfer from={0} to={1} worker={2}",
                sourceWorld.Name,
                message.TargetWorld.Name,
                sourceWorker.WorkerId);
        }

        PlayerWorldTransfer.SaveToWorld(player, sourceWorld);
        CompoundTag sourceEntityNbt = player.WriteToNbt();
        PlayerWorldTransfer.PlayerTransform transform = new(message.Position, message.Pitch, message.Yaw, message.HeadYaw);

        PlayerEntitySnapshot snapshot = CaptureSnapshot(
            player,
            message.TargetWorld,
            message.TargetDimensionId,
            transform,
            message.CarryFlags,
            sourceEntityNbt);

        WorldPlayerPresence.OnPlayerLeftWorld(server, sourceWorld);

        player.GetTrait<PlayerChunkRenderingTrait>()?.FlushClientChunks();

        if (player.Dimension is Dimension sourceDimension)
        {
            player.Despawn(new EntityDespawnOptions());
            sourceDimension.RemoveEntity(player, complete: true);
        }

        session.ActiveEntity = null;

        server.Scheduler.RequestAttach(message.TargetWorld);

        if (server.Scheduler is not WorldScheduler worldScheduler)
        {
            AbortTransfer(server, session, "PrepareTransfer: scheduler is not multi-worker.");
            return;
        }

        int? targetWorkerId = message.TargetWorld.AttachedWorkerId;
        if (!targetWorkerId.HasValue)
        {
            AbortTransfer(server, session, "PrepareTransfer: target world has no worker assignment.");
            return;
        }

        worldScheduler.Pool.GetWorker(targetWorkerId.Value).Enqueue(new CompleteTransferMessage
        {
            Session = session,
            Snapshot = snapshot
        });
    }

    public static void HandleCompleteTransfer(Server server, WorldWorker targetWorker, CompleteTransferMessage message)
    {
        PlayerSession session = message.Session;
        PlayerEntitySnapshot snapshot = message.Snapshot;

        if (!server.TryGetWorld(snapshot.TargetWorldId, out WorldInstance? targetWorld) || targetWorld is null)
        {
            AbortTransfer(server, session, $"CompleteTransfer: world '{snapshot.TargetWorldId}' not found.");
            return;
        }

        Dimension? targetDimension = targetWorld.GetDimension(snapshot.TargetDimensionId)
            ?? targetWorld.GetDimension(DimensionType.Overworld);

        if (targetDimension is null)
        {
            AbortTransfer(server, session, $"CompleteTransfer: dimension '{snapshot.TargetDimensionId}' not found.");
            return;
        }

        try
        {
            CompoundTag entityNbt = PlayerWorldTransfer.BuildEntityNbtFromSnapshot(
                snapshot.SourceEntityNbt,
                targetWorld,
                snapshot.Xuid,
                snapshot.CarryFlags);

            PlayerInstance player = new(snapshot.Username, snapshot.Xuid, snapshot.Uuid, snapshot.RuntimeId);
            player.FromNBT(entityNbt);
            player.Position = snapshot.Position;
            player.Pitch = snapshot.Pitch;
            player.Yaw = snapshot.Yaw;
            player.HeadYaw = snapshot.HeadYaw;
            player.Session = session;

            EntitySpawnOptions spawnOptions = new(InitialSpawn: false);
            player.Spawn(targetDimension, spawnOptions);
            WorldPlayerPresence.OnPlayerEnteredWorld(server, targetWorld);

            bool crossWorld = !snapshot.SourceWorldId.Equals(snapshot.TargetWorldId, StringComparison.OrdinalIgnoreCase);
            bool useDimensionChange = crossWorld && snapshot.SourceDimensionType != targetDimension.Type;

            session.ActiveEntity = player;
            session.TransferState = TransferState.Idle;

            player.ResyncAfterWorldTransfer(useDimensionChange);

            if (server.Properties.WorldSchedulerDebug)
            {
                Logger.Debug(
                    "[Transfer] CompleteTransfer session={0} world={1} worker={2}",
                    session.Username,
                    targetWorld.Name,
                    targetWorker.WorkerId);
            }
        }
        catch (Exception exception)
        {
            AbortTransfer(server, session, $"CompleteTransfer failed: {exception.Message}");
        }
    }

    static void AbortTransfer(Server server, PlayerSession session, string reason)
    {
        Logger.Error("[Transfer] {0}", reason);
        session.TransferState = TransferState.Idle;
        session.ActiveEntity = null;
        session.SendMessage("§cWorld transfer failed. Please reconnect.");
        session.Disconnect("World transfer failed.");
    }
}
