using Godot;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Damage;

public partial class UnitDamageResistance : Node
{
    [Export] public NodePath UnitPath { get; set; } = new();
    [Export] public NodePath ServicePath { get; set; } = new();
    [Export] public DamageResistanceProfile? Profile { get; set; }

    public TacticalUnit Unit { get; private set; } = null!;
    private DamageResolutionService _service = null!;

    public override void _Ready()
    {
        Unit = GetNode<TacticalUnit>(UnitPath);
        _service = GetNode<DamageResolutionService>(ServicePath);
        _service.RegisterResistanceProvider(this);
    }

    public override void _ExitTree()
    {
        if (_service is not null)
            _service.UnregisterResistanceProvider(this);
        Profile = null;
        Unit = null!;
        _service = null!;
    }

    public float GetResistance(DamageType type) => Profile?.GetResistance(type) ?? 0.0f;
    public float GetResistanceValue(int typeValue) => GetResistance((DamageType)Mathf.Clamp(typeValue, 0, 4));
}
