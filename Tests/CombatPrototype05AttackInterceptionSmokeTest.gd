extends "res://Tests/CombatPrototype05TestBase.gd"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var piercing := await _spawn()
	piercing.controller.call("RemoveUnitFromCombat", piercing.c)
	piercing.setup.call("SetCoverMode", TOTAL)
	var pa_before := int(piercing.a.call("GetCurrentActionPoints"))
	_check(_select_attack(piercing) and not bool(piercing.actions.call("DeclareSelectedAttack")) and int(piercing.a.call("GetCurrentActionPoints")) == pa_before, "TOTAL bloque l'arme normale avant declaration, sans PA")
	piercing.setup.call("ActivateArmorPenWeapon")
	_check(not bool(piercing.actions.call("DeclareSelectedAttack")) and int(piercing.a.call("GetCurrentActionPoints")) == pa_before, "Armor Penetration 999 sans CoverPiercing reste bloquee")
	piercing.setup.call("ActivateAntiCoverWeapon")
	_check(bool(piercing.actions.call("DeclareSelectedAttack")), "CoverPiercing autorise la declaration a travers TOTAL")
	while bool(piercing.actions.call("GetHasOffensiveOpportunity")):
		piercing.actions.call("RefuseReaction")
	if bool(piercing.actions.call("GetHasPendingDefensiveReaction")):
		piercing.actions.call("RefuseDefensiveReaction")
	_check(int(piercing.a.call("GetCurrentActionPoints")) == pa_before - 1 and int(piercing.b.call("GetCurrentHealth")) == 90, "CoverPiercing engage les PA et applique 20 x 0.5 = 10 degats")
	_check(bool(piercing.rules.call("GetLastUsesCoverPiercing")) and is_equal_approx(float(piercing.rules.call("GetLastDamageMultiplier")), 0.5), "Le resultat expose COVER PIERCING et DAMAGE x0.5")
	await _dispose(piercing)

	var interception := await _spawn()
	interception.setup.call("SetCoverMode", NONE)
	var c_health := int(interception.c.call("GetCurrentHealth"))
	_check(_declare_and_refuse_offensive(interception), "Le tir aligne A vers B est declare avec C intermediaire ennemi")
	_check(int(interception.c.call("GetCurrentHealth")) == c_health - 20 and int(interception.b.call("GetCurrentHealth")) == 100, "La premiere unite ennemie intercepte 100% du projectile et B reste intacte")
	_check(str(interception.rules.call("GetLastInterceptorName")) == "UNITE C" and str(interception.rules.call("GetLastResolvedTargetName")) == "UNITE C", "La trajectoire expose clairement l'intercepteur reel")
	interception.setup.call("SetFriendlyInterceptor", true)
	c_health = int(interception.c.call("GetCurrentHealth"))
	var pa_before_friendly := int(interception.a.call("GetCurrentActionPoints"))
	_check(_declare_and_refuse_offensive(interception), "Le joueur peut lancer volontairement un tir avec alliee intermediaire")
	_check(bool(interception.rules.call("GetLastIsFriendlyFire")) and str(interception.rules.call("GetLastInterceptorName")) == "UNITE C", "L'analyse annonce FRIENDLY FIRE et l'intercepteur")
	_check(int(interception.c.call("GetCurrentHealth")) == c_health - 20 and int(interception.b.call("GetCurrentHealth")) == 100 and int(interception.a.call("GetCurrentActionPoints")) == pa_before_friendly - 1, "Le friendly fire consomme les PA, touche C et jamais B")
	await _dispose(interception)
	_finish("COMBAT_PROTOTYPE_05_ATTACK_INTERCEPTION_SMOKE_TEST")
