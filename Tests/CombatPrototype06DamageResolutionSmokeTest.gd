extends "res://Tests/CombatPrototype06TestBase.gd"

const BALLISTIC_WEAPON := "res://Data/Weapons/DebugAntiCoverRifle.tres"
const HYBRID_WEAPON := "res://Data/Weapons/DebugHybridRifle.tres"
const ARMOR_PEN_WEAPON := "res://Data/Weapons/DebugArmorPenRifle.tres"
const PEN_50_WEAPON := "res://Data/Weapons/DebugV6Pen50Rifle.tres"

func _initialize() -> void:
	call_deferred("_run")

func _close(a: float, b: float, tolerance := 0.0001) -> bool:
	return abs(a - b) <= tolerance

func _run() -> void:
	var formula := await _spawn()
	var ballistic := load(BALLISTIC_WEAPON) as Resource

	_set_profile(formula.res_b, PROFILE_NONE)
	_check(_preview(formula, formula.b, ballistic) and _close(float(formula.damage.call("GetLastReductionPercentage", 0)), 0.0) and int(formula.damage.call("GetLastFinalDamage")) == 20, "Resistance 0 inflige 20 degats complets")

	_set_profile(formula.res_b, PROFILE_BALLISTIC_30)
	_check(_preview(formula, formula.b, ballistic) and _close(float(formula.damage.call("GetLastReductionPercentage", 0)), 0.10) and _close(float(formula.damage.call("GetLastDamageAfterResistance", 0)), 18.0) and int(formula.damage.call("GetLastFinalDamage")) == 18, "Resistance 30 produit 10 pourcent et 18 degats")

	_set_profile(formula.res_b, PROFILE_BALLISTIC_50)
	_check(_preview(formula, formula.b, ballistic) and _close(float(formula.damage.call("GetLastReductionPercentage", 0)), 0.125) and _close(float(formula.damage.call("GetLastDamageAfterResistance", 0)), 17.5) and int(formula.damage.call("GetLastFinalDamage")) == 17, "Resistance 50 produit 12.5 pourcent puis floor 17")

	_set_profile(formula.res_b, PROFILE_STRONG)
	var pen50 := load(PEN_50_WEAPON) as Resource
	_check(_preview(formula, formula.b, pen50) and _close(float(formula.damage.call("GetLastEffectiveResistance", 0)), 50.0) and _close(float(formula.damage.call("GetLastReductionPercentage", 0)), 0.125) and int(formula.damage.call("GetLastFinalDamage")) == 17, "Penetration 50 reduit Resistance 100 a 50")

	_set_profile(formula.res_b, PROFILE_BALLISTIC_10)
	var over_pen := load(ARMOR_PEN_WEAPON) as Resource
	_check(_preview(formula, formula.b, over_pen) and _close(float(formula.damage.call("GetLastEffectiveResistance", 0)), 0.0) and _close(float(formula.damage.call("GetLastReductionPercentage", 0)), 0.0) and int(formula.damage.call("GetLastFinalDamage")) == 20, "Penetration superieure clamp la Resistance a 0 sans bonus de degats")

	_set_profile(formula.res_b, PROFILE_BALLISTIC_ENERGY_30)
	var hybrid_no_pen := load(HYBRID_WEAPON) as Resource
	hybrid_no_pen.set("Penetration", 0.0)
	_check(_preview(formula, formula.b, hybrid_no_pen) and int(formula.damage.call("GetLastComponentCount")) == 2 and _close(float(formula.damage.call("GetLastDamageAfterResistance", 0)), 18.0) and _close(float(formula.damage.call("GetLastDamageAfterResistance", 1)), 4.5) and _close(float(formula.damage.call("GetLastDecimalTotalDamage")), 22.5) and int(formula.damage.call("GetLastFinalDamage")) == 22, "Les composantes Ballistic et Energy sont resolues independamment")

	var hybrid_primary_pen := hybrid_no_pen
	hybrid_primary_pen.set("Penetration", 30.0)
	_check(_preview(formula, formula.b, hybrid_primary_pen) and _close(float(formula.damage.call("GetLastPenetrationApplied", 0)), 30.0) and _close(float(formula.damage.call("GetLastEffectiveResistance", 0)), 0.0) and _close(float(formula.damage.call("GetLastPenetrationApplied", 1)), 0.0) and _close(float(formula.damage.call("GetLastEffectiveResistance", 1)), 30.0), "La Penetration ne concerne que le Primary Damage Type")

	_set_profile(formula.res_b, PROFILE_BALLISTIC_ENERGY_100)
	hybrid_no_pen.set("Penetration", 0.0)
	_check(_preview(formula, formula.b, hybrid_no_pen) and _close(float(formula.damage.call("GetLastDecimalTotalDamage")), 21.153846, 0.0002) and int(formula.damage.call("GetLastFinalDamage")) == 21, "Le floor est applique une fois apres la somme decimale multi-types")

	_set_profile(formula.res_b, PROFILE_BALLISTIC_1000)
	_preview(formula, formula.b, ballistic)
	var reduction_1000 := float(formula.damage.call("GetLastReductionPercentage", 0))
	_set_profile(formula.res_b, PROFILE_BALLISTIC_1000000)
	_preview(formula, formula.b, ballistic)
	var reduction_million := float(formula.damage.call("GetLastReductionPercentage", 0))
	_check(reduction_1000 < 0.20 and reduction_million < 0.20 and reduction_million > reduction_1000, "Les Resistances gigantesques tendent vers 20 pourcent sans jamais le depasser")

	_set_profile(formula.res_b, PROFILE_BALLISTIC_30)
	var piercing := load(BALLISTIC_WEAPON) as Resource
	_check(_preview(formula, formula.b, piercing, 0.5) and _close(float(formula.damage.call("GetLastDamageBeforeResistance", 0)), 10.0) and _close(float(formula.damage.call("GetLastDamageAfterResistance", 0)), 9.0) and int(formula.damage.call("GetLastFinalDamage")) == 9, "Cover Piercing applique x0.5 avant Resistance")
	hybrid_no_pen.set("Penetration", 10.0)

	formula.setup.call("SetCoverMode", NONE)
	_set_profile(formula.res_c, PROFILE_BALLISTIC_30)
	formula.setup.call("ActivateNormalWeapon")
	var c_hp := int(formula.c.call("GetCurrentHealth"))
	var pa := int(formula.a.call("GetCurrentActionPoints"))
	_check(_declare_and_resolve(formula, "UNITE C") and int(formula.c.call("GetCurrentHealth")) == c_hp - 18 and int(formula.a.call("GetCurrentActionPoints")) == pa - 1 and str(formula.damage.call("GetLastTargetName")) == "UNITE C", "Une attaque reelle applique le resolver V6 aux HP apres engagement des PA")

	formula.a.call("BeginTurn")
	formula.b.call("RestoreFullHealth")
	var c_actor := formula.root.get_node("CombatV5/CombatV4/InventoryV1/CombatV3/CombatV2/UnitCActor") as Node3D
	var c_actor_position := c_actor.global_position
	c_actor.global_position += Vector3(20, 0, 0)
	_set_profile(formula.res_b, PROFILE_BALLISTIC_30)
	formula.setup.call("SetCoverMode", TOTAL)
	formula.setup.call("ActivateArmorPenWeapon")
	pa = int(formula.a.call("GetCurrentActionPoints"))
	_check(bool(formula.actions.call("BeginAttackSelection")) and bool(formula.actions.call("SelectTargetByName", "UNITE B")) and not bool(formula.actions.call("DeclareSelectedAttack")) and int(formula.a.call("GetCurrentActionPoints")) == pa, "Armor Penetration 999 ne traverse toujours pas TOTAL et ne consomme aucun PA")
	formula.actions.call("CancelAttackSelection")
	formula.setup.call("ActivateAntiCoverWeapon")
	var b_hp := int(formula.b.call("GetCurrentHealth"))
	_check(_declare_and_resolve(formula) and int(formula.b.call("GetCurrentHealth")) == b_hp - 9 and _close(float(formula.damage.call("GetLastCoverMultiplier", 0)), 0.5) and _close(float(formula.damage.call("GetLastDamageBeforeResistance", 0)), 10.0), "Cover Piercing traverse TOTAL puis applique Resistance dans le bon ordre")

	formula.a.call("BeginTurn")
	formula.b.call("RestoreFullHealth")
	formula.c.call("RestoreFullHealth")
	c_actor.global_position = c_actor_position
	formula.setup.call("SetCoverMode", NONE)
	formula.setup.call("SetFriendlyInterceptor", true)
	_set_profile(formula.res_b, PROFILE_NONE)
	_set_profile(formula.res_c, PROFILE_BALLISTIC_50)
	formula.setup.call("ActivateNormalWeapon")
	c_hp = int(formula.c.call("GetCurrentHealth"))
	_check(_declare_and_resolve(formula) and int(formula.c.call("GetCurrentHealth")) == c_hp - 17 and int(formula.b.call("GetCurrentHealth")) == 100 and str(formula.damage.call("GetLastTargetName")) == "UNITE C" and _close(float(formula.damage.call("GetLastBaseResistance", 0)), 50.0), "Friendly fire utilise les Resistances de l'intercepteur reel C")

	formula.a.call("BeginTurn")
	formula.b.call("RestoreFullHealth")
	c_actor.global_position += Vector3(20, 0, 0)
	formula.setup.call("SetCoverMode", NONE)
	_set_profile(formula.res_b, PROFILE_NONE)
	var lethal_weapon := load(BALLISTIC_WEAPON) as Resource
	var components := lethal_weapon.get("DamageComponents") as Array
	lethal_weapon.set("Traits", 0)
	(components[0] as Resource).set("Amount", 200.0)
	_check(bool(formula.setup.call("EquipWeaponForTest", lethal_weapon)) and _declare_and_resolve(formula) and bool(formula.b.call("GetIsNeutralized")) and bool(formula.damage.call("GetLastTargetNeutralized")) and int(formula.damage.call("GetLastHpAfter")) == 0, "Les degats V6 reutilisent la neutralisation et le systeme HP existants")
	(components[0] as Resource).set("Amount", 20.0)
	lethal_weapon.set("Traits", 1)
	await _dispose(formula)

	_finish("COMBAT_PROTOTYPE_06_DAMAGE_RESOLUTION_SMOKE_TEST")
