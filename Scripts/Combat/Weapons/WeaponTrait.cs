using System;

namespace MankindRenewal.Combat.Weapons;

[Flags]
public enum WeaponTrait
{
    None = 0,
    CoverPiercing = 1 << 0,
}
