using System;
using Godot;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Damage;

[GlobalClass]
public partial class DamageResistanceProfile : Resource
{
    [Export] public DamageResistanceEntry[] Entries { get; set; } = Array.Empty<DamageResistanceEntry>();

    public float GetResistance(DamageType type)
    {
        float resistance = 0.0f;
        foreach (DamageResistanceEntry entry in Entries)
        {
            if (entry is not null && entry.Type == type)
                resistance = Mathf.Max(resistance, entry.Resistance);
        }
        return Mathf.Max(resistance, 0.0f);
    }

    public float GetResistanceValue(int typeValue)
        => GetResistance((DamageType)Mathf.Clamp(typeValue, 0, 4));
}
