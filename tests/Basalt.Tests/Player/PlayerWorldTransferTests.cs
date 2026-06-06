namespace Basalt.Tests.Player;

using Basalt.Protocol.Enums;
using Basalt.Protocol.Nbt;
using Basalt.Protocol.Types;
using Basalt.Server.Player;
using Basalt.Server.World;
using Basalt.Server.World.Dimension.Provider;
using ChunkColumn = Basalt.Server.World.Dimension.Chunk.Chunk;
using WorldInstance = Basalt.Server.World.World;

public sealed class PlayerWorldTransferTests
{
    [Fact]
    public void BuildEntityNbtFromSnapshot_WithoutCarry_UsesTargetSave()
    {
        TestWorldProvider targetProvider = new();
        WorldInstance targetWorld = new("target", targetProvider);

        CompoundTag targetSave = MakePlayerNbt("Steve", "123", 10f, 64f, 20f, inventorySlot: 99);
        targetSave.Set("isOp", new ByteTag { Value = 1 });
        targetProvider.SavePlayerData("123", targetSave);

        CompoundTag sourceNbt = MakePlayerNbt("Steve", "123", 1f, 2f, 3f, inventorySlot: 42);
        sourceNbt.Set("isOp", new ByteTag { Value = 0 });

        CompoundTag result = PlayerWorldTransfer.BuildEntityNbtFromSnapshot(
            sourceNbt,
            targetWorld,
            "123",
            TransferCarryFlags.None);

        Assert.Equal(10f, result.Get<FloatTag>("x")?.Value);
        Assert.Equal((sbyte)1, result.Get<ByteTag>("isOp")?.Value);
        Assert.Equal(99, GetFirstInventorySlot(result));
    }

    [Fact]
    public void BuildEntityNbtFromSnapshot_WithCarryInventory_MergesSourceInventory()
    {
        TestWorldProvider targetProvider = new();
        WorldInstance targetWorld = new("target", targetProvider);

        CompoundTag targetSave = MakePlayerNbt("Steve", "123", 10f, 64f, 20f, inventorySlot: 99);
        targetProvider.SavePlayerData("123", targetSave);

        CompoundTag sourceNbt = MakePlayerNbt("Steve", "123", 1f, 2f, 3f, inventorySlot: 42);

        CompoundTag result = PlayerWorldTransfer.BuildEntityNbtFromSnapshot(
            sourceNbt,
            targetWorld,
            "123",
            TransferCarryFlags.Inventory);

        Assert.Equal(10f, result.Get<FloatTag>("x")?.Value);
        Assert.Equal(42, GetFirstInventorySlot(result));
    }

    [Fact]
    public void ResolveDestinationTransform_WithoutCarry_UsesTargetSavePosition()
    {
        TestWorldProvider targetProvider = new();
        WorldInstance targetWorld = new("target", targetProvider);
        targetProvider.SavePlayerData("123", MakePlayerNbt("Steve", "123", 5f, 70f, 15f, inventorySlot: 0));

        Player source = new("Steve", "123", Guid.NewGuid());
        source.Position = new Vec3f { X = 100f, Y = 100f, Z = 100f };

        PlayerWorldTransfer.PlayerTransform transform = PlayerWorldTransfer.ResolveDestinationTransform(
            source,
            targetWorld,
            explicitCoords: null,
            TransferCarryFlags.None);

        Assert.Equal(5f, transform.Position.X);
        Assert.Equal(70f, transform.Position.Y);
        Assert.Equal(15f, transform.Position.Z);
    }

    [Fact]
    public void ResolveDestinationTransform_WithCarryPosition_UsesSourcePosition()
    {
        WorldInstance targetWorld = new("target", new TestWorldProvider());

        Player source = new("Steve", "123", Guid.NewGuid());
        source.Position = new Vec3f { X = 100f, Y = 80f, Z = 50f };
        source.Pitch = 10f;
        source.Yaw = 20f;
        source.HeadYaw = 30f;

        PlayerWorldTransfer.PlayerTransform transform = PlayerWorldTransfer.ResolveDestinationTransform(
            source,
            targetWorld,
            explicitCoords: null,
            TransferCarryFlags.Position);

        Assert.Equal(100f, transform.Position.X);
        Assert.Equal(10f, transform.Pitch);
        Assert.Equal(20f, transform.Yaw);
        Assert.Equal(30f, transform.HeadYaw);
    }

    [Fact]
    public void ResolveDestinationTransform_WithExplicitCoords_UsesCommandPosition()
    {
        WorldInstance targetWorld = new("target", new TestWorldProvider());

        Player source = new("Steve", "123", Guid.NewGuid());
        source.Position = new Vec3f { X = 1f, Y = 2f, Z = 3f };

        Vec3f explicitCoords = new() { X = 200f, Y = 65f, Z = 300f };
        PlayerWorldTransfer.PlayerTransform transform = PlayerWorldTransfer.ResolveDestinationTransform(
            source,
            targetWorld,
            explicitCoords,
            TransferCarryFlags.None);

        Assert.Equal(200f, transform.Position.X);
        Assert.Equal(65f, transform.Position.Y);
        Assert.Equal(300f, transform.Position.Z);
    }

    [Fact]
    public void ResolveDestinationTransform_FirstVisit_UsesDefaultSpawn()
    {
        WorldInstance targetWorld = new("target", new TestWorldProvider());
        Player source = new("Steve", "123", Guid.NewGuid());

        PlayerWorldTransfer.PlayerTransform transform = PlayerWorldTransfer.ResolveDestinationTransform(
            source,
            targetWorld,
            explicitCoords: null,
            TransferCarryFlags.None);

        Assert.Equal(PlayerWorldTransfer.DefaultSpawn.X, transform.Position.X);
        Assert.Equal(PlayerWorldTransfer.DefaultSpawn.Y, transform.Position.Y);
        Assert.Equal(PlayerWorldTransfer.DefaultSpawn.Z, transform.Position.Z);
    }

    [Fact]
    public void SaveToWorld_PersistsPlayerDataOnProvider()
    {
        TestWorldProvider provider = new();
        WorldInstance world = new("world", provider);
        Player player = new("Steve", "123", Guid.NewGuid());
        player.Position = new Vec3f { X = 7f, Y = 8f, Z = 9f };

        PlayerWorldTransfer.SaveToWorld(player, world);

        CompoundTag? saved = provider.LoadPlayerData("123");
        Assert.NotNull(saved);
        Assert.Equal(7f, saved!.Get<FloatTag>("x")?.Value);
    }

    static CompoundTag MakePlayerNbt(string username, string xuid, float x, float y, float z, int inventorySlot)
    {
        CompoundTag root = new();
        root.Set("username", new StringTag { Value = username });
        root.Set("xuid", new StringTag { Value = xuid });
        root.Set("uuid", new StringTag { Value = Guid.NewGuid().ToString() });
        root.Set("x", new FloatTag { Value = x });
        root.Set("y", new FloatTag { Value = y });
        root.Set("z", new FloatTag { Value = z });

        ListTag inventory = new() { Name = "Inventory" };
        CompoundTag entry = new();
        entry.Set("Slot", new IntTag { Value = 0 });
        entry.Set("Name", new StringTag { Value = "minecraft:stone" });
        entry.Set("Count", new IntTag { Value = inventorySlot });
        inventory.Values.Add(entry);
        root.Set("Inventory", inventory);

        return root;
    }

    static int GetFirstInventorySlot(CompoundTag root)
    {
        ListTag? inventory = root.Get<ListTag>("Inventory");
        Assert.NotNull(inventory);
        Assert.NotEmpty(inventory!.Values);
        CompoundTag entry = Assert.IsType<CompoundTag>(inventory.Values[0]);
        return entry.Get<IntTag>("Count")?.Value ?? -1;
    }

    sealed class TestWorldProvider : WorldProvider
    {
        readonly Dictionary<string, CompoundTag> _players = new(StringComparer.Ordinal);

        public override string Identifier => "test";

        public override bool HasChunk(DimensionType dimensionType, int x, int z) => false;

        public override ChunkColumn? LoadChunk(DimensionType dimensionType, int x, int z) => null;

        public override void SaveChunk(ChunkColumn chunk)
        {
        }

        public override void DeleteChunk(DimensionType dimensionType, int x, int z)
        {
        }

        public override void Dispose()
        {
        }

        public override CompoundTag? LoadPlayerData(string xuid)
        {
            return _players.TryGetValue(xuid, out CompoundTag? data) ? data : null;
        }

        public override void SavePlayerData(string xuid, CompoundTag data)
        {
            _players[xuid] = data;
        }
    }
}
