namespace Basalt.Server;

public class Properties
{   
    [ServerProperties.PropertyOrder(1)]
    [ServerProperties.PropertyKey("server-port")]
    [ServerProperties.PropertyComment("IPv4 port the server should listen to.")]
    public ushort Port { get; set; } = 19132;

    [ServerProperties.PropertyOrder(2)]
    [ServerProperties.PropertyKey("raknet-mtu")]
    [ServerProperties.PropertyComment("Maximum transmission unit for RakNet.")]
    public ushort Mtu { get; set; } = 1024;

    [ServerProperties.PropertyOrder(3)]
    [ServerProperties.PropertyKey("max-players")]
    [ServerProperties.PropertyComment("The maximum number of players that can play on the server.")]
    public int MaxPlayers { get; set; } = 10;
    
    [ServerProperties.PropertyOrder(4)]
    [ServerProperties.PropertyKey("online-mode")]
    [ServerProperties.PropertyComment("If true all connected players must be authenticated.")]
    public bool OnlineMode { get; set; } = true;

    [ServerProperties.PropertyOrder(5)]
    [ServerProperties.PropertyKey("compression-algorithm")]
    [ServerProperties.PropertyComment("Allowed values: zlib, snappy.")]
    public string CompressionMethod { get; set; } = "zlib";

    [ServerProperties.PropertyOrder(6)]
    [ServerProperties.PropertyKey("compression-threshold")]
    [ServerProperties.PropertyComment("Smallest payload size to compress.")]
    public int CompressionThreshold { get; set; } = 1;

    [ServerProperties.PropertyOrder(7)]
    [ServerProperties.PropertyKey("max-view-distance")]
    [ServerProperties.PropertyComment("Maximum chunk view distance players can request.")]
    public int MaxViewDistance { get; set; } = 32;

    [ServerProperties.PropertyOrder(8)]
    [ServerProperties.PropertyKey("simulation-distance")]
    [ServerProperties.PropertyComment("Chunk distance around players where entities are ticked.")]
    public int SimulationDistance { get; set; } = 4;

    [ServerProperties.PropertyOrder(9)]
    [ServerProperties.PropertyKey("world-provider")]
    [ServerProperties.PropertyComment("World provider type.")]
    public string WorldProvider { get; set; } = "leveldb";
    
    [ServerProperties.PropertyOrder(10)]
    [ServerProperties.PropertyKey("world-path")]
    [ServerProperties.PropertyComment("Path to world data.")]
    public string WorldPath { get; set; } = "worlds/world";

    [ServerProperties.PropertyOrder(11)]
    [ServerProperties.PropertyKey("default-world")]
    [ServerProperties.PropertyComment("Identifier of the default world.")]
    public string DefaultWorldIdentifier { get; set; } = "world";

    [ServerProperties.PropertyOrder(12)]
    [ServerProperties.PropertyKey("plugins-directory")]
    [ServerProperties.PropertyComment("Directory where plugin DLLs are loaded from.")]
    public string PluginsDirectory { get; set; } = "plugins";

    [ServerProperties.PropertyOrder(13)]
    [ServerProperties.PropertyKey("world-scheduler-debug")]
    [ServerProperties.PropertyComment("Verbose scheduler and packet routing logs.")]
    public bool WorldSchedulerDebug { get; set; } = false;

    [ServerProperties.PropertyOrder(14)]
    [ServerProperties.PropertyKey("world-thread-count")]
    [ServerProperties.PropertyComment("Number of world simulation worker threads.")]
    public int WorldThreadCount { get; set; } = 4;

    [ServerProperties.PropertyOrder(15)]
    [ServerProperties.PropertyKey("world-scheduler-enabled")]
    [ServerProperties.PropertyComment("Enable multi-worker world scheduler.")]
    public bool WorldSchedulerEnabled { get; set; } = false;

    [ServerProperties.PropertyOrder(16)]
    [ServerProperties.PropertyKey("world-default-allowed-workers")]
    [ServerProperties.PropertyComment("Comma-separated worker indices for worlds without world.json. Empty = all workers.")]
    public string DefaultAllowedWorkers { get; set; } = "";

    [ServerProperties.PropertyOrder(17)]
    [ServerProperties.PropertyKey("additional-worlds")]
    [ServerProperties.PropertyComment("Comma-separated world identifiers to load at startup (from worlds/{id}).")]
    public string AdditionalWorlds { get; set; } = "world_copy";
}






