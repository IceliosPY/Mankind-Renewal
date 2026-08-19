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
	await _wait_physics_frames(8)

func _run() -> void:
	_check(ProjectSettings.get_setting("rendering/renderer/rendering_method") == "forward_plus", "Le projet est configuré en Forward+")
	var packed_slice := load("res://Scenes/Tests/VisualSlice01.tscn") as PackedScene
	_check(packed_slice != null, "VisualSlice01.tscn se charge")
	if packed_slice == null:
		quit(1)
		return

	var slice := packed_slice.instantiate()
	root.add_child(slice)
	var player := slice.get_node_or_null("Player") as CharacterBody3D
	var navigation_region := slice.get_node_or_null("World/NavigationRegion3D") as NavigationRegion3D
	var camera_rig := slice.get_node_or_null("CameraRig") as Node3D
	var camera := slice.get_node_or_null("CameraRig/PitchPivot/SpringArm3D/Camera3D") as Camera3D
	var floor_modules := slice.get_node_or_null("World/Architecture/MainFloorModules") as Node3D
	var upper_deck := slice.get_node_or_null("World/Architecture/UpperDeck") as Node3D
	var lighting := slice.get_node_or_null("World/Lighting") as Node3D
	var environment_node := slice.get_node_or_null("World/WorldEnvironment") as WorldEnvironment

	_check(player != null and navigation_region != null, "Le Player et la navigation locale sont présents")
	_check(camera_rig != null and camera != null and camera.current, "La caméra tactique actuelle est active")
	_check(floor_modules != null and floor_modules.get_child_count() == 36, "Le sol principal utilise 36 modules Quaternius éditables")
	_check(upper_deck != null and upper_deck.get_child_count() == 4, "La zone surélevée utilise quatre modules éditables")
	_check(lighting != null and lighting.find_children("*", "OmniLight3D", true, false).size() == 5, "Cinq éclairages locaux possèdent une origine visuelle")
	_check(environment_node != null and environment_node.environment != null and environment_node.environment.glow_enabled and environment_node.environment.ssao_enabled, "Le WorldEnvironment Forward+ active glow et SSAO")
	if player == null or navigation_region == null or camera_rig == null or camera == null:
		quit(1)
		return

	var mesh_count := slice.find_children("*", "MeshInstance3D", true, false).size()
	var primitive_mesh_count := 0
	var textured_material_count := 0
	for child in slice.find_children("*", "MeshInstance3D", true, false):
		var mesh_instance := child as MeshInstance3D
		if mesh_instance.mesh is BoxMesh or mesh_instance.mesh is PlaneMesh or mesh_instance.mesh is CylinderMesh:
			primitive_mesh_count += 1
		if mesh_instance.mesh == null:
			continue
		for surface_index in mesh_instance.mesh.get_surface_count():
			var material := mesh_instance.get_active_material(surface_index) as BaseMaterial3D
			if material != null and (material.albedo_texture != null or material.normal_texture != null or material.orm_texture != null):
				textured_material_count += 1
	_check(mesh_count > 70 and textured_material_count > 0, "La micro-zone contient une composition dense avec les matériaux texturés du pack")
	_check(primitive_mesh_count == 0, "Aucune primitive visible n'est utilisée comme décor final, hors placeholder Player")

	var required_editable_nodes := [
		"World/Architecture/RampLeft", "World/Architecture/RampRight",
		"World/Architecture/IndustrialGate", "World/Infrastructure/CentralPipeManifold",
		"World/Props/CrateIsland/Crate3", "World/Props/CrateIsland/Crate4",
		"World/Props/CoverRails/GroundCoverRail",
	]
	_check(required_editable_nodes.all(func(path: String) -> bool: return slice.get_node_or_null(path) is Node3D), "Les éléments importants restent des Node3D séparés et éditables")

	for _frame in 180:
		await physics_frame
		if NavigationServer3D.map_get_iteration_id(navigation_region.get_navigation_map()) > 0 and player.is_on_floor():
			break
	_check(NavigationServer3D.map_get_iteration_id(navigation_region.get_navigation_map()) > 0, "Le NavigationMesh précalculé est synchronisé")
	_check(player.is_on_floor(), "Le Player se pose sur le sol modulaire")

	await _reset_player(player, Vector3(0, 0.9, 10))
	var manual_start := player.global_position
	Input.action_press("move_forward")
	await _wait_physics_frames(30)
	Input.action_release("move_forward")
	_check(_horizontal_distance(player.global_position, manual_start) > 1.0, "ZQSD fonctionne dans VisualSlice01")

	await _reset_player(player, Vector3(-2, 0.9, 5))
	var ground_target := Vector3(-2, 0, -2)
	_check(bool(player.call("TrySetClickDestination", ground_target)), "Le click-to-move accepte une destination derrière l'îlot technique")
	var maximum_ground_deviation := 0.0
	for _frame in 600:
		if not bool(player.call("GetIsAutoMoving")):
			break
		await physics_frame
		maximum_ground_deviation = max(maximum_ground_deviation, abs(player.global_position.x + 2.0))
	_check(not bool(player.call("GetIsAutoMoving")) and _horizontal_distance(player.global_position, ground_target) < 0.7, "Le Player atteint la destination au niveau principal")
	_check(maximum_ground_deviation > 2.0, "Le chemin contourne réellement l'îlot technique")

	await _reset_player(player, Vector3(8, 0.9, 2))
	var upper_target := Vector3(8, 2, -8)
	_check(bool(player.call("TrySetClickDestination", upper_target)), "Le poste de contrôle surélevé est une destination navigable")
	var maximum_height := player.global_position.y
	for _frame in 720:
		if not bool(player.call("GetIsAutoMoving")):
			break
		await physics_frame
		maximum_height = max(maximum_height, player.global_position.y)
	_check(not bool(player.call("GetIsAutoMoving")) and _horizontal_distance(player.global_position, upper_target) < 0.8, "Le Player atteint le poste de contrôle par la rampe")
	_check(maximum_height > 2.6 and player.global_position.y > 2.6, "La navigation et la collision conservent l'élévation de deux mètres")

	var initial_yaw := camera_rig.rotation.y
	camera_rig.call("ApplyRotation", Vector2(35, 12))
	_check(not is_equal_approx(camera_rig.rotation.y, initial_yaw), "La rotation de caméra fonctionne")
	var initial_zoom := float(camera_rig.call("GetZoom"))
	camera_rig.call("ApplyZoom", 1.5)
	_check(float(camera_rig.call("GetZoom")) > initial_zoom, "Le zoom fonctionne")
	camera_rig.call("ApplyPan", Vector2(24, -16))
	_check((camera_rig.call("GetPanOffset") as Vector3).length() > 0.1, "Le pan fonctionne")

	slice.queue_free()
	await process_frame
	if failures.is_empty():
		print("VISUAL_SLICE_01_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("VISUAL_SLICE_01_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
