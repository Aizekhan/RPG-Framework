using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Moves the player and turns button presses into game actions: attack the nearest enemy in reach, use the
    /// nearest place (a chest, a door: <see cref="InteractionPointView"/>), respawn.
    /// Keyboard: WASD / arrows, Space or left mouse button to attack, E to use, R to respawn.
    /// Gamepad: left stick, south button (A / Cross) to attack, west button (X / Square) to use, Start to respawn.
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
        private InputAction _use;
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

            _use = new InputAction("Use", InputActionType.Button);
            _use.AddBinding("<Keyboard>/e");
            _use.AddBinding("<Gamepad>/buttonWest");
        }

        protected virtual void OnEnable()
        {
            _move.Enable();
            _attack.Enable();
            _respawn.Enable();
            _use.Enable();
        }

        protected virtual void OnDisable()
        {
            _move.Disable();
            _attack.Disable();
            _respawn.Disable();
            _use.Disable();
        }

        protected virtual void OnDestroy()
        {
            _move.Dispose();
            _attack.Dispose();
            _respawn.Dispose();
            _use.Dispose();
        }

        protected virtual void Update()
        {
            var player = Actor;
            // A paused game: the player stands where they are
            if (player == null || World.IsPaused)
                return;

            if (!player.IsAlive())
            {
                if (_respawn.WasPressedThisFrame())
                    World.RevivePlayer();
                return;
            }

            _cooldown -= Time.deltaTime;
            // A stunned player stands still (but still falls)
            Move(player.IsStunned ? Vector2.zero : _move.ReadValue<Vector2>());
            if (!player.IsStunned && _attack.IsPressed())
                TryAttackNearest();
            if (!player.IsStunned && _use.WasPressedThisFrame())
                World.Interact();
        }

        /// <summary>
        /// Strikes the nearest living enemy in reach, if the attack is ready.
        /// </summary>
        /// <returns>True when it struck at an enemy (in co-op as a client, the host then deals the damage).</returns>
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
            World.Attack(EntityId, target);
            return true;
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

            var velocity = direction * (Definition.MoveSpeed * (Actor?.MoveMultiplier ?? 1f));
            if (_body != null)
            {
                // No ground anywhere below (an empty test scene): stay at this height instead of falling forever
                bool groundBelow = _body.isGrounded || Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 500f);
                _fallSpeed = _body.isGrounded ? -1f : groundBelow ? _fallSpeed + Physics.gravity.y * Time.deltaTime : 0f;
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
            // Whoever the rules let this player hit: enemies, and in PvP the other sides
            foreach (var actor in World.Actors)
            {
                int other = actor.EntityId;
                if (!actor.IsAlive() || !World.AreHostile(EntityId, other) || !TryGetPosition(other, out var candidate))
                    continue;
                float distance = Flat(candidate - transform.position).sqrMagnitude;
                if (distance > best)
                    continue;
                best = distance;
                target = other;
                position = candidate;
            }
            return target >= 0;
        }
    }
}
