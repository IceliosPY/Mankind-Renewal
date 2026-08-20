using Godot;
using MankindRenewal.Items;

namespace MankindRenewal.Combat.Weapons;

[GlobalClass]
public partial class WeaponDefinition : ItemDefinition
{
    [ExportGroup("Attack")]
    [Export] public WeaponAttackType AttackType { get; set; } = WeaponAttackType.Ranged;
    [Export(PropertyHint.Range, "1,20,1")] public int ActionPointCost { get; set; } = 1;
    [Export(PropertyHint.Range, "0,1000,1")] public int BaseAccuracy { get; set; } = 20;
    [Export(PropertyHint.Range, "1,100,1")] public int RangeInCells { get; set; } = 1;
    [Export] public bool AllowsParry { get; set; }

    [ExportGroup("Damage")]
    [Export] public DamageType PrimaryDamageType { get; set; } = DamageType.Ballistic;
    [Export] public Godot.Collections.Array<DamageComponent> DamageComponents { get; set; } = new();
    [Export(PropertyHint.Range, "0,1000,0.1")] public float Penetration { get; set; }

    public float GetRawDamage()
    {
        float total = 0.0f;
        foreach (DamageComponent component in DamageComponents)
            total += Mathf.Max(component.Amount, 0.0f);
        return total;
    }

    public bool IsValidDefinition()
    {
        if (!IsIdentityValid() || ActionPointCost <= 0 || BaseAccuracy < 0 || RangeInCells <= 0 || DamageComponents.Count == 0)
            return false;
        foreach (DamageComponent component in DamageComponents)
        {
            if (component is null || component.Amount < 0.0f)
                return false;
        }
        return true;
    }

    public string GetItemId() => ItemId;
    public string GetDisplayName() => DisplayName;
    public int GetActionPointCost() => ActionPointCost;
    public int GetBaseAccuracy() => BaseAccuracy;
    public int GetRangeInCells() => RangeInCells;
    public int GetAttackTypeValue() => (int)AttackType;
    public int GetPrimaryDamageTypeValue() => (int)PrimaryDamageType;
    public int GetDamageComponentCount() => DamageComponents.Count;
    public float GetRawDamageValue() => GetRawDamage();
    public bool GetAllowsParry() => AllowsParry;
    public bool GetIsValidDefinition() => IsValidDefinition();
}
