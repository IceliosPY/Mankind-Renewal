using System;
using System.Collections.Generic;
using Godot;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Damage;

public partial class DamageResolutionService : Node, IDamageResolver
{
    [Export] public DamageRulesDefinition? Rules { get; set; }

    public DamageResolutionResult? LastResult { get; private set; }
    private readonly Dictionary<TacticalUnit, UnitDamageResistance> _resistanceProviders = new();

    public override void _Ready()
    {
        Rules ??= new DamageRulesDefinition();
        AddToGroup("damage_resolution_services");
    }

    public override void _ExitTree()
    {
        LastResult = null;
        _resistanceProviders.Clear();
        Rules = null;
    }

    public DamageResolutionResult ResolveAndApply(TacticalUnit target, WeaponDefinition weapon, float coverMultiplier)
    {
        DamageResolutionResult result = Resolve(target, weapon, coverMultiplier);
        result.HpBefore = target.CurrentHealth;
        target.ApplyRawDamage(result.FinalDamage);
        result.HpAfter = target.CurrentHealth;
        result.TargetNeutralized = target.IsNeutralized;
        LastResult = result;
        return result;
    }

    public DamageResolutionResult Preview(TacticalUnit target, WeaponDefinition weapon, float coverMultiplier)
    {
        DamageResolutionResult result = Resolve(target, weapon, coverMultiplier);
        result.HpBefore = target.CurrentHealth;
        result.HpAfter = target.CurrentHealth;
        result.TargetNeutralized = target.IsNeutralized;
        LastResult = result;
        return result;
    }

    public bool PreviewDamage(TacticalUnit? target, WeaponDefinition? weapon, float coverMultiplier = 1.0f)
    {
        if (target is null || weapon is null)
            return false;
        Preview(target, weapon, coverMultiplier);
        return true;
    }

    public bool ResolveDamageForTest(TacticalUnit? target, WeaponDefinition? weapon, float coverMultiplier = 1.0f)
    {
        if (target is null || weapon is null)
            return false;
        ResolveAndApply(target, weapon, coverMultiplier);
        return true;
    }

    public int GetLastComponentCount() => LastResult?.Components.Count ?? 0;
    public int GetLastComponentType(int index) => IsValidIndex(index) ? (int)LastResult!.Components[index].DamageType : -1;
    public double GetLastRawDamage(int index) => IsValidIndex(index) ? LastResult!.Components[index].RawDamage : 0.0;
    public double GetLastCoverMultiplier(int index) => IsValidIndex(index) ? LastResult!.Components[index].CoverMultiplier : 1.0;
    public double GetLastDamageBeforeResistance(int index) => IsValidIndex(index) ? LastResult!.Components[index].DamageBeforeResistance : 0.0;
    public double GetLastBaseResistance(int index) => IsValidIndex(index) ? LastResult!.Components[index].BaseResistance : 0.0;
    public double GetLastPenetrationApplied(int index) => IsValidIndex(index) ? LastResult!.Components[index].PenetrationApplied : 0.0;
    public double GetLastEffectiveResistance(int index) => IsValidIndex(index) ? LastResult!.Components[index].EffectiveResistance : 0.0;
    public double GetLastReductionPercentage(int index) => IsValidIndex(index) ? LastResult!.Components[index].ReductionPercentage : 0.0;
    public double GetLastDamageAfterResistance(int index) => IsValidIndex(index) ? LastResult!.Components[index].DamageAfterResistance : 0.0;
    public double GetLastDecimalTotalDamage() => LastResult?.DecimalTotalDamage ?? 0.0;
    public int GetLastFinalDamage() => LastResult?.FinalDamage ?? 0;
    public int GetLastHpBefore() => LastResult?.HpBefore ?? 0;
    public int GetLastHpAfter() => LastResult?.HpAfter ?? 0;
    public bool GetLastTargetNeutralized() => LastResult?.TargetNeutralized ?? false;
    public string GetLastTargetName() => LastResult?.Target.UnitDisplayName ?? string.Empty;
    public float GetUnitResistance(TacticalUnit? unit, DamageType type)
        => unit is not null && _resistanceProviders.TryGetValue(unit, out UnitDamageResistance? provider)
            ? provider.GetResistance(type)
            : 0.0f;
    public float GetUnitResistanceValue(TacticalUnit? unit, int typeValue)
        => GetUnitResistance(unit, (DamageType)Mathf.Clamp(typeValue, 0, 4));

    public void RegisterResistanceProvider(UnitDamageResistance provider)
    {
        if (provider.Unit is not null)
            _resistanceProviders[provider.Unit] = provider;
    }

    public void UnregisterResistanceProvider(UnitDamageResistance provider)
    {
        if (provider.Unit is not null && _resistanceProviders.TryGetValue(provider.Unit, out UnitDamageResistance? current) && current == provider)
            _resistanceProviders.Remove(provider.Unit);
    }

    private DamageResolutionResult Resolve(TacticalUnit target, WeaponDefinition weapon, float coverMultiplier)
    {
        DamageRulesDefinition rules = Rules ?? new DamageRulesDefinition();
        double maximumReduction = Math.Round(
            Math.Clamp(rules.GetMaxResistanceReduction(), 0.0f, DamageRulesDefinition.AbsoluteMaximumResistanceReduction),
            6,
            MidpointRounding.AwayFromZero);
        double resistanceScale = Math.Max(
            Math.Round(rules.GetResistanceScale(), 6, MidpointRounding.AwayFromZero),
            0.0001);
        double multiplier = Math.Max(coverMultiplier, 0.0f);
        DamageResolutionResult result = new() { Target = target };

        foreach (DamageComponent component in weapon.DamageComponents)
        {
            if (component is null)
                continue;
            double rawDamage = Math.Max(component.Amount, 0.0f);
            double damageBeforeResistance = rawDamage * multiplier;
            double baseResistance = GetUnitResistance(target, component.Type);
            double penetration = component.Type == weapon.PrimaryDamageType ? Math.Max(weapon.Penetration, 0.0f) : 0.0;
            double effectiveResistance = Math.Max(0.0, baseResistance - penetration);
            double reduction = effectiveResistance <= 0.0
                ? 0.0
                : maximumReduction * effectiveResistance / (effectiveResistance + resistanceScale);
            reduction = Math.Clamp(reduction, 0.0, DamageRulesDefinition.AbsoluteMaximumResistanceReduction);
            double damageAfterResistance = damageBeforeResistance * (1.0 - reduction);
            result.Components.Add(new DamageComponentResult
            {
                DamageType = component.Type,
                RawDamage = rawDamage,
                CoverMultiplier = multiplier,
                DamageBeforeResistance = damageBeforeResistance,
                BaseResistance = baseResistance,
                PenetrationApplied = penetration,
                EffectiveResistance = effectiveResistance,
                ReductionPercentage = reduction,
                DamageAfterResistance = damageAfterResistance,
            });
            result.DecimalTotalDamage += damageAfterResistance;
        }

        result.FinalDamage = Math.Max((int)Math.Floor(result.DecimalTotalDamage), 0);
        return result;
    }

    private bool IsValidIndex(int index)
        => LastResult is not null && index >= 0 && index < LastResult.Components.Count;
}
