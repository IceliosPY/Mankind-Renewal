using System.Collections.Generic;
using System.Linq;
using Godot;
using MankindRenewal.Combat.Actions;
using MankindRenewal.Combat.Cover;
using MankindRenewal.Combat.LineOfFire;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat;

public partial class CombatV5RulesService : Node, ICombatAttackRules
{
    [Export] public NodePath GridPath { get; set; } = new();
    [Export] public CoverRulesDefinition? Rules { get; set; }
    [Export] public Godot.Collections.Array<NodePath> ProviderPaths { get; set; } = new();
    [Export] public Godot.Collections.Array<NodePath> UnitPaths { get; set; } = new();

    private TacticalGrid _grid = null!;
    private CoverEvaluator _cover = null!;
    private LineOfFireEvaluator _lineOfFire = null!;
    private ShotTrajectoryResult? _lastEvaluation;
    private readonly List<CoverProvider3D> _providers = new();
    private readonly List<TacticalUnit> _units = new();

    public override void _Ready()
    {
        _grid = GetNode<TacticalGrid>(GridPath);
        Rules ??= new CoverRulesDefinition();
        ResolveConfiguredNodes();
        _cover = new CoverEvaluator(_grid, Rules, GetProviders);
        _lineOfFire = new LineOfFireEvaluator(_grid, Rules, GetProviders, GetUnits);
        AddToGroup("combat_attack_rules");
    }

    public override void _ExitTree()
    {
        _lastEvaluation = null;
        _providers.Clear();
        _units.Clear();
        _cover = null!;
        _lineOfFire = null!;
        _grid = null!;
        Rules = null;
    }

    public CombatAttackEvaluation EvaluateAttack(TacticalUnit attacker, TacticalUnit target, WeaponDefinition weapon)
    {
        CoverEvaluation cover = weapon.AttackType == WeaponAttackType.Melee
            ? new CoverEvaluation()
            : _cover.Evaluate(attacker, target);
        (CoverProvider3D? blocker, float _) = _lineOfFire.FindFirstObstruction(attacker, target);
        bool blockedByTotalCover = cover.EffectiveCover == CoverLevel.Total;
        bool obstructed = blocker is not null || blockedByTotalCover;
        bool piercing = obstructed && weapon.HasTrait(WeaponTrait.CoverPiercing) && weapon.AttackType == WeaponAttackType.Ranged;
        bool blocked = obstructed && !piercing;
        (TacticalUnit? interceptor, float fraction) = blocked
            ? (null, 1.0f)
            : _lineOfFire.FindFirstInterceptor(attacker, target);
        TacticalUnit resolved = interceptor ?? target;

        _lastEvaluation = new ShotTrajectoryResult
        {
            IntendedTarget = target,
            ResolvedTarget = resolved,
            Interceptor = interceptor,
            HasLineOfSight = !obstructed,
            HasLineOfFire = !blocked,
            IsBlocked = blocked,
            UsesCoverPiercing = piercing,
            IsFriendlyFire = interceptor?.TeamId == attacker.TeamId,
            EffectiveAccuracy = Mathf.Max(weapon.BaseAccuracy - cover.AccuracyPenalty, 0),
            DamageMultiplier = piercing ? Rules!.CoverPiercingDamageMultiplier : 1.0f,
            BlockReason = blocked ? (blockedByTotalCover ? "TOTAL COVER" : "OBSTRUCTION") : string.Empty,
            Cover = cover,
            BlockingProvider = blocker,
            InterceptorPathFraction = fraction,
        };
        return _lastEvaluation;
    }

    public ShotTrajectoryResult? EvaluateDetailed(TacticalUnit? attacker, TacticalUnit? target, WeaponDefinition? weapon)
    {
        return attacker is null || target is null || weapon is null ? null : (ShotTrajectoryResult)EvaluateAttack(attacker, target, weapon);
    }

    public bool CanReactionAttack(TacticalUnit reactor, TacticalUnit target, WeaponDefinition weapon)
        => !EvaluateAttack(reactor, target, weapon).IsBlocked;

    public bool EvaluateByUnitNames(string attackerName, string targetName)
    {
        TacticalUnit? attacker = GetUnits().FirstOrDefault(unit => unit.UnitDisplayName == attackerName);
        TacticalUnit? target = GetUnits().FirstOrDefault(unit => unit.UnitDisplayName == targetName);
        WeaponDefinition? weapon = attacker?.GetActiveWeapon();
        if (attacker is null || target is null || weapon is null)
            return false;
        EvaluateAttack(attacker, target, weapon);
        return true;
    }

    public int ApplyHeightForTest(int coverLevel, float attackerHeight, float targetHeight)
        => (int)_cover.ApplyHeightForTest((CoverLevel)coverLevel, attackerHeight, targetHeight);

    public int GetLastBaseCoverValue() => (int)(_lastEvaluation?.Cover.BaseCover ?? CoverLevel.None);
    public int GetLastEffectiveCoverValue() => (int)(_lastEvaluation?.Cover.EffectiveCover ?? CoverLevel.None);
    public int GetLastCoverPenalty() => _lastEvaluation?.Cover.AccuracyPenalty ?? 0;
    public int GetLastEffectiveAccuracy() => _lastEvaluation?.EffectiveAccuracy ?? 0;
    public bool GetLastIsFlanked() => _lastEvaluation?.Cover.IsFlanked ?? false;
    public bool GetLastIsBlocked() => _lastEvaluation?.IsBlocked ?? false;
    public bool GetLastHasLineOfSight() => _lastEvaluation?.HasLineOfSight ?? false;
    public bool GetLastHasLineOfFire() => _lastEvaluation?.HasLineOfFire ?? false;
    public bool GetLastUsesCoverPiercing() => _lastEvaluation?.UsesCoverPiercing ?? false;
    public bool GetLastIsFriendlyFire() => _lastEvaluation?.IsFriendlyFire ?? false;
    public string GetLastInterceptorName() => _lastEvaluation?.Interceptor?.UnitDisplayName ?? string.Empty;
    public string GetLastResolvedTargetName() => _lastEvaluation?.ResolvedTarget.UnitDisplayName ?? string.Empty;
    public string GetLastBlockReason() => _lastEvaluation?.BlockReason ?? string.Empty;
    public string GetLastBlockingProviderName() => _lastEvaluation?.BlockingProvider?.Name ?? string.Empty;
    public float GetLastDamageMultiplier() => _lastEvaluation?.DamageMultiplier ?? 1.0f;
    public int GetLastHeightLevelsReduced() => _lastEvaluation?.Cover.HeightLevelsReduced ?? 0;
    public string GetLastAttackDirections() => _lastEvaluation is null ? string.Empty : string.Join("+", _lastEvaluation.Cover.AttackDirections.Select(direction => direction.ToString().ToUpperInvariant()));

    private IEnumerable<CoverProvider3D> GetProviders()
        => _providers;

    private IEnumerable<TacticalUnit> GetUnits()
        => _units;

    public void RegisterProvider(CoverProvider3D provider)
    {
        if (!_providers.Contains(provider))
            _providers.Add(provider);
    }

    public void RegisterUnit(TacticalUnit unit)
    {
        if (!_units.Contains(unit))
            _units.Add(unit);
    }

    private void ResolveConfiguredNodes()
    {
        foreach (NodePath path in ProviderPaths)
        {
            CoverProvider3D? provider = GetNodeOrNull<CoverProvider3D>(path);
            if (provider is not null)
                RegisterProvider(provider);
        }
        foreach (NodePath path in UnitPaths)
        {
            TacticalUnit? unit = GetNodeOrNull<TacticalUnit>(path);
            if (unit is not null)
                RegisterUnit(unit);
        }
    }
}
