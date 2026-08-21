extends "res://Tests/CombatPrototype05TestBase.gd"

const COVER_BUTTONS := "DebugUI/CombatV5DebugPanel/Margin/VBox/SecondaryScroll/Secondary/CoverButtons/"
const COVER_DIRECTION_BUTTONS := "DebugUI/CombatV5DebugPanel/Margin/VBox/SecondaryScroll/Secondary/CoverDirectionButtons/"

func _initialize() -> void:
	call_deferred("_run")

func _click_cover(nodes: Dictionary, button_name: String) -> void:
	var button := nodes.root.get_node(COVER_BUTTONS + button_name) as Button
	button.emit_signal("pressed")

func _click_direction(nodes: Dictionary, button_name: String) -> void:
	var button := nodes.root.get_node(COVER_DIRECTION_BUTTONS + button_name) as Button
	button.emit_signal("pressed")

func _wait_for_stop(unit: Node, maximum := 360) -> void:
	for _frame in maximum:
		if not bool(unit.call("GetIsMoving")):
			return
		await physics_frame

func _run() -> void:
	var nodes := await _spawn()
	var provider := nodes.root.get_node("TestZones/TargetCoverProvider") as Node
	var panel := nodes.root.get_node("DebugUI/CombatV5DebugPanel") as Node
	var initial_b_cell := int(nodes.b.call("GetCurrentCellId"))

	for direction in [["North", 0], ["East", 1], ["South", 2], ["West", 3]]:
		_click_direction(nodes, direction[0])
		_check(int(provider.call("GetProtectedDirectionValue")) == direction[1], "Le bouton %s configure le cote reel du provider" % direction[0].to_upper())
	_click_direction(nodes, "South")
	_click_cover(nodes, "Light")
	_check(int(provider.call("GetCoverLevelValue")) == LIGHT, "Le bouton LIGHT configure le vrai TargetCoverProvider")
	_click_cover(nodes, "Heavy")
	_check(int(provider.call("GetCoverLevelValue")) == HEAVY, "Le bouton HEAVY configure le vrai TargetCoverProvider")
	_click_cover(nodes, "Total")
	_check(int(provider.call("GetCoverLevelValue")) == TOTAL, "Le bouton TOTAL configure le vrai TargetCoverProvider")
	_check(int(provider.call("GetProtectedCellId", nodes.grid)) == initial_b_cell, "Avant mouvement, le provider protege bien la cellule X de B")

	# Move B through the real tactical controller, then return to A's next turn.
	_check(bool(nodes.controller.call("RequestEndTurn")) and str(nodes.turns.call("GetActiveUnitName")) == "UNITE B", "Le test passe au tour de B")
	var destination := _cell_offset(nodes, nodes.b, Vector3(0, 0, -2))
	_check(destination >= 0 and bool(nodes.controller.call("TrySelectDestinationCellId", destination)), "B commence un deplacement vers une autre cellule Y")
	await _wait_for_stop(nodes.b)
	var moved_b_cell := int(nodes.b.call("GetCurrentCellId"))
	_check(moved_b_cell == destination and moved_b_cell != initial_b_cell, "B atteint reellement la cellule Y")
	_check(bool(nodes.controller.call("RequestEndTurn")), "Le tour de B se termine")
	_check(bool(nodes.controller.call("RequestEndTurn")), "Le tour de C se termine")
	_check(bool(nodes.controller.call("RequestEndTurn")) and str(nodes.turns.call("GetActiveUnitName")) == "UNITE A", "Le round suivant revient a A")
	_check(bool(nodes.actions.call("BeginAttackSelection")) and bool(nodes.actions.call("SelectTargetByName", "UNITE B")), "A reselectionne B apres son deplacement")

	_click_cover(nodes, "Light")
	_check(int(provider.call("GetCoverLevelValue")) == LIGHT, "LIGHT reste applique au provider apres deplacement")
	_check(int(provider.call("GetProtectedCellId", nodes.grid)) == moved_b_cell, "Reappliquer LIGHT rattache le provider a la nouvelle cellule Y")
	_check(_evaluate(nodes) and int(nodes.rules.call("GetLastBaseCoverValue")) == LIGHT and int(nodes.rules.call("GetLastCoverPenalty")) == 5, "Depuis le cote protege, LIGHT donne BaseCover LIGHT et -5")

	_click_cover(nodes, "Heavy")
	_check(int(provider.call("GetCoverLevelValue")) == HEAVY and int(provider.call("GetProtectedCellId", nodes.grid)) == moved_b_cell, "HEAVY conserve la cellule Y")
	_check(_evaluate(nodes) and int(nodes.rules.call("GetLastBaseCoverValue")) == HEAVY and int(nodes.rules.call("GetLastCoverPenalty")) == 10, "Depuis le cote protege, HEAVY donne BaseCover HEAVY et -10")

	_click_cover(nodes, "Total")
	_check(int(provider.call("GetCoverLevelValue")) == TOTAL and int(provider.call("GetProtectedCellId", nodes.grid)) == moved_b_cell, "TOTAL conserve la cellule Y")
	_check(_evaluate(nodes) and int(nodes.rules.call("GetLastBaseCoverValue")) == TOTAL and bool(nodes.rules.call("GetLastIsBlocked")), "Depuis le cote protege, TOTAL bloque l'attaque directe")
	await _wait_frames(8)
	var analysis := str(panel.call("GetAnalysisText"))
	_check("BASE COVER : TOTAL" in analysis and "BLOCKED : TOTAL COVER" in analysis, "Le panneau affiche l'evaluation reelle du provider deplace")

	await _dispose(nodes)

	# Real +2 m platform case requested for manual validation.
	var height := await _spawn()
	height.controller.call("ExitCombat")
	var elevated_id := int(height.grid.call("GetCellIdNearWorld", Vector3(6, 2, -7), 0.9))
	var low_id := int(height.grid.call("GetCellIdNearWorld", Vector3(2, 0, -7), 0.9))
	var elevated_position := height.grid.call("GetCellWorldPosition", elevated_id) as Vector3
	var low_position := height.grid.call("GetCellWorldPosition", low_id) as Vector3
	var player := height.root.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/VisualSlice01/Player") as Node3D
	var b_actor := height.root.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/UnitBActor") as Node3D
	player.global_position = elevated_position + Vector3.UP * float(height.a.get("PlayerCenterHeight"))
	b_actor.global_position = low_position + Vector3.UP * float(height.b.get("PlayerCenterHeight"))
	_check(bool(height.controller.call("EnterCombat")), "Le scenario hauteur reentre en combat")
	await _wait_frames(5)
	_check(int(height.a.call("GetCurrentCellId")) == elevated_id and int(height.b.call("GetCurrentCellId")) == low_id, "A est a +2 m et B reste au niveau bas")
	for provider_path in ["TestZones/ReactionWallProvider", "TestZones/ZoneLight", "TestZones/ZoneHeavy", "TestZones/ZoneTotal"]:
		var other_provider := height.root.get_node(provider_path) as Node
		other_provider.set("TacticalEnabled", false)
		other_provider.call("InvalidateCellAssociation")
	_check(bool(height.actions.call("BeginAttackSelection")) and bool(height.actions.call("SelectTargetByName", "UNITE B")), "A selectionne B depuis la plateforme")

	_click_direction(height, "East")
	_click_cover(height, "Heavy")
	_check(_evaluate(height) and str(height.rules.call("GetLastAttackDirections")) == "EAST", "La plateforme produit AttackDirection EAST")
	_check(int(height.rules.call("GetLastBaseCoverValue")) == HEAVY and int(height.rules.call("GetLastHeightLevelsReduced")) == 1, "EAST + HEAVY donne Base HEAVY et Height -1 level")
	_check(int(height.rules.call("GetLastEffectiveCoverValue")) == LIGHT and int(height.rules.call("GetLastCoverPenalty")) == 5, "EAST + HEAVY devient LIGHT avec penalite -5")
	_check(int(height.rules.call("GetLastEffectiveAccuracy")) == 15, "EAST + HEAVY donne Accuracy 20 -> 15")
	await _wait_frames(8)
	analysis = str((height.root.get_node("DebugUI/CombatV5DebugPanel") as Node).call("GetAnalysisText"))
	_check("BASE COVER : HEAVY" in analysis and "HEIGHT MODIFIER : -1 LEVEL" in analysis and "EFFECTIVE COVER : LIGHT" in analysis and "EFFECTIVE ACCURACY : 15" in analysis, "Le panneau affiche le scenario hauteur HEAVY complet")

	_click_cover(height, "Light")
	_check(_evaluate(height) and str(height.rules.call("GetLastAttackDirections")) == "EAST", "Choisir LIGHT conserve le cote EAST")
	_check(int(height.rules.call("GetLastBaseCoverValue")) == LIGHT and int(height.rules.call("GetLastHeightLevelsReduced")) == 1, "EAST + LIGHT donne Base LIGHT et Height -1 level")
	_check(int(height.rules.call("GetLastEffectiveCoverValue")) == NONE and int(height.rules.call("GetLastCoverPenalty")) == 0 and int(height.rules.call("GetLastEffectiveAccuracy")) == 20, "EAST + LIGHT devient NONE avec Accuracy 20")
	await _wait_frames(8)
	analysis = str((height.root.get_node("DebugUI/CombatV5DebugPanel") as Node).call("GetAnalysisText"))
	_check("BASE COVER : LIGHT" in analysis and "EFFECTIVE COVER : NONE" in analysis and "COVER PENALTY : -0" in analysis and "EFFECTIVE ACCURACY : 20" in analysis, "Le panneau affiche le scenario hauteur LIGHT complet")

	await _dispose(height)
	_finish("COMBAT_PROTOTYPE_05_DEBUG_COVER_SMOKE_TEST")
