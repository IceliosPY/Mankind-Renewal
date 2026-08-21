using Godot;
using MankindRenewal.Combat;
using MankindRenewal.Combat.Cover;
using MankindRenewal.Combat.Weapons;
using MankindRenewal.Equipment;
using MankindRenewal.Items;

namespace MankindRenewal.Tests;

public partial class CombatPrototype05Setup : Node
{
    [Export] public NodePath InventorySetupPath { get; set; } = new();
    [Export] public NodePath TargetCoverPath { get; set; } = new();
    [Export] public NodePath ReactionWallPath { get; set; } = new();
    [Export] public NodePath InterceptorUnitPath { get; set; } = new();
    [Export] public WeaponDefinition? AntiCoverRifle { get; set; }
    [Export] public WeaponDefinition? ArmorPenRifle { get; set; }

    public ItemInstance? AntiCoverInstance { get; private set; }
    public ItemInstance? ArmorPenInstance { get; private set; }

    private InventoryEquipmentPrototypeSetup _inventorySetup = null!;
    private EquipmentLoadout _unitA = null!;
    private TacticalUnit _coverTarget = null!;
    private CoverProvider3D _targetCover = null!;
    private CoverProvider3D _reactionWall = null!;
    private TacticalUnit _interceptor = null!;

    public override void _Ready()
    {
        _inventorySetup = GetNode<InventoryEquipmentPrototypeSetup>(InventorySetupPath);
        _unitA = _inventorySetup.GetLoadout("UNITE A")!;
        _coverTarget = _inventorySetup.GetLoadout("UNITE B")?.OwnerUnit!;
        _targetCover = GetNode<CoverProvider3D>(TargetCoverPath);
        _reactionWall = GetNode<CoverProvider3D>(ReactionWallPath);
        _interceptor = GetNode<TacticalUnit>(InterceptorUnitPath);
        if (AntiCoverRifle is null || ArmorPenRifle is null || _unitA is null || _coverTarget is null)
        {
            GD.PushError("CombatPrototype05Setup: configuration incomplete.");
            return;
        }
        AntiCoverInstance = Add(_unitA.Inventory, AntiCoverRifle);
        ArmorPenInstance = Add(_unitA.Inventory, ArmorPenRifle);
        SetCoverMode((int)CoverLevel.Heavy);
        SetFriendlyInterceptor(false);
    }

    public bool ActivateNormalWeapon() => _unitA.SetActiveSlot(InventoryEquipmentPrototypeSetup.PrimaryWeaponSlotId);

    public bool ActivateAntiCoverWeapon()
    {
        return AntiCoverInstance is not null
            && _unitA.Equip(AntiCoverInstance, InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId)
            && _unitA.SetActiveSlot(InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId);
    }

    public bool ActivateArmorPenWeapon()
    {
        return ArmorPenInstance is not null
            && _unitA.Equip(ArmorPenInstance, InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId)
            && _unitA.SetActiveSlot(InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId);
    }

    public void SetCoverMode(int levelValue)
    {
        CoverLevel level = (CoverLevel)Mathf.Clamp(levelValue, 0, 3);
        AssociateTargetCoverWithCurrentCell();
        _targetCover.TacticalEnabled = level != CoverLevel.None;
        _targetCover.Level = level;
        _targetCover.BlocksLineOfFire = level == CoverLevel.Total;
        _targetCover.InvalidateCellAssociation();
    }

    public void SetCoverDirection(int directionValue)
    {
        AssociateTargetCoverWithCurrentCell();
        _targetCover.ProtectedDirection = (CoverDirection)Mathf.Clamp(directionValue, 0, 3);
        _targetCover.InvalidateCellAssociation();
    }

    public void SetFlankedMode()
    {
        AssociateTargetCoverWithCurrentCell();
        _targetCover.TacticalEnabled = true;
        _targetCover.Level = CoverLevel.Heavy;
        _targetCover.ProtectedDirection = CoverDirection.North;
        _targetCover.BlocksLineOfFire = false;
        _targetCover.InvalidateCellAssociation();
    }

    public void SetReactionWallEnabled(bool enabled)
    {
        _reactionWall.TacticalEnabled = enabled;
        Node3D? wallVisual = _reactionWall.GetNodeOrNull<Node3D>("WallVisual");
        if (wallVisual is not null)
            wallVisual.Visible = enabled;
        _reactionWall.InvalidateCellAssociation();
    }

    public void SetFriendlyInterceptor(bool friendly)
    {
        _interceptor.TeamId = friendly ? 1 : 2;
    }

    public string GetAntiCoverInstanceId() => AntiCoverInstance?.InstanceId ?? string.Empty;
    public string GetArmorPenInstanceId() => ArmorPenInstance?.InstanceId ?? string.Empty;

    private void AssociateTargetCoverWithCurrentCell()
    {
        _targetCover.GlobalPosition = _coverTarget.CurrentCell?.WorldPosition
            ?? _coverTarget.Actor.GlobalPosition - Vector3.Up * _coverTarget.PlayerCenterHeight;
        _targetCover.InvalidateCellAssociation();
    }

    private static ItemInstance Add(Inventory inventory, WeaponDefinition definition)
    {
        ItemInstance item = ItemInstance.Create(definition);
        if (!inventory.AddItem(item))
            throw new System.InvalidOperationException($"Impossible d'ajouter {definition.DefinitionId} au prototype V5.");
        return item;
    }

}
