namespace Basalt.Server.Player;

/// <summary>
/// Optional state to merge from the source world during cross-world transfer.
/// By default nothing is carried; the target world's LevelDB save is used.
/// </summary>
[Flags]
public enum TransferCarryFlags
{
    None = 0,
    Inventory = 1,
    Position = 2
}
