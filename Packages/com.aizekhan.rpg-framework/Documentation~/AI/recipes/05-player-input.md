# Recipe: player input

1. Install the Input System package and add `Unity.InputSystem` to the runtime asmdef references.
2. In the controller MonoBehaviour create `InputAction`s in `Awake` (keyboard, mouse and gamepad bindings),
   enable them in `OnEnable`, disable in `OnDisable`, dispose in `OnDestroy`. See `PlayerController` in the package (`ActionRpg/Unity`).
3. Input decides nothing: it calls the rules (`game.Attack(heroId, targetId)`) and the rules decide whether it works.
   Purely visual movement (walking around) may stay in the view.
4. Expose each action as a public method (`TryAttackNearest()`) so PlayMode tests can trigger it without devices.
5. Click to move (as in Diablo) is already in the kit: tick **Click To Move** on the `PlayerController` (or set
   `ClickToMove` in code). Bake a NavMesh for the scene so the walk goes round obstacles (without one it walks
   straight). Feed a direction of your own by overriding `MoveInput()`, a pick of your own by overriding `TryPick`,
   and drive it from your own input with `MoveTo`, `MoveToAttack` and `Stop`; do not copy the controller.
