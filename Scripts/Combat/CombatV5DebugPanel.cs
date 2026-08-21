using System.Collections.Generic;
using System.Text;
using Godot;
using MankindRenewal.Combat.Cover;
using MankindRenewal.Combat.LineOfFire;
using MankindRenewal.Combat.Weapons;
using MankindRenewal.Tests;

namespace MankindRenewal.Combat;

public partial class CombatV5DebugPanel : PanelContainer
{
    [Export] public NodePath ActionControllerPath { get; set; } = new();
    [Export] public NodePath RulesServicePath { get; set; } = new();
    [Export] public NodePath SetupPath { get; set; } = new();

    private CombatV4ActionController _actions = null!;
    private CombatV5RulesService _rules = null!;
    private CombatPrototype05Setup _setup = null!;
    private Label _analysis = null!;
    private readonly List<(Button Button, System.Action Handler)> _buttonBindings = new();
    private CheckButton? _friendlyFireButton;
    private CheckButton? _reactionWallButton;
    private BaseButton.ToggledEventHandler? _friendlyFireHandler;
    private BaseButton.ToggledEventHandler? _reactionWallHandler;
    private double _refreshCountdown;

    public override void _Ready()
    {
        _actions = GetNode<CombatV4ActionController>(ActionControllerPath);
        _rules = GetNode<CombatV5RulesService>(RulesServicePath);
        _setup = GetNode<CombatPrototype05Setup>(SetupPath);
        _analysis = GetNode<Label>("Margin/VBox/Analysis");
        const string secondary = "Margin/VBox/SecondaryScroll/Secondary/";
        Bind(secondary + "CoverDirectionButtons/North", () => _setup.SetCoverDirection((int)CoverDirection.North));
        Bind(secondary + "CoverDirectionButtons/East", () => _setup.SetCoverDirection((int)CoverDirection.East));
        Bind(secondary + "CoverDirectionButtons/South", () => _setup.SetCoverDirection((int)CoverDirection.South));
        Bind(secondary + "CoverDirectionButtons/West", () => _setup.SetCoverDirection((int)CoverDirection.West));
        Bind(secondary + "CoverButtons/None", () => _setup.SetCoverMode((int)CoverLevel.None));
        Bind(secondary + "CoverButtons/Light", () => _setup.SetCoverMode((int)CoverLevel.Light));
        Bind(secondary + "CoverButtons/Heavy", () => _setup.SetCoverMode((int)CoverLevel.Heavy));
        Bind(secondary + "CoverButtons/Total", () => _setup.SetCoverMode((int)CoverLevel.Total));
        Bind(secondary + "Flank", _setup.SetFlankedMode);
        Bind(secondary + "WeaponButtons/Normal", () => _setup.ActivateNormalWeapon());
        Bind(secondary + "WeaponButtons/ArmorPen", () => _setup.ActivateArmorPenWeapon());
        Bind(secondary + "WeaponButtons/Piercing", () => _setup.ActivateAntiCoverWeapon());
        _friendlyFireButton = GetNode<CheckButton>(secondary + "FriendlyFire");
        _reactionWallButton = GetNode<CheckButton>(secondary + "ReactionWall");
        _friendlyFireHandler = _setup.SetFriendlyInterceptor;
        _reactionWallHandler = _setup.SetReactionWallEnabled;
        _friendlyFireButton.Toggled += _friendlyFireHandler;
        _reactionWallButton.Toggled += _reactionWallHandler;
    }

    public override void _ExitTree()
    {
        foreach ((Button button, System.Action handler) in _buttonBindings)
            button.Pressed -= handler;
        _buttonBindings.Clear();
        if (_friendlyFireButton is not null && _friendlyFireHandler is not null)
            _friendlyFireButton.Toggled -= _friendlyFireHandler;
        if (_reactionWallButton is not null && _reactionWallHandler is not null)
            _reactionWallButton.Toggled -= _reactionWallHandler;
        _friendlyFireButton = null;
        _reactionWallButton = null;
        _friendlyFireHandler = null;
        _reactionWallHandler = null;
        _actions = null!;
        _rules = null!;
        _setup = null!;
        _analysis = null!;
    }

    public override void _Process(double delta)
    {
        _refreshCountdown -= delta;
        if (_refreshCountdown > 0.0)
            return;
        _refreshCountdown = 0.1;
        RefreshAnalysis();
    }

    public string GetAnalysisText() => _analysis.Text;

    private void RefreshAnalysis()
    {
        TacticalUnit? attacker = GetTree().GetFirstNodeInGroup("turn_manager") is TurnManager turns ? turns.ActiveUnit : null;
        TacticalUnit? target = _actions.SelectedTarget;
        WeaponDefinition? weapon = attacker?.GetActiveWeapon();
        ShotTrajectoryResult? result = _rules.EvaluateDetailed(attacker, target, weapon);
        if (attacker is null || target is null || weapon is null || result is null)
        {
            _analysis.Text = "TARGET : -\nSelectionnez ATTAQUER puis une cible dans le panneau V4.";
            return;
        }

        StringBuilder text = new();
        text.AppendLine($"TARGET : {target.UnitDisplayName}");
        text.AppendLine($"RANGE : {Actions.AttackActionPipeline.GetTacticalDistance(attacker, target)} / {weapon.RangeInCells}");
        text.AppendLine($"LINE OF SIGHT : {(result.HasLineOfSight ? "CLEAR" : "BLOCKED")}");
        text.AppendLine($"LINE OF FIRE : {(result.HasLineOfFire ? "CLEAR" : "BLOCKED")}");
        text.AppendLine($"ATTACK DIRECTION : {string.Join(" + ", result.Cover.AttackDirections)}");
        text.AppendLine($"BASE COVER : {result.Cover.BaseCover.ToString().ToUpperInvariant()}");
        text.AppendLine($"HEIGHT MODIFIER : {(result.Cover.HeightLevelsReduced > 0 ? "-1 LEVEL" : "NONE")}");
        text.AppendLine($"EFFECTIVE COVER : {result.Cover.EffectiveCover.ToString().ToUpperInvariant()}");
        text.AppendLine($"COVER PENALTY : -{result.Cover.AccuracyPenalty}");
        text.AppendLine($"BASE WEAPON ACCURACY : {weapon.BaseAccuracy}");
        text.AppendLine($"EFFECTIVE ACCURACY : {result.EffectiveAccuracy}");
        if (result.Cover.IsFlanked)
            text.AppendLine("FLANKED — NO COVER FROM THIS DIRECTION");
        if (result.IsBlocked)
            text.AppendLine($"BLOCKED : {result.BlockReason}");
        if (result.Interceptor is not null)
            text.AppendLine($"INTERCEPTOR : {result.Interceptor.UnitDisplayName}");
        if (result.IsFriendlyFire)
            text.AppendLine("FRIENDLY FIRE");
        if (result.UsesCoverPiercing)
            text.AppendLine($"COVER PIERCING — DAMAGE ×{result.DamageMultiplier:0.##}");
        text.AppendLine($"ARMOR PENETRATION : {weapon.Penetration:0.##} (sans effet V5)");
        _analysis.Text = text.ToString();
    }

    private void Bind(string path, System.Action action)
    {
        Button button = GetNode<Button>(path);
        System.Action handler = () => { action(); RefreshAnalysis(); };
        button.Pressed += handler;
        _buttonBindings.Add((button, handler));
    }
}
