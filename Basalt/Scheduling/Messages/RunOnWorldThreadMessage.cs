namespace Basalt.Server.Scheduling.Messages;

/// <summary>
/// Runs an action on the worker thread that owns a world.
/// </summary>
public sealed class RunOnWorldThreadMessage : IWorldMessage
{
    public required Action Action { get; init; }
    public TaskCompletionSource<object?>? Completion { get; init; }
}
