using Godot;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Damage;

[GlobalClass]
public partial class DamageResistanceEntry : Resource
{
    [Export] public DamageType Type { get; set; } = DamageType.Ballistic;
    [Export(PropertyHint.Range, "0,1000000,0.1")] public float Resistance { get; set; }
}
