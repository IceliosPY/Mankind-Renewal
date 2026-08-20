using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using MankindRenewal.Combat.Actions;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat;

public partial class CombatV3DebugPanel : PanelContainer
{
    [Export] public NodePath TurnManagerPath { get; set; } = new();
    [Export] public NodePath CombatControllerPath { get; set; } = new();
    [Export] public NodePath ActionControllerPath { get; set; } = new();
    [Export] public NodePath ReinforcementUnitPath { get; set; } = new();

    private TurnManager _turnManager = null!;
    private CombatModeController _combatController = null!;
    private CombatActionController _actionController = null!;
    private TacticalUnit _reinforcement = null!;
    private Label _status = null!;
    private Label _targetStatus = null!;
    private VBoxContainer _reactionBox = null!;
    private Label _reactionLabel = null!;
    private OptionButton _targetSelector = null!;
    private OptionButton _initiativeSelector = null!;
    private Label _initiativeStatus = null!;
    private Button _attackButton = null!;
    private Button _declareButton = null!;
    private Button _endTurnButton = null!;
    private Button _acceptReactionButton = null!;
    private readonly List<TacticalUnit> _targets = new();
    private readonly List<TacticalUnit> _initiativeTargets = new();
    private TacticalUnit? _selectedInitiativeTarget;
    private string _lastActiveName = string.Empty;
    private double _refreshCountdown;

    public override void _Ready()
    {
        _turnManager = GetNode<TurnManager>(TurnManagerPath);
        _combatController = GetNode<CombatModeController>(CombatControllerPath);
        _actionController = GetNode<CombatActionController>(ActionControllerPath);
        _reinforcement = GetNode<TacticalUnit>(ReinforcementUnitPath);
        _status = GetNode<Label>("Margin/VBox/Status");
        _targetStatus = GetNode<Label>("Margin/VBox/TargetStatus");
        _reactionBox = GetNode<VBoxContainer>("Margin/VBox/ReactionBox");
        _reactionLabel = GetNode<Label>("Margin/VBox/ReactionBox/ReactionLabel");
        _targetSelector = GetNode<OptionButton>("Margin/VBox/TargetSelector");
        _initiativeSelector = GetNode<OptionButton>("Margin/VBox/SecondaryScroll/SecondaryControls/InitiativeTarget");
        _initiativeStatus = GetNode<Label>("Margin/VBox/SecondaryScroll/SecondaryControls/InitiativeStatus");
        _attackButton = GetNode<Button>("Margin/VBox/AttackRow/Attack");
        _declareButton = GetNode<Button>("Margin/VBox/AttackRow/Declare");
        _endTurnButton = GetNode<Button>("Margin/VBox/EndTurn");
        _acceptReactionButton = GetNode<Button>("Margin/VBox/ReactionBox/ReactionRow/Accept");

        _attackButton.Pressed += OnAttack;
        _declareButton.Pressed += OnDeclare;
        _endTurnButton.Pressed += OnEndTurn;
        _targetSelector.ItemSelected += OnTargetSelected;
        _acceptReactionButton.Pressed += OnAcceptReaction;
        GetNode<Button>("Margin/VBox/ReactionBox/ReactionRow/Refuse").Pressed += OnRefuseReaction;
        _initiativeSelector.ItemSelected += OnInitiativeSelected;
        GetNode<Button>("Margin/VBox/SecondaryScroll/SecondaryControls/InitiativeRow/Decrease").Pressed += () => ModifySelectedInitiative(-10);
        GetNode<Button>("Margin/VBox/SecondaryScroll/SecondaryControls/InitiativeRow/Increase").Pressed += () => ModifySelectedInitiative(10);
        GetNode<Button>("Margin/VBox/SecondaryScroll/SecondaryControls/SpendActionPoint").Pressed += () => _combatController.SpendActiveActionPoint();
        GetNode<Button>("Margin/VBox/SecondaryScroll/SecondaryControls/ReinforcementRow/Add").Pressed += OnAddReinforcement;
        GetNode<Button>("Margin/VBox/SecondaryScroll/SecondaryControls/ReinforcementRow/Remove").Pressed += () => _combatController.RemoveUnitFromCombat(_reinforcement);

        RefreshUnitSelectors(true);
        RefreshDisplay();
        SetProcess(true);
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
            RefreshUnitSelectors(false);
        }
        RefreshDisplay();
    }

    public string GetStatusText() => _status.Text;
    public string GetTargetStatusText() => _targetStatus.Text;
    public string GetReactionStatusText() => _reactionLabel.Text;
    public bool GetReactionControlsVisible() => _reactionBox.Visible;
    public int GetTargetCount() => _targets.Count;
    public int GetInitiativeTargetCount() => _initiativeTargets.Count;
    public string GetSelectedInitiativeTargetName() => _selectedInitiativeTarget?.UnitDisplayName ?? string.Empty;

    public bool SelectTargetByName(string displayName)
    {
        int index = _targets.FindIndex(unit => unit.UnitDisplayName == displayName);
        if (index < 0)
            return false;
        _targetSelector.Select(index);
        return _actionController.SelectTarget(_targets[index]);
    }

    public bool SelectInitiativeTargetByName(string displayName)
    {
        int index = _initiativeTargets.FindIndex(unit => unit.UnitDisplayName == displayName);
        if (index < 0)
            return false;
        _initiativeSelector.Select(index);
        _selectedInitiativeTarget = _initiativeTargets[index];
        RefreshDisplay();
        return true;
    }

    public bool ModifySelectedInitiative(int delta)
    {
        if (_selectedInitiativeTarget is null)
            return false;
        _turnManager.ModifyInitiative(_selectedInitiativeTarget, delta);
        RefreshDisplay();
        return true;
    }

    private void RefreshDisplay()
    {
        TacticalUnit? active = _turnManager.ActiveUnit;
        WeaponDefinition? weapon = active?.GetActiveWeapon();
        StringBuilder main = new();
        main.AppendLine($"ROUND : {_turnManager.RoundNumber}");
        main.AppendLine($"UNITE ACTIVE : {active?.UnitDisplayName ?? "-"}");
        main.AppendLine($"PV : {active?.CurrentHealth ?? 0} / {active?.MaxHealth ?? 0}");
        main.AppendLine($"INITIATIVE : {active?.Initiative ?? 0}");
        main.AppendLine($"PA : {active?.CurrentActionPoints ?? 0} / {active?.MaxActionPoints ?? 0}");
        main.AppendLine($"PM : {active?.CurrentMovementPoints ?? 0} / {active?.MaxMovementPoints ?? 0}");
        main.AppendLine($"ARME ACTIVE : {weapon?.DisplayName ?? "-"}   COUT : {weapon?.ActionPointCost ?? 0} PA");
        main.AppendLine($"PRECISION : {weapon?.BaseAccuracy ?? 0} | PORTEE : {weapon?.RangeInCells ?? 0} | DEGATS : {FormatDamageComponents(weapon)}");
        main.AppendLine("ORDRE DES TOURS :");
        main.Append(_turnManager.GetDebugOrderText());
        _status.Text = main.ToString();

        TacticalUnit? target = _actionController.SelectedTarget;
        int distance = _actionController.GetSelectedDistanceInCells();
        _targetStatus.Text = target is null
            ? "CIBLE : -\nSTATUT : selectionnez ATTAQUER puis une unite"
            : $"CIBLE : {target.UnitDisplayName}   PV : {target.CurrentHealth} / {target.MaxHealth}\n" +
              $"DISTANCE : {(distance < 0 ? "-" : distance)}   COUT : {weapon?.ActionPointCost ?? 0} PA\n" +
              $"DEFENSE DISPONIBLE : {GetAvailableDefenceText(target, weapon)}\n" +
              $"STATUT : {_actionController.GetSelectedTargetStatusText()}";

        bool awaitingReaction = _actionController.GetHasPendingReaction();
        _reactionBox.Visible = awaitingReaction;
        _reactionLabel.Text = awaitingReaction
            ? $"REACTION DISPONIBLE : {GetReactionUiName()}\nAction #{_actionController.GetCurrentActionId()} declaree — PA non depenses"
            : BuildLastOutcomeText();
        _acceptReactionButton.Text = GetReactionUiVerb();
        _attackButton.Disabled = active is null || active.IsMoving || active.GetActiveWeapon() is null || awaitingReaction;
        _declareButton.Disabled = !_actionController.IsTargetSelectionActive
            || target is null
            || _actionController.GetSelectedTargetStatus() != AttackValidationStatus.Valid
            || awaitingReaction;
        _endTurnButton.Disabled = awaitingReaction;

        _initiativeStatus.Text = _selectedInitiativeTarget is null
            ? "CIBLE INITIATIVE : -"
            : $"CIBLE INITIATIVE : {_selectedInitiativeTarget.UnitDisplayName}\nInitiative : {_selectedInitiativeTarget.Initiative}\nPosition : {GetInitiativePosition(_selectedInitiativeTarget)}";
    }

    private void RefreshUnitSelectors(bool initial)
    {
        string selectedTarget = _actionController.SelectedTarget?.UnitDisplayName ?? string.Empty;
        string selectedInitiative = _selectedInitiativeTarget?.UnitDisplayName ?? "UNITE B";
        List<TacticalUnit> all = GetTree().GetNodesInGroup("tactical_units").OfType<TacticalUnit>().OrderBy(unit => unit.UnitDisplayName).ToList();

        _targets.Clear();
        _targets.AddRange(all.Where(unit => unit != _turnManager.ActiveUnit));
        _targetSelector.Clear();
        foreach (TacticalUnit unit in _targets)
            _targetSelector.AddItem(unit.UnitDisplayName);
        int targetIndex = _targets.FindIndex(unit => unit.UnitDisplayName == selectedTarget);
        if (targetIndex >= 0)
            _targetSelector.Select(targetIndex);

        _initiativeTargets.Clear();
        _initiativeTargets.AddRange(all);
        _initiativeSelector.Clear();
        foreach (TacticalUnit unit in _initiativeTargets)
            _initiativeSelector.AddItem(unit.UnitDisplayName);
        int initiativeIndex = _initiativeTargets.FindIndex(unit => unit.UnitDisplayName == selectedInitiative);
        if (initiativeIndex < 0 && _initiativeTargets.Count > 0)
            initiativeIndex = 0;
        if (initiativeIndex >= 0)
        {
            _initiativeSelector.Select(initiativeIndex);
            _selectedInitiativeTarget = _initiativeTargets[initiativeIndex];
        }
    }

    private void OnAttack()
    {
        if (!_actionController.BeginAttackSelection())
            return;
        RefreshUnitSelectors(false);
        RefreshDisplay();
    }

    private void OnTargetSelected(long index)
    {
        if (index >= 0 && index < _targets.Count)
            _actionController.SelectTarget(_targets[(int)index]);
        RefreshDisplay();
    }

    private void OnInitiativeSelected(long index)
    {
        _selectedInitiativeTarget = index >= 0 && index < _initiativeTargets.Count ? _initiativeTargets[(int)index] : null;
        RefreshDisplay();
    }

    private void OnDeclare()
    {
        _actionController.DeclareSelectedAttack();
        RefreshDisplay();
    }

    private void OnAcceptReaction()
    {
        _actionController.AcceptReaction();
        RefreshDisplay();
    }

    private void OnRefuseReaction()
    {
        _actionController.RefuseReaction();
        RefreshDisplay();
    }

    private void OnEndTurn()
    {
        if (!_actionController.GetHasPendingReaction())
            _combatController.RequestEndTurn();
    }

    private void OnAddReinforcement()
    {
        if (_reinforcement.IsNeutralized)
            _reinforcement.RestoreFullHealth();
        _combatController.AddUnitToCombat(_reinforcement);
        RefreshUnitSelectors(false);
    }

    private string BuildLastOutcomeText()
    {
        if (_actionController.GetCurrentActionId() == 0)
            return string.Empty;
        return $"DERNIERE ACTION #{_actionController.GetCurrentActionId()} : {_actionController.GetCurrentOutcomeText()}\n" +
               $"Degats : {_actionController.GetLastAppliedDamage()}";
    }

    private string GetReactionUiName() => _actionController.GetOfferedReactionText() == "PARRY" ? "PARADE" : "ESQUIVE";

    private string GetReactionUiVerb() => _actionController.GetOfferedReactionText() == "PARRY" ? "PARER" : "ESQUIVER";

    private static string GetAvailableDefenceText(TacticalUnit target, WeaponDefinition? weapon)
    {
        if (weapon is null)
            return "AUCUNE";
        if (weapon.AttackType == WeaponAttackType.Ranged)
            return $"ESQUIVE ({target.GetEffectiveDodge()})";
        return target.GetActiveWeapon()?.AllowsParry == true
            ? $"PARADE ({target.GetEffectiveParry()})"
            : "AUCUNE";
    }

    private static string FormatDamageComponents(WeaponDefinition? weapon)
    {
        return weapon is null || weapon.DamageComponents.Count == 0
            ? "-"
            : string.Join(" + ", weapon.DamageComponents.Select(component => $"{component.Amount:0.##} {AbbreviateDamageType(component.Type)}"));
    }

    private static string AbbreviateDamageType(DamageType type) => type switch
    {
        DamageType.Ballistic => "BAL",
        DamageType.Energy => "ENE",
        DamageType.Thermal => "THR",
        DamageType.Electric => "ELE",
        DamageType.Explosive => "EXP",
        _ => type.ToString().ToUpperInvariant(),
    };

    private string GetInitiativePosition(TacticalUnit target)
    {
        if (target == _turnManager.ActiveUnit)
            return "1 (ACTIVE)";
        for (int index = 0; index < _turnManager.RemainingOrder.Count; index++)
        {
            if (_turnManager.RemainingOrder[index] == target)
                return $"{index + (_turnManager.ActiveUnit is null ? 1 : 2)} (ORDRE RESTANT)";
        }
        if (target.HasActedThisRound && _turnManager.Participants.Contains(target))
            return "DEJA JOUE";
        return "HORS COMBAT";
    }
}
