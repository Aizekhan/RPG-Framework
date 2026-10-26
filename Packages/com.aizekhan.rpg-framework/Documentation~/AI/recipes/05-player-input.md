# Recipe: player input

1. Install the Input System package and add `Unity.InputSystem` to the runtime asmdef references.
2. In the controller MonoBehaviour create `InputAction`s in `Awake` (keyboard, mouse and gamepad bindings),
   enable them in `OnEnable`, disable in `OnDisable`, dispose in `OnDestroy`. See `PlayerController` in the package (`ActionRpg/Unity`).
3. Input decides nothing: it calls the rules (`game.Attack(heroId, targetId)`) and the rules decide whether it works.
   Purely visual movement (walking around) may stay in the view.
4. Expose each action as a public method (`TryAttackNearest()`) so PlayMode tests can trigger it without devices.
