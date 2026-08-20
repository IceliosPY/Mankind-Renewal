using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace MankindRenewal.Combat;

public partial class CombatDebugPanel : PanelContainer
{
    [Export] public NodePath TurnManagerPath { get; set; } = new();
    [Export] public NodePath CombatControllerPath { get; set; } = new();
    [Export] public NodePath ReinforcementUnitPath { get; set; } = new();

    private TurnManager _turnManager = null!;
    private CombatModeController _controller = null!;
    private TacticalUnit _reinforcement = null!;
    private Label _statusLabel = null!;
    private OptionButton _initiativeTargetSelector = null!;
    private Label _initiativeTargetStatus = null!;
    private readonly List<TacticalUnit> _initiativeTargets = new();
    private TacticalUnit? _selectedInitiativeTarget;
    private double _refreshCountdown;

    public override void _Ready()
    {
        _turnManager = GetNode<TurnManager>(TurnManagerPath);
        _controller = GetNode<CombatModeController>(CombatControllerPath);
        _reinforcement = GetNode<TacticalUnit>(ReinforcementUnitPath);
        _statusLabel = GetNode<Label>("Margin/VBox/Status");
        _initiativeTargetSelector = GetNode<OptionButton>("Margin/VBox/SecondaryScroll/SecondaryControls/InitiativeTarget");
        _initiativeTargetStatus = GetNode<Label>("Margin/VBox/SecondaryScroll/SecondaryControls/InitiativeTargetStatus");

        GetNode<Button>("Margin/VBox/SecondaryScroll/SecondaryControls/SpendActionPoint").Pressed += OnSpendActionPoint;
        GetNode<Button>("Margin/VBox/EndTurn").Pressed += OnEndTurn;
        _initiativeTargetSelector.ItemSelected += OnInitiativeTargetSelected;
        GetNode<Button>("Margin/VBox/SecondaryScroll/SecondaryControls/InitiativeRow/IncreaseTarget").Pressed += OnIncreaseTargetInitiative;
        GetNode<Button>("Margin/VBox/SecondaryScroll/SecondaryControls/InitiativeRow/DecreaseTarget").Pressed += OnDecreaseTargetInitiative;
        GetNode<Button>("Margin/VBox/SecondaryScroll/SecondaryControls/ReinforcementRow/AddReinforcement").Pressed += OnAddReinforcement;
        GetNode<Button>("Margin/VBox/SecondaryScroll/SecondaryControls/ReinforcementRow/RemoveReinforcement").Pressed += OnRemoveReinforcement;
        RefreshInitiativeTargets();
        SetProcess(true);
        RefreshStatus();
    }

    public override void _Process(double delta)
    {
        Visible = _controller.GetIsCombatActive();
        _refreshCountdown -= delta;
        if (_refreshCountdown > 0.0)
            return;
        _refreshCountdown = 0.1;
        RefreshStatus();
    }

    public string GetStatusText() => _statusLabel.Text;

    public string GetSelectedInitiativeTargetName() => _selectedInitiativeTarget?.UnitDisplayName ?? string.Empty;

    public int GetSelectedInitiativeValue() => _selectedInitiativeTarget?.Initiative ?? 0;

    public string GetSelectedInitiativePositionText() => BuildSelectedTargetPositionText();

    public int GetInitiativeTargetCount() => _initiativeTargets.Count;

    public bool SelectInitiativeTargetByName(string displayName)
    {
        int index = _initiativeTargets.FindIndex(unit => unit.UnitDisplayName == displayName);
        if (index < 0)
            return false;
        _initiativeTargetSelector.Select(index);
        SetSelectedInitiativeTarget(index);
        return true;
    }

    public bool ModifySelectedInitiative(int delta)
    {
        if (_selectedInitiativeTarget is null)
            return false;
        _selectedInitiativeTarget.Initiative += delta;
        RefreshStatus();
        return true;
    }

    private void RefreshStatus()
    {
        TacticalUnit? active = _turnManager.ActiveUnit;
        StringBuilder text = new();
        text.AppendLine($"ROUND : {_turnManager.RoundNumber}");
        text.AppendLine($"UNITE ACTIVE : {active?.UnitDisplayName ?? "-"}");
        text.AppendLine($"INITIATIVE : {active?.Initiative ?? 0}");
        text.AppendLine($"PA : {active?.CurrentActionPoints ?? 0} / {active?.MaxActionPoints ?? 0}");
        text.AppendLine($"PM : {active?.CurrentMovementPoints ?? 0} / {active?.MaxMovementPoints ?? 0}");
        text.AppendLine();
        text.AppendLine("ORDRE DES TOURS :");
        text.Append(_turnManager.GetDebugOrderText());
        _statusLabel.Text = text.ToString();

        if (_selectedInitiativeTarget is null)
        {
            _initiativeTargetStatus.Text = "CIBLE : -";
            return;
        }
        _initiativeTargetStatus.Text =
            $"CIBLE INITIATIVE DEBUG : {_selectedInitiativeTarget.UnitDisplayName}\n" +
            $"Initiative : {_selectedInitiativeTarget.Initiative}\n" +
            $"Position : {BuildSelectedTargetPositionText()}";
    }

    private void RefreshInitiativeTargets()
    {
        string previousSelection = _selectedInitiativeTarget?.UnitDisplayName ?? "UNITE B";
        _initiativeTargets.Clear();
        _initiativeTargets.AddRange(
            GetTree().GetNodesInGroup("tactical_units")
                .OfType<TacticalUnit>()
                .OrderBy(unit => unit.UnitDisplayName));
        _initiativeTargetSelector.Clear();
        foreach (TacticalUnit unit in _initiativeTargets)
            _initiativeTargetSelector.AddItem(unit.UnitDisplayName);

        int selectedIndex = _initiativeTargets.FindIndex(unit => unit.UnitDisplayName == previousSelection);
        if (selectedIndex < 0 && _initiativeTargets.Count > 0)
            selectedIndex = 0;
        if (selectedIndex >= 0)
        {
            _initiativeTargetSelector.Select(selectedIndex);
            SetSelectedInitiativeTarget(selectedIndex);
        }
    }

    private void OnInitiativeTargetSelected(long index) => SetSelectedInitiativeTarget((int)index);

    private void SetSelectedInitiativeTarget(int index)
    {
        _selectedInitiativeTarget = index >= 0 && index < _initiativeTargets.Count
            ? _initiativeTargets[index]
            : null;
        RefreshStatus();
    }

    private string BuildSelectedTargetPositionText()
    {
        TacticalUnit? target = _selectedInitiativeTarget;
        if (target is null)
            return "-";
        if (target == _turnManager.ActiveUnit)
            return "1 (ACTIVE)";

        for (int index = 0; index < _turnManager.RemainingOrder.Count; index++)
        {
            if (_turnManager.RemainingOrder[index] != target)
                continue;
            int position = index + (_turnManager.ActiveUnit is null ? 1 : 2);
            return $"{position} (ORDRE RESTANT)";
        }

        if (target.HasActedThisRound && _turnManager.Participants.Contains(target))
            return "DEJA JOUE";
        return "HORS COMBAT";
    }

    private void OnSpendActionPoint() => _controller.SpendActiveActionPoint();
    private void OnEndTurn() => _controller.RequestEndTurn();
    private void OnIncreaseTargetInitiative() => ModifySelectedInitiative(10);
    private void OnDecreaseTargetInitiative() => ModifySelectedInitiative(-10);
    private void OnAddReinforcement() => _controller.AddUnitToCombat(_reinforcement);
    private void OnRemoveReinforcement() => _controller.RemoveUnitFromCombat(_reinforcement);
}
