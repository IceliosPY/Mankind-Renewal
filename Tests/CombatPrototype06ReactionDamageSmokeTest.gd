extends "res://Tests/CombatPrototype06TestBase.gd"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var reaction := await _spawn()
	reaction.setup.call("SetCoverMode", NONE)
	_set_profile(reaction.res_a, PROFILE_BALLISTIC_50)
	var destination := _cell_offset(reaction, reaction.a, Vector3(0, 0, -4))
	_check(destination >= 0 and bool(reaction.controller.call("TrySelectDestinationCellId", destination)) and await _wait_pending(reaction.actions), "Le mouvement V6 atteint la reaction distante de B")
	var hp_before := int(reaction.a.call("GetCurrentHealth"))
	_check(str(reaction.actions.call("GetPendingReactorName")) == "UNITE B" and bool(reaction.actions.call("ChooseReaction", 0)) and int(reaction.a.call("GetCurrentHealth")) == hp_before - 26 and int(reaction.actions.call("GetLastReactionDamage")) == 26, "La reaction offensive utilise Resistance 50 via le resolver commun")
	_check(str(reaction.damage.call("GetLastTargetName")) == "UNITE A" and abs(float(reaction.damage.call("GetLastBaseResistance", 0)) - 50.0) < 0.0001 and abs(float(reaction.damage.call("GetLastPenetrationApplied", 0)) - 4.0) < 0.0001 and abs(float(reaction.damage.call("GetLastEffectiveResistance", 0)) - 46.0) < 0.0001, "Le breakdown de reaction expose Resistance 50 et Penetration 4 de la vraie attaque")
	if bool(reaction.actions.call("GetHasOffensiveOpportunity")):
		reaction.actions.call("RefuseReaction")
	if bool(reaction.actions.call("GetIsAwaitingMovementChoice")):
		reaction.actions.call("StopMovement")
	await _dispose(reaction)

	var counter := await _spawn()
	counter.setup.call("SetCoverMode", NONE)
	counter.controller.call("RemoveUnitFromCombat", counter.c)
	_set_profile(counter.res_a, PROFILE_BALLISTIC_50)
	var pa_before := int(counter.a.call("GetCurrentActionPoints"))
	_check(bool(counter.actions.call("BeginAttackSelection")) and bool(counter.actions.call("SelectTargetByName", "UNITE B")) and bool(counter.actions.call("DeclareSelectedAttack")) and str(counter.actions.call("GetPendingReactorName")) == "UNITE D", "Le counter V6 est propose avant Launch")
	_check(int(counter.a.call("GetCurrentActionPoints")) == pa_before and bool(counter.actions.call("ChooseReaction", 0)) and int(counter.a.call("GetCurrentHealth")) == 74 and int(counter.actions.call("GetLastReactionDamage")) == 26, "Le counter utilise Damage Resolution sans consommer les PA de l'attaque initiale")
	_check(str(counter.damage.call("GetLastTargetName")) == "UNITE A" and int(counter.damage.call("GetLastFinalDamage")) == 26 and bool(counter.actions.call("GetHasPendingDefensiveReaction")), "Le counter conserve son breakdown puis l'attaque reprend vers Dodge")
	_check(bool(counter.actions.call("RefuseDefensiveReaction")) and int(counter.a.call("GetCurrentActionPoints")) == pa_before - 1, "Les PA sont engages seulement apres counter et defense")
	await _dispose(counter)

	var lethal_counter := await _spawn()
	lethal_counter.setup.call("SetCoverMode", NONE)
	lethal_counter.controller.call("RemoveUnitFromCombat", lethal_counter.c)
	_set_profile(lethal_counter.res_a, PROFILE_NONE)
	lethal_counter.a.call("ApplyRawDamage", 80.0)
	_check(bool(lethal_counter.actions.call("BeginAttackSelection")) and bool(lethal_counter.actions.call("SelectTargetByName", "UNITE B")) and bool(lethal_counter.actions.call("DeclareSelectedAttack")) and bool(lethal_counter.actions.call("ChooseReaction", 0)), "Le counter lethal V6 se resout avant Launch")
	await _wait_frames(5)
	_check(bool(lethal_counter.a.call("GetIsNeutralized")) and bool(lethal_counter.actions.call("GetLastAttackCancelledBeforeLaunch")) and not bool(lethal_counter.actions.call("GetLastAttackCostCommitted")) and not bool(lethal_counter.actions.call("GetHasPendingDefensiveReaction")), "Le counter lethal annule l'attaque initiale sans engagement de PA ni defense")
	await _dispose(lethal_counter)

	_finish("COMBAT_PROTOTYPE_06_REACTION_DAMAGE_SMOKE_TEST")
