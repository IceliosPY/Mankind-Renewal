using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using MankindRenewal.Combat.Damage;
using MankindRenewal.Combat.Weapons;
using MankindRenewal.Tests;

namespace MankindRenewal.Combat;

public partial class CombatV6DebugPanel : PanelContainer
{
    [Export] public NodePath ActionControllerPath { get; set; } = new();
    [Export] public NodePath DamageServicePath { get; set; } = new();
    [Export] public NodePath SetupPath { get; set; } = new();

    private CombatV4ActionController _actions = null!;
    private DamageResolutionService _damage = null!;
    private CombatPrototype06Setup _setup = null!;
    private Label _analysis = null!;
    private readonly List<(Button Button, Action Handler)> _bindings = new();
    private CheckButton? _friendlyFire;
    private BaseButton.ToggledEventHandler? _friendlyFireHandler;
    private double _refreshCountdown;

    public override void _Ready()
    {
        _actions = GetNode<CombatV4ActionController>(ActionControllerPath);
        _damage = GetNode<DamageResolutionService>(DamageServicePath);
        _setup = GetNode<CombatPrototype06Setup>(SetupPath);
        _analysis = GetNode<Label>("Margin/VBox/Scroll/Content/Analysis");
        const string root = "Margin/VBox/Scroll/Content/";
        Bind(root + "ResistanceButtons/None", () => _setup.SetTargetResistancePreset(0));
        Bind(root + "ResistanceButtons/Light", () => _setup.SetTargetResistancePreset(1));
        Bind(root + "ResistanceButtons/Strong", () => _setup.SetTargetResistancePreset(2));
        Bind(root + "ResistanceButtons/Extreme", () => _setup.SetTargetResistancePreset(3));
        Bind(root + "InterceptorResistanceButtons/None", () => _setup.SetInterceptorResistancePreset(0));
        Bind(root + "InterceptorResistanceButtons/Light", () => _setup.SetInterceptorResistancePreset(1));
        Bind(root + "InterceptorResistanceButtons/Strong", () => _setup.SetInterceptorResistancePreset(2));
        Bind(root + "WeaponButtons/Normal", () => _setup.ActivateNormalWeapon());
        Bind(root + "WeaponButtons/Hybrid", () => _setup.ActivateHybridWeapon());
        Bind(root + "WeaponButtons/ArmorPen", () => _setup.ActivateArmorPenWeapon());
        Bind(root + "WeaponButtons/Piercing", () => _setup.ActivateAntiCoverWeapon());
        Bind(root + "CoverButtons/Open", () => _setup.SetCoverMode(0));
        Bind(root + "CoverButtons/Total", () => _setup.SetCoverMode(3));
        _friendlyFire = GetNode<CheckButton>(root + "FriendlyFire");
        _friendlyFireHandler = _setup.SetFriendlyInterceptor;
        _friendlyFire.Toggled += _friendlyFireHandler;
        RefreshAnalysis();
    }

    public override void _ExitTree()
    {
        foreach ((Button button, Action handler) in _bindings)
            button.Pressed -= handler;
        _bindings.Clear();
        if (_friendlyFire is not null && _friendlyFireHandler is not null)
            _friendlyFire.Toggled -= _friendlyFireHandler;
        _friendlyFire = null;
        _friendlyFireHandler = null;
        _actions = null!;
        _damage = null!;
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
        TacticalUnit target = _actions.SelectedTarget ?? _setup.GetTargetUnit();
        StringBuilder text = new();
        text.AppendLine($"TARGET RESISTANCES — {target.UnitDisplayName}");
        foreach (DamageType type in Enum.GetValues<DamageType>())
            text.AppendLine($"{type.ToString().ToUpperInvariant()} : {_damage.GetUnitResistance(target, type):0.##}");

        text.AppendLine();
        text.AppendLine("DAMAGE BREAKDOWN");
        DamageResolutionResult? result = _damage.LastResult;
        if (result is null)
        {
            text.AppendLine("AUCUN DEGAT RESOLU");
            _analysis.Text = text.ToString();
            return;
        }

        text.AppendLine($"RESOLVED TARGET : {result.Target.UnitDisplayName}");
        foreach (DamageComponentResult component in result.Components)
        {
            text.AppendLine();
            text.AppendLine(component.DamageType.ToString().ToUpperInvariant());
            text.AppendLine($"Raw Damage: {component.RawDamage:0.###}");
            text.AppendLine($"Cover Multiplier: {component.CoverMultiplier:0.###}");
            text.AppendLine($"Damage Before Resistance: {component.DamageBeforeResistance:0.###}");
            text.AppendLine($"Base Resistance: {component.BaseResistance:0.###}");
            text.AppendLine($"Penetration: {component.PenetrationApplied:0.###}");
            text.AppendLine($"Effective Resistance: {component.EffectiveResistance:0.###}");
            text.AppendLine($"Reduction: {component.ReductionPercentage * 100.0:0.###}%");
            text.AppendLine($"Damage Result: {component.DamageAfterResistance:0.###}");
        }
        text.AppendLine();
        text.AppendLine($"TOTAL DECIMAL: {result.DecimalTotalDamage:0.###}");
        text.AppendLine($"FINAL DAMAGE: {result.FinalDamage}");
        text.AppendLine($"HP: {result.HpBefore} -> {result.HpAfter}");
        text.AppendLine($"NEUTRALIZED: {(result.TargetNeutralized ? "YES" : "NO")}");
        _analysis.Text = text.ToString();
    }

    private void Bind(string path, Action action)
    {
        Button button = GetNode<Button>(path);
        Action handler = () => { action(); RefreshAnalysis(); };
        button.Pressed += handler;
        _bindings.Add((button, handler));
    }
}
