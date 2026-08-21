extends SceneTree

const SCENE := "res://Scenes/Tests/CombatPrototype05.tscn"
const NONE := 0

var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("_run")

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
		"actions": prototype.get_node("CombatV4/CombatV4ActionController"),
		"setup": prototype.get_node("PrototypeSetupV5"),
		"a": prototype.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/TacticalUnit"),
	}
	for _frame in 240:
		await physics_frame
		if bool(nodes.grid.call("GetIsBuilt")):
			break
	nodes.controller.call("EnterCombat")
	await _wait_frames(5)
	return nodes

func _dispose(nodes: Dictionary) -> void:
	nodes.controller.call("ExitCombat")
	nodes.root.queue_free()
	await process_frame
	await process_frame

func _cell_offset(nodes: Dictionary, offset: Vector3) -> int:
	var position := nodes.grid.call("GetCellWorldPosition", int(nodes.a.call("GetCurrentCellId"))) as Vector3
	return int(nodes.grid.call("GetCellIdNearWorld", position + offset, 0.9))

func _wait_pending(actions: Node, maximum := 240) -> bool:
	for _frame in maximum:
		if bool(actions.call("GetHasOffensiveOpportunity")):
			return true
		await physics_frame
	return false

func _resolve_first_reaction(nodes: Dictionary) -> void:
	nodes.actions.call("ChooseReaction", 0)
	if bool(nodes.actions.call("GetHasOffensiveOpportunity")):
		nodes.actions.call("RefuseReaction")

func _run() -> void:
	var modified := await _spawn()
	modified.setup.call("SetCoverMode", NONE)
	var destination := _cell_offset(modified, Vector3(0, 0, -4))
	_check(destination >= 0 and bool(modified.controller.call("TrySelectDestinationCellId", destination)) and await _wait_pending(modified.actions), "Le scenario MODIFY V5 atteint une opportunite valide")
	var modified_id := int(modified.actions.call("GetCurrentMovementActionId"))
	_resolve_first_reaction(modified)
	var modified_target := _cell_offset(modified, Vector3(-2, 0, 0))
	_check(bool(modified.actions.call("GetIsAwaitingMovementChoice")) and bool(modified.actions.call("BeginModifyMovement")) and modified_target >= 0 and bool(modified.controller.call("TrySelectDestinationCellId", modified_target)), "MODIFY V5 recalcule depuis la cellule interrompue")
	await _wait_frames(90)
	_check(int(modified.actions.call("GetLastCompletedMovementActionId")) == modified_id, "MODIFY V5 conserve le meme ActionId")
	await _dispose(modified)

	var zero_step := await _spawn()
	zero_step.setup.call("SetCoverMode", NONE)
	destination = _cell_offset(zero_step, Vector3(0, 0, -4))
	_check(destination >= 0 and bool(zero_step.controller.call("TrySelectDestinationCellId", destination)) and await _wait_pending(zero_step.actions), "Le scenario MODIFY zero-step atteint une opportunite valide")
	var zero_step_id := int(zero_step.actions.call("GetCurrentMovementActionId"))
	_resolve_first_reaction(zero_step)
	var current_cell := int(zero_step.a.call("GetCurrentCellId"))
	var zero_step_pm := int(zero_step.a.call("GetCurrentMovementPoints"))
	_check(bool(zero_step.actions.call("GetIsAwaitingMovementChoice")) and bool(zero_step.actions.call("BeginModifyMovement")) and bool(zero_step.controller.call("TrySelectDestinationCellId", current_cell)), "MODIFY accepte la cellule actuelle comme destination sans trajet restant")
	await _wait_frames(3)
	_check(int(zero_step.actions.call("GetLastCompletedMovementActionId")) == zero_step_id and int(zero_step.actions.call("GetCurrentMovementActionId")) == 0 and int(zero_step.a.call("GetCurrentMovementPoints")) == zero_step_pm and not bool(zero_step.actions.call("GetIsMovementInProgress")) and not bool(zero_step.a.call("GetIsMoving")), "MODIFY zero-step termine la meme ActionId sans PM supplementaire ni mouvement fantome")
	await _dispose(zero_step)

	var stopped := await _spawn()
	stopped.setup.call("SetCoverMode", NONE)
	destination = _cell_offset(stopped, Vector3(0, 0, -4))
	_check(destination >= 0 and bool(stopped.controller.call("TrySelectDestinationCellId", destination)) and await _wait_pending(stopped.actions), "Le scenario STOP V5 atteint une opportunite valide")
	var stopped_id := int(stopped.actions.call("GetCurrentMovementActionId"))
	_resolve_first_reaction(stopped)
	_check(bool(stopped.actions.call("GetIsAwaitingMovementChoice")) and bool(stopped.actions.call("StopMovement")), "STOP V5 termine l'action interrompue")
	var fresh_target := _cell_offset(stopped, Vector3(-2, 0, 0))
	_check(fresh_target >= 0 and bool(stopped.controller.call("TrySelectDestinationCellId", fresh_target)) and await _wait_pending(stopped.actions), "Un nouveau mouvement V5 peut de nouveau proposer une reaction")
	_check(int(stopped.actions.call("GetCurrentMovementActionId")) != stopped_id, "STOP puis nouveau mouvement V5 cree un nouvel ActionId")
	await _dispose(stopped)

	if failures.is_empty():
		print("COMBAT_PROTOTYPE_05_MOVEMENT_CHOICES_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("COMBAT_PROTOTYPE_05_MOVEMENT_CHOICES_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
