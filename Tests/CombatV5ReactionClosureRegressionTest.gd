extends "res://Tests/CombatPrototype05TestBase.gd"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var nodes := await _spawn()
	nodes.setup.call("SetCoverMode", NONE)
	nodes.setup.call("SetReactionWallEnabled", false)
	var pm_before := int(nodes.a.call("GetCurrentMovementPoints"))
	var destination := _cell_offset(nodes, nodes.a, Vector3(0, 0, -4))
	_check(destination >= 0 and bool(nodes.controller.call("TrySelectDestinationCellId", destination)) and await _wait_pending(nodes.actions), "V5 ouvre B puis C sur Action #1")
	var action_id := int(nodes.actions.call("GetCurrentMovementActionId"))
	_check(bool(nodes.actions.call("RefuseReaction")) and bool(nodes.actions.call("RefuseReaction")), "B et C sont fermes pour toute Action #1")
	# Refusal resumes on the controller's deferred callback. Re-evaluate geometry
	# only after that real transition, not while the original interruption is still
	# unwinding on the same call stack.
	await process_frame
	_check(not bool(nodes.actions.call("GetIsMovementPausedForReaction")) and bool(nodes.a.call("GetIsMoving")), "Le mouvement reel a repris avant le retrigger LoF/portee")
	var reached_cell := nodes.grid.call("GetCellById", int(nodes.a.call("GetCurrentCellId"))) as Node

	# TEST B: LoF disappears and returns, but closed reactors remain filtered.
	nodes.setup.call("SetReactionWallEnabled", true)
	_check(not bool(nodes.actions.call("OnMovementCellReached", nodes.a, reached_cell)), "Sortie de LoF : aucune opportunity fermee n'est reconstruite")
	nodes.setup.call("SetReactionWallEnabled", false)
	_check(not bool(nodes.actions.call("OnMovementCellReached", nodes.a, reached_cell)), "Retour en LoF : B reste ferme pour la meme ActionId")

	# TEST C: range disappears and returns, with the same closure invariant.
	var weapon := nodes.b.call("GetActiveWeapon") as Resource
	var original_range := int(weapon.get("RangeInCells"))
	weapon.set("RangeInCells", 1)
	_check(not bool(nodes.actions.call("OnMovementCellReached", nodes.a, reached_cell)), "Sortie de portee : aucune nouvelle opportunity")
	weapon.set("RangeInCells", original_range)
	_check(not bool(nodes.actions.call("OnMovementCellReached", nodes.a, reached_cell)), "Retour en portee : B n'est pas repropose")
	_check(not bool(nodes.actions.call("GetHasPendingReaction")) and not bool(nodes.actions.call("GetIsMovementPausedForReaction")), "LoF/portee retablies avec zero reacteur eligible ne suspendent jamais le mouvement")

	await _wait_frames(90)
	_check(int(nodes.actions.call("GetLastCompletedMovementActionId")) == action_id and int(nodes.actions.call("GetCurrentMovementActionId")) == 0, "Action #1 conserve son identite puis se termine")
	_check(int(nodes.a.call("GetCurrentMovementPoints")) == pm_before - 2 and not bool(nodes.a.call("GetIsMoving")), "Les PM sont consommes et aucun soft-lock ne subsiste")
	await _dispose(nodes)
	_finish("COMBAT_V5_REACTION_CLOSURE_REGRESSION_TEST")
