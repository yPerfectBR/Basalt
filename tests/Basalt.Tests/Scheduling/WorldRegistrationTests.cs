namespace Basalt.Tests.Scheduling;

using Basalt.Server.Scheduling;

public sealed class WorldRegistrationTests
{
    [Fact]
    public void WorldRegistration_Validate_RejectsEmptyAllowedWorkers()
    {
        WorldRegistration registration = new()
        {
            Identifier = "test",
            AllowedWorkers = []
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            WorldRegistration.Validate(registration, workerCount: 4));

        Assert.Contains("AllowedWorkers", exception.Message);
    }

    [Fact]
    public void WorldRegistration_Validate_RejectsOutOfRangeWorker()
    {
        WorldRegistration registration = new()
        {
            Identifier = "test",
            AllowedWorkers = [4]
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldRegistration.Validate(registration, workerCount: 4));
    }

    [Fact]
    public void WorldRegistrationDefaults_ForWorld_UsesAllWorkersWhenUnset()
    {
        WorldRegistration registration = WorldRegistrationDefaults.ForWorld(
            identifier: "island",
            properties: new Basalt.Server.Properties { WorldThreadCount = 4 });

        Assert.Equal("island", registration.Identifier);
        Assert.Equal([0, 1, 2, 3], registration.AllowedWorkers);
    }

    [Fact]
    public void WorldRegistrationDefaults_ForWorld_ParsesConfiguredWorkers()
    {
        WorldRegistration registration = WorldRegistrationDefaults.ForWorld(
            identifier: "hub",
            properties: new Basalt.Server.Properties
            {
                WorldThreadCount = 4,
                DefaultAllowedWorkers = "0"
            });

        Assert.Equal([0], registration.AllowedWorkers);
    }

    [Fact]
    public void WorldRegistrationLoader_LoadsFromWorldJson()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "basalt-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string jsonPath = Path.Combine(tempDir, "world.json");
            File.WriteAllText(jsonPath, """
                {
                  "identifier": "island_042",
                  "allowedWorkers": [1, 2],
                  "maxConcurrentPlayers": 4
                }
                """);

            WorldRegistration registration = WorldRegistrationLoader.LoadFromFile(jsonPath, "island_042", workerCount: 4);

            Assert.Equal("island_042", registration.Identifier);
            Assert.Equal([1, 2], registration.AllowedWorkers);
            Assert.Equal(4, registration.MaxConcurrentPlayers);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
