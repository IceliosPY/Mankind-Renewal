extends "res://Tests/CombatPrototype06TestBase.gd"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var nodes := await _spawn()
	var rules := load("res://Data/Damage/DebugDamageRules.tres")
	var hybrid := load("res://Data/Weapons/DebugHybridRifle.tres")
	_check(rules != null and abs(float(rules.call("GetMaxResistanceReduction")) - 0.2) < 0.0001 and abs(float(rules.call("GetResistanceScale")) - 30.0) < 0.0001, "DamageRulesDefinition centralise Max 0.20 et Scale 30")
	_check(hybrid != null and int((hybrid.get("DamageComponents") as Array).size()) == 2 and int(hybrid.get("PrimaryDamageType")) == BALLISTIC and abs(float(hybrid.get("Penetration")) - 10.0) < 0.0001, "DebugHybridRifle est data-driven, multi-types et Primary Ballistic")
	_check(not str(nodes.setup.call("GetHybridInstanceId")).is_empty() and bool(nodes.setup.call("ActivateHybridWeapon")) and str(nodes.a.call("GetActiveWeaponName")) == "Debug Hybrid Rifle", "L'ItemInstance hybride possede son ID et peut etre equipee")
	nodes.setup.call("SetTargetResistancePreset", 1)
	_check(abs(float(nodes.damage.call("GetUnitResistanceValue", nodes.b, BALLISTIC)) - 20.0) < 0.0001 and abs(float(nodes.damage.call("GetUnitResistanceValue", nodes.b, ENERGY)) - 10.0) < 0.0001, "Le preset LIGHT applique Ballistic 20 et Energy 10")
	nodes.setup.call("SetTargetResistancePreset", 2)
	_check(abs(float(nodes.damage.call("GetUnitResistanceValue", nodes.b, BALLISTIC)) - 100.0) < 0.0001 and abs(float(nodes.damage.call("GetUnitResistanceValue", nodes.b, ENERGY)) - 50.0) < 0.0001, "Le preset STRONG applique Ballistic 100 et Energy 50")
	nodes.setup.call("SetTargetResistancePreset", 3)
	_check(abs(float(nodes.damage.call("GetUnitResistanceValue", nodes.b, BALLISTIC)) - 1000000.0) < 0.0001, "Le preset EXTREME rend le plafond 20 pourcent testable manuellement")
	await _wait_frames(8)
	var analysis := str(nodes.panel.call("GetAnalysisText"))
	_check("TARGET RESISTANCES" in analysis and "BALLISTIC : 1000000" in analysis and "DAMAGE BREAKDOWN" in analysis, "Le panneau V6 affiche profils et breakdown")
	_check(not bool(nodes.root.get_node("CombatV5/DebugUI/CombatV5DebugPanel").visible) and bool((nodes.panel as Control).visible), "CombatPrototype06 remplace uniquement le panneau V5 par le panneau V6")
	await _dispose(nodes)
	_finish("COMBAT_PROTOTYPE_06_SMOKE_TEST")
