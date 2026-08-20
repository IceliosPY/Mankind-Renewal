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

func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame

func _rect_inside(inner: Rect2, outer: Rect2, tolerance := 1.0) -> bool:
	return inner.position.x >= outer.position.x - tolerance \
		and inner.position.y >= outer.position.y - tolerance \
		and inner.end.x <= outer.end.x + tolerance \
		and inner.end.y <= outer.end.y + tolerance

func _validate_layout(prototype: Node, debug_panel: Control, requested_size: Vector2i, label: String) -> void:
	var window := root as Window
	window.size = requested_size
	await _wait_frames(6)

	var viewport_rect := prototype.get_viewport().get_visible_rect()
	var panel_rect := debug_panel.get_global_rect()
	var status := debug_panel.get_node("Margin/VBox/Status") as Control
	var end_turn := debug_panel.get_node("Margin/VBox/EndTurn") as Control
	var separator := debug_panel.get_node("Margin/VBox/PrimarySecondarySeparator") as Control
	var scroll := debug_panel.get_node("Margin/VBox/SecondaryScroll") as ScrollContainer
	var secondary := scroll.get_node("SecondaryControls") as Control
	var add_button := scroll.get_node("SecondaryControls/ReinforcementRow/AddReinforcement") as Button

	_check(_rect_inside(panel_rect, viewport_rect), "%s : le panneau reste dans le viewport" % label)
	_check(is_equal_approx(panel_rect.position.x, 12.0) and is_equal_approx(panel_rect.position.y, 12.0), "%s : le panneau reste ancre a gauche" % label)
	_check(abs(panel_rect.end.y - (viewport_rect.end.y - 12.0)) < 1.1, "%s : le panneau utilise la hauteur disponible" % label)
	_check(panel_rect.size.x < viewport_rect.size.x * 0.5, "%s : la scene 3D conserve la majorite de la largeur" % label)
	_check(_rect_inside(status.get_global_rect(), panel_rect) and _rect_inside(end_turn.get_global_rect(), panel_rect), "%s : les informations principales et Fin du tour sont visibles" % label)
	_check(status.get_global_rect().end.y <= end_turn.get_global_rect().position.y + 1.0, "%s : les informations principales ne chevauchent pas Fin du tour" % label)
	_check(end_turn.get_global_rect().end.y <= separator.get_global_rect().position.y + 1.0 and separator.get_global_rect().end.y <= scroll.get_global_rect().position.y + 1.0, "%s : les sections principales et secondaires ne se chevauchent pas" % label)
	_check(_rect_inside(scroll.get_global_rect(), panel_rect) and scroll.clip_contents, "%s : les commandes secondaires sont contenues dans le ScrollContainer" % label)
	_check(secondary.size.x <= scroll.size.x + 1.0 and add_button.size.x > 1.0 and add_button.size.y > 1.0, "%s : les controles secondaires restent utilisables" % label)

func _run() -> void:
	_check(bool(ProjectSettings.get_setting("display/window/size/resizable")), "La fenetre est explicitement redimensionnable")
	_check(str(ProjectSettings.get_setting("display/window/stretch/mode")) == "canvas_items", "Le contenu Canvas suit la taille de fenetre")
	_check(str(ProjectSettings.get_setting("display/window/stretch/aspect")) == "expand", "Les changements de ratio utilisent le mode expand")

	var packed := load("res://Scenes/Tests/CombatPrototype02.tscn") as PackedScene
	_check(packed != null, "CombatPrototype02 se charge pour le test responsive")
	if packed == null:
		quit(1)
		return

	var prototype := packed.instantiate()
	root.add_child(prototype)
	var controller := prototype.get_node("CombatV1/CombatModeController") as Node
	var grid := prototype.get_node("CombatV1/TacticalGrid") as Node3D
	var debug_panel := prototype.get_node("DebugUI/CombatDebugPanel") as Control
	var display_controller := prototype.get_node("WindowDisplayController") as Node
	var camera := prototype.get_node("CombatV1/VisualSlice01/CameraRig/PitchPivot/SpringArm3D/Camera3D") as Camera3D

	for _frame in 240:
		await physics_frame
		if bool(grid.call("GetIsBuilt")):
			break
	_check(bool(controller.call("EnterCombat")), "Le panneau responsive est teste avec ses informations reelles")
	await _wait_frames(8)

	await _validate_layout(prototype, debug_panel, Vector2i(1280, 720), "1280x720")
	await _validate_layout(prototype, debug_panel, Vector2i(1600, 900), "1600x900")
	await _validate_layout(prototype, debug_panel, Vector2i(1920, 1080), "1920x1080")
	await _validate_layout(prototype, debug_panel, Vector2i(1440, 800), "redimensionnement manuel 1440x800")

	_check(camera.current and camera.get_viewport() == prototype.get_viewport(), "La camera 3D continue d'utiliser tout le viewport")
	if DisplayServer.get_name() != "headless":
		display_controller.call("ToggleFullscreen")
		await _wait_frames(8)
		_check(bool(display_controller.call("GetIsFullscreen")), "F11 utilise le mode fullscreen borderless")
		_check(_rect_inside(debug_panel.get_global_rect(), prototype.get_viewport().get_visible_rect()), "Plein ecran : le panneau reste dans le viewport")
		display_controller.call("ToggleFullscreen")
		await _wait_frames(10)
		_check(not bool(display_controller.call("GetIsFullscreen")), "Une seconde bascule restaure le mode fenetre")
	else:
		_check(true, "Le plein ecran reel est reserve a un DisplayServer non-headless")

	controller.call("ExitCombat")
	prototype.queue_free()
	await process_frame
	if failures.is_empty():
		print("RESPONSIVE_DISPLAY_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("RESPONSIVE_DISPLAY_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
