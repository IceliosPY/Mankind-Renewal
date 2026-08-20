extends SceneTree

const PRIMARY := "weapon.primary"
const SECONDARY := "weapon.secondary"

var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("_run")

func _check(condition: bool, message: String) -> void:
	if condition:
		print("PASS: ", message)
	else:
		failures.append(message)
		push_error("FAIL: " + message)

func _wait_physics_frames(count: int) -> void:
	for _frame in count:
		await physics_frame

func _begin_select(action_controller: Node, target_name: String) -> bool:
	return bool(action_controller.call("BeginAttackSelection")) and bool(action_controller.call("SelectTargetByName", target_name))

func _run() -> void:
	_check(ResourceLoader.exists("res://Scenes/Tests/InventoryEquipmentPrototype01.tscn"), "La scene InventoryEquipmentPrototype01 existe")
	var packed := load("res://Scenes/Tests/InventoryEquipmentPrototype01.tscn") as PackedScene
	_check(packed != null, "InventoryEquipmentPrototype01 se charge")
	if packed == null:
		quit(1)
		return

	var prototype := packed.instantiate()
	root.add_child(prototype)
	var combat_v3 := prototype.get_node_or_null("CombatV3") as Node3D
	var setup := prototype.get_node_or_null("PrototypeSetup") as Node
	var inventory_panel := prototype.get_node_or_null("InventoryDebugUI/InventoryEquipmentDebugPanel") as Control
	var combat_panel := prototype.get_node_or_null("CombatV3/DebugUI/CombatV3DebugPanel") as Control
	var controller := prototype.get_node_or_null("CombatV3/CombatV2/CombatV1/CombatModeController") as Node
	var turn_manager := prototype.get_node_or_null("CombatV3/CombatV2/TurnManager") as Node
	var action_controller := prototype.get_node_or_null("CombatV3/CombatActionController") as Node
	var grid := prototype.get_node_or_null("CombatV3/CombatV2/CombatV1/TacticalGrid") as Node3D
	var display_controller := prototype.get_node_or_null("CombatV3/CombatV2/WindowDisplayController") as Node
	var unit_a := prototype.get_node_or_null("CombatV3/CombatV2/CombatV1/TacticalUnit") as Node
	var unit_b := prototype.get_node_or_null("CombatV3/CombatV2/UnitB") as Node
	var a_inventory := prototype.get_node_or_null("InventorySystems/UnitAInventory") as Node
	var b_inventory := prototype.get_node_or_null("InventorySystems/UnitBInventory") as Node
	var a_loadout := prototype.get_node_or_null("InventorySystems/UnitAEquipment") as Node
	var b_loadout := prototype.get_node_or_null("InventorySystems/UnitBEquipment") as Node
	_check(combat_v3 != null, "Le prototype Inventory instancie CombatPrototype03 sans le dupliquer")
	_check(setup != null and inventory_panel != null and controller != null and turn_manager != null and action_controller != null and grid != null, "Setup, UI, combat, tours et grille sont separes")
	_check(unit_a != null and unit_b != null and a_inventory != null and b_inventory != null and a_loadout != null and b_loadout != null, "Chaque unite de test possede ses composants independants")
	if setup == null or inventory_panel == null or combat_panel == null or controller == null or turn_manager == null or action_controller == null or grid == null or display_controller == null or unit_a == null or unit_b == null or a_inventory == null or b_inventory == null or a_loadout == null or b_loadout == null:
		quit(1)
		return

	for _frame in 240:
		await physics_frame
		if bool(grid.call("GetIsBuilt")):
			break
	_check(bool(grid.call("GetIsBuilt")), "La grille Combat V3 reste fonctionnelle")

	# Definition / instance identity.
	var pistol_a_id := str(setup.call("GetUnitAPistolAId"))
	var pistol_b_id := str(setup.call("GetUnitAPistolBId"))
	var blade_id := str(setup.call("GetUnitABladeId"))
	var pistol_a: Variant = a_inventory.call("FindByInstanceId", pistol_a_id)
	var pistol_b: Variant = a_inventory.call("FindByInstanceId", pistol_b_id)
	var blade: Variant = a_inventory.call("FindByInstanceId", blade_id)
	_check(not pistol_a_id.is_empty() and not pistol_b_id.is_empty() and pistol_a_id != pistol_b_id, "Deux DebugPistol recoivent des InstanceId uniques")
	_check(pistol_a != null and pistol_b != null and blade != null, "Les instances initiales sont recherchables par identifiant")
	_check(str(pistol_a.call("GetDefinitionId")) == "weapon.debug_pistol" and str(pistol_b.call("GetDefinitionId")) == "weapon.debug_pistol", "Les deux pistolets partagent le meme DefinitionId stable")
	_check(pistol_a.call("GetDefinition") == pistol_b.call("GetDefinition"), "Les deux instances referencent exactement la meme WeaponDefinition")
	_check(bool(pistol_a.call("GetIsValidInstance")) and bool(pistol_b.call("GetIsValidInstance")), "Les ItemInstance initiales sont valides")

	# Inventory authority, duplicates, lookup, removal, isolation.
	_check(int(a_inventory.call("GetItemCount")) == 4 and int(b_inventory.call("GetItemCount")) == 2, "Les inventaires A et B sont separes")
	_check(not bool(a_inventory.call("AddItem", pistol_a)), "La meme instance ne peut pas etre ajoutee deux fois")
	_check(not bool(b_inventory.call("AddItem", pistol_a)), "Une instance deja possedee ne peut pas appartenir a deux inventaires")
	var duplicate_id: Variant = setup.call("CreateLooseInstance", "weapon.debug_pistol")
	duplicate_id.set("InstanceId", pistol_a_id)
	_check(not bool(a_inventory.call("AddItem", duplicate_id)), "Un InstanceId duplique est refuse")
	var loose_rifle: Variant = setup.call("CreateLooseInstance", "weapon.debug_rifle")
	_check(loose_rifle != null and bool(b_inventory.call("AddItem", loose_rifle)), "Une nouvelle instance peut etre ajoutee")
	_check(bool(b_inventory.call("HasInstanceId", loose_rifle.get("InstanceId"))) and b_inventory.call("FindByInstanceId", loose_rifle.get("InstanceId")) == loose_rifle, "Recherche et possession utilisent l'identite d'instance")
	_check(bool(b_inventory.call("RemoveItem", loose_rifle)) and not bool(b_inventory.call("HasItem", loose_rifle)), "Une instance non equipee peut etre retiree")

	# Initial equipment and identical-instance isolation.
	_check(str(a_loadout.call("GetEquippedInstanceId", PRIMARY)) == pistol_a_id and str(a_loadout.call("GetEquippedInstanceId", SECONDARY)) == blade_id, "A commence avec Pistol A en Slot 1 et Blade en Slot 2")
	_check(str(a_loadout.call("GetActiveSlotId")) == PRIMARY and str(a_loadout.call("GetActiveWeaponName")) == "Debug Pistol", "Weapon Slot 1 est l'arme active initiale")
	_check(not bool(a_loadout.call("IsEquipped", pistol_b)), "Equiper Pistol A ne marque jamais Pistol B comme equipe")
	_check(bool(a_loadout.call("Equip", pistol_b, PRIMARY)), "Pistol B remplace proprement Pistol A dans le Slot 1")
	_check(bool(a_inventory.call("HasItem", pistol_a)) and not bool(a_loadout.call("IsEquipped", pistol_a)), "L'ancien Pistol A reste possede et devient non equipe")
	_check(not bool(a_loadout.call("Equip", pistol_b, SECONDARY)), "Une meme instance ne peut pas etre equipee dans deux slots")
	_check(not bool(a_inventory.call("RemoveItem", pistol_b)), "Le retrait d'une instance equipee est refuse")

	# Ownership and compatibility validation.
	var non_owned_pistol: Variant = setup.call("CreateLooseInstance", "weapon.debug_pistol")
	_check(not bool(a_loadout.call("Equip", non_owned_pistol, PRIMARY)), "Un objet non possede ne peut pas etre equipe")
	var utility: Variant = setup.call("CreateAndAdd", "UNITE A", "utility.debug_scanner")
	_check(utility != null and not bool(a_loadout.call("Equip", utility, PRIMARY)), "Un ItemDefinition non arme est incompatible avec un slot d'arme")
	_check(bool(a_inventory.call("RemoveItem", utility)), "L'objet incompatible reste une instance normale retirable")

	# Unequip policy: active falls back to another occupied weapon, then none.
	_check(bool(a_loadout.call("Unequip", PRIMARY)) and str(a_loadout.call("GetActiveSlotId")) == SECONDARY, "Desequiper l'arme active selectionne l'autre arme equipee")
	_check(bool(a_inventory.call("RemoveItem", pistol_b)), "L'instance remplacee peut etre retiree une fois desequipee")
	_check(bool(a_loadout.call("Unequip", SECONDARY)) and not bool(a_loadout.call("GetHasActiveWeapon")), "Sans autre arme equipee, ActiveWeapon devient null")
	_check(not bool(a_loadout.call("SetActiveSlot", PRIMARY)) and not bool(a_loadout.call("SetActiveSlot", SECONDARY)), "Un slot vide ne peut pas devenir actif")
	_check(str(unit_a.call("GetActiveWeaponName")) == "", "TacticalUnit ne cree aucun fallback magique sans arme")
	_check(not bool(action_controller.call("BeginAttackSelection")), "ATTAQUER est indisponible sans arme active")

	# Restore production loadout and dynamic selection.
	_check(bool(a_loadout.call("Equip", pistol_a, PRIMARY)) and bool(a_loadout.call("Equip", blade, SECONDARY)), "Les memes ItemInstance peuvent etre reequipees sans copie")
	_check(bool(a_loadout.call("SetActiveSlot", PRIMARY)) and str(unit_a.call("GetActiveWeaponName")) == "Debug Pistol", "IActiveWeaponProvider expose le pistolet reequipe")
	_check(bool(a_loadout.call("SetActiveSlot", SECONDARY)) and str(unit_a.call("GetActiveWeaponName")) == "Debug Blade", "Le changement vers la lame est dynamique")
	_check(int(unit_a.call("GetEffectiveAccuracy")) == 20, "La precision provient de l'arme active reelle")
	_check(bool(a_loadout.call("SetActiveSlot", PRIMARY)), "Le Slot 1 peut redevenir actif gratuitement en DEBUG")

	# Debug UI: multiple units, instances, add/remove and equipped-state safety.
	await _wait_physics_frames(4)
	_check(bool(inventory_panel.call("SelectUnitByName", "UNITE A")) and int(inventory_panel.call("GetDisplayedItemCount")) == 3, "L'UI inspecte l'inventaire propre de A")
	_check("weapon.debug_pistol" in str(inventory_panel.call("GetInventoryListText")) and "EQUIPE SLOT 1" in str(inventory_panel.call("GetInventoryListText")), "L'UI affiche DefinitionId, InstanceId abrege et etat equipe")
	_check(bool(inventory_panel.call("SelectUnitByName", "UNITE B")) and int(inventory_panel.call("GetDisplayedItemCount")) == 2, "Changer d'unite affiche uniquement l'inventaire de B")
	inventory_panel.call("SelectUnitByName", "UNITE A")
	var before_ui_add := int(a_inventory.call("GetItemCount"))
	_check(bool(inventory_panel.call("AddDebugPistol")) and int(a_inventory.call("GetItemCount")) == before_ui_add + 1, "L'UI ajoute une nouvelle instance de test")
	_check(bool(inventory_panel.call("RemoveSelected")) and int(a_inventory.call("GetItemCount")) == before_ui_add, "L'UI retire l'instance non equipee selectionnee")
	inventory_panel.call("SelectItemByInstanceId", pistol_a_id)
	_check(not bool(inventory_panel.call("RemoveSelected")) and "OBJET EQUIPE" in str(inventory_panel.call("GetOperationStatusText")), "L'UI refuse clairement le retrait d'un objet equipe")

	# Combat uses EquipmentLoadout without pipeline changes.
	_check(bool(controller.call("EnterCombat")), "Le prototype Inventory entre dans Combat V3")
	await _wait_physics_frames(8)
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE A" and str(unit_a.call("GetActiveWeaponName")) == "Debug Pistol", "A commence le combat avec son pistolet equipe")
	_check("ARME ACTIVE : Debug Pistol" in str(combat_panel.call("GetStatusText")), "Le panneau Combat lit immediatement EquipmentLoadout")
	_check(_begin_select(action_controller, "UNITE B") and bool(action_controller.call("DeclareSelectedAttack")), "Le pistolet equipe declare une attaque distante")
	_check(str(action_controller.call("GetOfferedReactionText")) == "DODGE" and bool(action_controller.call("RefuseReaction")), "Le pistolet propose Esquive puis se resout")
	_check(int(action_controller.call("GetLastAppliedDamage")) == 20 and int(unit_b.call("GetCurrentHealth")) == 80, "Les degats correspondent au DebugPistol actif")
	var a_pa_after_pistol := int(unit_a.call("GetCurrentActionPoints"))
	_check(bool(a_loadout.call("SetActiveSlot", SECONDARY)) and int(unit_a.call("GetCurrentActionPoints")) == a_pa_after_pistol, "Le changement d'arme V1 est gratuit et marque DEBUG")
	await _wait_physics_frames(8)
	_check("ARME ACTIVE : Debug Blade" in str(combat_panel.call("GetStatusText")) and "PORTEE : 1" in str(combat_panel.call("GetStatusText")) and "24 BAL + 1 ENE" in str(combat_panel.call("GetStatusText")), "Le panneau Combat actualise arme, portee, cout et composantes sans relancer la scene")
	_check(_begin_select(action_controller, "UNITE B") and bool(action_controller.call("DeclareSelectedAttack")), "La lame equipee utilise automatiquement le ciblage melee")
	_check(str(action_controller.call("GetOfferedReactionText")) == "PARRY" and bool(action_controller.call("RefuseReaction")), "La cible equipee d'une lame peut Parer")
	_check(int(action_controller.call("GetLastAppliedDamage")) == 25 and int(unit_b.call("GetCurrentHealth")) == 55, "Les composantes de la DebugBlade active infligent 25")

	# Defender active equipment controls parry availability.
	controller.call("RequestEndTurn")
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE B", "B devient active sans changement des regles de tours")
	_check(str(a_loadout.call("GetActiveWeaponName")) == "Debug Blade", "A defend initialement avec sa lame active")
	_check(_begin_select(action_controller, "UNITE A") and bool(action_controller.call("DeclareSelectedAttack")) and str(action_controller.call("GetOfferedReactionText")) == "PARRY", "Parade disponible quand le defenseur a DebugBlade active")
	_check(bool(action_controller.call("AcceptReaction")), "La premiere attaque de B resout la Parade")
	_check(bool(a_loadout.call("SetActiveSlot", PRIMARY)) and str(a_loadout.call("GetActiveWeaponName")) == "Debug Pistol", "A change son equipement reel vers le pistolet")
	_check(_begin_select(action_controller, "UNITE A") and bool(action_controller.call("DeclareSelectedAttack")), "B peut declarer une seconde attaque melee")
	_check(not bool(action_controller.call("GetHasPendingReaction")) and str(action_controller.call("GetOfferedReactionText")) == "NONE", "Parade indisponible quand le defenseur a DebugPistol actif")
	_check(int(action_controller.call("GetLastAppliedDamage")) == 25, "La seconde attaque sans Parade utilise toujours l'arme active de B")

	# Responsive UI at all required reference sizes.
	for size in [Vector2i(1280, 720), Vector2i(1600, 900), Vector2i(1920, 1080)]:
		root.size = size
		await process_frame
		var viewport_rect := prototype.get_viewport().get_visible_rect()
		var inventory_rect := inventory_panel.get_global_rect()
		var combat_rect := combat_panel.get_global_rect()
		_check(inventory_rect.position.x >= 0 and inventory_rect.position.y >= 0 and inventory_rect.end.x <= viewport_rect.end.x + 1 and inventory_rect.end.y <= viewport_rect.end.y + 1, "UI Inventory contenue dans %dx%d" % [size.x, size.y])
		_check(combat_rect.end.x < inventory_rect.position.x, "Les panneaux Combat et Inventory ne se chevauchent pas en %dx%d" % [size.x, size.y])
	var scroll := inventory_panel.get_node("Margin/VBox/InventoryScroll") as ScrollContainer
	_check(scroll != null and scroll.clip_contents, "La liste verticale d'inventaire reste scrollable")
	if DisplayServer.get_name() != "headless":
		display_controller.call("ToggleFullscreen")
		await _wait_physics_frames(8)
		_check(bool(display_controller.call("GetIsFullscreen")), "Inventory V1 reste utilisable en fullscreen borderless")
		_check(inventory_panel.get_global_rect().end.x <= prototype.get_viewport().get_visible_rect().end.x + 1, "Le panneau Inventory reste dans le viewport fullscreen")
		display_controller.call("ToggleFullscreen")
		await _wait_physics_frames(8)
		_check(not bool(display_controller.call("GetIsFullscreen")), "Le retour au mode fenetre reste fonctionnel")

	controller.call("ExitCombat")
	await _wait_physics_frames(3)
	_check(str(ProjectSettings.get_setting("application/run/main_scene")) == "res://Scenes/Tests/CombatPrototype03.tscn", "La scene principale reste celle validee avant Inventory V1")

	prototype.queue_free()
	await process_frame
	if failures.is_empty():
		print("INVENTORY_EQUIPMENT_PROTOTYPE_01_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("INVENTORY_EQUIPMENT_PROTOTYPE_01_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
