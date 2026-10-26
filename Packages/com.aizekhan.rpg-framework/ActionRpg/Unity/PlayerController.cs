using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Moves the player and turns button presses into game actions: attack the nearest enemy in reach, respawn.
    /// Keyboard: WASD / arrows, Space or left mouse button to attack, R to respawn.
    /// Gamepad: left stick, south button (A / Cross) to attack, Start to respawn.
    /// Speed, reach and attack rate come from the player's <see cref="CharacterDefinition"/>.
    /// </summary>
    public class PlayerController : ActorView
    {
        [SerializeField, Min(0f)] private float _turnSpeed = 900f;

        private CharacterController _body;
        private StrikeAnimator _strike;
        private InputAction _move;
        private InputAction _attack;
        private InputAction _respawn;
        private float _cooldown;
        private float _fallSpeed;

        protected virtual void Awake()
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

        protected virtual void OnEnable()
        {
            _move.Enable();
            _attack.Enable();
            _respawn.Enable();
        }

        protected virtual void OnDisable()
        {
            _move.Disable();
            _attack.Disable();
            _respawn.Disable();
        }

        protected virtual void OnDestroy()
        {
            _move.Dispose();
            _attack.Dispose();
            _respawn.Dispose();
        }

        protected virtual void Update()
        {
            var player = Actor;
            if (player == null)
                return;

            if (!player.IsAlive())
            {
                if (_respawn.WasPressedThisFrame())
                    World.RevivePlayer();
                return;
            }

            Move(_move.ReadValue<Vector2>());

            _cooldown -= Time.deltaTime;
            if (_attack.IsPressed())
                TryAttackNearest();
        }

        /// <summary>
        /// Strikes the nearest living enemy in reach, if the attack is ready.
        /// </summary>
        /// <returns>True when an enemy was hit.</returns>
        public bool TryAttackNearest()
        {
            if (!IsBound || _cooldown > 0f || !TryFindTarget(out int target, out Vector3 targetPosition))
                return false;

            _cooldown = Definition.AttackCooldown;
            var toTarget = Flat(targetPosition - transform.position);
            if (toTarget.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toTarget);
            if (_strike != null)
                _strike.Play();
            return World.Attack(EntityId, target) > 0f;
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

            var velocity = direction * Definition.MoveSpeed;
            if (_body != null)
            {
                _fallSpeed = _body.isGrounded ? -1f : _fallSpeed + Physics.gravity.y * Time.deltaTime;
                _body.Move((velocity + Vector3.up * _fallSpeed) * Time.deltaTime);
            }
            else
            {
                transform.position += velocity * Time.deltaTime;
            }
            Face(direction, _turnSpeed);
        }

        private bool TryFindTarget(out int target, out Vector3 position)
        {
            target = -1;
            position = default;
            float best = Definition.AttackRange * Definition.AttackRange;
            foreach (int enemy in World.LivingEnemies)
            {
                if (!TryGetPosition(enemy, out var candidate))
                    continue;
                float distance = Flat(candidate - transform.position).sqrMagnitude;
                if (distance > best)
                    continue;
                best = distance;
                target = enemy;
                position = candidate;
            }
            return target >= 0;
        }
    }
}
