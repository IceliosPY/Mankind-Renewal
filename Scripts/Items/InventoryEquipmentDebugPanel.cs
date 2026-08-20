using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using MankindRenewal.Combat;
using MankindRenewal.Combat.Weapons;
using MankindRenewal.Equipment;
using MankindRenewal.Tests;

namespace MankindRenewal.Items;

public partial class InventoryEquipmentDebugPanel : PanelContainer
{
    [Export] public NodePath SetupPath { get; set; } = new();
    [Export] public NodePath ActionControllerPath { get; set; } = new();

    private InventoryEquipmentPrototypeSetup _setup = null!;
    private CombatActionController _actionController = null!;
    private OptionButton _unitSelector = null!;
    private OptionButton _itemSelector = null!;
    private Label _equipmentStatus = null!;
    private Label _selectedItemStatus = null!;
    private Label _inventoryList = null!;
    private Label _operationStatus = null!;
    private readonly List<EquipmentLoadout> _loadouts = new();
    private readonly List<ItemInstance> _displayedItems = new();
    private EquipmentLoadout? _selectedLoadout;
    private ItemInstance? _selectedItem;
    private double _refreshCountdown;
    private int _lastInventoryCount = -1;

    public override void _Ready()
    {
        _setup = GetNode<InventoryEquipmentPrototypeSetup>(SetupPath);
        _actionController = GetNode<CombatActionController>(ActionControllerPath);
        _unitSelector = GetNode<OptionButton>("Margin/VBox/UnitSelector");
        _itemSelector = GetNode<OptionButton>("Margin/VBox/ItemSelector");
        _equipmentStatus = GetNode<Label>("Margin/VBox/EquipmentStatus");
        _selectedItemStatus = GetNode<Label>("Margin/VBox/SelectedItemStatus");
        _operationStatus = GetNode<Label>("Margin/VBox/OperationStatus");
        _inventoryList = GetNode<Label>("Margin/VBox/InventoryScroll/InventoryList");

        _unitSelector.ItemSelected += OnUnitSelected;
        _itemSelector.ItemSelected += OnItemSelected;
        GetNode<Button>("Margin/VBox/EquipRow/EquipPrimary").Pressed += () => EquipSelected(InventoryEquipmentPrototypeSetup.PrimaryWeaponSlotId);
        GetNode<Button>("Margin/VBox/EquipRow/EquipSecondary").Pressed += () => EquipSelected(InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId);
        GetNode<Button>("Margin/VBox/UnequipRow/UnequipPrimary").Pressed += () => Unequip(InventoryEquipmentPrototypeSetup.PrimaryWeaponSlotId);
        GetNode<Button>("Margin/VBox/UnequipRow/UnequipSecondary").Pressed += () => Unequip(InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId);
        GetNode<Button>("Margin/VBox/ActiveRow/ActivePrimary").Pressed += () => SetActive(InventoryEquipmentPrototypeSetup.PrimaryWeaponSlotId);
        GetNode<Button>("Margin/VBox/ActiveRow/ActiveSecondary").Pressed += () => SetActive(InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId);
        GetNode<Button>("Margin/VBox/InventoryActions/AddPistol").Pressed += () => { AddDebugPistol(); };
        GetNode<Button>("Margin/VBox/InventoryActions/RemoveSelected").Pressed += () => { RemoveSelected(); };

        RefreshLoadouts();
        SetProcess(true);
        RefreshDisplay();
    }

    public override void _Process(double delta)
    {
        _refreshCountdown -= delta;
        if (_refreshCountdown > 0.0)
            return;
        _refreshCountdown = 0.1;
        if (_selectedLoadout is not null && _selectedLoadout.Inventory.GetItemCount() != _lastInventoryCount)
            RefreshItems();
        RefreshDisplay();
    }

    public string GetEquipmentStatusText() => _equipmentStatus.Text;
    public string GetSelectedItemStatusText() => _selectedItemStatus.Text;
    public string GetInventoryListText() => _inventoryList.Text;
    public string GetOperationStatusText() => _operationStatus.Text;
    public string GetSelectedUnitName() => _selectedLoadout?.OwnerUnit.UnitDisplayName ?? string.Empty;
    public string GetSelectedItemInstanceId() => _selectedItem?.InstanceId ?? string.Empty;
    public int GetDisplayedItemCount() => _displayedItems.Count;

    public bool SelectUnitByName(string displayName)
    {
        int index = _loadouts.FindIndex(loadout => loadout.OwnerUnit.UnitDisplayName == displayName);
        if (index < 0)
            return false;
        _unitSelector.Select(index);
        SelectLoadout(index);
        return true;
    }

    public bool SelectItemByInstanceId(string instanceId)
    {
        int index = _displayedItems.FindIndex(item => item.InstanceId == instanceId);
        if (index < 0)
            return false;
        _itemSelector.Select(index);
        SelectItem(index);
        return true;
    }

    public bool EquipSelected(string slotId)
    {
        if (OperationsLocked() || _selectedLoadout is null || _selectedItem is null)
            return SetOperationResult(false, "EQUIPEMENT REFUSE");
        bool result = _selectedLoadout.Equip(_selectedItem, slotId);
        return SetOperationResult(result, result ? "OBJET EQUIPE" : "EQUIPEMENT REFUSE");
    }

    public bool Unequip(string slotId)
    {
        if (OperationsLocked() || _selectedLoadout is null)
            return SetOperationResult(false, "DESEQUIPEMENT REFUSE");
        bool result = _selectedLoadout.Unequip(slotId);
        return SetOperationResult(result, result ? "SLOT DESEQUIPE" : "SLOT DEJA VIDE");
    }

    public bool SetActive(string slotId)
    {
        if (OperationsLocked() || _selectedLoadout is null)
            return SetOperationResult(false, "CHANGEMENT REFUSE");
        bool result = _selectedLoadout.SetActiveSlot(slotId);
        return SetOperationResult(result, result ? "ARME ACTIVE MODIFIEE (DEBUG GRATUIT)" : "SLOT ACTIF INVALIDE");
    }

    public bool AddDebugPistol()
    {
        if (OperationsLocked() || _selectedLoadout is null)
            return SetOperationResult(false, "AJOUT REFUSE");
        ItemInstance? created = _setup.CreateAndAdd(_selectedLoadout.OwnerUnit.UnitDisplayName, "weapon.debug_pistol");
        if (created is null)
            return SetOperationResult(false, "AJOUT REFUSE");
        RefreshItems(created.InstanceId);
        return SetOperationResult(true, "NOUVELLE INSTANCE AJOUTEE");
    }

    public bool RemoveSelected()
    {
        if (OperationsLocked() || _selectedLoadout is null || _selectedItem is null)
            return SetOperationResult(false, "RETRAIT REFUSE");
        bool result = _selectedLoadout.Inventory.RemoveItem(_selectedItem);
        if (result)
            RefreshItems();
        return SetOperationResult(result, result ? "INSTANCE RETIREE" : "RETRAIT REFUSE : OBJET EQUIPE OU INVALIDE");
    }

    private void RefreshLoadouts()
    {
        _loadouts.Clear();
        _loadouts.AddRange(GetTree().GetNodesInGroup("equipment_loadouts")
            .OfType<EquipmentLoadout>()
            .OrderBy(loadout => loadout.OwnerUnit.UnitDisplayName));
        _unitSelector.Clear();
        foreach (EquipmentLoadout loadout in _loadouts)
            _unitSelector.AddItem(loadout.OwnerUnit.UnitDisplayName);
        if (_loadouts.Count > 0)
        {
            _unitSelector.Select(0);
            SelectLoadout(0);
        }
    }

    private void RefreshItems(string preferredInstanceId = "")
    {
        preferredInstanceId = string.IsNullOrEmpty(preferredInstanceId) ? _selectedItem?.InstanceId ?? string.Empty : preferredInstanceId;
        _displayedItems.Clear();
        _itemSelector.Clear();
        if (_selectedLoadout is null)
            return;
        _displayedItems.AddRange(_selectedLoadout.Inventory.Items);
        foreach (ItemInstance item in _displayedItems)
            _itemSelector.AddItem($"{item.Definition?.DisplayName ?? "?"} [{Abbreviate(item.InstanceId)}]");
        int index = _displayedItems.FindIndex(item => item.InstanceId == preferredInstanceId);
        if (index < 0 && _displayedItems.Count > 0)
            index = 0;
        _selectedItem = index >= 0 ? _displayedItems[index] : null;
        if (index >= 0)
            _itemSelector.Select(index);
        _lastInventoryCount = _selectedLoadout.Inventory.GetItemCount();
    }

    private void RefreshDisplay()
    {
        if (_selectedLoadout is null)
            return;
        string primaryId = InventoryEquipmentPrototypeSetup.PrimaryWeaponSlotId;
        string secondaryId = InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId;
        WeaponDefinition? activeWeapon = _selectedLoadout.GetActiveWeapon();
        _equipmentStatus.Text =
            $"WEAPON SLOT 1 : {FormatSlot(_selectedLoadout.GetEquippedItem(primaryId))}\n" +
            $"WEAPON SLOT 2 : {FormatSlot(_selectedLoadout.GetEquippedItem(secondaryId))}\n" +
            $"ARME ACTIVE : {activeWeapon?.DisplayName ?? "AUCUNE ARME EQUIPEE"}\n" +
            $"COUT {activeWeapon?.ActionPointCost ?? 0} PA | PRECISION {activeWeapon?.BaseAccuracy ?? 0} | PORTEE {activeWeapon?.RangeInCells ?? 0}\n" +
            $"DEGATS BRUTS : {activeWeapon?.GetRawDamage() ?? 0:0.##} | PARADE : {(activeWeapon?.AllowsParry == true ? "OUI" : "NON")}";

        _selectedItemStatus.Text = _selectedItem is null
            ? "INSTANCE SELECTIONNEE : -"
            : $"INSTANCE SELECTIONNEE : {_selectedItem.Definition?.DisplayName}\n" +
              $"DefinitionId : {_selectedItem.Definition?.DefinitionId}\n" +
              $"InstanceId : {_selectedItem.InstanceId}\n" +
              $"ETAT : {GetEquipmentState(_selectedItem)}";

        StringBuilder inventory = new("INVENTAIRE — OBJETS POSSEDES\n\n");
        foreach (ItemInstance item in _selectedLoadout.Inventory.Items)
        {
            inventory.AppendLine(item.Definition?.DisplayName ?? "?");
            inventory.AppendLine(item.Definition?.DefinitionId ?? "-");
            inventory.AppendLine($"Instance : {Abbreviate(item.InstanceId)}");
            inventory.AppendLine($"[{GetEquipmentState(item)}]");
            inventory.AppendLine();
        }
        _inventoryList.Text = inventory.ToString();
    }

    private void OnUnitSelected(long index) => SelectLoadout((int)index);
    private void OnItemSelected(long index) => SelectItem((int)index);

    private void SelectLoadout(int index)
    {
        _selectedLoadout = index >= 0 && index < _loadouts.Count ? _loadouts[index] : null;
        _selectedItem = null;
        _operationStatus.Text = "CHANGEMENT D'ARME : GRATUIT (DEBUG V1)";
        RefreshItems();
        RefreshDisplay();
    }

    private void SelectItem(int index)
    {
        _selectedItem = index >= 0 && index < _displayedItems.Count ? _displayedItems[index] : null;
        RefreshDisplay();
    }

    private string GetEquipmentState(ItemInstance item)
    {
        if (_selectedLoadout is null)
            return "INVENTAIRE";
        string slotId = _selectedLoadout.GetEquippedSlotId(item);
        if (string.IsNullOrEmpty(slotId))
            return "INVENTAIRE";
        string slotName = slotId == InventoryEquipmentPrototypeSetup.PrimaryWeaponSlotId ? "EQUIPE SLOT 1" : "EQUIPE SLOT 2";
        return _selectedLoadout.ActiveSlotId == slotId ? $"{slotName} — ACTIVE" : slotName;
    }

    private bool SetOperationResult(bool result, string message)
    {
        _operationStatus.Text = message;
        RefreshDisplay();
        return result;
    }

    private bool OperationsLocked() => _actionController.GetHasPendingReaction();
    private static string FormatSlot(ItemInstance? item) => item is null ? "VIDE" : $"{item.Definition?.DisplayName} [{Abbreviate(item.InstanceId)}]";
    private static string Abbreviate(string id) => id.Length <= 8 ? id : id[..8];
}
