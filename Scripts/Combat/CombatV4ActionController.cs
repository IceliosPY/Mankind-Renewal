using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MankindRenewal.Combat.Actions;
using MankindRenewal.Combat.Reactions;
using MankindRenewal.Combat.Weapons;
using MankindRenewal.Equipment;
using MankindRenewal.Items;

namespace MankindRenewal.Combat;

public partial class CombatV4ActionController : Node, ICombatTargetSelectionHandler, ICombatMovementActionHandler, IEquipmentOperationLock
{
    [Export] public NodePath TurnManagerPath { get; set; } = new();
    [Export] public NodePath CombatControllerPath { get; set; } = new();
    [Export] public NodePath CameraPath { get; set; } = new();
    [Export] public NodePath TargetMarkerPath { get; set; } = new();
    [Export] public NodePath AttackRulesPath { get; set; } = new();
    [Export(PropertyHint.Range, "10,200,1")] public float ScreenSelectionRadius { get; set; } = 55.0f;
    [Export] public ulong ReactionOrderSeed { get; set; } = 8404;

    public bool IsTargetSelectionActive { get; private set; }
    public TacticalUnit? SelectedTarget { get; private set; }
    public CombatActionContext? CurrentActionContext { get; private set; }
    public CombatAttackAction? CurrentAttack { get; private set; }
    public ReactionOpportunity? PendingOpportunity { get; private set; }

    public event Action? StateChanged;

    private TurnManager _turnManager = null!;
    private CombatModeController _combatController = null!;
    private Camera3D _camera = null!;
    private Node3D? _targetMarker;
    private AttackActionPipeline _attackPipeline = null!;
    private ICombatAttackRules? _attackRules;
    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<long, HashSet<TacticalUnit>> _closedReactors = new();
    private readonly Dictionary<(TacticalUnit Unit, string ReactionId), int> _roundUses = new();
    private readonly Dictionary<(long ActionId, TacticalUnit Unit), int> _offerCounts = new();
    private CombatActionContext? _interruptedContext;
    private CombatActionContext? _movementContext;
    private ReactionTriggerType _pendingTrigger;
    private bool _acceptedDuringInterruption;
    private bool _movementPausedForReaction;
    private bool _movementInProgress;
    private bool _awaitingMovementChoice;
    private bool _isResolvingReaction;
    private ResumeRequest _resumeRequest;
    private long _lastCompletedMovementActionId;
    private long _lastReactionActionId;
    private int _lastReactionDamage;
    private string _lastReactionName = string.Empty;
    private string _lastDecisionText = string.Empty;
    private bool _lastAttackCancelledBeforeLaunch;
    private bool _lastReactionOrderUsedTieBreaker;

    private enum ResumeRequest
    {
        None,
        Continue,
        Modify,
    }

    public override void _Ready()
    {
        _turnManager = GetNode<TurnManager>(TurnManagerPath);
        _combatController = GetNode<CombatModeController>(CombatControllerPath);
        _camera = GetNode<Camera3D>(CameraPath);
        _targetMarker = GetNodeOrNull<Node3D>(TargetMarkerPath);
        _attackRules = AttackRulesPath.IsEmpty ? null : GetNodeOrNull(AttackRulesPath) as ICombatAttackRules;
        _attackPipeline = new AttackActionPipeline(_turnManager, NeutralizeUnit, _attackRules);
        _random.Seed = ReactionOrderSeed == 0 ? 8404 : ReactionOrderSeed;
        _turnManager.RoundStarted += OnRoundStarted;
        _turnManager.TurnStarted += OnTurnStarted;
        _turnManager.CombatEnded += OnCombatEnded;
        AddToGroup("equipment_operation_locks");
        SetMarkerVisible(false);
    }

    public override void _ExitTree()
    {
        if (_turnManager is null)
            return;
        _turnManager.RoundStarted -= OnRoundStarted;
        _turnManager.TurnStarted -= OnTurnStarted;
        _turnManager.CombatEnded -= OnCombatEnded;
        CurrentActionContext = null;
        CurrentAttack = null;
        PendingOpportunity = null;
        SelectedTarget = null;
        _interruptedContext = null;
        _movementContext = null;
        _closedReactors.Clear();
        _roundUses.Clear();
        _offerCounts.Clear();
        _attackRules = null;
        _attackPipeline = null!;
        _targetMarker = null;
        _camera = null!;
        _combatController = null!;
        _turnManager = null!;
    }

    public override void _Process(double delta)
    {
        if (_targetMarker?.Visible == true && SelectedTarget is not null)
            _targetMarker.GlobalPosition = SelectedTarget.Actor.GlobalPosition - Vector3.Up * (SelectedTarget.PlayerCenterHeight - 0.06f);
    }

    public bool BeginAttackSelection()
    {
        TacticalUnit? active = _turnManager.ActiveUnit;
        if (!_combatController.IsCombatActive || active is null || active.IsMoving || active.IsNeutralized
            || active.GetActiveWeapon() is null || IsInteractionLocked() || _awaitingMovementChoice || _movementContext is not null)
            return false;
        CurrentActionContext = null;
        CurrentAttack = null;
        SelectedTarget = null;
        IsTargetSelectionActive = true;
        _lastAttackCancelledBeforeLaunch = false;
        SetMarkerVisible(false);
        NotifyStateChanged();
        return true;
    }

    public void CancelAttackSelection()
    {
        if (IsInteractionLocked())
            return;
        IsTargetSelectionActive = false;
        SelectedTarget = null;
        CurrentActionContext = null;
        CurrentAttack = null;
        SetMarkerVisible(false);
        NotifyStateChanged();
    }

    public bool SelectTarget(TacticalUnit target)
    {
        if (!IsTargetSelectionActive || target == _turnManager.ActiveUnit)
            return false;
        SelectedTarget = target;
        SetMarkerVisible(!target.IsNeutralized);
        NotifyStateChanged();
        return true;
    }

    public bool SelectTargetByName(string displayName)
    {
        TacticalUnit? target = GetAllUnits().FirstOrDefault(unit => unit.UnitDisplayName == displayName);
        return target is not null && SelectTarget(target);
    }

    public bool TrySelectTargetFromScreen(Vector2 screenPosition)
    {
        if (!IsTargetSelectionActive || IsInteractionLocked())
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
        if (!IsTargetSelectionActive || attacker is null || SelectedTarget is null || weapon is null
            || _attackPipeline.Validate(attacker, SelectedTarget, weapon) != AttackValidationStatus.Valid)
            return false;

        ItemInstance? weaponInstance = attacker.GetActiveWeaponInstance();
        CurrentActionContext = CombatActionContext.NormalAttack(attacker, SelectedTarget, weaponInstance, weapon);
        _interruptedContext = CurrentActionContext;
        _pendingTrigger = ReactionTriggerType.NormalAttackDeclared;
        _acceptedDuringInterruption = false;
        if (!OfferNextOpportunity())
            LaunchDeclaredAttack();
        NotifyStateChanged();
        return true;
    }

    public AttackValidationStatus GetSelectedTargetStatus()
    {
        TacticalUnit? active = _turnManager.ActiveUnit;
        return _attackPipeline.Validate(active, SelectedTarget, active?.GetActiveWeapon());
    }

    public bool ChooseReaction(int choiceIndex)
    {
        if (PendingOpportunity is null || choiceIndex < 0 || choiceIndex >= PendingOpportunity.Choices.Count)
            return false;

        ReactionOpportunity opportunity = PendingOpportunity;
        ReactionDefinition definition = opportunity.Choices[choiceIndex];
        CloseReactor(opportunity.SourceAction.ActionId, opportunity.Reactor);
        _roundUses[(opportunity.Reactor, definition.ReactionId)] = GetRoundUseCount(opportunity.Reactor, definition) + 1;
        PendingOpportunity = null;
        _acceptedDuringInterruption = true;
        _lastDecisionText = $"{opportunity.Reactor.UnitDisplayName} accepte {definition.DisplayName}";
        _lastReactionName = definition.DisplayName;

        WeaponDefinition? weapon = opportunity.Reactor.GetActiveWeapon();
        ItemInstance? instance = opportunity.Reactor.GetActiveWeaponInstance();
        if (weapon is not null)
        {
            CombatActionContext reaction = new()
            {
                Kind = CombatActionKind.ReactionAttack,
                Source = opportunity.Reactor,
                Target = opportunity.SourceAction.Source,
                WeaponInstance = instance,
                Weapon = weapon,
                PlannedActionPointCost = 0,
                CanTriggerReactions = false,
            };
            _lastReactionActionId = reaction.ActionId;
            _isResolvingReaction = true;
            _attackPipeline.ResolveImmediateReactionAttack(reaction);
            _isResolvingReaction = false;
            _lastReactionDamage = reaction.AppliedDamage;
        }

        AdvanceAfterDecision();
        return true;
    }

    public bool RefuseReaction()
    {
        if (PendingOpportunity is null)
            return false;
        ReactionOpportunity opportunity = PendingOpportunity;
        CloseReactor(opportunity.SourceAction.ActionId, opportunity.Reactor);
        PendingOpportunity = null;
        _lastDecisionText = $"{opportunity.Reactor.UnitDisplayName} refuse pour l'action #{opportunity.SourceAction.ActionId}";
        AdvanceAfterDecision();
        return true;
    }

    public bool AcceptDefensiveReaction() => ResolveDefensiveReaction(true);
    public bool RefuseDefensiveReaction() => ResolveDefensiveReaction(false);

    public bool CanStartMovement(TacticalUnit unit, TacticalCell destination)
    {
        if (IsInteractionLocked())
            return false;
        if (_movementContext is null)
            return !_awaitingMovementChoice;
        return _awaitingMovementChoice && _resumeRequest != ResumeRequest.None && _movementContext.Source == unit;
    }

    public void OnMovementStarted(TacticalUnit unit, TacticalCell destination)
    {
        if (_movementContext is null)
            _movementContext = CombatActionContext.NormalMovement(unit, destination.CellId);
        else
            _movementContext.DestinationCellId = destination.CellId;
        CurrentActionContext = _movementContext;
        _movementInProgress = true;
        _movementPausedForReaction = false;
        _awaitingMovementChoice = false;
        _resumeRequest = ResumeRequest.None;
        NotifyStateChanged();
    }

    public bool OnMovementCellReached(TacticalUnit unit, TacticalCell cell)
    {
        if (_movementContext is null || _movementContext.Source != unit || !_movementContext.MovementTriggersEnabled)
            return false;
        _interruptedContext = _movementContext;
        _pendingTrigger = ReactionTriggerType.NormalMovementCellReached;
        _acceptedDuringInterruption = false;
        if (!OfferNextOpportunity())
            return false;
        _movementPausedForReaction = true;
        _movementInProgress = false;
        NotifyStateChanged();
        return true;
    }

    public void OnMovementPathCompleted(TacticalUnit unit)
    {
        if (_movementContext is null || _movementContext.Source != unit)
            return;
        _movementInProgress = false;
        if (_movementPausedForReaction)
            return;
        _lastCompletedMovementActionId = _movementContext.ActionId;
        _movementContext = null;
        _interruptedContext = null;
        CurrentActionContext = null;
        NotifyStateChanged();
    }

    public bool ContinueMovement()
    {
        if (!_awaitingMovementChoice || _movementContext is null || _movementContext.Source.IsNeutralized)
            return false;
        if (CompleteMovementIfDestinationReached())
        {
            NotifyStateChanged();
            return true;
        }
        _resumeRequest = ResumeRequest.Continue;
        bool result = _combatController.TrySelectDestinationCellId(_movementContext.DestinationCellId);
        if (!result)
            _resumeRequest = ResumeRequest.None;
        return result;
    }

    public bool BeginModifyMovement()
    {
        if (!_awaitingMovementChoice || _movementContext is null || _movementContext.Source.IsNeutralized)
            return false;
        _resumeRequest = ResumeRequest.Modify;
        _lastDecisionText = $"Action #{_movementContext.ActionId} : choisissez une nouvelle destination";
        NotifyStateChanged();
        return true;
    }

    public bool StopMovement()
    {
        if (!_awaitingMovementChoice || _movementContext is null)
            return false;
        _lastCompletedMovementActionId = _movementContext.ActionId;
        _lastDecisionText = $"Action #{_movementContext.ActionId} arretee, PM depenses conserves";
        _movementContext = null;
        _interruptedContext = null;
        CurrentActionContext = null;
        _awaitingMovementChoice = false;
        _movementPausedForReaction = false;
        _resumeRequest = ResumeRequest.None;
        NotifyStateChanged();
        return true;
    }

    public bool CanEndTurn(TacticalUnit? activeUnit) => !IsInteractionLocked() && !_awaitingMovementChoice && !_movementInProgress;

    public bool IsEquipmentOperationLocked(TacticalUnit owner) => IsInteractionLocked();

    public bool GetHasPendingReaction() => IsInteractionLocked();
    public bool GetHasOffensiveOpportunity() => PendingOpportunity is not null;
    public bool GetHasPendingDefensiveReaction() => CurrentAttack?.Phase == AttackActionPhase.AwaitingReaction;
    public bool GetIsAwaitingMovementChoice() => _awaitingMovementChoice;
    public bool GetIsAwaitingModifiedDestination() => _resumeRequest == ResumeRequest.Modify;
    public bool GetIsMovementPausedForReaction() => _movementPausedForReaction;
    public bool GetIsMovementInProgress() => _movementInProgress;
    public long GetCurrentActionId() => CurrentActionContext?.ActionId ?? _movementContext?.ActionId ?? 0;
    public long GetCurrentMovementActionId() => _movementContext?.ActionId ?? 0;
    public long GetLastCompletedMovementActionId() => _lastCompletedMovementActionId;
    public long GetLastReactionActionId() => _lastReactionActionId;
    public int GetLastReactionDamage() => _lastReactionDamage;
    public string GetLastReactionName() => _lastReactionName;
    public string GetLastDecisionText() => _lastDecisionText;
    public string GetPendingReactorName() => PendingOpportunity?.Reactor.UnitDisplayName ?? string.Empty;
    public int GetPendingReactorInitiative() => PendingOpportunity?.Reactor.Initiative ?? 0;
    public int GetReactionChoiceCount() => PendingOpportunity?.Choices.Count ?? 0;
    public string GetReactionChoiceId(int index) => PendingOpportunity is not null && index >= 0 && index < PendingOpportunity.Choices.Count ? PendingOpportunity.Choices[index].ReactionId : string.Empty;
    public string GetReactionChoiceName(int index) => PendingOpportunity is not null && index >= 0 && index < PendingOpportunity.Choices.Count ? PendingOpportunity.Choices[index].DisplayName : string.Empty;
    public bool GetLastAttackCancelledBeforeLaunch() => _lastAttackCancelledBeforeLaunch || CurrentActionContext?.WasCancelledBeforeLaunch == true;
    public bool GetLastAttackWasLaunched() => CurrentActionContext?.WasLaunched == true;
    public bool GetLastAttackCostCommitted() => CurrentActionContext?.WasCostCommitted == true;
    public int GetLastAttackDamage() => CurrentActionContext?.AppliedDamage ?? 0;
    public int GetSelectedDistanceInCells() => SelectedTarget is null || _turnManager.ActiveUnit is null ? -1 : AttackActionPipeline.GetTacticalDistance(_turnManager.ActiveUnit, SelectedTarget);
    public string GetSelectedTargetName() => SelectedTarget?.UnitDisplayName ?? string.Empty;
    public string GetCurrentOutcomeText() => CurrentAttack?.Outcome.ToString().ToUpperInvariant() ?? (_lastAttackCancelledBeforeLaunch ? "CANCELLED_BEFORE_LAUNCH" : "-");
    public string GetDefensiveReactionText() => CurrentAttack?.OfferedReaction.ToString().ToUpperInvariant() ?? "NONE";
    public string GetCurrentWeaponInstanceId() => CurrentActionContext?.WeaponInstance?.InstanceId ?? string.Empty;
    public string GetCurrentWeaponDefinitionId() => CurrentActionContext?.Weapon?.DefinitionId ?? string.Empty;
    public int GetOpportunityOfferCount(long actionId, string unitDisplayName)
    {
        TacticalUnit? unit = GetAllUnits().FirstOrDefault(candidate => candidate.UnitDisplayName == unitDisplayName);
        return unit is not null && _offerCounts.TryGetValue((actionId, unit), out int count) ? count : 0;
    }
    public bool GetLastReactionOrderUsedTieBreaker() => _lastReactionOrderUsedTieBreaker;

    private bool OfferNextOpportunity()
    {
        PendingOpportunity = BuildNextOpportunity();
        if (PendingOpportunity is not null)
        {
            (long, TacticalUnit) key = (PendingOpportunity.SourceAction.ActionId, PendingOpportunity.Reactor);
            _offerCounts[key] = _offerCounts.TryGetValue(key, out int count) ? count + 1 : 1;
        }
        return PendingOpportunity is not null;
    }

    private ReactionOpportunity? BuildNextOpportunity()
    {
        CombatActionContext? action = _interruptedContext;
        if (action is null || action.IsReaction || action.IsExplicitFreeAction || !action.CanTriggerReactions || action.Source.IsNeutralized || !action.Source.IsCombatActive)
            return null;
        if (_pendingTrigger == ReactionTriggerType.NormalMovementCellReached && (action.Kind != CombatActionKind.NormalMovement || !action.MovementTriggersEnabled))
            return null;
        if (_pendingTrigger == ReactionTriggerType.NormalAttackDeclared && action.Kind != CombatActionKind.NormalAttack)
            return null;

        HashSet<TacticalUnit> closed = GetClosedReactors(action.ActionId);
        List<(TacticalUnit Unit, List<ReactionDefinition> Choices, uint Tie)> candidates = new();
        foreach (IReactionProvider provider in GetReactionProviders())
        {
            TacticalUnit reactor = provider.OwnerUnit;
            if (closed.Contains(reactor) || reactor == action.Source || reactor.TeamId == action.Source.TeamId
                || reactor.IsNeutralized || !reactor.IsCombatActive)
                continue;
            WeaponDefinition? weapon = reactor.GetActiveWeapon();
            if (weapon is null || !weapon.IsValidDefinition())
                continue;
            int distance = AttackActionPipeline.GetTacticalDistance(reactor, action.Source);
            List<ReactionDefinition> choices = provider.GetReactionDefinitions()
                .Where(definition => IsDefinitionEligible(definition, reactor, weapon, distance))
                .ToList();
            if (choices.Count > 0)
                candidates.Add((reactor, choices, _random.Randi()));
        }

        int highestInitiative = candidates.Count == 0 ? int.MinValue : candidates.Max(candidate => candidate.Unit.Initiative);
        _lastReactionOrderUsedTieBreaker = candidates.Count(candidate => candidate.Unit.Initiative == highestInitiative) > 1;
        (TacticalUnit Unit, List<ReactionDefinition> Choices, uint Tie) selected = candidates
            .OrderByDescending(candidate => candidate.Unit.Initiative)
            .ThenBy(candidate => candidate.Tie)
            .FirstOrDefault();
        return selected.Unit is null ? null : new ReactionOpportunity(action, selected.Unit, selected.Choices);
    }

    private bool IsDefinitionEligible(ReactionDefinition definition, TacticalUnit reactor, WeaponDefinition weapon, int distance)
    {
        if (definition.Trigger != _pendingTrigger || !definition.IsValidDefinition() || definition.AllowsReactionChains)
            return false;
        if (definition.ActionPointCost != 0 || definition.MovementPointCost != 0 || definition.SpecialResourceCost != 0)
            return false;
        if (definition.MaximumUsesPerRound > 0 && GetRoundUseCount(reactor, definition) >= definition.MaximumUsesPerRound)
            return false;
        if (definition.WeaponRequirement == ReactionWeaponRequirement.Ranged && weapon.AttackType != WeaponAttackType.Ranged)
            return false;
        if (definition.WeaponRequirement == ReactionWeaponRequirement.Melee && weapon.AttackType != WeaponAttackType.Melee)
            return false;
        int maximumRange = definition.MaximumRangeInCells > 0
            ? Math.Min(definition.MaximumRangeInCells, weapon.RangeInCells)
            : weapon.RangeInCells;
        if (distance < 1 || distance > maximumRange)
            return false;
        return _attackRules?.EvaluateAttack(reactor, _interruptedContext!.Source, weapon).IsBlocked != true;
    }

    private void AdvanceAfterDecision()
    {
        if (OfferNextOpportunity())
        {
            NotifyStateChanged();
            return;
        }

        if (_interruptedContext?.Kind == CombatActionKind.NormalMovement)
        {
            CombatActionContext movement = _interruptedContext;
            if (movement.Source.IsNeutralized || !movement.Source.IsCombatActive || _turnManager.ActiveUnit != movement.Source)
            {
                _lastCompletedMovementActionId = movement.ActionId;
                _movementContext = null;
                CurrentActionContext = null;
                _interruptedContext = null;
                _movementPausedForReaction = false;
                _awaitingMovementChoice = false;
            }
            else if (_acceptedDuringInterruption)
            {
                _awaitingMovementChoice = true;
                _movementPausedForReaction = false;
                _lastDecisionText = $"Action #{movement.ActionId} suspendue : CONTINUER / MODIFIER / ARRETER";
            }
            else
            {
                if (!CompleteMovementIfDestinationReached())
                    CallDeferred(nameof(ResumeMovementAutomatically));
            }
        }
        else if (_interruptedContext?.Kind == CombatActionKind.NormalAttack)
        {
            LaunchDeclaredAttack();
        }
        NotifyStateChanged();
    }

    private void ResumeMovementAutomatically()
    {
        if (_movementContext is null || _movementContext.Source.IsNeutralized || _turnManager.ActiveUnit != _movementContext.Source)
            return;
        if (CompleteMovementIfDestinationReached())
        {
            NotifyStateChanged();
            return;
        }
        _movementPausedForReaction = false;
        _awaitingMovementChoice = true;
        _resumeRequest = ResumeRequest.Continue;
        if (!_combatController.TrySelectDestinationCellId(_movementContext.DestinationCellId))
        {
            _resumeRequest = ResumeRequest.None;
            _awaitingMovementChoice = true;
            _lastDecisionText = $"Action #{_movementContext.ActionId} : reprise impossible, choisissez MODIFIER ou ARRETER";
        }
        NotifyStateChanged();
    }

    private bool CompleteMovementIfDestinationReached()
    {
        if (_movementContext is null || _movementContext.Source.CurrentCell?.CellId != _movementContext.DestinationCellId)
            return false;

        _lastCompletedMovementActionId = _movementContext.ActionId;
        _lastDecisionText = $"Action #{_movementContext.ActionId} terminee sur la cellule de destination";
        _movementContext = null;
        _interruptedContext = null;
        CurrentActionContext = null;
        PendingOpportunity = null;
        _movementPausedForReaction = false;
        _movementInProgress = false;
        _awaitingMovementChoice = false;
        _resumeRequest = ResumeRequest.None;
        return true;
    }

    private void LaunchDeclaredAttack()
    {
        if (CurrentActionContext is null || CurrentActionContext.Kind != CombatActionKind.NormalAttack)
            return;
        PendingOpportunity = null;
        _interruptedContext = null;
        if (CurrentActionContext.Source.IsNeutralized || !CurrentActionContext.Source.IsCombatActive || _turnManager.ActiveUnit != CurrentActionContext.Source)
        {
            CurrentActionContext.WasCancelledBeforeLaunch = true;
            _lastAttackCancelledBeforeLaunch = true;
            IsTargetSelectionActive = false;
            SetMarkerVisible(false);
            return;
        }
        CurrentAttack = _attackPipeline.Declare(CurrentActionContext);
        if (CurrentAttack is null)
        {
            _lastAttackCancelledBeforeLaunch = true;
            IsTargetSelectionActive = false;
            SetMarkerVisible(false);
            return;
        }
        if (CurrentAttack.Phase != AttackActionPhase.AwaitingReaction)
        {
            IsTargetSelectionActive = false;
            SetMarkerVisible(false);
        }
    }

    private bool ResolveDefensiveReaction(bool accept)
    {
        if (CurrentActionContext is null || CurrentAttack is null
            || !_attackPipeline.ResolveReaction(CurrentActionContext, CurrentAttack, accept))
            return false;
        IsTargetSelectionActive = false;
        SetMarkerVisible(false);
        NotifyStateChanged();
        return true;
    }

    private bool IsInteractionLocked() => PendingOpportunity is not null || CurrentAttack?.Phase == AttackActionPhase.AwaitingReaction || _isResolvingReaction;

    private IEnumerable<IReactionProvider> GetReactionProviders()
    {
        return GetTree().GetNodesInGroup("reaction_providers").OfType<IReactionProvider>();
    }

    private List<TacticalUnit> GetAllUnits()
    {
        return GetTree().GetNodesInGroup("tactical_units").OfType<TacticalUnit>().OrderBy(unit => unit.UnitDisplayName).ToList();
    }

    private HashSet<TacticalUnit> GetClosedReactors(long actionId)
    {
        if (!_closedReactors.TryGetValue(actionId, out HashSet<TacticalUnit>? closed))
        {
            closed = new HashSet<TacticalUnit>();
            _closedReactors[actionId] = closed;
        }
        return closed;
    }

    private void CloseReactor(long actionId, TacticalUnit reactor) => GetClosedReactors(actionId).Add(reactor);
    private int GetRoundUseCount(TacticalUnit unit, ReactionDefinition definition) => _roundUses.TryGetValue((unit, definition.ReactionId), out int count) ? count : 0;

    private void NeutralizeUnit(TacticalUnit unit)
    {
        if (unit.IsCombatActive)
            _combatController.RemoveUnitFromCombat(unit);
    }

    private void OnRoundStarted(int round)
    {
        _roundUses.Clear();
        _closedReactors.Clear();
        _offerCounts.Clear();
    }

    private void OnTurnStarted(TacticalUnit unit)
    {
        if (_isResolvingReaction)
            return;
        ClearTransientState();
    }

    private void OnCombatEnded() => ClearTransientState();

    private void ClearTransientState()
    {
        IsTargetSelectionActive = false;
        SelectedTarget = null;
        CurrentActionContext = null;
        CurrentAttack = null;
        PendingOpportunity = null;
        _interruptedContext = null;
        _movementContext = null;
        _movementInProgress = false;
        _movementPausedForReaction = false;
        _awaitingMovementChoice = false;
        _resumeRequest = ResumeRequest.None;
        SetMarkerVisible(false);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    private void SetMarkerVisible(bool visible)
    {
        if (_targetMarker is not null)
            _targetMarker.Visible = visible;
    }
}
