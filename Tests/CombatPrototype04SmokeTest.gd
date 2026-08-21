extends SceneTree

const SCENE := "res://Scenes/Tests/CombatPrototype04.tscn"
const PRIMARY := "weapon.primary"
const SECONDARY := "weapon.secondary"

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

func _spawn_v4() -> Dictionary:
	var packed := load(SCENE) as PackedScene
	if packed == null:
		return {}
	var prototype := packed.instantiate()
	root.add_child(prototype)
	var nodes := {
		"root": prototype,
		"controller": prototype.get_node("InventoryV1/CombatV3/CombatV2/CombatV1/CombatModeController"),
		"grid": prototype.get_node("InventoryV1/CombatV3/CombatV2/CombatV1/TacticalGrid"),
		"turns": prototype.get_node("InventoryV1/CombatV3/CombatV2/TurnManager"),
		"actions": prototype.get_node("CombatV4ActionController"),
		"a": prototype.get_node("InventoryV1/CombatV3/CombatV2/CombatV1/TacticalUnit"),
		"b": prototype.get_node("InventoryV1/CombatV3/CombatV2/UnitB"),
		"c": prototype.get_node("InventoryV1/CombatV3/CombatV2/UnitC"),
		"d": prototype.get_node("InventoryV1/CombatV3/CombatV2/ReinforcementUnit"),
		"setup": prototype.get_node("InventoryV1/PrototypeSetup"),
		"panel": prototype.get_node("DebugUI/CombatV4DebugPanel"),
		"b_reactions": prototype.get_node("ReactionSystems/UnitBReactions"),
		"c_reactions": prototype.get_node("ReactionSystems/UnitCReactions"),
	}
	for _frame in 240:
		await physics_frame
		if bool(nodes.grid.call("GetIsBuilt")):
			break
	if bool(nodes.grid.call("GetIsBuilt")):
		nodes.controller.call("EnterCombat")
		await _wait_physics_frames(5)
	return nodes

func _dispose(nodes: Dictionary) -> void:
	if nodes.is_empty():
		return
	if bool(nodes.controller.call("GetIsCombatActive")):
		nodes.controller.call("ExitCombat")
	nodes.root.queue_free()
	await process_frame
	await process_frame

func _cell_offset(nodes: Dictionary, unit: Node, offset: Vector3) -> int:
	var position := nodes.grid.call("GetCellWorldPosition", int(unit.call("GetCurrentCellId"))) as Vector3
	return int(nodes.grid.call("GetCellIdNearWorld", position + offset, 0.9))

func _start_two_step_movement(nodes: Dictionary) -> int:
	var target := _cell_offset(nodes, nodes.a, Vector3(0, 0, -4))
	return target if target >= 0 and bool(nodes.controller.call("TrySelectDestinationCellId", target)) else -1

func _wait_for_pending(actions: Node, maximum_frames := 240) -> bool:
	for _frame in maximum_frames:
		if bool(actions.call("GetHasOffensiveOpportunity")):
			return true
		await physics_frame
	return false

func _wait_for_stop(unit: Node, maximum_frames := 360) -> void:
	for _frame in maximum_frames:
		if not bool(unit.call("GetIsMoving")):
			return
		await physics_frame

func _begin_attack(actions: Node, target_name: String) -> bool:
	return bool(actions.call("BeginAttackSelection")) and bool(actions.call("SelectTargetByName", target_name)) and bool(actions.call("DeclareSelectedAttack"))

func _run() -> void:
	_check(ResourceLoader.exists(SCENE), "CombatPrototype04.tscn existe")
	for path in [
		"res://Data/Reactions/DebugInterceptionShot.tres",
		"res://Data/Reactions/DebugInterceptionBurst.tres",
		"res://Data/Reactions/DebugGuardStrike.tres",
		"res://Data/Reactions/DebugAttackCounter.tres",
	]:
		var definition := load(path)
		_check(definition != null and bool(definition.call("GetIsValidDefinition")) and not str(definition.call("GetReactionId")).is_empty(), "ReactionDefinition charge avec ID stable : " + path.get_file())
	_check(str(ProjectSettings.get_setting("application/run/main_scene")) == "res://Scenes/Tests/CombatPrototype04.tscn", "La scene principale reste CombatPrototype04")

	# Refusal closes a reactor for the complete movement action; a new movement gets a new ID.
	var refusal := await _spawn_v4()
	_check(not refusal.is_empty(), "CombatPrototype04 se charge et instancie Inventory/Equipment V1")
	if refusal.is_empty():
		quit(1)
		return
	_check(str(refusal.turns.call("GetActiveUnitName")) == "UNITE A" and int(refusal.b_reactions.call("GetReactionCount")) == 2 and int(refusal.c_reactions.call("GetReactionCount")) == 2, "La configuration V4 expose A active et les providers de B/C")
	_check(str(refusal.b.call("GetActiveWeaponName")) == "Debug Heavy Pistol" and str(refusal.c.call("GetActiveWeaponName")) == "Debug Blade", "Les reactions utilisent les armes reellement equipees")
	var start_cell := int(refusal.a.call("GetCurrentCellId"))
	var pm_before := int(refusal.a.call("GetCurrentMovementPoints"))
	_check(_start_two_step_movement(refusal) >= 0 and await _wait_for_pending(refusal.actions), "La premiere occasion valide suspend le mouvement cellule par cellule")
	var first_move_id := int(refusal.actions.call("GetCurrentMovementActionId"))
	_check(first_move_id > 0 and int(refusal.a.call("GetCurrentCellId")) != start_cell and int(refusal.a.call("GetCurrentMovementPoints")) == pm_before - 1, "Le mouvement possede un ActionId et la cellule/PM sont engages avant la reaction")
	_check(str(refusal.actions.call("GetPendingReactorName")) == "UNITE B" and int(refusal.actions.call("GetReactionChoiceCount")) == 2, "B reagit avant C selon Initiative et choisit entre deux reactions")
	_check(bool(refusal.actions.call("RefuseReaction")) and str(refusal.actions.call("GetPendingReactorName")) == "UNITE C", "Le refus de B ferme B et revalide C")
	_check(bool(refusal.actions.call("RefuseReaction")), "C peut egalement refuser manuellement")
	# The refusal resumes on the next deferred frame; wait for that resumed segment to finish.
	await _wait_physics_frames(90)
	_check(int(refusal.actions.call("GetOpportunityOfferCount", first_move_id, "UNITE B")) == 1 and not bool(refusal.actions.call("GetHasPendingReaction")), "B n'est proposee qu'une fois pour toute la meme action refusee")
	var new_target := _cell_offset(refusal, refusal.a, Vector3(0, 0, 2))
	_check(new_target >= 0 and bool(refusal.controller.call("TrySelectDestinationCellId", new_target)) and await _wait_for_pending(refusal.actions), "Une nouvelle action de mouvement rend la reaction de nouveau disponible")
	var second_move_id := int(refusal.actions.call("GetCurrentMovementActionId"))
	_check(second_move_id > first_move_id and str(refusal.actions.call("GetPendingReactorName")) == "UNITE B", "Le nouveau mouvement recoit un nouvel ActionId")
	await _dispose(refusal)

	# Accepted reaction, revalidation, equipment lock and modified route preserve the action ID.
	var modified := await _spawn_v4()
	_check(_start_two_step_movement(modified) >= 0 and await _wait_for_pending(modified.actions), "Le scenario de reaction acceptee atteint B")
	var modified_id := int(modified.actions.call("GetCurrentMovementActionId"))
	var health_before := int(modified.a.call("GetCurrentHealth"))
	var loadout_a := modified.setup.call("GetLoadout", "UNITE A") as Node
	var active_instance_before := str(loadout_a.call("GetEquippedInstanceId", PRIMARY))
	_check(not bool(loadout_a.call("SetActiveSlot", SECONDARY)) and str(loadout_a.call("GetEquippedInstanceId", PRIMARY)) == active_instance_before, "L'equipement est verrouille pendant une decision de reaction")
	_check(bool(modified.actions.call("ChooseReaction", 0)) and int(modified.a.call("GetCurrentHealth")) == health_before - 30, "Le tir d'interception gratuit applique les degats de la vraie arme active")
	_check(int(modified.b.call("GetCurrentActionPoints")) == 0 and int(modified.b.call("GetCurrentMovementPoints")) == 0, "La reaction ne restaure ni ne consomme PA/PM et n'est pas un tour")
	_check(str(modified.actions.call("GetPendingReactorName")) == "UNITE C" and bool(modified.actions.call("RefuseReaction")), "C est revalidee apres la resolution de B")
	_check(bool(modified.actions.call("GetIsAwaitingMovementChoice")) and bool(modified.actions.call("BeginModifyMovement")), "Le joueur recupere CONTINUER / MODIFIER / ARRETER apres une reaction acceptee")
	var modified_target := _cell_offset(modified, modified.a, Vector3(-2, 0, 0))
	_check(modified_target >= 0 and bool(modified.controller.call("TrySelectDestinationCellId", modified_target)), "MODIFIER recalcule un trajet depuis la cellule actuelle")
	await _wait_for_stop(modified.a)
	await _wait_physics_frames(5)
	_check(int(modified.actions.call("GetLastCompletedMovementActionId")) == modified_id and int(modified.actions.call("GetOpportunityOfferCount", modified_id, "UNITE B")) == 1, "Le trajet modifie conserve ActionId et les reacteurs deja fermes")
	await _dispose(modified)

	# Continue recomputes the route to the original destination; accept the melee reaction here.
	var continued := await _spawn_v4()
	var continue_pm := int(continued.a.call("GetCurrentMovementPoints"))
	_check(_start_two_step_movement(continued) >= 0 and await _wait_for_pending(continued.actions), "Le scenario CONTINUER atteint la premiere occasion")
	var continue_id := int(continued.actions.call("GetCurrentMovementActionId"))
	_check(bool(continued.actions.call("RefuseReaction")) and str(continued.actions.call("GetPendingReactorName")) == "UNITE C", "B refuse et laisse C proposer l'interception de melee")
	_check(bool(continued.actions.call("ChooseReaction", 0)) and int(continued.a.call("GetCurrentHealth")) == 75, "DebugGuardStrike utilise gratuitement la vraie DebugBlade")
	_check(bool(continued.actions.call("GetIsAwaitingMovementChoice")) and bool(continued.actions.call("ContinueMovement")), "CONTINUER recalcule le chemin vers la destination initiale")
	await _wait_physics_frames(90)
	_check(int(continued.actions.call("GetLastCompletedMovementActionId")) == continue_id and int(continued.a.call("GetCurrentMovementPoints")) == continue_pm - 2, "CONTINUER conserve ActionId et les PM deja depenses")
	await _dispose(continued)

	# Per-reaction frequency, stop, then a fresh action.
	var frequency := await _spawn_v4()
	_check(_start_two_step_movement(frequency) >= 0 and await _wait_for_pending(frequency.actions), "Le test de frequence ouvre les deux choix de B")
	_check(bool(frequency.actions.call("ChooseReaction", 1)) and bool(frequency.actions.call("RefuseReaction")) and bool(frequency.actions.call("StopMovement")), "La rafale limitee est acceptee puis l'action est arretee")
	var stopped_id := int(frequency.actions.call("GetLastCompletedMovementActionId"))
	var fresh_target := _cell_offset(frequency, frequency.a, Vector3(-2, 0, 0))
	_check(fresh_target >= 0 and bool(frequency.controller.call("TrySelectDestinationCellId", fresh_target)) and await _wait_for_pending(frequency.actions), "ARRETER permet un nouveau mouvement reactif")
	_check(int(frequency.actions.call("GetCurrentMovementActionId")) != stopped_id and int(frequency.actions.call("GetReactionChoiceCount")) == 1 and str(frequency.actions.call("GetReactionChoiceId", 0)) == "reaction.debug_interception_shot", "La limite propre a la rafale vaut pour le round sans jauge universelle")
	await _dispose(frequency)

	# Initiative tie uses random tie breaking; neutralisation revalidates away the second reactor.
	var lethal_move := await _spawn_v4()
	lethal_move.b.call("SetInitiative", 20)
	lethal_move.c.call("SetInitiative", 20)
	lethal_move.a.call("ApplyRawDamage", 70.0)
	_check(_start_two_step_movement(lethal_move) >= 0 and await _wait_for_pending(lethal_move.actions), "Deux reacteurs a Initiative egale deviennent simultanement eligibles")
	_check(bool(lethal_move.actions.call("GetLastReactionOrderUsedTieBreaker")), "Une egalite exacte de reactions utilise un departage aleatoire")
	# Select the ranged lethal reactor if C won the tie: refuse C, then B is revalidated.
	if str(lethal_move.actions.call("GetPendingReactorName")) == "UNITE C":
		lethal_move.actions.call("RefuseReaction")
	_check(str(lethal_move.actions.call("GetPendingReactorName")) == "UNITE B" and bool(lethal_move.actions.call("ChooseReaction", 0)), "B peut neutraliser A pendant l'interruption")
	await _wait_physics_frames(5)
	_check(bool(lethal_move.a.call("GetIsNeutralized")) and str(lethal_move.turns.call("GetActiveUnitName")) != "UNITE A", "La neutralisation arrete le mouvement et TurnManager continue proprement")
	_check(not bool(lethal_move.actions.call("GetHasOffensiveOpportunity")) and not bool(lethal_move.actions.call("GetIsAwaitingMovementChoice")), "La reaction restante est annulee par revalidation et aucun bouton de reprise n'est valide")
	await _dispose(lethal_move)

	# Attack counter: survives, resumes, then defensive V3 reaction and AP commitment.
	var attack := await _spawn_v4()
	var attack_pa := int(attack.a.call("GetCurrentActionPoints"))
	var attack_weapon_id := str((attack.setup.call("GetLoadout", "UNITE A") as Node).call("GetEquippedInstanceId", PRIMARY))
	_check(_begin_attack(attack.actions, "UNITE B") and str(attack.actions.call("GetPendingReactorName")) == "UNITE D", "Le counter est propose apres declaration et avant lancement")
	_check(int(attack.a.call("GetCurrentActionPoints")) == attack_pa and not bool(attack.actions.call("GetLastAttackCostCommitted")), "Aucun PA n'est depense pendant le counter en attente")
	_check(str(attack.actions.call("GetCurrentWeaponInstanceId")) == attack_weapon_id and str(attack.actions.call("GetCurrentWeaponDefinitionId")) == "weapon.debug_pistol", "Le contexte conserve le snapshot de l'ItemInstance et de la WeaponDefinition")
	_check(bool(attack.actions.call("ChooseReaction", 0)) and int(attack.a.call("GetCurrentHealth")) == 70, "Le counter gratuit utilise l'arme equipee de D et A survit")
	_check(bool(attack.actions.call("GetHasPendingDefensiveReaction")) and str(attack.actions.call("GetDefensiveReactionText")) == "DODGE", "L'attaque normale reprend ensuite le pipeline defensif V3")
	_check(bool(attack.actions.call("RefuseDefensiveReaction")) and int(attack.a.call("GetCurrentActionPoints")) == attack_pa - 1 and int(attack.b.call("GetCurrentHealth")) == 80, "Les PA sont engages au lancement et l'attaque normale se resout")
	_check(int(attack.actions.call("GetLastReactionActionId")) != int(attack.actions.call("GetCurrentActionId")), "L'attaque de reaction possede sa propre identite sans creer de chaine")
	await _dispose(attack)

	# Lethal pre-launch counter: no AP commitment and no defensive reaction.
	var cancelled := await _spawn_v4()
	cancelled.a.call("ApplyRawDamage", 70.0)
	_check(_begin_attack(cancelled.actions, "UNITE B") and bool(cancelled.actions.call("ChooseReaction", 0)), "Le counter lethal se resout avant le lancement")
	await _wait_physics_frames(5)
	_check(bool(cancelled.a.call("GetIsNeutralized")) and bool(cancelled.actions.call("GetLastAttackCancelledBeforeLaunch")), "L'attaquant neutralise annule l'attaque avant lancement")
	_check(not bool(cancelled.actions.call("GetLastAttackWasLaunched")) and not bool(cancelled.actions.call("GetLastAttackCostCommitted")) and not bool(cancelled.actions.call("GetHasPendingDefensiveReaction")), "Aucun PA n'est engage, aucun degat initial et aucune Esquive/Parade n'est proposee")
	await _dispose(cancelled)

	# Responsive debug UI.
	var ui := await _spawn_v4()
	var panel := ui.panel as Control
	var viewport := root as Window
	for size in [Vector2i(1280, 720), Vector2i(1600, 900), Vector2i(1920, 1080)]:
		viewport.size = size
		await process_frame
		_check(panel.position.x >= 0.0 and panel.position.y >= 0.0 and panel.position.x + panel.size.x <= size.x and panel.position.y + panel.size.y <= size.y, "UI V4 contenue dans %dx%d" % [size.x, size.y])
	var scroll := panel.get_node("Margin/VBox/SecondaryScroll") as ScrollContainer
	_check(scroll != null and scroll.size.y >= 0.0 and "ROUND" in str(panel.call("GetStatusText")), "Les informations principales restent visibles et le secondaire reste scrollable")
	await _dispose(ui)

	if failures.is_empty():
		print("COMBAT_PROTOTYPE_04_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("COMBAT_PROTOTYPE_04_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
