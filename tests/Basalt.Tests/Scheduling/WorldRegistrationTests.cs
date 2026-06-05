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
    public void WorldRegistrationDefaults_Light_ReturnsExpectedWorkers()
    {
        WorldRegistration registration = WorldRegistrationDefaults.ForProfile(
            WorldProfile.Light,
            identifier: "island",
            workerCount: 4);

        Assert.Equal(WorldProfile.Light, registration.Profile);
        Assert.Equal("island", registration.Identifier);
        Assert.Equal([1, 2], registration.AllowedWorkers);
    }
}
