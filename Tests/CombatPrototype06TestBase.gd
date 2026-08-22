extends SceneTree

const SCENE := "res://Scenes/Tests/CombatPrototype06.tscn"
const BALLISTIC := 0
const ENERGY := 1
const NONE := 0
const TOTAL := 3
const PROFILE_NONE := "res://Data/Resistance/DebugResistanceNone.tres"
const PROFILE_STRONG := "res://Data/Resistance/DebugResistanceStrong.tres"
const PROFILE_BALLISTIC_10 := "res://Data/Resistance/Tests/Ballistic10.tres"
const PROFILE_BALLISTIC_30 := "res://Data/Resistance/Tests/Ballistic30.tres"
const PROFILE_BALLISTIC_50 := "res://Data/Resistance/Tests/Ballistic50.tres"
const PROFILE_BALLISTIC_ENERGY_30 := "res://Data/Resistance/Tests/BallisticEnergy30.tres"
const PROFILE_BALLISTIC_ENERGY_100 := "res://Data/Resistance/Tests/BallisticEnergy100.tres"
const PROFILE_BALLISTIC_1000 := "res://Data/Resistance/Tests/Ballistic1000.tres"
const PROFILE_BALLISTIC_1000000 := "res://Data/Resistance/Tests/Ballistic1000000.tres"

var failures: Array[String] = []

func _check(condition: bool, message: String) -> void:
	if condition:
		print("PASS: ", message)
	else:
		failures.append(message)
		push_error("FAIL: " + message)

func _wait_frames(count: int) -> void:
	for _frame in count:
		await physics_frame

func _spawn() -> Dictionary:
	var prototype := (load(SCENE) as PackedScene).instantiate()
	root.add_child(prototype)
	var nodes := {
		"root": prototype,
		"controller": prototype.get_node("CombatV5/CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/CombatModeController"),
		"grid": prototype.get_node("CombatV5/CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/TacticalGrid"),
		"turns": prototype.get_node("CombatV5/CombatV4/InventoryV1/CombatV3/CombatV2/TurnManager"),
		"actions": prototype.get_node("CombatV5/CombatV4/CombatV4ActionController"),
		"rules": prototype.get_node("CombatV5/CombatV5RulesService"),
		"damage": prototype.get_node("DamageResolutionService"),
		"setup": prototype.get_node("PrototypeSetupV6"),
		"v5_setup": prototype.get_node("CombatV5/PrototypeSetupV5"),
		"inventory_setup": prototype.get_node("CombatV5/CombatV4/InventoryV1/PrototypeSetup"),
		"a": prototype.get_node("CombatV5/CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/TacticalUnit"),
		"b": prototype.get_node("CombatV5/CombatV4/InventoryV1/CombatV3/CombatV2/UnitB"),
		"c": prototype.get_node("CombatV5/CombatV4/InventoryV1/CombatV3/CombatV2/UnitC"),
		"d": prototype.get_node("CombatV5/CombatV4/InventoryV1/CombatV3/CombatV2/ReinforcementUnit"),
		"res_a": prototype.get_node("ResistanceProfiles/UnitA"),
		"res_b": prototype.get_node("ResistanceProfiles/UnitB"),
		"res_c": prototype.get_node("ResistanceProfiles/UnitC"),
		"res_d": prototype.get_node("ResistanceProfiles/UnitD"),
		"panel": prototype.get_node("DebugUIV6/CombatV6DebugPanel"),
	}
	for _frame in 240:
		await physics_frame
		if bool(nodes.grid.call("GetIsBuilt")):
			break
	_check(bool(nodes.controller.call("EnterCombat")), "CombatPrototype06 entre en combat")
	await _wait_frames(5)
	return nodes

func _dispose(nodes: Dictionary) -> void:
	if bool(nodes.controller.call("GetIsCombatActive")):
		nodes.controller.call("ExitCombat")
	nodes.root.queue_free()
	await process_frame
	await process_frame

func _set_profile(provider: Node, profile_path: String) -> void:
	provider.set("Profile", load(profile_path))

func _preview(nodes: Dictionary, target: Node, weapon: Resource, multiplier := 1.0) -> bool:
	return bool(nodes.damage.call("PreviewDamage", target, weapon, multiplier))

func _declare_and_resolve(nodes: Dictionary, target_name := "UNITE B") -> bool:
	if not bool(nodes.actions.call("BeginAttackSelection")) or not bool(nodes.actions.call("SelectTargetByName", target_name)) or not bool(nodes.actions.call("DeclareSelectedAttack")):
		return false
	var guard := 0
	while bool(nodes.actions.call("GetHasOffensiveOpportunity")) and guard < 8:
		nodes.actions.call("RefuseReaction")
		guard += 1
	if bool(nodes.actions.call("GetHasPendingDefensiveReaction")):
		nodes.actions.call("RefuseDefensiveReaction")
	return true

func _cell_offset(nodes: Dictionary, unit: Node, offset: Vector3) -> int:
	var position := nodes.grid.call("GetCellWorldPosition", int(unit.call("GetCurrentCellId"))) as Vector3
	return int(nodes.grid.call("GetCellIdNearWorld", position + offset, 0.9))

func _wait_pending(actions: Node, maximum := 240) -> bool:
	for _frame in maximum:
		if bool(actions.call("GetHasOffensiveOpportunity")):
			return true
		await physics_frame
	return false

func _finish(label: String) -> void:
	if failures.is_empty():
		print(label + ": SUCCESS")
		quit(0)
	else:
		print("%s: %d FAILURE(S)" % [label, failures.size()])
		quit(1)
