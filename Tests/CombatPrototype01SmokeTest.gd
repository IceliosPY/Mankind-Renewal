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

func _horizontal_distance(a: Vector3, b: Vector3) -> float:
	return Vector2(a.x, a.z).distance_to(Vector2(b.x, b.z))

func _wait_for_tactical_stop(unit: Node, maximum_frames: int) -> float:
	var maximum_height := -1000.0
	for _frame in maximum_frames:
		maximum_height = max(maximum_height, (unit.get_node("../VisualSlice01/Player") as Node3D).global_position.y)
		if not bool(unit.call("GetIsMoving")):
			break
		await physics_frame
	return maximum_height

func _run() -> void:
	_check(ResourceLoader.exists("res://Scenes/Tests/CombatPrototype01.tscn"), "La scene CombatPrototype01 existe")
	var packed := load("res://Scenes/Tests/CombatPrototype01.tscn") as PackedScene
	_check(packed != null, "CombatPrototype01.tscn se charge")
	if packed == null:
		quit(1)
		return

	var prototype := packed.instantiate()
	root.add_child(prototype)
	var slice := prototype.get_node_or_null("VisualSlice01") as Node3D
	var grid := prototype.get_node_or_null("TacticalGrid") as Node3D
	var unit := prototype.get_node_or_null("TacticalUnit") as Node
	var controller := prototype.get_node_or_null("CombatModeController") as Node
	var player := prototype.get_node_or_null("VisualSlice01/Player") as CharacterBody3D
	var manager := prototype.get_node_or_null("VisualSlice01/GameModeManager") as Node
	var navigation_region := prototype.get_node_or_null("VisualSlice01/World/NavigationRegion3D") as NavigationRegion3D
	var camera_rig := prototype.get_node_or_null("VisualSlice01/CameraRig") as Node3D

	_check(slice != null, "VisualSlice01 est instanciee comme reference intacte")
	_check(grid != null and unit != null and controller != null, "Les responsabilites grille, unite et mode sont separees")
	_check(player != null and manager != null and navigation_region != null and camera_rig != null, "La Fondation V2 reste instanciee")
	if grid == null or unit == null or controller == null or player == null or manager == null or navigation_region == null or camera_rig == null:
		quit(1)
		return

	for _frame in 240:
		await physics_frame
		if bool(grid.call("GetIsBuilt")) and player.is_on_floor() and NavigationServer3D.map_get_iteration_id(navigation_region.get_navigation_map()) > 0:
			break

	_check(bool(grid.call("GetIsBuilt")), "La grille est construite depuis les collisions de la micro-zone")
	_check(int(grid.call("GetCellCount")) > 100, "La grille contient un ensemble significatif de cellules")
	_check(not grid.visible, "La grille est cachee en exploration")
	_check(bool(manager.call("IsExploration")), "Le prototype demarre en exploration")
	_check(InputMap.has_action("toggle_combat") and InputMap.has_action("cancel_combat"), "C et Escape disposent d'actions dediees")

	var ground_cell := int(grid.call("GetCellIdNearWorld", Vector3(-2, 0, -3), 0.8))
	var ramp_low_cell := int(grid.call("GetCellIdNearWorld", Vector3(8, 0.5, -1), 0.9))
	var ramp_high_cell := int(grid.call("GetCellIdNearWorld", Vector3(8, 1.5, -3), 0.9))
	var deck_entry_cell := int(grid.call("GetCellIdNearWorld", Vector3(8, 2, -5), 0.9))
	var deck_cell := int(grid.call("GetCellIdNearWorld", Vector3(8, 2, -7), 0.8))
	_check(ground_cell >= 0 and ramp_low_cell >= 0 and ramp_high_cell >= 0 and deck_cell >= 0, "Des cellules existent au sol, sur les deux hauteurs de rampe et sur la plateforme")
	_check(ramp_low_cell >= 0 and int(grid.call("GetCellNeighborCount", ramp_low_cell)) >= 2, "La rampe basse est connectee au graphe")
	_check(ramp_high_cell >= 0 and int(grid.call("GetCellNeighborCount", ramp_high_cell)) >= 2, "La rampe haute est connectee au graphe")
	_check(deck_cell >= 0 and is_equal_approx(float(grid.call("GetCellSurfaceHeight", deck_cell)), 2.0), "La hauteur +2 m est stockee dans les cellules")
	_check(int(grid.call("GetPathLengthBetweenCells", ground_cell, deck_cell)) > 0, "Le corridor physique entre les garde-corps relie sol, rampe et plateforme")
	_check(int(grid.call("GetCellIdNearWorld", Vector3(-2, 0, 1), 0.6)) == -1, "Le collecteur central supprime les cellules qui le traverseraient")
	_check(int(grid.call("GetCellIdNearWorld", Vector3(6, 0, 7), 0.6)) == -1, "Les caisses bloquent la grille via leurs collisions")
	_check(int(grid.call("GetCellIdNearWorld", Vector3(-10, 0, -9), 0.6)) == -1, "La colonne de tuyaux sans collision d'origine est bloquee dans le prototype")
	_check(int(grid.call("GetCellIdNearWorld", Vector3(13, 0, 0), 0.6)) == -1, "Aucune cellule n'est creee hors de la micro-zone")

	# Exploration: continuous movement and NavigationAgent remain authoritative.
	player.global_position = Vector3(0, 0.9, 10)
	player.velocity = Vector3.ZERO
	await _wait_physics_frames(10)
	var manual_start := player.global_position
	Input.action_press("move_forward")
	await _wait_physics_frames(24)
	Input.action_release("move_forward")
	_check(_horizontal_distance(player.global_position, manual_start) > 0.8, "ZQSD reste continu en exploration")

	player.call("CancelAutoMovement")
	player.global_position = Vector3(0, 0.9, 10)
	player.velocity = Vector3.ZERO
	await _wait_physics_frames(8)
	_check(bool(player.call("TrySetClickDestination", Vector3(-2, 0, 6))), "Le click-to-move NavigationMesh reste disponible en exploration")
	await _wait_physics_frames(8)
	_check(bool(player.call("GetIsAutoMoving")), "Le NavigationAgent commence son trajet d'exploration")

	var pre_entry_position := player.global_position
	_check(bool(controller.call("EnterCombat")), "L'entree en combat trouve une cellule proche")
	var snapped_position := player.global_position
	_check(not bool(manager.call("IsExploration")) and bool(controller.call("GetIsCombatActive")), "Le mode Combat est actif")
	_check(not bool(player.call("GetIsAutoMoving")), "L'entree en combat annule le click-to-move")
	_check(grid.visible, "La grille devient visible en combat")
	_check(_horizontal_distance(pre_entry_position, snapped_position) < 2.3, "Le snap d'entree reste local")
	var start_cell := int(unit.call("GetCurrentCellId"))
	_check(start_cell >= 0 and bool(grid.call("GetCellIsOccupied", start_cell)), "La cellule de depart stocke l'occupant Player")

	var combat_stationary := player.global_position
	Input.action_press("move_forward")
	await _wait_physics_frames(24)
	Input.action_release("move_forward")
	_check(player.global_position.distance_to(combat_stationary) < 0.02, "ZQSD est bloque pendant le combat")

	_check(bool(controller.call("SetHoveredCellFromWorld", Vector3(-2, 0, -3))), "Le survol souris peut cibler une cellule praticable")
	_check(int(grid.call("GetHoveredCellId")) == ground_cell, "La cellule survolee est identifiee")
	_check(bool(controller.call("TrySelectDestinationCellId", ground_cell)), "Un clic tactique calcule un chemin discret")
	var direct_manhattan_nodes := 8
	_check(int(controller.call("GetLastPathLength")) > direct_manhattan_nodes, "A* detourne le collecteur central au lieu de le traverser")
	await _wait_for_tactical_stop(unit, 600)
	_check(not bool(unit.call("GetIsMoving")) and int(unit.call("GetCurrentCellId")) == ground_cell, "Le Player atteint la destination cellule par cellule")
	_check(int(unit.call("GetCompletedStepCount")) >= int(controller.call("GetLastPathLength")) - 1, "Chaque changement de cellule est comptabilise")

	_check(bool(controller.call("TrySelectDestinationCellId", deck_cell)), "A* trouve un chemin du sol vers la plateforme")
	var maximum_height := await _wait_for_tactical_stop(unit, 900)
	_check(not bool(unit.call("GetIsMoving")) and int(unit.call("GetCurrentCellId")) == deck_cell, "Le trajet tactique atteint la plateforme")
	_check(maximum_height > 2.6 and player.global_position.y > 2.6, "Le mouvement interpole les hauteurs reelles de la rampe jusqu'a +2 m")

	var position_before_exit := player.global_position
	controller.call("ExitCombat")
	await _wait_physics_frames(2)
	_check(bool(manager.call("IsExploration")) and not bool(controller.call("GetIsCombatActive")), "Escape/sortie restaure le mode Exploration")
	_check(not grid.visible, "La grille est masquee a la sortie")
	_check(player.global_position.distance_to(position_before_exit) < 0.1, "La sortie conserve la position atteinte")
	_check(bool(player.call("TrySetClickDestination", Vector3(9, 2, -7))), "Le click-to-move est restaure apres le combat")
	player.call("CancelAutoMovement")
	var restored_start := player.global_position
	Input.action_press("move_backward")
	await _wait_physics_frames(18)
	Input.action_release("move_backward")
	_check(_horizontal_distance(player.global_position, restored_start) > 0.3, "ZQSD est restaure apres le combat")

	var initial_yaw := camera_rig.rotation.y
	camera_rig.call("ApplyRotation", Vector2(25, 8))
	var initial_zoom := float(camera_rig.call("GetZoom"))
	camera_rig.call("ApplyZoom", 1.5)
	_check(not is_equal_approx(camera_rig.rotation.y, initial_yaw) and float(camera_rig.call("GetZoom")) > initial_zoom, "Rotation et zoom de la camera actuelle restent fonctionnels")

	prototype.queue_free()
	await process_frame
	if failures.is_empty():
		print("COMBAT_PROTOTYPE_01_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("COMBAT_PROTOTYPE_01_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
