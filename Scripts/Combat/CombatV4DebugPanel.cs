using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using MankindRenewal.Combat.Actions;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat;

public partial class CombatV4DebugPanel : PanelContainer
{
    [Export] public NodePath TurnManagerPath { get; set; } = new();
    [Export] public NodePath CombatControllerPath { get; set; } = new();
    [Export] public NodePath ActionControllerPath { get; set; } = new();

    private TurnManager _turnManager = null!;
    private CombatModeController _combatController = null!;
    private CombatV4ActionController _actions = null!;
    private Label _status = null!;
    private Label _targetStatus = null!;
    private Label _reactionStatus = null!;
    private Label _movementStatus = null!;
    private OptionButton _targetSelector = null!;
    private Button _attackButton = null!;
    private Button _declareButton = null!;
    private Button _choiceOne = null!;
    private Button _choiceTwo = null!;
    private Button _refuse = null!;
    private VBoxContainer _reactionBox = null!;
    private HBoxContainer _movementRow = null!;
    private readonly List<TacticalUnit> _targets = new();
    private string _lastActiveName = string.Empty;
    private double _refreshCountdown;

    public override void _Ready()
    {
        _turnManager = GetNode<TurnManager>(TurnManagerPath);
        _combatController = GetNode<CombatModeController>(CombatControllerPath);
        _actions = GetNode<CombatV4ActionController>(ActionControllerPath);
        _status = GetNode<Label>("Margin/VBox/Status");
        _targetStatus = GetNode<Label>("Margin/VBox/TargetStatus");
        _reactionBox = GetNode<VBoxContainer>("Margin/VBox/ReactionBox");
        _reactionStatus = GetNode<Label>("Margin/VBox/ReactionBox/ReactionStatus");
        _choiceOne = GetNode<Button>("Margin/VBox/ReactionBox/ChoiceRow/ChoiceOne");
        _choiceTwo = GetNode<Button>("Margin/VBox/ReactionBox/ChoiceRow/ChoiceTwo");
        _refuse = GetNode<Button>("Margin/VBox/ReactionBox/Refuse");
        _movementStatus = GetNode<Label>("Margin/VBox/MovementStatus");
        _movementRow = GetNode<HBoxContainer>("Margin/VBox/MovementRow");
        _targetSelector = GetNode<OptionButton>("Margin/VBox/AttackBox/TargetSelector");
        _attackButton = GetNode<Button>("Margin/VBox/AttackBox/AttackRow/Attack");
        _declareButton = GetNode<Button>("Margin/VBox/AttackBox/AttackRow/Declare");

        _attackButton.Pressed += OnAttack;
        _declareButton.Pressed += OnDeclare;
        _targetSelector.ItemSelected += OnTargetSelected;
        _choiceOne.Pressed += () => ChooseReaction(0);
        _choiceTwo.Pressed += () => ChooseReaction(1);
        _refuse.Pressed += OnRefuse;
        GetNode<Button>("Margin/VBox/MovementRow/Continue").Pressed += () => _actions.ContinueMovement();
        GetNode<Button>("Margin/VBox/MovementRow/Modify").Pressed += () => _actions.BeginModifyMovement();
        GetNode<Button>("Margin/VBox/MovementRow/Stop").Pressed += () => _actions.StopMovement();
        GetNode<Button>("Margin/VBox/EndTurn").Pressed += () => _combatController.RequestEndTurn();
        _actions.StateChanged += RefreshDisplay;
        RefreshTargets();
        RefreshDisplay();
    }

    public override void _ExitTree()
    {
        if (_actions is not null)
            _actions.StateChanged -= RefreshDisplay;
    }

    public override void _Process(double delta)
    {
        Visible = _combatController.IsCombatActive;
        _refreshCountdown -= delta;
        if (_refreshCountdown > 0.0)
            return;
        _refreshCountdown = 0.1;
        string activeName = _turnManager.ActiveUnit?.UnitDisplayName ?? string.Empty;
        if (activeName != _lastActiveName)
        {
            _lastActiveName = activeName;
            RefreshTargets();
        }
        RefreshDisplay();
    }

    public string GetStatusText() => _status.Text;
    public string GetReactionStatusText() => _reactionStatus.Text;
    public string GetMovementStatusText() => _movementStatus.Text;
    public bool GetReactionControlsVisible() => _reactionBox.Visible;
    public bool GetMovementControlsVisible() => _movementRow.Visible;

    private void RefreshDisplay()
    {
        TacticalUnit? active = _turnManager.ActiveUnit;
        WeaponDefinition? weapon = active?.GetActiveWeapon();
        StringBuilder text = new();
        text.AppendLine($"ROUND : {_turnManager.RoundNumber}");
        text.AppendLine($"UNITE ACTIVE : {active?.UnitDisplayName ?? "-"}");
        text.AppendLine($"INITIATIVE : {active?.Initiative ?? 0}");
        text.AppendLine($"PV : {active?.CurrentHealth ?? 0} / {active?.MaxHealth ?? 0}");
        text.AppendLine($"PA : {active?.CurrentActionPoints ?? 0} / {active?.MaxActionPoints ?? 0}");
        text.AppendLine($"PM : {active?.CurrentMovementPoints ?? 0} / {active?.MaxMovementPoints ?? 0}");
        text.AppendLine($"ARME : {weapon?.DisplayName ?? "-"}");
        text.AppendLine("ORDRE DES TOURS :");
        text.Append(_turnManager.GetDebugOrderText());
        _status.Text = text.ToString();

        TacticalUnit? target = _actions.SelectedTarget;
        _targetStatus.Text = target is null
            ? "CIBLE : -"
            : $"CIBLE : {target.UnitDisplayName} | DISTANCE : {_actions.GetSelectedDistanceInCells()} | {_actions.GetSelectedTargetStatus()}";

        bool offensive = _actions.GetHasOffensiveOpportunity();
        bool defensive = _actions.GetHasPendingDefensiveReaction();
        _reactionBox.Visible = offensive || defensive;
        if (offensive)
        {
            _reactionStatus.Text = $"REACTION — ACTION #{_actions.GetCurrentActionId()}\n" +
                $"UNITE REACTIVE : {_actions.GetPendingReactorName()} | INIT {_actions.GetPendingReactorInitiative()}";
            ConfigureChoice(_choiceOne, 0);
            ConfigureChoice(_choiceTwo, 1);
            _refuse.Text = "REFUSER POUR CETTE ACTION";
        }
        else if (defensive)
        {
            _reactionStatus.Text = $"REACTION DEFENSIVE V3 — ACTION #{_actions.GetCurrentActionId()}\n{_actions.GetDefensiveReactionText()}";
            _choiceOne.Visible = true;
            _choiceOne.Text = _actions.GetDefensiveReactionText() == "PARRY" ? "PARER" : "ESQUIVER";
            _choiceTwo.Visible = false;
            _refuse.Text = "REFUSER";
        }

        _movementRow.Visible = _actions.GetIsAwaitingMovementChoice();
        _movementStatus.Text = _actions.GetIsAwaitingModifiedDestination()
            ? $"ACTION MOUVEMENT #{_actions.GetCurrentMovementActionId()} — CLIQUEZ UNE NOUVELLE DESTINATION"
            : (_actions.GetIsAwaitingMovementChoice()
                ? $"ACTION MOUVEMENT #{_actions.GetCurrentMovementActionId()} SUSPENDUE"
                : _actions.GetLastDecisionText());

        bool locked = _actions.GetHasPendingReaction() || _actions.GetIsAwaitingMovementChoice();
        _attackButton.Disabled = active is null || active.IsMoving || active.GetActiveWeapon() is null || locked;
        _declareButton.Disabled = !_actions.IsTargetSelectionActive || target is null || _actions.GetSelectedTargetStatus() != AttackValidationStatus.Valid || locked;
        GetNode<Button>("Margin/VBox/EndTurn").Disabled = locked || active?.IsMoving == true;
    }

    private void ConfigureChoice(Button button, int index)
    {
        button.Visible = index < _actions.GetReactionChoiceCount();
        if (button.Visible)
            button.Text = _actions.GetReactionChoiceName(index);
    }

    private void RefreshTargets()
    {
        string selected = _actions.SelectedTarget?.UnitDisplayName ?? string.Empty;
        _targets.Clear();
        _targets.AddRange(GetTree().GetNodesInGroup("tactical_units").OfType<TacticalUnit>()
            .Where(unit => unit != _turnManager.ActiveUnit).OrderBy(unit => unit.UnitDisplayName));
        _targetSelector.Clear();
        foreach (TacticalUnit unit in _targets)
            _targetSelector.AddItem(unit.UnitDisplayName);
        int index = _targets.FindIndex(unit => unit.UnitDisplayName == selected);
        if (index >= 0)
            _targetSelector.Select(index);
    }

    private void OnAttack()
    {
        if (_actions.BeginAttackSelection())
            RefreshTargets();
    }

    private void OnDeclare() => _actions.DeclareSelectedAttack();

    private void OnTargetSelected(long index)
    {
        if (index >= 0 && index < _targets.Count)
            _actions.SelectTarget(_targets[(int)index]);
    }

    private void ChooseReaction(int index)
    {
        if (_actions.GetHasOffensiveOpportunity())
            _actions.ChooseReaction(index);
        else
            _actions.AcceptDefensiveReaction();
    }

    private void OnRefuse()
    {
        if (_actions.GetHasOffensiveOpportunity())
            _actions.RefuseReaction();
        else
            _actions.RefuseDefensiveReaction();
    }
}
