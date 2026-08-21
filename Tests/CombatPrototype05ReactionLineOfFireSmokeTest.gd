extends "res://Tests/CombatPrototype05TestBase.gd"

func _initialize() -> void:
	call_deferred("_run")

func _global_aabb(mesh: MeshInstance3D) -> AABB:
	var local := mesh.get_aabb()
	var minimum := Vector3(INF, INF, INF)
	var maximum := Vector3(-INF, -INF, -INF)
	for x in [local.position.x, local.end.x]:
		for y in [local.position.y, local.end.y]:
			for z in [local.position.z, local.end.z]:
				var point := mesh.to_global(Vector3(x, y, z))
				minimum = minimum.min(point)
				maximum = maximum.max(point)
	return AABB(minimum, maximum - minimum)

func _trace_cell(label: String, nodes: Dictionary) -> void:
	print("REACTION_LOF_TRACE ", label,
		" ACell=", int(nodes.a.call("GetCurrentCellId")),
		" APos=", nodes.a.call("GetActorWorldPosition"),
		" BCell=", int(nodes.b.call("GetCurrentCellId")),
		" BFireOrigin=", nodes.b.call("GetFireOriginWorldPosition"),
		" ATargetPoint=", nodes.a.call("GetTargetPointWorldPosition"),
		" ActionId=", int(nodes.actions.call("GetCurrentMovementActionId")),
		" LoS=", bool(nodes.rules.call("GetLastHasLineOfSight")),
		" LoF=", bool(nodes.rules.call("GetLastHasLineOfFire")),
		" Blocker=", str(nodes.rules.call("GetLastBlockingProviderName")),
		" PendingReactor=", str(nodes.actions.call("GetPendingReactorName")),
		" Choices=", int(nodes.actions.call("GetReactionChoiceCount")))

func _run() -> void:
	var reactions := await _spawn()
	reactions.setup.call("SetCoverMode", NONE)
	var wall_button := reactions.root.get_node("DebugUI/CombatV5DebugPanel/Margin/VBox/SecondaryScroll/Secondary/ReactionWall") as CheckButton
	wall_button.emit_signal("toggled", true)
	var wall_visual := reactions.reaction_wall.get_node("WallVisual") as Node3D
	var wall_mesh := wall_visual.find_children("*", "MeshInstance3D", true, false)[0] as MeshInstance3D
	var wall_bounds := _global_aabb(wall_mesh)
	var blocker_point := reactions.reaction_wall.call("GetEdgeWorldPosition", reactions.grid) as Vector3
	var horizontal_offset := Vector2(wall_bounds.get_center().x - blocker_point.x, wall_bounds.get_center().z - blocker_point.z).length()
	_check(horizontal_offset <= 0.75 and wall_bounds.size.z > wall_bounds.size.x, "Le mur visible est aligne avec le proxy LoF et parallele au trajet manuel")
	_check(bool(reactions.reaction_wall.get("TacticalEnabled")) and wall_visual.visible and _evaluate(reactions, "UNITE B", "UNITE A") and bool(reactions.rules.call("GetLastIsBlocked")), "Le bouton DEBUG active le mur lateral et A commence occultee")
	_trace_cell("START", reactions)
	var start_cell := int(reactions.a.call("GetCurrentCellId"))
	var exposed_target := _cell_offset(reactions, reactions.a, Vector3(0, 0, -4))
	_check(exposed_target >= 0 and int(reactions.grid.call("GetPathLengthBetweenCells", start_cell, exposed_target)) == 3 and bool(reactions.controller.call("TrySelectDestinationCellId", exposed_target)), "Le mur actif laisse a A un trajet de deux cellules")
	var exposed_action_id := int(reactions.actions.call("GetCurrentMovementActionId"))
	for _frame in 240:
		if int(reactions.a.call("GetCurrentCellId")) != start_cell:
			break
		await physics_frame
	var concealed_cell := int(reactions.a.call("GetCurrentCellId"))
	_check(concealed_cell != start_cell and concealed_cell != exposed_target and not bool(reactions.actions.call("GetHasPendingReaction")) and bool(reactions.a.call("GetIsMoving")), "A atteint la premiere cellule occultee sans reaction et continue")
	_check(_evaluate(reactions, "UNITE B", "UNITE A") and bool(reactions.rules.call("GetLastIsBlocked")), "Le mur bloque encore la LoF de B sur la cellule occultee")
	_trace_cell("CELL_1_CONCEALED", reactions)
	_check(await _wait_pending(reactions.actions), "La reaction apparait a la premiere cellule exposee apres la sortie du mur")
	_check(int(reactions.a.call("GetCurrentCellId")) == exposed_target and _evaluate(reactions, "UNITE B", "UNITE A") and not bool(reactions.rules.call("GetLastIsBlocked")), "La premiere cellule exposee possede une Line Of Fire valide")
	_trace_cell("CELL_2_EXPOSED", reactions)
	_check(exposed_action_id > 0 and int(reactions.actions.call("GetCurrentMovementActionId")) == exposed_action_id and str(reactions.actions.call("GetPendingReactorName")) == "UNITE B", "La premiere opportunite exposee conserve l'ActionId du mouvement")
	_check(bool(reactions.actions.call("RefuseReaction")), "B peut refuser sa reaction sans interrompre le tour")
	await _wait_frames(90)
	_check(int(reactions.actions.call("GetLastCompletedMovementActionId")) == exposed_action_id and not bool(reactions.a.call("GetIsMoving")) and not bool(reactions.actions.call("GetHasPendingReaction")), "Le trajet LOS se termine sans soft-lock avec exactement le meme ActionId")
	await _dispose(reactions)

	var refusal := await _spawn()
	refusal.setup.call("SetCoverMode", NONE)
	var refusal_target := _cell_offset(refusal, refusal.a, Vector3(0, 0, -4))
	_check(bool(refusal.controller.call("TrySelectDestinationCellId", refusal_target)) and await _wait_pending(refusal.actions), "Le scenario refus V5 ouvre une reaction")
	var refusal_id := int(refusal.actions.call("GetCurrentMovementActionId"))
	refusal.actions.call("RefuseReaction")
	if bool(refusal.actions.call("GetHasOffensiveOpportunity")):
		refusal.actions.call("RefuseReaction")
	await _wait_frames(90)
	_check(int(refusal.actions.call("GetOpportunityOfferCount", refusal_id, "UNITE B")) == 1, "Un refus ferme toujours B pour toute l'ActionId, meme si la LoF evolue")
	await _dispose(refusal)
	_finish("COMBAT_PROTOTYPE_05_REACTION_LINE_OF_FIRE_SMOKE_TEST")
