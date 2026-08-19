using System;
using Godot;

namespace MankindRenewal.Systems;

public enum GameMode
{
    Exploration,
    Combat,
}

public partial class GameModeManager : Node
{
    [Export]
    public GameMode InitialMode { get; set; } = GameMode.Exploration;

    public GameMode CurrentMode { get; private set; } = GameMode.Exploration;

    public event Action<GameMode>? ModeChanged;

    public override void _Ready()
    {
        SetMode(InitialMode);
    }

    public void SetMode(GameMode mode)
    {
        if (CurrentMode == mode)
            return;

        CurrentMode = mode;
        ModeChanged?.Invoke(CurrentMode);
    }

    public bool IsExploration() => CurrentMode == GameMode.Exploration;

    public void SetExplorationMode() => SetMode(GameMode.Exploration);

    public void SetCombatMode() => SetMode(GameMode.Combat);
}
