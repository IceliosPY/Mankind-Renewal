extends SceneTree

const SCENE := "res://Scenes/Tests/CombatPrototype05.tscn"
const NONE := 0
const LIGHT := 1
const HEAVY := 2
const TOTAL := 3

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
		"controller": prototype.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/CombatModeController"),
		"grid": prototype.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/TacticalGrid"),
		"turns": prototype.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/TurnManager"),
		"actions": prototype.get_node("CombatV4/CombatV4ActionController"),
		"rules": prototype.get_node("CombatV5RulesService"),
		"setup": prototype.get_node("PrototypeSetupV5"),
		"a": prototype.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/TacticalUnit"),
		"b": prototype.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/UnitB"),
		"c": prototype.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/UnitC"),
		"d": prototype.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/ReinforcementUnit"),
		"reaction_wall": prototype.get_node("TestZones/ReactionWallProvider"),
	}
	for _frame in 240:
		await physics_frame
		if bool(nodes.grid.call("GetIsBuilt")):
			break
	nodes.controller.call("EnterCombat")
	await _wait_frames(5)
	return nodes

func _dispose(nodes: Dictionary) -> void:
	if bool(nodes.controller.call("GetIsCombatActive")):
		nodes.controller.call("ExitCombat")
	nodes.root.queue_free()
	await process_frame
	await process_frame

func _evaluate(nodes: Dictionary, attacker := "UNITE A", target := "UNITE B") -> bool:
	return bool(nodes.rules.call("EvaluateByUnitNames", attacker, target))

func _select_attack(nodes: Dictionary, target := "UNITE B") -> bool:
	return bool(nodes.actions.call("BeginAttackSelection")) and bool(nodes.actions.call("SelectTargetByName", target))

func _declare_and_refuse_offensive(nodes: Dictionary, target := "UNITE B") -> bool:
	if not _select_attack(nodes, target) or not bool(nodes.actions.call("DeclareSelectedAttack")):
		return false
	var guard := 0
	while bool(nodes.actions.call("GetHasOffensiveOpportunity")) and guard < 8:
		nodes.actions.call("RefuseReaction")
		guard += 1
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
