using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MankindRenewal.Combat.Actions;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat;

public partial class CombatActionController : Node, ICombatTargetSelectionHandler
{
    [Export] public NodePath TurnManagerPath { get; set; } = new();
    [Export] public NodePath CombatControllerPath { get; set; } = new();
    [Export] public NodePath CameraPath { get; set; } = new();
    [Export] public NodePath TargetMarkerPath { get; set; } = new();
    [Export(PropertyHint.Range, "10,200,1")] public float ScreenSelectionRadius { get; set; } = 55.0f;

    public bool IsTargetSelectionActive { get; private set; }
    public TacticalUnit? SelectedTarget { get; private set; }
    public CombatAttackAction? CurrentAction { get; private set; }

    public event Action? StateChanged;

    private TurnManager _turnManager = null!;
    private CombatModeController _combatController = null!;
    private Camera3D _camera = null!;
    private Node3D? _targetMarker;
    private AttackActionPipeline _pipeline = null!;

    public override void _Ready()
    {
        _turnManager = GetNode<TurnManager>(TurnManagerPath);
        _combatController = GetNode<CombatModeController>(CombatControllerPath);
        _camera = GetNode<Camera3D>(CameraPath);
        _targetMarker = GetNodeOrNull<Node3D>(TargetMarkerPath);
        _pipeline = new AttackActionPipeline(_turnManager, NeutralizeUnit);
        _turnManager.TurnStarted += OnTurnStarted;
        _turnManager.CombatEnded += OnCombatEnded;
        SetMarkerVisible(false);
    }

    public override void _ExitTree()
    {
        if (_turnManager is null)
            return;
        _turnManager.TurnStarted -= OnTurnStarted;
        _turnManager.CombatEnded -= OnCombatEnded;
    }

    public override void _Process(double delta)
    {
        if (_targetMarker?.Visible == true && SelectedTarget is not null)
            _targetMarker.GlobalPosition = SelectedTarget.Actor.GlobalPosition - Vector3.Up * (SelectedTarget.PlayerCenterHeight - 0.06f);
    }

    public bool BeginAttackSelection()
    {
        TacticalUnit? active = _turnManager.ActiveUnit;
        if (!_combatController.IsCombatActive || active is null || active.IsMoving || active.IsNeutralized || active.GetActiveWeapon() is null)
            return false;
        if (CurrentAction?.Phase == AttackActionPhase.AwaitingReaction)
            return false;
        CurrentAction = null;
        SelectedTarget = null;
        IsTargetSelectionActive = true;
        SetMarkerVisible(false);
        StateChanged?.Invoke();
        return true;
    }

    public void CancelAttackSelection()
    {
        if (CurrentAction?.Phase == AttackActionPhase.AwaitingReaction)
            return;
        IsTargetSelectionActive = false;
        SelectedTarget = null;
        CurrentAction = null;
        SetMarkerVisible(false);
        StateChanged?.Invoke();
    }

    public bool SelectTarget(TacticalUnit target)
    {
        if (!IsTargetSelectionActive || target == _turnManager.ActiveUnit)
            return false;
        SelectedTarget = target;
        SetMarkerVisible(!target.IsNeutralized);
        StateChanged?.Invoke();
        return true;
    }

    public bool SelectTargetByName(string displayName)
    {
        TacticalUnit? target = GetAllUnits().FirstOrDefault(unit => unit.UnitDisplayName == displayName);
        return target is not null && SelectTarget(target);
    }

    public bool TrySelectTargetFromScreen(Vector2 screenPosition)
    {
        if (!IsTargetSelectionActive || CurrentAction?.Phase == AttackActionPhase.AwaitingReaction)
            return false;
        TacticalUnit? closest = null;
        float bestDistance = ScreenSelectionRadius;
        foreach (TacticalUnit unit in GetAllUnits())
        {
            if (unit == _turnManager.ActiveUnit || unit.IsNeutralized || !unit.Actor.Visible || _camera.IsPositionBehind(unit.Actor.GlobalPosition))
                continue;
            float distance = _camera.UnprojectPosition(unit.Actor.GlobalPosition).DistanceTo(screenPosition);
            if (distance >= bestDistance)
                continue;
            closest = unit;
            bestDistance = distance;
        }
        return closest is not null && SelectTarget(closest);
    }

    public bool DeclareSelectedAttack()
    {
        TacticalUnit? attacker = _turnManager.ActiveUnit;
        WeaponDefinition? weapon = attacker?.GetActiveWeapon();
        if (!IsTargetSelectionActive || attacker is null || SelectedTarget is null || weapon is null)
            return false;
        CurrentAction = _pipeline.Declare(attacker, SelectedTarget, weapon);
        if (CurrentAction is null)
        {
            StateChanged?.Invoke();
            return false;
        }
        if (CurrentAction.Phase != AttackActionPhase.AwaitingReaction)
            IsTargetSelectionActive = false;
        StateChanged?.Invoke();
        return true;
    }

    public bool AcceptReaction() => ResolveReaction(true);
    public bool RefuseReaction() => ResolveReaction(false);

    public AttackValidationStatus GetSelectedTargetStatus()
    {
        TacticalUnit? active = _turnManager.ActiveUnit;
        return _pipeline.Validate(active, SelectedTarget, active?.GetActiveWeapon());
    }

    public string GetSelectedTargetStatusText() => GetStatusText(GetSelectedTargetStatus());
    public string GetSelectedTargetName() => SelectedTarget?.UnitDisplayName ?? string.Empty;
    public int GetSelectedDistanceInCells() => SelectedTarget is null || _turnManager.ActiveUnit is null
        ? -1
        : AttackActionPipeline.GetTacticalDistance(_turnManager.ActiveUnit, SelectedTarget);
    public bool GetIsTargetSelectionActive() => IsTargetSelectionActive;
    public bool GetHasPendingReaction() => CurrentAction?.Phase == AttackActionPhase.AwaitingReaction;
    public long GetCurrentActionId() => CurrentAction?.ActionId ?? 0;
    public int GetCurrentActionPhaseValue() => (int)(CurrentAction?.Phase ?? AttackActionPhase.Created);
    public string GetCurrentOutcomeText() => CurrentAction?.Outcome.ToString().ToUpperInvariant() ?? "-";
    public string GetOfferedReactionText() => CurrentAction?.OfferedReaction.ToString().ToUpperInvariant() ?? "NONE";
    public int GetLastAppliedDamage() => CurrentAction?.AppliedDamage ?? 0;
    public bool GetLastActionWasLaunched() => CurrentAction?.WasLaunched ?? false;
    public bool GetLastActionCommittedCost() => CurrentAction?.WasCostCommitted ?? false;
    public bool GetLastReactionWasRefused() => CurrentAction?.ReactionWasRefused ?? false;
    public int GetUnitCount() => GetAllUnits().Count;

    private bool ResolveReaction(bool accept)
    {
        if (CurrentAction is null || !_pipeline.ResolveReaction(CurrentAction, accept))
            return false;
        IsTargetSelectionActive = false;
        SetMarkerVisible(false);
        StateChanged?.Invoke();
        return true;
    }

    private List<TacticalUnit> GetAllUnits()
    {
        return GetTree().GetNodesInGroup("tactical_units").OfType<TacticalUnit>().OrderBy(unit => unit.UnitDisplayName).ToList();
    }

    private void NeutralizeUnit(TacticalUnit unit)
    {
        if (unit.IsCombatActive)
            _combatController.RemoveUnitFromCombat(unit);
    }

    private void OnTurnStarted(TacticalUnit unit)
    {
        IsTargetSelectionActive = false;
        SelectedTarget = null;
        CurrentAction = null;
        SetMarkerVisible(false);
        StateChanged?.Invoke();
    }

    private void OnCombatEnded() => OnTurnStarted(null!);

    private void SetMarkerVisible(bool visible)
    {
        if (_targetMarker is not null)
            _targetMarker.Visible = visible;
    }

    private static string GetStatusText(AttackValidationStatus status)
    {
        return status switch
        {
            AttackValidationStatus.Valid => "VALIDE",
            AttackValidationStatus.NoActiveUnit => "AUCUNE UNITE ACTIVE",
            AttackValidationStatus.NoWeapon => "AUCUNE ARME",
            AttackValidationStatus.InvalidTarget => "CIBLE INVALIDE",
            AttackValidationStatus.AlliedTarget => "ALLIEE",
            AttackValidationStatus.NeutralizedTarget => "NEUTRALISEE",
            AttackValidationStatus.OutOfRange => "HORS DE PORTEE",
            AttackValidationStatus.InsufficientActionPoints => "PA INSUFFISANTS",
            AttackValidationStatus.AttackerBusy => "ATTAQUANT EN MOUVEMENT",
            AttackValidationStatus.LineOfFireBlocked => "LINE OF FIRE BLOQUEE",
            _ => status.ToString().ToUpperInvariant(),
        };
    }
}
