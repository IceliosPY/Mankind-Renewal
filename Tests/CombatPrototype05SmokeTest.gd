extends SceneTree

const SCENE := "res://Scenes/Tests/CombatPrototype05.tscn"
const NONE := 0
const LIGHT := 1
const HEAVY := 2
const TOTAL := 3

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
	var packed := load(SCENE) as PackedScene
	if packed == null:
		return {}
	var p := packed.instantiate()
	root.add_child(p)
	var n := {
		"root": p,
		"controller": p.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/CombatModeController"),
		"grid": p.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/TacticalGrid"),
		"turns": p.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/TurnManager"),
		"actions": p.get_node("CombatV4/CombatV4ActionController"),
		"rules": p.get_node("CombatV5RulesService"),
		"setup": p.get_node("PrototypeSetupV5"),
		"panel": p.get_node("DebugUI/CombatV5DebugPanel"),
		"a": p.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/CombatV1/TacticalUnit"),
		"b": p.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/UnitB"),
		"c": p.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/UnitC"),
		"d": p.get_node("CombatV4/InventoryV1/CombatV3/CombatV2/ReinforcementUnit"),
		"target_cover": p.get_node("TestZones/TargetCoverProvider"),
		"reaction_wall": p.get_node("TestZones/ReactionWallProvider"),
	}
	for _frame in 240:
		await physics_frame
		if bool(n.grid.call("GetIsBuilt")):
			break
	if bool(n.grid.call("GetIsBuilt")):
		n.controller.call("EnterCombat")
		await _wait_frames(5)
	return n

func _dispose(n: Dictionary) -> void:
	if n.is_empty():
		return
	if bool(n.controller.call("GetIsCombatActive")):
		n.controller.call("ExitCombat")
	n.root.queue_free()
	await process_frame
	await process_frame

func _evaluate(n: Dictionary, attacker := "UNITE A", target := "UNITE B") -> bool:
	return bool(n.rules.call("EvaluateByUnitNames", attacker, target))

func _select_attack(n: Dictionary, target := "UNITE B") -> bool:
	return bool(n.actions.call("BeginAttackSelection")) and bool(n.actions.call("SelectTargetByName", target))

func _declare_and_refuse_offensive(n: Dictionary, target := "UNITE B") -> bool:
	if not _select_attack(n, target) or not bool(n.actions.call("DeclareSelectedAttack")):
		return false
	var guard := 0
	while bool(n.actions.call("GetHasOffensiveOpportunity")) and guard < 8:
		n.actions.call("RefuseReaction")
		guard += 1
	return true

func _cell_offset(n: Dictionary, unit: Node, offset: Vector3) -> int:
	var pos := n.grid.call("GetCellWorldPosition", int(unit.call("GetCurrentCellId"))) as Vector3
	return int(n.grid.call("GetCellIdNearWorld", pos + offset, 0.9))

func _wait_pending(actions: Node, maximum := 240) -> bool:
	for _frame in maximum:
		if bool(actions.call("GetHasOffensiveOpportunity")):
			return true
		await physics_frame
	return false

func _run() -> void:
	_check(ResourceLoader.exists(SCENE), "CombatPrototype05.tscn existe")
	var rules_resource := load("res://Data/Cover/DebugCoverRules.tres")
	var anti := load("res://Data/Weapons/DebugAntiCoverRifle.tres")
	var armor_pen := load("res://Data/Weapons/DebugArmorPenRifle.tres")
	_check(rules_resource != null and int(rules_resource.call("GetLightAccuracyPenalty")) == 5 and int(rules_resource.call("GetHeavyAccuracyPenalty")) == 10, "Les penalites LIGHT -5 et HEAVY -10 sont centralisees")
	_check(anti != null and bool(anti.call("GetHasCoverPiercing")), "DebugAntiCoverRifle porte le trait CoverPiercing data-driven")
	_check(armor_pen != null and not bool(armor_pen.call("GetHasCoverPiercing")) and float(armor_pen.get("Penetration")) == 999.0, "Armor Penetration reste distincte de CoverPiercing")
	_check(str(ProjectSettings.get_setting("application/run/main_scene")) == "res://Scenes/Tests/CombatPrototype04.tscn", "La scene principale reste CombatPrototype04")

	# Discrete cover levels, direction and flank.
	var cover := await _spawn()
	_check(not cover.is_empty() and str(cover.turns.call("GetActiveUnitName")) == "UNITE A", "Combat V5 reutilise V4 et entre en combat")
	cover.setup.call("SetCoverMode", NONE)
	_check(_evaluate(cover) and int(cover.rules.call("GetLastBaseCoverValue")) == NONE and int(cover.rules.call("GetLastCoverPenalty")) == 0, "Terrain ouvert : NONE et penalite 0")
	cover.setup.call("SetCoverMode", LIGHT)
	_check(_evaluate(cover) and int(cover.rules.call("GetLastBaseCoverValue")) == LIGHT and int(cover.rules.call("GetLastCoverPenalty")) == 5, "Bonne direction : LIGHT et penalite -5")
	cover.setup.call("SetCoverMode", HEAVY)
	_check(_evaluate(cover) and int(cover.rules.call("GetLastBaseCoverValue")) == HEAVY and int(cover.rules.call("GetLastEffectiveAccuracy")) == 10, "Bonne direction : HEAVY donne EffectiveAccuracy 20 - 10 = 10")
	_check("WEST" in str(cover.rules.call("GetLastAttackDirections")) and "SOUTH" in str(cover.rules.call("GetLastAttackDirections")), "La diagonale exacte consulte deux cotes discrets et documentes")
	cover.setup.call("SetFlankedMode")
	_check(_evaluate(cover) and int(cover.rules.call("GetLastEffectiveCoverValue")) == NONE and bool(cover.rules.call("GetLastIsFlanked")), "Mauvaise direction : le flanking supprime le cover")
	_check(int(cover.rules.call("ApplyHeightForTest", HEAVY, 2.0, 0.0)) == LIGHT and int(cover.rules.call("ApplyHeightForTest", LIGHT, 2.0, 0.0)) == NONE, "+2 m degrade HEAVY vers LIGHT et LIGHT vers NONE")
	_check(int(cover.rules.call("ApplyHeightForTest", NONE, 2.0, 0.0)) == NONE and int(cover.rules.call("ApplyHeightForTest", TOTAL, 2.0, 0.0)) == TOTAL, "La hauteur ne bonifie pas NONE et ne reduit jamais TOTAL")
	cover.setup.call("SetCoverMode", HEAVY)
	_check(_select_attack(cover) and _evaluate(cover), "L'analyse UI recoit une cible reelle")
	await _wait_frames(8)
	var analysis := str(cover.panel.call("GetAnalysisText"))
	_check("LINE OF SIGHT" in analysis and "LINE OF FIRE" in analysis and "BASE COVER" in analysis and "EFFECTIVE ACCURACY" in analysis and "INTERCEPTOR" in analysis, "L'UI V5 explique LoS, LoF, cover, accuracy et interception")
	_check(cover.root.get_node("TestZones/ZoneLight") != null and cover.root.get_node("TestZones/ZoneHeavy") != null and cover.root.get_node("TestZones/ZoneTotal") != null, "Les zones LIGHT, HEAVY et TOTAL restent des Node3D editables separes")
	var panel := cover.panel as Control
	var window := root as Window
	for size in [Vector2i(1280,720), Vector2i(1600,900), Vector2i(1920,1080)]:
		window.size = size
		await _wait_frames(3)
		var panel_rect := panel.get_global_rect()
		var viewport_size := panel.get_viewport_rect().size
		_check(panel_rect.position.x >= 0 and panel_rect.position.y >= 0 and panel_rect.end.x <= viewport_size.x and panel_rect.end.y <= viewport_size.y, "UI V5 contenue dans %dx%d" % [size.x,size.y])
	await _dispose(cover)

	# Cover modifies Dodge independently; equality still belongs to attacker.
	var dodge := await _spawn()
	dodge.controller.call("RemoveUnitFromCombat", dodge.c)
	dodge.setup.call("SetCoverMode", HEAVY)
	dodge.b.call("SetBaseDodge", 12)
	_check(_declare_and_refuse_offensive(dodge) and bool(dodge.actions.call("GetHasPendingDefensiveReaction")) and bool(dodge.actions.call("AcceptDefensiveReaction")), "HEAVY puis Dodge 12 contre Accuracy 10 ouvre et resout l'Esquive")
	_check(str(dodge.actions.call("GetCurrentOutcomeText")) == "DODGED" and int(dodge.b.call("GetCurrentHealth")) == 100, "Cover et Dodge restent independants : 12 > 10 esquive")
	dodge.b.call("SetBaseDodge", 10)
	_check(_declare_and_refuse_offensive(dodge) and bool(dodge.actions.call("AcceptDefensiveReaction")), "Une seconde attaque teste l'egalite sous couvert")
	_check(str(dodge.actions.call("GetCurrentOutcomeText")) == "HIT" and int(dodge.b.call("GetCurrentHealth")) == 80, "Egalite Dodge 10 = Accuracy 10 : l'attaquant gagne")
	await _dispose(dodge)

	if failures.is_empty():
		print("COMBAT_PROTOTYPE_05_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("COMBAT_PROTOTYPE_05_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
