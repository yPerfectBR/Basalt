namespace Basalt.Tests.Plugins;

using Basalt.Server;
using Basalt.Server.Plugins;
using Basalt.Tests.Support;

public sealed class PluginLoadTests
{
    [Fact]
    public void PluginManager_LoadsJoinAnnouncerSample()
    {
        string? sourceDll = LocateJoinAnnouncerDll();
        Assert.NotNull(sourceDll);

        string pluginsDir = Path.Combine(Path.GetTempPath(), "basalt-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pluginsDir);
        File.Copy(sourceDll!, Path.Combine(pluginsDir, "JoinAnnouncer.dll"));

        Server server = new(new Properties
        {
            WorldProvider = "memory",
            Port = 0,
            AdditionalWorlds = "",
            WorldSchedulerEnabled = false,
            PluginsDirectory = pluginsDir
        });

        PluginContainer? loaded = server.Plugins.Plugins
            .FirstOrDefault(plugin => plugin.Description.Name == "JoinAnnouncer");
        Assert.NotNull(loaded);
        Assert.Equal(PluginState.Loaded, loaded.State);

        server.Start();

        Assert.Equal(PluginState.Started, loaded.State);
        server.Stop();
    }

    static string? LocateJoinAnnouncerDll()
    {
        string? directory = AppContext.BaseDirectory;
        for (int depth = 0; depth < 10 && directory is not null; depth++)
        {
            string candidate = Path.Combine(
                directory,
                "samples",
                "plugins",
                "JoinAnnouncer",
                "bin",
                "Debug",
                "net10.0",
                "JoinAnnouncer.dll");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}
