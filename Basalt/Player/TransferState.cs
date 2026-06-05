namespace Basalt.Server.Player;

/// <summary>
/// Session state during cross-worker transfers (Phase 4+).
/// </summary>
public enum TransferState
{
    Idle,
    Transferring
}
