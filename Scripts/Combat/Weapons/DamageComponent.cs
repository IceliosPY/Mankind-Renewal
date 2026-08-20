using Godot;

namespace MankindRenewal.Combat.Weapons;

[GlobalClass]
public partial class DamageComponent : Resource
{
    [Export] public DamageType Type { get; set; } = DamageType.Ballistic;
    [Export(PropertyHint.Range, "0,10000,0.1")] public float Amount { get; set; }
}
