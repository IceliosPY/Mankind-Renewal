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

func _reset_player(player: CharacterBody3D, position: Vector3) -> void:
	Input.action_release("move_forward")
	Input.action_release("move_backward")
	Input.action_release("move_left")
	Input.action_release("move_right")
	player.call("CancelAutoMovement")
	player.global_position = position
	player.velocity = Vector3.ZERO
	await _wait_physics_frames(5)

func _wait_for_destination(player: CharacterBody3D, target: Vector3, maximum_frames: int) -> Dictionary:
	var maximum_side_deviation := 0.0
	var frames := 0
	while frames < maximum_frames and bool(player.call("GetIsAutoMoving")):
		await physics_frame
		maximum_side_deviation = max(maximum_side_deviation, abs(player.global_position.x))
		frames += 1
	return {
		"finished": not bool(player.call("GetIsAutoMoving")),
		"distance": _horizontal_distance(player.global_position, target),
		"maximum_side_deviation": maximum_side_deviation,
		"frames": frames,
	}

func _run() -> void:
	var packed_main := load("res://Scenes/Levels/Main.tscn") as PackedScene
	_check(packed_main != null, "La scène principale se charge")
	if packed_main == null:
		quit(1)
		return

	var main := packed_main.instantiate()
	root.add_child(main)
	var player := main.get_node_or_null("Player") as CharacterBody3D
	var floor := main.get_node_or_null("Environment/TestFloor") as StaticBody3D
	var obstacle := main.get_node_or_null("Environment/TestObstacles/TestObstacle") as StaticBody3D
	var navigation_region := main.get_node_or_null("Environment/NavigationRegion3D") as NavigationRegion3D
	var camera_rig := main.get_node_or_null("CameraRig") as Node3D
	var camera := main.get_node_or_null("CameraRig/PitchPivot/SpringArm3D/Camera3D") as Camera3D
	var game_mode_manager := main.get_node_or_null("GameModeManager")

	var required_actions := [
		"move_forward", "move_backward", "move_left", "move_right", "jump",
		"click_to_move", "camera_rotate", "camera_pan", "camera_zoom_in",
		"camera_zoom_out", "camera_recenter",
	]
	_check(required_actions.all(func(action: String) -> bool: return InputMap.has_action(action)), "Toutes les actions V2 sont configurées dans l'Input Map")
	_check(player != null, "Le Player est un CharacterBody3D")
	_check(floor != null and obstacle != null, "Le sol et l'obstacle de test sont présents")
	_check(navigation_region != null and navigation_region.navigation_mesh != null, "La NavigationRegion3D possède un NavigationMesh")
	_check(camera_rig != null and camera != null and camera.current, "Le rig tactique indépendant et la caméra active sont présents")
	_check(game_mode_manager != null and bool(game_mode_manager.call("IsExploration")), "Le mode initial est EXPLORATION")
	if player == null or navigation_region == null or camera_rig == null or camera == null or game_mode_manager == null:
		quit(1)
		return

	for _frame in 120:
		await physics_frame
		if NavigationServer3D.map_get_iteration_id(navigation_region.get_navigation_map()) > 0 and player.is_on_floor():
			break
	_check(NavigationServer3D.map_get_iteration_id(navigation_region.get_navigation_map()) > 0, "La carte de navigation est synchronisée")
	_check(player.is_on_floor(), "La gravité pose le Player sur le sol")

	# ZQSD seul.
	await _reset_player(player, Vector3(0, 0.9, 5))
	var manual_start := player.global_position
	Input.action_press("move_forward")
	await _wait_physics_frames(30)
	Input.action_release("move_forward")
	_check(_horizontal_distance(player.global_position, manual_start) > 1.0, "ZQSD déplace le Player relativement à la caméra")

	# Clic-to-move seul, y compris le raycast depuis un point écran.
	await _reset_player(player, Vector3(-4, 0.9, 5))
	var click_target := Vector3(4, 0, 5)
	var click_screen_position := camera.unproject_position(click_target)
	_check(bool(player.call("TrySetClickDestinationFromScreen", click_screen_position)), "Un clic projeté sur le sol lance le click-to-move")
	var click_result := await _wait_for_destination(player, click_target, 360)
	_check(click_result.finished and click_result.distance < 0.55, "Le click-to-move atteint puis libère proprement sa destination")
	_check(player.velocity.length() < 0.35, "Le Player s'arrête à destination")

	# Clic puis interruption immédiate par ZQSD.
	await _reset_player(player, Vector3(-4, 0.9, 5))
	_check(bool(player.call("TrySetClickDestination", Vector3(4, 0, 5))), "Une destination automatique valide est acceptée")
	await _wait_physics_frames(10)
	Input.action_press("move_left")
	await physics_frame
	_check(not bool(player.call("GetIsAutoMoving")), "Une entrée ZQSD annule immédiatement le déplacement automatique")
	Input.action_release("move_left")

	# ZQSD puis clic : le manuel garde la priorité.
	await _reset_player(player, Vector3(-4, 0.9, 5))
	Input.action_press("move_forward")
	await physics_frame
	_check(not bool(player.call("TrySetClickDestination", Vector3(4, 0, 5))), "Un clic est refusé tant qu'une entrée ZQSD est active")
	Input.action_release("move_forward")
	await physics_frame
	_check(bool(player.call("TrySetClickDestination", Vector3(4, 0, 5))), "Le clic redevient disponible après relâchement de ZQSD")
	player.call("CancelAutoMovement")

	# Clic hors zone navigable.
	_check(not bool(player.call("TrySetClickDestination", Vector3(30, 0, 30))), "Une destination hors NavigationMesh est refusée")

	# Contournement de l'obstacle central.
	await _reset_player(player, Vector3(0, 0.9, 5))
	var obstacle_target := Vector3(0, 0, -5)
	_check(bool(player.call("TrySetClickDestination", obstacle_target)), "La destination derrière l'obstacle est acceptée")
	var obstacle_result := await _wait_for_destination(player, obstacle_target, 480)
	_check(obstacle_result.finished and obstacle_result.distance < 0.6, "Le Player atteint une destination située derrière l'obstacle")
	_check(obstacle_result.maximum_side_deviation > 1.5, "Le chemin contourne réellement le trou de navigation de l'obstacle")

	# Rotation, zoom, pan et recentrage.
	var initial_yaw := camera_rig.rotation.y
	camera_rig.call("ApplyRotation", Vector2(40, 15))
	_check(not is_equal_approx(camera_rig.rotation.y, initial_yaw), "Le clic droit + mouvement peut faire tourner la caméra")
	var initial_zoom := float(camera_rig.call("GetZoom"))
	camera_rig.call("ApplyZoom", -1.5)
	_check(float(camera_rig.call("GetZoom")) < initial_zoom, "La molette peut zoomer")
	camera_rig.call("ApplyPan", Vector2(30, -20))
	_check((camera_rig.call("GetPanOffset") as Vector3).length() > 0.1, "Le clic milieu + mouvement applique un pan")
	camera_rig.call("Recenter", false)
	_check((camera_rig.call("GetPanOffset") as Vector3).is_zero_approx(), "Le recentrage annule le pan")

	# État COMBAT préparatoire : annulation et verrouillage du mouvement d'exploration.
	await _reset_player(player, Vector3(-4, 0.9, 5))
	_check(bool(player.call("TrySetClickDestination", Vector3(4, 0, 5))), "Une navigation est active avant le changement de mode")
	game_mode_manager.call("SetCombatMode")
	await physics_frame
	_check(not bool(game_mode_manager.call("IsExploration")) and not bool(player.call("GetIsAutoMoving")), "Le mode COMBAT préparatoire annule le click-to-move")
	var combat_start := player.global_position
	Input.action_press("move_forward")
	await _wait_physics_frames(20)
	Input.action_release("move_forward")
	_check(_horizontal_distance(player.global_position, combat_start) < 0.15, "ZQSD ne déplace pas le Player dans l'état COMBAT préparatoire")
	game_mode_manager.call("SetExplorationMode")
	_check(bool(game_mode_manager.call("IsExploration")), "Le retour à l'état EXPLORATION est préparé")

	main.queue_free()
	await process_frame
	if failures.is_empty():
		print("FOUNDATION_V2_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("FOUNDATION_V2_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
