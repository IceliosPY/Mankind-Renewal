extends SceneTree

const SCENE := "res://Scenes/Tests/CombatPrototype04.tscn"

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
		"controller": prototype.get_node("InventoryV1/CombatV3/CombatV2/CombatV1/CombatModeController"),
		"grid": prototype.get_node("InventoryV1/CombatV3/CombatV2/CombatV1/TacticalGrid"),
		"turns": prototype.get_node("InventoryV1/CombatV3/CombatV2/TurnManager"),
		"actions": prototype.get_node("CombatV4ActionController"),
		"a": prototype.get_node("InventoryV1/CombatV3/CombatV2/CombatV1/TacticalUnit"),
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

func _cell_offset(nodes: Dictionary, offset: Vector3) -> int:
	var position := nodes.grid.call("GetCellWorldPosition", int(nodes.a.call("GetCurrentCellId"))) as Vector3
	return int(nodes.grid.call("GetCellIdNearWorld", position + offset, 0.9))

func _cell_text(nodes: Dictionary, cell_id: int) -> String:
	var cell := nodes.grid.call("GetCellById", cell_id) as Node
	if cell == null:
		return "<none>"
	return "%d:(%d,%d)" % [cell_id, int(cell.get("GridX")), int(cell.get("GridZ"))]

func _wait_pending(actions: Node, maximum := 240) -> bool:
	for _frame in maximum:
		if bool(actions.call("GetHasOffensiveOpportunity")):
			return true
		await physics_frame
	return false

func _wait_movement_finished(a: Node, maximum := 360) -> void:
	for _frame in maximum:
		if not bool(a.call("GetIsMoving")):
			return
		await physics_frame

func _start_move(nodes: Dictionary, offset: Vector3) -> int:
	var destination := _cell_offset(nodes, offset)
	return destination if destination >= 0 and bool(nodes.controller.call("TrySelectDestinationCellId", destination)) else -1

func _assert_unlocked(nodes: Dictionary, message: String) -> void:
	_check(
		not bool(nodes.a.call("GetIsMoving"))
		and not bool(nodes.actions.call("GetIsMovementInProgress"))
		and not bool(nodes.actions.call("GetIsMovementPausedForReaction"))
		and not bool(nodes.actions.call("GetHasPendingReaction"))
		and not bool(nodes.actions.call("GetIsAwaitingMovementChoice"))
		and bool(nodes.actions.call("CanEndTurn", nodes.a)),
		message)

func _run() -> void:
	# A — Exact yellow path from the screenshot: (5,10) -> (5,9).
	# The reaction cell is the original destination, so RemainingPath is empty.
	var yellow := await _spawn()
	var yellow_start := int(yellow.a.call("GetCurrentCellId"))
	var yellow_pm := int(yellow.a.call("GetCurrentMovementPoints"))
	var yellow_destination := _cell_offset(yellow, Vector3(0, 0, -2))
	print("YELLOW_PATH_TRACE start=", _cell_text(yellow, yellow_start),
		" destination=", _cell_text(yellow, yellow_destination),
		" astar=[", _cell_text(yellow, yellow_start), ", ", _cell_text(yellow, yellow_destination), "]")
	_check(int(yellow.grid.call("GetPathLengthBetweenCells", yellow_start, yellow_destination)) == 2, "Trajet jaune reconstruit cellule par cellule")
	_check(bool(yellow.controller.call("TrySelectDestinationCellId", yellow_destination)) and await _wait_pending(yellow.actions), "Le trajet jaune atteint la reaction distante de B")
	var yellow_action := int(yellow.actions.call("GetCurrentMovementActionId"))
	print("YELLOW_PATH_TRACE B cell=", _cell_text(yellow, int(yellow.a.call("GetCurrentCellId"))),
		" pm=", int(yellow.a.call("GetCurrentMovementPoints")), " action=", yellow_action,
		" remaining=[] suspended=", bool(yellow.actions.call("GetIsMovementPausedForReaction")),
		" moving=", bool(yellow.a.call("GetIsMoving")))
	_check(str(yellow.actions.call("GetPendingReactorName")) == "UNITE B" and int(yellow.a.call("GetCurrentCellId")) == yellow_destination and int(yellow.a.call("GetCurrentMovementPoints")) == yellow_pm - 1, "B reagit sur la destination exacte apres 1 PM")
	_check(bool(yellow.actions.call("RefuseReaction")) and str(yellow.actions.call("GetPendingReactorName")) == "UNITE C", "B refuse et C est revalidee sur la meme cellule")
	print("YELLOW_PATH_TRACE after_B_refusal current=", _cell_text(yellow, int(yellow.a.call("GetCurrentCellId"))),
		" remaining=[] suspended=", bool(yellow.actions.call("GetIsMovementPausedForReaction")),
		" pending=", str(yellow.actions.call("GetPendingReactorName")))
	_check(bool(yellow.actions.call("RefuseReaction")), "C refuse la meme ActionId")
	await process_frame
	print("YELLOW_PATH_TRACE after_C_refusal current=", _cell_text(yellow, int(yellow.a.call("GetCurrentCellId"))),
		" action=", int(yellow.actions.call("GetCurrentMovementActionId")),
		" completed=", int(yellow.actions.call("GetLastCompletedMovementActionId")),
		" remaining=[] suspended=", bool(yellow.actions.call("GetIsMovementPausedForReaction")),
		" moving=", bool(yellow.a.call("GetIsMoving")),
		" movement_in_progress=", bool(yellow.actions.call("GetIsMovementInProgress")))
	_check(int(yellow.actions.call("GetLastCompletedMovementActionId")) == yellow_action and int(yellow.actions.call("GetCurrentMovementActionId")) == 0, "La destination atteinte termine la meme ActionId sans reprise current -> current")
	_assert_unlocked(yellow, "Le trajet jaune ne laisse aucun chemin, reaction, choix ou lock fantome")
	await _dispose(yellow)

	# B — Neighboring working path: same first cell, destination one cell farther.
	# B/C are closed before the real remaining segment resumes.
	var neighbor := await _spawn()
	var neighbor_start := int(neighbor.a.call("GetCurrentCellId"))
	var neighbor_pm := int(neighbor.a.call("GetCurrentMovementPoints"))
	var neighbor_destination := _cell_offset(neighbor, Vector3(0, 0, -4))
	print("NEIGHBOR_PATH_TRACE astar=[", _cell_text(neighbor, neighbor_start), ", ", _cell_text(neighbor, _cell_offset(neighbor, Vector3(0, 0, -2))), ", ", _cell_text(neighbor, neighbor_destination), "]")
	_check(int(neighbor.grid.call("GetPathLengthBetweenCells", neighbor_start, neighbor_destination)) == 3 and bool(neighbor.controller.call("TrySelectDestinationCellId", neighbor_destination)) and await _wait_pending(neighbor.actions), "Le trajet voisin partage la cellule reactive mais conserve une cellule restante")
	var neighbor_action := int(neighbor.actions.call("GetCurrentMovementActionId"))
	_check(bool(neighbor.actions.call("RefuseReaction")) and bool(neighbor.actions.call("RefuseReaction")), "B et C ferment le trajet voisin pour la meme ActionId")
	await _wait_frames(120)
	_check(int(neighbor.a.call("GetCurrentCellId")) == neighbor_destination and int(neighbor.a.call("GetCurrentMovementPoints")) == neighbor_pm - 2, "Le RemainingPath reel est parcouru et conserve les PM depenses")
	_check(int(neighbor.actions.call("GetOpportunityOfferCount", neighbor_action, "UNITE B")) == 1 and int(neighbor.actions.call("GetOpportunityOfferCount", neighbor_action, "UNITE C")) == 1, "Le retrigger geometrique en fin de trajet ne repropose ni B ni C")
	_check(int(neighbor.actions.call("GetLastCompletedMovementActionId")) == neighbor_action, "Le trajet voisin termine la meme ActionId")
	_assert_unlocked(neighbor, "Le trajet voisin reste utilisable sans PendingReaction")
	await _dispose(neighbor)

	# C — B accepts, C refuses; CONTINUE at an already reached destination completes.
	var b_accepts := await _spawn()
	var b_accepts_pm := int(b_accepts.a.call("GetCurrentMovementPoints"))
	_check(_start_move(b_accepts, Vector3(0, 0, -2)) >= 0 and await _wait_pending(b_accepts.actions), "B ACCEPT/C REFUSE atteint la cellule jaune")
	var b_accepts_action := int(b_accepts.actions.call("GetCurrentMovementActionId"))
	_check(bool(b_accepts.actions.call("ChooseReaction", 0)) and bool(b_accepts.actions.call("RefuseReaction")), "B accepte et C refuse")
	_check(bool(b_accepts.actions.call("GetIsAwaitingMovementChoice")) and bool(b_accepts.actions.call("ContinueMovement")), "CONTINUE est disponible et termine une destination deja atteinte")
	_check(int(b_accepts.actions.call("GetLastCompletedMovementActionId")) == b_accepts_action and int(b_accepts.a.call("GetCurrentMovementPoints")) == b_accepts_pm - 1, "B ACCEPT/C REFUSE conserve ActionId et PM")
	_assert_unlocked(b_accepts, "B ACCEPT/C REFUSE reprend sans soft-lock")
	await _dispose(b_accepts)

	# D — B refuses, C accepts; same Continue invariant.
	var c_accepts := await _spawn()
	_check(_start_move(c_accepts, Vector3(0, 0, -2)) >= 0 and await _wait_pending(c_accepts.actions), "B REFUSE/C ACCEPT atteint la cellule jaune")
	var c_accepts_action := int(c_accepts.actions.call("GetCurrentMovementActionId"))
	_check(bool(c_accepts.actions.call("RefuseReaction")) and bool(c_accepts.actions.call("ChooseReaction", 0)), "B refuse et C accepte")
	_check(bool(c_accepts.actions.call("ContinueMovement")) and int(c_accepts.actions.call("GetLastCompletedMovementActionId")) == c_accepts_action, "B REFUSE/C ACCEPT termine la meme ActionId avec CONTINUE")
	_assert_unlocked(c_accepts, "B REFUSE/C ACCEPT reprend sans soft-lock")
	await _dispose(c_accepts)

	# E — STOP closes the interrupted action; a fresh move gets a fresh ActionId/reactor.
	var stopped := await _spawn()
	_check(_start_move(stopped, Vector3(0, 0, -2)) >= 0 and await _wait_pending(stopped.actions), "STOP atteint une reaction jaune")
	var stopped_action := int(stopped.actions.call("GetCurrentMovementActionId"))
	_check(bool(stopped.actions.call("ChooseReaction", 0)) and bool(stopped.actions.call("RefuseReaction")) and bool(stopped.actions.call("StopMovement")), "STOP termine explicitement l'action interrompue")
	_check(_start_move(stopped, Vector3(0, 0, 2)) >= 0 and await _wait_pending(stopped.actions), "Un nouveau mouvement repropose B")
	_check(int(stopped.actions.call("GetCurrentMovementActionId")) != stopped_action and str(stopped.actions.call("GetPendingReactorName")) == "UNITE B", "STOP cree un nouvel ActionId et rouvre les reactions")
	await _dispose(stopped)

	# F — MODIFY preserves the interrupted ActionId and already spent PM.
	var modified := await _spawn()
	var modified_pm := int(modified.a.call("GetCurrentMovementPoints"))
	_check(_start_move(modified, Vector3(0, 0, -2)) >= 0 and await _wait_pending(modified.actions), "MODIFY atteint une reaction jaune")
	var modified_action := int(modified.actions.call("GetCurrentMovementActionId"))
	_check(bool(modified.actions.call("ChooseReaction", 0)) and bool(modified.actions.call("RefuseReaction")) and bool(modified.actions.call("BeginModifyMovement")), "MODIFY est disponible apres la chaine de reactions")
	var modified_destination := _cell_offset(modified, Vector3(-2, 0, 0))
	_check(modified_destination >= 0 and bool(modified.controller.call("TrySelectDestinationCellId", modified_destination)), "MODIFY lance un vrai RemainingPath depuis CurrentCell")
	await _wait_movement_finished(modified.a)
	await _wait_frames(5)
	_check(int(modified.actions.call("GetLastCompletedMovementActionId")) == modified_action and int(modified.a.call("GetCurrentMovementPoints")) == modified_pm - 2, "MODIFY conserve ActionId et tous les PM depenses")
	_check(int(modified.actions.call("GetOpportunityOfferCount", modified_action, "UNITE B")) == 1 and not bool(modified.actions.call("GetHasPendingReaction")), "MODIFY ne rouvre pas un reacteur ferme et ne soft-lock pas")
	await _dispose(modified)

	if failures.is_empty():
		print("COMBAT_V4_REACTION_CLOSURE_REGRESSION_TEST: SUCCESS")
		quit(0)
	else:
		print("COMBAT_V4_REACTION_CLOSURE_REGRESSION_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
