extends SceneTree

const EXPECTED_ASSETS := [
	"Architecture/WallAstra_Straight",
	"Architecture/Platform_3Plates",
	"Architecture/Platform_Ramp_4",
	"Architecture/Door_Frame_A",
	"Architecture/Column_Pipes",
	"Infrastructure/Prop_AccessPoint",
	"Infrastructure/Prop_Computer",
	"Infrastructure/Prop_PipeHolder",
	"Infrastructure/Prop_Light_Floor",
	"Infrastructure/Prop_Fan_Small",
	"Props/Prop_Crate4",
	"Props/Prop_Chest",
	"Props/Prop_Rail_3",
	"Props/Prop_Barrel_Large",
]

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

func _run() -> void:
	var packed_gallery := load("res://Scenes/Tests/AssetGallery.tscn") as PackedScene
	_check(packed_gallery != null, "AssetGallery.tscn se charge")
	if packed_gallery == null:
		quit(1)
		return

	var gallery := packed_gallery.instantiate()
	root.add_child(gallery)
	var assets_root := gallery.get_node_or_null("GalleryAssets") as Node3D
	var player := gallery.get_node_or_null("Player") as CharacterBody3D
	var navigation_region := gallery.get_node_or_null("Environment/NavigationRegion3D") as NavigationRegion3D
	var camera_rig := gallery.get_node_or_null("CameraRig") as Node3D
	var camera := gallery.get_node_or_null("CameraRig/PitchPivot/SpringArm3D/Camera3D") as Camera3D

	_check(assets_root != null, "La galerie contient un groupe d'assets éditables")
	_check(player != null and navigation_region != null, "Le Player et la navigation de test sont présents")
	_check(camera_rig != null and camera != null and camera.current, "La caméra tactique actuelle est instanciée et active")
	if assets_root == null or player == null or navigation_region == null or camera_rig == null or camera == null:
		quit(1)
		return

	var mesh_count := 0
	var material_count := 0
	var textured_material_count := 0
	for relative_path in EXPECTED_ASSETS:
		var wrapper := assets_root.get_node_or_null(relative_path) as Node3D
		_check(wrapper != null, "Asset séparé et éditable : " + relative_path)
		if wrapper == null:
			continue
		var model := wrapper.get_node_or_null("Model") as Node3D
		_check(model != null, "Instance glTF présente : " + relative_path)
		if model == null:
			continue
		for child in model.find_children("*", "MeshInstance3D", true, false):
			var mesh_instance := child as MeshInstance3D
			if mesh_instance.mesh == null:
				continue
			mesh_count += 1
			for surface_index in mesh_instance.mesh.get_surface_count():
				var material := mesh_instance.get_active_material(surface_index) as BaseMaterial3D
				if material == null:
					continue
				material_count += 1
				if material.albedo_texture != null or material.normal_texture != null or material.orm_texture != null:
					textured_material_count += 1

	_check(mesh_count >= EXPECTED_ASSETS.size(), "Tous les modèles contiennent une géométrie importée")
	_check(material_count > 0 and textured_material_count > 0, "Les matériaux texturés d'origine sont actifs")
	_check(assets_root.get_node("Architecture/Door_Frame_A").scale.is_equal_approx(Vector3.ONE), "La porte reste à l'échelle 1:1")
	_check(assets_root.get_node("Props/Prop_Crate4").scale.is_equal_approx(Vector3.ONE), "Les props restent à l'échelle 1:1")

	var collision_assets := [
		"Architecture/WallAstra_Straight",
		"Architecture/Platform_Ramp_4",
		"Architecture/Door_Frame_A",
		"Architecture/Column_Pipes",
		"Infrastructure/Prop_Computer",
		"Infrastructure/Prop_PipeHolder",
		"Props/Prop_Crate4",
		"Props/Prop_Chest",
		"Props/Prop_Rail_3",
		"Props/Prop_Barrel_Large",
	]
	_check(collision_assets.all(func(path: String) -> bool: return assets_root.get_node(path).get_node_or_null("Collision") is StaticBody3D), "Les gros objets utilisent des collisions simples séparées")

	for _frame in 120:
		await physics_frame
		if NavigationServer3D.map_get_iteration_id(navigation_region.get_navigation_map()) > 0 and player.is_on_floor():
			break
	_check(NavigationServer3D.map_get_iteration_id(navigation_region.get_navigation_map()) > 0, "Le NavigationMesh local de la galerie est synchronisé")
	_check(player.is_on_floor(), "Le Player se pose sur le sol neutre")

	await _reset_player(player, Vector3(0, 0.9, 5))
	var manual_start := player.global_position
	Input.action_press("move_forward")
	await _wait_physics_frames(30)
	Input.action_release("move_forward")
	_check(_horizontal_distance(player.global_position, manual_start) > 1.0, "ZQSD fonctionne dans la galerie")

	await _reset_player(player, Vector3(0, 0.9, 5))
	var click_target := Vector3(0, 0, -5)
	_check(bool(player.call("TrySetClickDestination", click_target)), "Le click-to-move accepte une destination derrière l'équipement central")
	var maximum_side_deviation := 0.0
	for _frame in 480:
		if not bool(player.call("GetIsAutoMoving")):
			break
		await physics_frame
		maximum_side_deviation = max(maximum_side_deviation, abs(player.global_position.x))
	_check(not bool(player.call("GetIsAutoMoving")) and _horizontal_distance(player.global_position, click_target) < 0.65, "Le Player atteint la destination de galerie")
	_check(maximum_side_deviation > 2.0, "Le chemin contourne le Prop_PipeHolder")

	var initial_yaw := camera_rig.rotation.y
	camera_rig.call("ApplyRotation", Vector2(35, 10))
	_check(not is_equal_approx(camera_rig.rotation.y, initial_yaw), "La rotation de caméra fonctionne")
	var initial_zoom := float(camera_rig.call("GetZoom"))
	camera_rig.call("ApplyZoom", -1.0)
	_check(float(camera_rig.call("GetZoom")) < initial_zoom, "Le zoom fonctionne")
	camera_rig.call("ApplyPan", Vector2(25, -15))
	_check((camera_rig.call("GetPanOffset") as Vector3).length() > 0.1, "Le pan fonctionne")

	gallery.queue_free()
	await process_frame
	if failures.is_empty():
		print("ASSET_GALLERY_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("ASSET_GALLERY_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)

