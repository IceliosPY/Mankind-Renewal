extends SceneTree

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

func _wait_for_unit_stop(unit: Node, maximum_frames: int) -> float:
	var maximum_height := -1000.0
	for _frame in maximum_frames:
		maximum_height = max(maximum_height, (unit.call("GetActorWorldPosition") as Vector3).y)
		if not bool(unit.call("GetIsMoving")):
			break
		await physics_frame
	return maximum_height

func _run() -> void:
	var packed := load("res://Scenes/Tests/CombatPrototype02.tscn") as PackedScene
	_check(packed != null, "CombatPrototype02.tscn se charge")
	if packed == null:
		quit(1)
		return

	var prototype := packed.instantiate()
	root.add_child(prototype)
	var combat_v1 := prototype.get_node_or_null("CombatV1") as Node3D
	var controller := prototype.get_node_or_null("CombatV1/CombatModeController") as Node
	var grid := prototype.get_node_or_null("CombatV1/TacticalGrid") as Node3D
	var player := prototype.get_node_or_null("CombatV1/VisualSlice01/Player") as CharacterBody3D
	var game_mode := prototype.get_node_or_null("CombatV1/VisualSlice01/GameModeManager") as Node
	var turn_manager := prototype.get_node_or_null("TurnManager") as Node
	var unit_a := prototype.get_node_or_null("CombatV1/TacticalUnit") as Node
	var unit_b := prototype.get_node_or_null("UnitB") as Node
	var unit_c := prototype.get_node_or_null("UnitC") as Node
	var unit_d := prototype.get_node_or_null("ReinforcementUnit") as Node
	var debug_panel := prototype.get_node_or_null("DebugUI/CombatDebugPanel") as Control

	_check(combat_v1 != null, "Combat V2 instancie CombatPrototype01 au lieu de le dupliquer")
	_check(controller != null and grid != null and turn_manager != null, "Grille, controleur et TurnManager restent separes")
	_check(unit_a != null and unit_b != null and unit_c != null and unit_d != null, "Trois participants et un renfort debug sont presents")
	if controller == null or grid == null or player == null or game_mode == null or turn_manager == null or unit_a == null or unit_b == null or unit_c == null or unit_d == null or debug_panel == null:
		quit(1)
		return

	for _frame in 240:
		await physics_frame
		if bool(grid.call("GetIsBuilt")) and player.is_on_floor():
			break
	_check(bool(grid.call("GetIsBuilt")) and is_equal_approx(float(grid.get("CellSize")), 2.0), "La grille V1 de 2 m reste utilisee")
	_check(not debug_panel.visible and bool(game_mode.call("IsExploration")), "Le prototype demarre en exploration avec l'interface masquee")

	_check(int(unit_a.call("GetInitiative")) == 30 and int(unit_b.call("GetInitiative")) == 20 and int(unit_c.call("GetInitiative")) == 10, "Les Initiatives appartiennent aux unites")
	_check(int(unit_a.call("GetMaxMovementPoints")) == 6 and int(unit_b.call("GetMaxMovementPoints")) == 8 and int(unit_c.call("GetMaxMovementPoints")) == 7, "Les valeurs PM 6, 8 et 7 sont propres aux unites")

	_check(bool(controller.call("EnterCombat")), "Combat V2 entre en combat")
	await _wait_physics_frames(8)
	_check(int(turn_manager.call("GetRoundNumber")) == 1, "Le premier round commence")
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "L'Initiative initiale active UNITE A")
	_check(str(turn_manager.call("GetRemainingOrderNames")) == "UNITE B,UNITE C", "L'ordre initial est trie du plus rapide au plus lent")
	_check(int(turn_manager.call("GetParticipantCount")) == 3, "Les trois unites initiales participent")
	_check(bool(unit_a.call("GetIsActiveTurn")) and int(unit_a.call("GetCurrentActionPoints")) == 2 and int(unit_a.call("GetCurrentMovementPoints")) == 6, "PA et PM sont restaures au debut du tour")
	_check(grid.visible and int(grid.call("GetReachableCellCount")) > 1, "Les cellules atteignables sont affichees pour l'unite active")
	_check(debug_panel.visible and "ROUND : 1" in str(debug_panel.call("GetStatusText")) and "UNITE ACTIVE : UNITE A" in str(debug_panel.call("GetStatusText")), "L'interface debug affiche round, unite active et ordre")
	_check(int(debug_panel.call("GetInitiativeTargetCount")) == 4, "Le selecteur Initiative propose A, B, C et le renfort D")

	# The selected target must remain stable even when its initiative changes its queue position.
	_check(bool(debug_panel.call("SelectInitiativeTargetByName", "UNITE B")), "B peut etre selectionnee explicitement comme cible Initiative")
	_check(bool(debug_panel.call("ModifySelectedInitiative", -20)), "Le premier clic -20 modifie B")
	_check(int(unit_b.call("GetInitiative")) == 0 and str(turn_manager.call("GetRemainingOrderNames")) == "UNITE C,UNITE B", "B passe derriere C")
	_check(str(debug_panel.call("GetSelectedInitiativeTargetName")) == "UNITE B" and "3" in str(debug_panel.call("GetSelectedInitiativePositionText")), "B reste selectionnee et sa nouvelle position est affichee")
	_check(bool(debug_panel.call("ModifySelectedInitiative", -10)), "Le second clic modifie toujours la cible memorisee")
	_check(int(unit_b.call("GetInitiative")) == -10 and int(unit_c.call("GetInitiative")) == 10 and str(debug_panel.call("GetSelectedInitiativeTargetName")) == "UNITE B", "Le second malus touche encore B, jamais C")
	_check(bool(debug_panel.call("SelectInitiativeTargetByName", "UNITE C")) and bool(debug_panel.call("ModifySelectedInitiative", 30)), "C peut etre selectionnee puis recevoir +30")
	_check(int(unit_c.call("GetInitiative")) == 40 and str(turn_manager.call("GetRemainingOrderNames")).begins_with("UNITE C") and str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "C remonte dans la file sans interrompre A")
	unit_b.call("SetInitiative", 20)
	unit_c.call("SetInitiative", 10)

	# PA debug: no negative value and no automatic end turn.
	_check(bool(controller.call("SpendActiveActionPoint")) and int(unit_a.call("GetCurrentActionPoints")) == 1, "La commande debug consomme 1 PA")
	_check(bool(controller.call("SpendActiveActionPoint")) and int(unit_a.call("GetCurrentActionPoints")) == 0, "Le second PA peut etre consomme")
	_check(not bool(controller.call("SpendActiveActionPoint")) and int(unit_a.call("GetCurrentActionPoints")) == 0, "Les PA ne passent jamais sous zero")
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "Zero PA ne termine pas automatiquement le tour")

	# Two-cell movement: PM must be consumed after every completed cell.
	var a_start_cell := int(unit_a.call("GetCurrentCellId"))
	var a_start_position := grid.call("GetCellWorldPosition", a_start_cell) as Vector3
	var first_target := int(grid.call("GetCellIdNearWorld", a_start_position + Vector3(-4, 0, 0), 0.8))
	var reachable_before := int(grid.call("GetReachableCellCount"))
	_check(first_target >= 0 and bool(controller.call("TrySelectDestinationCellId", first_target)), "Un trajet de deux cellules est accepte avec 6 PM")
	_check(int(controller.call("GetLastPathCost")) == 2 and int(unit_a.call("GetCurrentMovementPoints")) == 6, "Le cout est calcule avant le trajet sans depense atomique")
	for _frame in 180:
		if int(unit_a.call("GetCurrentMovementPoints")) == 5:
			break
		await physics_frame
	_check(int(unit_a.call("GetCurrentMovementPoints")) == 5 and bool(unit_a.call("GetIsMoving")), "Le premier PM est retire a la premiere cellule")
	await _wait_for_unit_stop(unit_a, 240)
	_check(int(unit_a.call("GetCurrentMovementPoints")) == 4 and int(unit_a.call("GetCurrentCellId")) == first_target, "Le second PM est retire a la seconde cellule")
	_check(int(grid.call("GetReachableCellCount")) < reachable_before, "La portee est recalculee apres le deplacement")

	var first_position := grid.call("GetCellWorldPosition", first_target) as Vector3
	var second_target := int(grid.call("GetCellIdNearWorld", first_position + Vector3(-2, 0, 0), 0.8))
	_check(bool(controller.call("TrySelectDestinationCellId", second_target)), "Un second deplacement distinct est possible pendant le meme tour")
	await _wait_for_unit_stop(unit_a, 180)
	_check(int(unit_a.call("GetCurrentMovementPoints")) == 3, "Les PM du premier deplacement restent depenses")

	var deck_cell := int(grid.call("GetCellIdNearWorld", Vector3(8, 2, -7), 0.8))
	_check(deck_cell >= 0 and not bool(grid.call("GetCellIsReachable", deck_cell)), "Une cellule au-dela des PM restants est affichee inaccessible")
	_check(not bool(controller.call("TrySelectDestinationCellId", deck_cell)), "Une destination trop couteuse est refusee")

	var second_position := grid.call("GetCellWorldPosition", second_target) as Vector3
	var exhaust_target := int(grid.call("GetCellIdNearWorld", second_position + Vector3(0, 0, -6), 0.8))
	_check(bool(controller.call("TrySelectDestinationCellId", exhaust_target)) and int(controller.call("GetLastPathCost")) == 3, "Les trois derniers PM peuvent etre engages")
	await _wait_for_unit_stop(unit_a, 300)
	_check(int(unit_a.call("GetCurrentMovementPoints")) == 0 and int(unit_a.call("GetCurrentActionPoints")) == 0, "PA et PM peuvent tous deux atteindre zero")
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "Zero PA et zero PM ne terminent toujours pas le tour")

	# Dynamic initiative only reorders units that still have to act.
	unit_c.call("SetInitiative", 25)
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE A" and str(turn_manager.call("GetRemainingOrderNames")) == "UNITE C,UNITE B", "Un bonus dynamique place C devant B sans interrompre A")
	unit_b.call("SetInitiative", 5)
	_check(str(turn_manager.call("GetRemainingOrderNames")) == "UNITE C,UNITE B", "Le malus de B le maintient derriere C")

	_check(bool(controller.call("RequestEndTurn")), "FIN DU TOUR termine explicitement le tour de A")
	_check(bool(unit_a.call("GetHasActedThisRound")) and int(unit_a.call("GetCurrentMovementPoints")) == 0, "A est marquee comme ayant joue et perd ses ressources restantes")
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE C", "C joue avant B apres la modification dynamique")
	await _wait_physics_frames(8)
	_check("[ACTED]" in str(debug_panel.call("GetStatusText")), "L'interface identifie les unites ayant deja joue")
	unit_a.call("SetInitiative", 130)
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE C" and "UNITE A" not in str(turn_manager.call("GetRemainingOrderNames")), "A ne rejoue pas ce round malgre +100 Initiative")

	_check(bool(controller.call("SpendActiveActionPoint")) and int(unit_c.call("GetCurrentActionPoints")) == 1, "Les PA de C sont independants et restaurés")
	controller.call("RequestEndTurn")
	_check(int(unit_c.call("GetCurrentActionPoints")) == 0 and str(turn_manager.call("GetActiveUnitName")) == "UNITE B", "Les PA restants de C sont perdus et B devient active")

	# Unit B starts beside the ramp and has 8 PM: reach the +2 m deck.
	_check(bool(controller.call("TrySelectDestinationCellId", deck_cell)), "B peut engager le chemin vers la plateforme avec ses 8 PM")
	var maximum_height := await _wait_for_unit_stop(unit_b, 480)
	_check(int(unit_b.call("GetCurrentCellId")) == deck_cell and maximum_height > 2.6, "Le mouvement PM traverse les rampes et atteint +2 m")
	_check(int(unit_b.call("GetCurrentMovementPoints")) == 4, "Le trajet de quatre transitions consomme quatre PM progressivement")
	controller.call("RequestEndTurn")
	_check(int(turn_manager.call("GetRoundNumber")) == 2 and str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "La fin du dernier tour recalcule l'Initiative et commence le round 2")
	_check(int(unit_a.call("GetCurrentActionPoints")) == 2 and int(unit_a.call("GetCurrentMovementPoints")) == 6, "Les ressources de A sont restaurees au nouveau round")

	# Reinforcement joins the remaining order but never interrupts the active turn.
	_check(bool(controller.call("AddUnitToCombat", unit_d)), "Le renfort D rejoint le combat en cours de round")
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE A" and int(turn_manager.call("GetParticipantCount")) == 4, "D n'interrompt pas le tour actif")
	_check(str(turn_manager.call("GetRemainingOrderNames")).begins_with("UNITE D"), "La forte Initiative de D le place en tete de l'ordre restant")
	controller.call("RequestEndTurn")
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE D", "D devient la prochaine unite eligible")
	_check(bool(controller.call("RemoveUnitFromCombat", unit_d)), "Une unite peut etre retiree immediatement")
	_check(int(turn_manager.call("GetParticipantCount")) == 3 and str(turn_manager.call("GetActiveUnitName")) == "UNITE C", "Le retrait de l'unite active maintient une file coherente")

	controller.call("RequestEndTurn")
	controller.call("RequestEndTurn")
	_check(int(turn_manager.call("GetRoundNumber")) == 3 and str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "Un nouveau round relit encore les Initiatives actuelles")
	unit_b.call("SetInitiative", 25)
	var tied_order := str(turn_manager.call("GetRemainingOrderNames"))
	_check((tied_order == "UNITE B,UNITE C" or tied_order == "UNITE C,UNITE B") and str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "Une egalite exacte est departagee sans interrompre l'unite active")

	var before_exit := player.global_position
	controller.call("ExitCombat")
	await _wait_physics_frames(3)
	_check(bool(game_mode.call("IsExploration")) and not bool(controller.call("GetIsCombatActive")), "Combat V2 retourne proprement en exploration")
	_check(not grid.visible and not debug_panel.visible, "Grille et interface debug sont masquees en exploration")
	_check(player.global_position.distance_to(before_exit) < 0.15, "La position du Player est conservee a la sortie")
	var exploration_target := Vector3(player.global_position.x, 0, player.global_position.z + 2.0)
	_check(bool(player.call("TrySetClickDestination", exploration_target)), "Le click-to-move est restaure apres Combat V2")
	player.call("CancelAutoMovement")
	var manual_start := player.global_position
	Input.action_press("move_backward")
	await _wait_physics_frames(18)
	Input.action_release("move_backward")
	_check(Vector2(player.global_position.x, player.global_position.z).distance_to(Vector2(manual_start.x, manual_start.z)) > 0.3, "ZQSD est restaure apres Combat V2")

	prototype.queue_free()
	await process_frame
	if failures.is_empty():
		print("COMBAT_PROTOTYPE_02_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("COMBAT_PROTOTYPE_02_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
