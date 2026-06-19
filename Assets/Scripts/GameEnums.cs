// ============================================================================
// GameEnums.cs — Shared enums for Robot Rescue Team
// All game-wide enumerations in one place to avoid circular dependencies.
// ============================================================================

/// <summary>
/// The four commands a player can queue for their robot.
/// </summary>
public enum CommandType
{
    MoveForward,
    TurnLeft,
    TurnRight,
    PickUp
}

/// <summary>
/// Cardinal direction the robot is facing on the grid.
/// North = +Z, East = +X, South = -Z, West = -X
/// </summary>
public enum FacingDirection
{
    North,  // +Z
    East,   // +X
    South,  // -Z
    West    // -X
}

/// <summary>
/// Type of each grid tile. Determines passability and interaction.
/// </summary>
public enum TileType
{
    Passable    = 0,
    Wall        = 1,
    Collectible = 2,
    Goal        = 3
}

/// <summary>
/// Server-authoritative game phase, synced via NetworkVariable.
/// </summary>
public enum GamePhase
{
    StartMenu,
    WaitingForPlayers,
    Planning,
    Executing,
    RoundResult,
    GameOver
}
