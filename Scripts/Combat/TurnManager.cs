using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MankindRenewal.Combat;

public partial class TurnManager : Node
{
    [Export] public ulong RandomSeed { get; set; }

    public int RoundNumber { get; private set; }
    public TacticalUnit? ActiveUnit { get; private set; }
    public bool IsCombatRunning { get; private set; }
    public IReadOnlyList<TacticalUnit> Participants => _participants;
    public IReadOnlyList<TacticalUnit> RemainingOrder => _remainingOrder;

    public event Action<int>? RoundStarted;
    public event Action<TacticalUnit>? TurnStarted;
    public event Action<TacticalUnit>? TurnEnded;
    public event Action? OrderChanged;
    public event Action? CombatEnded;

    private readonly List<TacticalUnit> _participants = new();
    private readonly List<TacticalUnit> _remainingOrder = new();
    private readonly RandomNumberGenerator _random = new();

    public override void _Ready()
    {
        AddToGroup("turn_manager");
        if (RandomSeed == 0)
            _random.Randomize();
        else
            _random.Seed = RandomSeed;
    }

    public void StartCombat(IEnumerable<TacticalUnit> participants)
    {
        ClearParticipants();
        foreach (TacticalUnit unit in participants)
            RegisterParticipant(unit);

        RoundNumber = 0;
        ActiveUnit = null;
        IsCombatRunning = _participants.Count > 0;
        if (IsCombatRunning)
            BeginNewRound();
    }

    public void EndCombat()
    {
        if (!IsCombatRunning && _participants.Count == 0)
            return;

        ActiveUnit?.CancelPathAndReturnToCurrentCell();
        ActiveUnit = null;
        IsCombatRunning = false;
        RoundNumber = 0;
        _remainingOrder.Clear();
        ClearParticipants();
        CombatEnded?.Invoke();
        OrderChanged?.Invoke();
    }

    public bool EndCurrentTurn()
    {
        if (!IsCombatRunning || ActiveUnit is null)
            return false;

        TacticalUnit completedUnit = ActiveUnit;
        completedUnit.EndTurn();
        TurnEnded?.Invoke(completedUnit);
        ActiveUnit = null;

        if (_participants.Count == 0)
        {
            EndCombat();
            return true;
        }

        if (_participants.All(unit => unit.HasActedThisRound))
            BeginNewRound();
        else
        {
            RebuildRemainingOrder();
            StartNextTurn();
        }
        return true;
    }

    public bool AddParticipant(TacticalUnit unit)
    {
        if (_participants.Contains(unit))
            return false;

        RegisterParticipant(unit);
        unit.PrepareForNewRound();
        if (IsCombatRunning)
        {
            RebuildRemainingOrder();
            if (ActiveUnit is null)
                StartNextTurn();
        }
        return true;
    }

    public bool RemoveParticipant(TacticalUnit unit)
    {
        if (!_participants.Remove(unit))
            return false;

        unit.InitiativeChanged -= OnInitiativeChanged;
        _remainingOrder.Remove(unit);
        bool removedActive = ActiveUnit == unit;
        if (removedActive)
        {
            unit.CancelPathAndReturnToCurrentCell();
            ActiveUnit = null;
        }

        OrderChanged?.Invoke();
        if (!IsCombatRunning)
            return true;
        if (_participants.Count == 0)
        {
            EndCombat();
            return true;
        }
        if (removedActive)
        {
            if (_participants.All(participant => participant.HasActedThisRound))
                BeginNewRound();
            else
            {
                RebuildRemainingOrder();
                StartNextTurn();
            }
        }
        return true;
    }

    public void ModifyInitiative(TacticalUnit unit, int delta)
    {
        if (_participants.Contains(unit))
            unit.Initiative += delta;
    }

    public TacticalUnit? GetNextEligibleUnit()
    {
        return _remainingOrder.FirstOrDefault(unit => !unit.HasActedThisRound && unit != ActiveUnit);
    }

    public TacticalUnit? GetParticipantByName(string displayName)
    {
        return _participants.FirstOrDefault(unit => unit.UnitDisplayName == displayName);
    }

    public int GetRoundNumber() => RoundNumber;
    public int GetParticipantCount() => _participants.Count;
    public string GetActiveUnitName() => ActiveUnit?.UnitDisplayName ?? string.Empty;
    public string GetRemainingOrderNames() => string.Join(",", _remainingOrder.Select(unit => unit.UnitDisplayName));
    public bool GetIsCombatRunning() => IsCombatRunning;

    public string GetDebugOrderText()
    {
        List<string> lines = new();
        int index = 1;
        if (ActiveUnit is not null)
            lines.Add($"{index++}. {ActiveUnit.UnitDisplayName}  [ACTIVE]  Init {ActiveUnit.Initiative}");
        foreach (TacticalUnit unit in _remainingOrder)
            lines.Add($"{index++}. {unit.UnitDisplayName}  Init {unit.Initiative}");
        foreach (TacticalUnit unit in _participants.Where(unit => unit.HasActedThisRound))
            lines.Add($"{index++}. {unit.UnitDisplayName}  [ACTED]  Init {unit.Initiative}");
        return string.Join("\n", lines);
    }

    private void BeginNewRound()
    {
        RoundNumber++;
        foreach (TacticalUnit unit in _participants)
            unit.PrepareForNewRound();
        RebuildRemainingOrder();
        RoundStarted?.Invoke(RoundNumber);
        StartNextTurn();
    }

    private void StartNextTurn()
    {
        _remainingOrder.RemoveAll(unit => !_participants.Contains(unit) || unit.HasActedThisRound);
        if (_remainingOrder.Count == 0)
        {
            if (_participants.Count > 0)
                BeginNewRound();
            return;
        }

        ActiveUnit = _remainingOrder[0];
        _remainingOrder.RemoveAt(0);
        ActiveUnit.BeginTurn();
        TurnStarted?.Invoke(ActiveUnit);
        OrderChanged?.Invoke();
    }

    private void RebuildRemainingOrder()
    {
        _remainingOrder.Clear();
        IEnumerable<TacticalUnit> eligible = _participants.Where(unit => !unit.HasActedThisRound && unit != ActiveUnit);
        _remainingOrder.AddRange(
            eligible
                .Select(unit => new { Unit = unit, TieBreaker = _random.Randi() })
                .OrderByDescending(entry => entry.Unit.Initiative)
                .ThenBy(entry => entry.TieBreaker)
                .Select(entry => entry.Unit));
        OrderChanged?.Invoke();
    }

    private void RegisterParticipant(TacticalUnit unit)
    {
        _participants.Add(unit);
        unit.InitiativeChanged += OnInitiativeChanged;
    }

    private void ClearParticipants()
    {
        foreach (TacticalUnit unit in _participants)
            unit.InitiativeChanged -= OnInitiativeChanged;
        _participants.Clear();
        _remainingOrder.Clear();
    }

    private void OnInitiativeChanged(TacticalUnit unit)
    {
        if (!IsCombatRunning || unit == ActiveUnit || unit.HasActedThisRound)
        {
            OrderChanged?.Invoke();
            return;
        }
        RebuildRemainingOrder();
    }
}
