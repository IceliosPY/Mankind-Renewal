using Godot;
using MankindRenewal.Combat;
using MankindRenewal.Combat.Damage;
using MankindRenewal.Combat.Weapons;
using MankindRenewal.Equipment;
using MankindRenewal.Items;

namespace MankindRenewal.Tests;

public partial class CombatPrototype06Setup : Node
{
    [Export] public NodePath InventorySetupPath { get; set; } = new();
    [Export] public NodePath V5SetupPath { get; set; } = new();
    [Export] public NodePath TargetResistancePath { get; set; } = new();
    [Export] public NodePath InterceptorResistancePath { get; set; } = new();
    [Export] public WeaponDefinition? HybridRifle { get; set; }
    [Export] public DamageResistanceProfile? NoneProfile { get; set; }
    [Export] public DamageResistanceProfile? LightProfile { get; set; }
    [Export] public DamageResistanceProfile? StrongProfile { get; set; }
    [Export] public DamageResistanceProfile? ExtremeProfile { get; set; }

    public ItemInstance? HybridInstance { get; private set; }

    private InventoryEquipmentPrototypeSetup _inventorySetup = null!;
    private CombatPrototype05Setup _v5Setup = null!;
    private EquipmentLoadout _unitA = null!;
    private TacticalUnit _targetB = null!;
    private TacticalUnit _interceptorC = null!;
    private UnitDamageResistance _targetResistance = null!;
    private UnitDamageResistance _interceptorResistance = null!;

    public override void _Ready()
    {
        _inventorySetup = GetNode<InventoryEquipmentPrototypeSetup>(InventorySetupPath);
        _v5Setup = GetNode<CombatPrototype05Setup>(V5SetupPath);
        _unitA = _inventorySetup.GetLoadout("UNITE A")!;
        _targetB = _inventorySetup.GetLoadout("UNITE B")?.OwnerUnit!;
        _interceptorC = _inventorySetup.GetLoadout("UNITE C")?.OwnerUnit!;
        _targetResistance = GetNode<UnitDamageResistance>(TargetResistancePath);
        _interceptorResistance = GetNode<UnitDamageResistance>(InterceptorResistancePath);
        if (_unitA is null || _targetB is null || _interceptorC is null || HybridRifle is null
            || NoneProfile is null || LightProfile is null || StrongProfile is null || ExtremeProfile is null)
        {
            GD.PushError("CombatPrototype06Setup: configuration incomplete.");
            return;
        }

        HybridInstance = ItemInstance.Create(HybridRifle);
        if (!_unitA.Inventory.AddItem(HybridInstance))
            throw new System.InvalidOperationException("Impossible d'ajouter DebugHybridRifle au prototype V6.");
        SetTargetResistancePreset(0);
        SetInterceptorResistancePreset(0);
    }

    public bool ActivateNormalWeapon() => _v5Setup.ActivateNormalWeapon();
    public bool ActivateAntiCoverWeapon() => _v5Setup.ActivateAntiCoverWeapon();
    public bool ActivateArmorPenWeapon() => _v5Setup.ActivateArmorPenWeapon();

    public bool ActivateHybridWeapon()
    {
        return HybridInstance is not null
            && _unitA.Equip(HybridInstance, InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId)
            && _unitA.SetActiveSlot(InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId);
    }

    public bool EquipWeaponForTest(WeaponDefinition? definition)
    {
        if (definition is null)
            return false;
        ItemInstance instance = ItemInstance.Create(definition);
        return _unitA.Inventory.AddItem(instance)
            && _unitA.Equip(instance, InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId)
            && _unitA.SetActiveSlot(InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId);
    }

    public void SetTargetResistancePreset(int preset) => _targetResistance.Profile = ResolvePreset(preset);
    public void SetInterceptorResistancePreset(int preset) => _interceptorResistance.Profile = ResolvePreset(preset);
    public void SetCoverMode(int levelValue) => _v5Setup.SetCoverMode(levelValue);
    public void SetFriendlyInterceptor(bool friendly) => _v5Setup.SetFriendlyInterceptor(friendly);

    public TacticalUnit GetTargetUnit() => _targetB;
    public TacticalUnit GetInterceptorUnit() => _interceptorC;
    public string GetHybridInstanceId() => HybridInstance?.InstanceId ?? string.Empty;

    private DamageResistanceProfile ResolvePreset(int preset) => preset switch
    {
        1 => LightProfile!,
        2 => StrongProfile!,
        3 => ExtremeProfile!,
        _ => NoneProfile!,
    };

}
