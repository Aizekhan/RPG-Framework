using UnityEngine;
using UnityEngine.InputSystem;

namespace ReferenceGame
{
    /// <summary>
    /// Moves the hero and turns button presses into game actions: attack the nearest goblin in reach, respawn.
    /// Keyboard: WASD / arrows, Space or left mouse button to attack, R to respawn.
    /// Gamepad: left stick, south button (A / Cross) to attack, Start to respawn.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class HeroController : CombatantView
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float _moveSpeed = 6f;
        [SerializeField, Min(0f)] private float _turnSpeed = 900f;

        [Header("Attack")]
        [Tooltip("How far (meters) the hero's sword reaches")]
        [SerializeField, Min(0f)] private float _attackRange = 2.4f;
        [SerializeField, Min(0f)] private float _attackCooldown = 0.4f;

        private CharacterController _body;
        private StrikeAnimator _strike;
        private InputAction _move;
        private InputAction _attack;
        private InputAction _respawn;
        private float _cooldown;
        private float _fallSpeed;

        public float AttackRange => _attackRange;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            _strike = GetComponent<StrikeAnimator>();

            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _attack = new InputAction("Attack", InputActionType.Button);
            _attack.AddBinding("<Keyboard>/space");
            _attack.AddBinding("<Mouse>/leftButton");
            _attack.AddBinding("<Gamepad>/buttonSouth");

            _respawn = new InputAction("Respawn", InputActionType.Button);
            _respawn.AddBinding("<Keyboard>/r");
            _respawn.AddBinding("<Gamepad>/start");
        }

        private void OnEnable()
        {
            _move.Enable();
            _attack.Enable();
            _respawn.Enable();
        }

        private void OnDisable()
        {
            _move.Disable();
            _attack.Disable();
            _respawn.Disable();
        }

        private void OnDestroy()
        {
            _move.Dispose();
            _attack.Dispose();
            _respawn.Dispose();
        }

        private void Update()
        {
            var hero = Combatant;
            if (hero == null)
                return;

            if (!hero.IsAlive())
            {
                if (_respawn.WasPressedThisFrame())
                    Game.RevivePlayer();
                return;
            }

            Move(_move.ReadValue<Vector2>());

            _cooldown -= Time.deltaTime;
            if (_attack.IsPressed())
                TryAttackNearest();
        }

        /// <summary>
        /// Strikes the nearest living goblin in reach, if the attack is ready.
        /// </summary>
        /// <returns>True when a goblin was hit.</returns>
        public bool TryAttackNearest()
        {
            if (!IsBound || _cooldown > 0f || !TryFindTarget(out int target, out Vector3 targetPosition))
                return false;

            _cooldown = _attackCooldown;
            transform.rotation = Quaternion.LookRotation(Flat(targetPosition - transform.position));
            if (_strike != null)
                _strike.Play();
            return Game.Attack(EntityId, target) > 0f;
        }

        private void Move(Vector2 input)
        {
            var direction = new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            // Screen-relative: "up" walks away from the camera
            var view = Camera.main;
            if (view != null)
                direction = Quaternion.Euler(0f, view.transform.eulerAngles.y, 0f) * direction;

            _fallSpeed = _body.isGrounded ? -1f : _fallSpeed + Physics.gravity.y * Time.deltaTime;
            _body.Move((direction * _moveSpeed + Vector3.up * _fallSpeed) * Time.deltaTime);
            Face(direction, _turnSpeed);
        }

        private bool TryFindTarget(out int target, out Vector3 position)
        {
            target = -1;
            position = default;
            float best = _attackRange * _attackRange;
            foreach (int goblin in Game.LivingGoblins)
            {
                if (!TryGetPosition(goblin, out var candidate))
                    continue;
                float distance = (Flat(candidate - transform.position)).sqrMagnitude;
                if (distance > best)
                    continue;
                best = distance;
                target = goblin;
                position = candidate;
            }
            return target >= 0;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude < 0.0001f ? Vector3.forward : vector;
        }
    }
}
