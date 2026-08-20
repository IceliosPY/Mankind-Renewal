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

func _wait_for_unit_stop(unit: Node, maximum_frames: int) -> void:
	for _frame in maximum_frames:
		if not bool(unit.call("GetIsMoving")):
			return
		await physics_frame

func _move_to_world(controller: Node, grid: Node3D, unit: Node, world: Vector3, label: String) -> bool:
	var cell_id := int(grid.call("GetCellIdNearWorld", world, 0.9))
	var accepted := cell_id >= 0 and bool(controller.call("TrySelectDestinationCellId", cell_id))
	_check(accepted, label)
	if accepted:
		await _wait_for_unit_stop(unit, 360)
	return accepted

func _move_to_any_reachable(controller: Node, grid: Node3D, unit: Node, label: String) -> bool:
	var current_id := int(unit.call("GetCurrentCellId"))
	var current_world := grid.call("GetCellWorldPosition", current_id) as Vector3
	for offset in [Vector3(2, 0, 0), Vector3(-2, 0, 0), Vector3(0, 0, 2), Vector3(0, 0, -2)]:
		var cell_id := int(grid.call("GetCellIdNearWorld", current_world + offset, 0.8))
		if cell_id < 0 or not bool(grid.call("GetCellIsReachable", cell_id)) or bool(grid.call("GetCellIsOccupied", cell_id)):
			continue
		if bool(controller.call("TrySelectDestinationCellId", cell_id)):
			_check(true, label)
			await _wait_for_unit_stop(unit, 360)
			return true
	_check(false, label)
	return false

func _begin_select(action_controller: Node, target_name: String) -> bool:
	return bool(action_controller.call("BeginAttackSelection")) and bool(action_controller.call("SelectTargetByName", target_name))

func _run() -> void:
	_check(ResourceLoader.exists("res://Data/Weapons/DebugPistol.tres"), "DebugPistol existe comme Resource")
	_check(ResourceLoader.exists("res://Data/Weapons/DebugBlade.tres"), "DebugBlade existe comme Resource")
	var pistol := load("res://Data/Weapons/DebugPistol.tres")
	var blade := load("res://Data/Weapons/DebugBlade.tres")
	_check(pistol != null and blade != null, "Les deux WeaponDefinition se chargent")
	if pistol == null or blade == null:
		quit(1)
		return
	_check(bool(pistol.call("GetIsValidDefinition")) and bool(blade.call("GetIsValidDefinition")), "Les Resources d'armes sont valides")
	_check(str(pistol.call("GetItemId")) != str(blade.call("GetItemId")), "Les IDs d'armes sont distincts")
	_check(int(pistol.call("GetActionPointCost")) == 1 and int(pistol.call("GetBaseAccuracy")) == 20 and int(pistol.call("GetRangeInCells")) == 6, "DebugPistol porte les valeurs V3")
	_check(int(blade.call("GetAttackTypeValue")) == 1 and int(blade.call("GetRangeInCells")) == 1 and bool(blade.call("GetAllowsParry")), "DebugBlade est une arme de melee parable")
	_check(int(pistol.call("GetDamageComponentCount")) == 1 and int(blade.call("GetDamageComponentCount")) == 2 and is_equal_approx(float(blade.call("GetRawDamageValue")), 25.0), "Une arme peut sommer plusieurs composantes de degats")
	_check(int(pistol.call("GetPrimaryDamageTypeValue")) == 0, "Le type principal balistique est explicite")

	_check(ResourceLoader.exists("res://Scenes/Tests/CombatPrototype03.tscn"), "CombatPrototype03 existe")
	var packed := load("res://Scenes/Tests/CombatPrototype03.tscn") as PackedScene
	_check(packed != null, "CombatPrototype03 se charge")
	if packed == null:
		quit(1)
		return

	var prototype := packed.instantiate()
	root.add_child(prototype)
	var combat_v2 := prototype.get_node_or_null("CombatV2") as Node3D
	var controller := prototype.get_node_or_null("CombatV2/CombatV1/CombatModeController") as Node
	var grid := prototype.get_node_or_null("CombatV2/CombatV1/TacticalGrid") as Node3D
	var turn_manager := prototype.get_node_or_null("CombatV2/TurnManager") as Node
	var action_controller := prototype.get_node_or_null("CombatActionController") as Node
	var panel := prototype.get_node_or_null("DebugUI/CombatV3DebugPanel") as Control
	var old_panel := prototype.get_node_or_null("CombatV2/DebugUI/CombatDebugPanel") as Control
	var camera := prototype.get_node_or_null("CombatV2/CombatV1/VisualSlice01/CameraRig/PitchPivot/SpringArm3D/Camera3D") as Camera3D
	var unit_b_actor := prototype.get_node_or_null("CombatV2/UnitBActor") as Node3D
	var target_marker := prototype.get_node_or_null("TargetMarker") as Node3D
	var unit_a := prototype.get_node_or_null("CombatV2/CombatV1/TacticalUnit") as Node
	var unit_b := prototype.get_node_or_null("CombatV2/UnitB") as Node
	var unit_c := prototype.get_node_or_null("CombatV2/UnitC") as Node
	var unit_d := prototype.get_node_or_null("CombatV2/ReinforcementUnit") as Node
	_check(combat_v2 != null, "Combat V3 instancie CombatPrototype02 sans duplication")
	_check(controller != null and grid != null and turn_manager != null and action_controller != null and panel != null, "Actions, tours, grille et UI ont des responsabilites separees")
	_check(unit_a != null and unit_b != null and unit_c != null and unit_d != null, "Les quatre unites de test sont disponibles")
	if controller == null or grid == null or turn_manager == null or action_controller == null or panel == null or unit_a == null or unit_b == null or unit_c == null or unit_d == null or camera == null or unit_b_actor == null or target_marker == null:
		quit(1)
		return

	for _frame in 240:
		await physics_frame
		if bool(grid.call("GetIsBuilt")):
			break
	_check(bool(grid.call("GetIsBuilt")) and is_equal_approx(float(grid.get("CellSize")), 2.0), "La grille V2 de 2 m reste la reference")
	_check(old_panel != null and old_panel.process_mode == Node.PROCESS_MODE_DISABLED, "Seule l'instance V3 remplace visuellement l'ancien panneau")
	_check(str(unit_a.call("GetActiveWeaponName")) == "Debug Pistol" and str(unit_b.call("GetActiveWeaponName")) == "Debug Blade", "Les loadouts de test fournissent l'arme active")
	_check(int(unit_a.call("GetTeamId")) == 1 and int(unit_b.call("GetTeamId")) == 2 and int(unit_c.call("GetTeamId")) == 1, "Les equipes alliees et ennemies sont explicites")
	_check(int(unit_c.call("ApplyRawDamage", 1.9)) == 1 and int(unit_c.call("GetCurrentHealth")) == 99, "Les degats appliques sont arrondis a l'inferieur")
	unit_c.call("RestoreFullHealth")

	_check(bool(controller.call("EnterCombat")), "Combat V3 entre en combat via le controleur V2")
	await _wait_physics_frames(8)
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE A" and int(unit_a.call("GetCurrentActionPoints")) == 2, "A commence avec ses PA V2 intacts")
	_check(panel.visible and "ARME ACTIVE : Debug Pistol" in str(panel.call("GetStatusText")) and "PV : 100 / 100" in str(panel.call("GetStatusText")), "L'UI affiche PV, arme, PA, PM et ordre")
	_check(bool(action_controller.call("BeginAttackSelection")) and bool(action_controller.call("TrySelectTargetFromScreen", camera.unproject_position(unit_b_actor.global_position))), "Le clic projete selectionne une TacticalUnit")
	_check(str(action_controller.call("GetSelectedTargetName")) == "UNITE B" and target_marker.visible, "La cible cliquee est memorisee et visuellement marquee")
	action_controller.call("CancelAttackSelection")

	# Target validation: ally, out of range, then valid enemy.
	_check(_begin_select(action_controller, "UNITE C"), "Une unite peut etre selectionnee comme cible debug")
	_check(str(action_controller.call("GetSelectedTargetStatusText")) == "ALLIEE", "Une cible alliee est distinguee et refusee")
	_check(not bool(action_controller.call("DeclareSelectedAttack")) and int(unit_a.call("GetCurrentActionPoints")) == 2, "Une cible alliee ne lance rien et ne coute aucun PA")
	action_controller.call("CancelAttackSelection")
	_check(bool(controller.call("AddUnitToCombat", unit_d)), "Le renfort ennemi D peut rejoindre le test de portee")
	pistol.set("RangeInCells", 2)
	_check(_begin_select(action_controller, "UNITE D") and str(action_controller.call("GetSelectedTargetStatusText")) == "HORS DE PORTEE", "La distance en cellules refuse une cible trop lointaine")
	_check(not bool(action_controller.call("DeclareSelectedAttack")) and int(unit_a.call("GetCurrentActionPoints")) == 2 and not bool(action_controller.call("GetHasPendingReaction")), "Hors de portee: aucun PA et aucune reaction")
	pistol.set("RangeInCells", 6)
	action_controller.call("CancelAttackSelection")
	controller.call("RemoveUnitFromCombat", unit_d)

	# Ranged refusal: declaration suspends without cost, refusal launches and hits.
	_check(_begin_select(action_controller, "UNITE B"), "ATTAQUER ouvre le ciblage de B")
	_check(str(action_controller.call("GetSelectedTargetStatusText")) == "VALIDE", "B est une cible ennemie a portee")
	var pa_before_declaration := int(unit_a.call("GetCurrentActionPoints"))
	_check(bool(action_controller.call("DeclareSelectedAttack")) and bool(action_controller.call("GetHasPendingReaction")), "La declaration ouvre la reaction Esquive")
	await _wait_physics_frames(8)
	_check(bool(panel.call("GetReactionControlsVisible")) and "ESQUIVE" in str(panel.call("GetReactionStatusText")), "L'interface suspendue affiche ESQUIVER / REFUSER")
	var first_action_id := int(action_controller.call("GetCurrentActionId"))
	_check(int(unit_a.call("GetCurrentActionPoints")) == pa_before_declaration and not bool(action_controller.call("GetLastActionCommittedCost")), "La declaration ne depense aucun PA prematurement")
	_check(str(action_controller.call("GetOfferedReactionText")) == "DODGE", "La defense distante par defaut est l'Esquive")
	_check(bool(action_controller.call("RefuseReaction")), "Le defenseur peut refuser l'Esquive")
	_check(int(unit_a.call("GetCurrentActionPoints")) == 1 and int(unit_b.call("GetCurrentHealth")) == 80, "Le refus produit une touche, 20 degats et consomme 1 PA")
	_check(bool(action_controller.call("GetLastActionWasLaunched")) and bool(action_controller.call("GetLastReactionWasRefused")), "Le pipeline distingue refus, lancement et cout engage")

	# Ranged defence succeeds and still consumes PA; second reaction on same action is rejected.
	_check(_begin_select(action_controller, "UNITE B") and bool(action_controller.call("DeclareSelectedAttack")), "Une seconde attaque cree une nouvelle reaction")
	var second_action_id := int(action_controller.call("GetCurrentActionId"))
	_check(second_action_id > first_action_id, "Chaque attaque possede une identite unique")
	_check(bool(action_controller.call("AcceptReaction")), "B choisit Esquiver")
	_check(not bool(action_controller.call("AcceptReaction")), "Une unite ne reagit qu'une fois a la meme action")
	_check(str(action_controller.call("GetCurrentOutcomeText")) == "DODGED" and int(unit_b.call("GetCurrentHealth")) == 80, "Esquive 25 > Precision 20: zero degat")
	_check(int(unit_a.call("GetCurrentActionPoints")) == 0, "Une Esquive reussie consomme quand meme le PA de l'attaque lancee")

	# Insufficient AP never launches and does not end the turn.
	_check(_begin_select(action_controller, "UNITE B") and str(action_controller.call("GetSelectedTargetStatusText")) == "PA INSUFFISANTS", "Le ciblage signale clairement PA insuffisants")
	_check(not bool(action_controller.call("DeclareSelectedAttack")) and str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "Zero PA: pas de lancement, pas de reaction, pas de fin automatique")
	action_controller.call("CancelAttackSelection")

	# Movement after attacks remains available.
	var a_position := grid.call("GetCellWorldPosition", int(unit_a.call("GetCurrentCellId"))) as Vector3
	await _move_to_world(controller, grid, unit_a, a_position + Vector3(0, 0, -2), "A peut se deplacer apres deux attaques")
	_check(int(unit_a.call("GetCurrentMovementPoints")) == 5, "Le mouvement apres attaque utilise seulement les PM")
	controller.call("RequestEndTurn")
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE B", "Le tour ne change que sur FIN DU TOUR")

	# Movement -> melee attack -> attack -> movement.
	var c_position := grid.call("GetCellWorldPosition", int(unit_c.call("GetCurrentCellId"))) as Vector3
	await _move_to_world(controller, grid, unit_b, c_position + Vector3(0, 0, -2), "B se deplace avant son attaque de melee")
	_check(_begin_select(action_controller, "UNITE C") and str(action_controller.call("GetSelectedTargetStatusText")) == "VALIDE", "C est adjacente apres le deplacement")
	_check(bool(action_controller.call("DeclareSelectedAttack")) and str(action_controller.call("GetOfferedReactionText")) == "PARRY", "DebugBlade propose la Parade permise par l'arme de C")
	_check(bool(action_controller.call("AcceptReaction")) and str(action_controller.call("GetCurrentOutcomeText")) == "PARRIED", "Parade 25 > Precision 20: Parade reussie")
	_check(int(unit_b.call("GetCurrentActionPoints")) == 1 and int(unit_c.call("GetCurrentHealth")) == 100, "La Parade annule les degats mais pas le cout")
	unit_c.call("SetBaseParry", 20)
	_check(_begin_select(action_controller, "UNITE C") and bool(action_controller.call("DeclareSelectedAttack")) and bool(action_controller.call("AcceptReaction")), "Une nouvelle attaque de melee accepte une nouvelle Parade")
	_check(str(action_controller.call("GetCurrentOutcomeText")) == "HIT" and int(unit_c.call("GetCurrentHealth")) == 75, "Parade = Precision: l'attaquant gagne et inflige 25")
	await _move_to_any_reachable(controller, grid, unit_b, "B peut encore se deplacer apres ses attaques")
	controller.call("RequestEndTurn")
	controller.call("RequestEndTurn")
	_check(int(turn_manager.call("GetRoundNumber")) == 2 and str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "Le pipeline d'actions ne bloque pas le round suivant")

	# Equality and lower defence: attacker wins both, then AP insufficient.
	unit_b.call("SetBaseDodge", 20)
	_check(_begin_select(action_controller, "UNITE B") and bool(action_controller.call("DeclareSelectedAttack")) and bool(action_controller.call("AcceptReaction")), "A attaque B avec Esquive egale")
	_check(str(action_controller.call("GetCurrentOutcomeText")) == "HIT" and int(unit_b.call("GetCurrentHealth")) == 60, "Esquive = Precision: l'attaquant gagne")
	unit_b.call("SetBaseDodge", 10)
	_check(_begin_select(action_controller, "UNITE B") and bool(action_controller.call("DeclareSelectedAttack")) and bool(action_controller.call("AcceptReaction")), "A attaque B avec Esquive inferieure")
	_check(str(action_controller.call("GetCurrentOutcomeText")) == "HIT" and int(unit_b.call("GetCurrentHealth")) == 40, "Esquive < Precision: l'attaque touche")
	_check(_begin_select(action_controller, "UNITE B") and str(action_controller.call("GetSelectedTargetStatusText")) == "PA INSUFFISANTS", "Deux attaques distinctes epuisent exactement les 2 PA")
	action_controller.call("CancelAttackSelection")
	controller.call("RequestEndTurn")
	controller.call("RequestEndTurn")
	controller.call("RequestEndTurn")
	_check(int(turn_manager.call("GetRoundNumber")) == 3 and str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "Le round 3 demarre sans regression de tours")

	# Neutralisation on the second attack removes B, frees its cell, and preserves A's active turn.
	var b_cell := int(unit_b.call("GetCurrentCellId"))
	_check(bool(grid.call("GetCellIsOccupied", b_cell)), "La cellule de B est occupee avant neutralisation")
	_check(_begin_select(action_controller, "UNITE B") and bool(action_controller.call("DeclareSelectedAttack")) and bool(action_controller.call("RefuseReaction")), "La premiere attaque finale reduit B a 20 PV")
	_check(int(unit_b.call("GetCurrentHealth")) == 20, "Les PV persistent entre les actions et les rounds")
	_check(_begin_select(action_controller, "UNITE B") and bool(action_controller.call("DeclareSelectedAttack")) and bool(action_controller.call("RefuseReaction")), "La seconde attaque finale atteint zero PV")
	_check(bool(unit_b.call("GetIsNeutralized")) and int(unit_b.call("GetCurrentHealth")) == 0, "B est neutralisee a PV zero")
	_check(int(turn_manager.call("GetParticipantCount")) == 2 and str(turn_manager.call("GetActiveUnitName")) == "UNITE A", "B quitte l'Initiative sans interrompre le tour de A")
	_check(not bool(grid.call("GetCellIsOccupied", b_cell)) and not (unit_b.get("Actor") as Node3D).visible, "La cellule est liberee et l'acteur neutralise masque")
	_check(_begin_select(action_controller, "UNITE B") and str(action_controller.call("GetSelectedTargetStatusText")) == "NEUTRALISEE", "Une unite neutralisee est identifiee et non ciblable")
	_check(not bool(action_controller.call("DeclareSelectedAttack")), "Aucune attaque ne peut etre declaree sur une cible neutralisee")
	action_controller.call("CancelAttackSelection")
	controller.call("RequestEndTurn")
	_check(str(turn_manager.call("GetActiveUnitName")) == "UNITE C", "Le combat continue normalement sans l'unite retiree")

	# Responsive panel checks at the requested reference sizes.
	for size in [Vector2i(1280, 720), Vector2i(1600, 900), Vector2i(1920, 1080)]:
		root.size = size
		await process_frame
		_check(panel.position.x >= 0.0 and panel.position.y >= 0.0 and panel.position.x + panel.size.x <= size.x and panel.position.y + panel.size.y <= size.y, "UI V3 contenue dans %dx%d" % [size.x, size.y])
	var scroll := panel.get_node("Margin/VBox/SecondaryScroll") as ScrollContainer
	_check(scroll != null and scroll.size.y >= 0.0, "Les controles secondaires restent dans un ScrollContainer")

	controller.call("ExitCombat")
	await _wait_physics_frames(3)
	_check(not panel.visible and not bool(controller.call("GetIsCombatActive")), "La sortie Combat retourne proprement en exploration")
	_check(str(ProjectSettings.get_setting("application/run/main_scene")) == "res://Scenes/Tests/CombatPrototype03.tscn", "La scene principale validee reste CombatPrototype03")

	prototype.queue_free()
	await process_frame
	if failures.is_empty():
		print("COMBAT_PROTOTYPE_03_SMOKE_TEST: SUCCESS")
		quit(0)
	else:
		print("COMBAT_PROTOTYPE_03_SMOKE_TEST: %d FAILURE(S)" % failures.size())
		quit(1)
