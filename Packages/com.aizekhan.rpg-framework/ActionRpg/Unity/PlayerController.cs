using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Moves the player and turns button presses into game actions: attack the nearest enemy in reach, use the
    /// nearest place (a chest, a door: <see cref="InteractionPointView"/>), respawn.
    /// Keyboard: WASD / arrows, Space or left mouse button to attack, E to use, R to respawn.
    /// Gamepad: left stick, south button (A / Cross) to attack, west button (X / Square) to use, Start to respawn.
    /// With <see cref="ClickToMove"/> on, a click on the ground walks there (around obstacles on the scene's NavMesh,
    /// see <see cref="GroundPath"/>) and a click on an enemy walks up to it and attacks it; holding the button keeps
    /// walking towards the pointer. The keys and the stick still move and take over from a click.
    /// Speed, reach and attack rate come from the player's <see cref="CharacterDefinition"/>.
    /// </summary>
    public class PlayerController : ActorView
    {
        private const float RepickSeconds = 0.1f;
        private const float RepathSeconds = 0.25f;
        private const float StuckSeconds = 0.5f;

        [SerializeField, Min(0f)] private float _turnSpeed = 900f;

        [Tooltip("Click the ground to walk there, an enemy to walk up to it and attack it")]
        [SerializeField] private bool _clickToMove;
        [Tooltip("The mouse button that moves. When it is the left one, the left button no longer attacks the nearest enemy")]
        [SerializeField] private ClickButton _moveButton = ClickButton.Left;
        [Tooltip("What a click can land on: the ground and the characters")]
        [SerializeField] private LayerMask _clickLayers = ~0;
        [Tooltip("An enemy this near the clicked point counts as clicked (metres), for enemies without a collider")]
        [SerializeField, Min(0f)] private float _pickRadius = 0.8f;
        [Tooltip("How near a point the walk ends (metres)")]
        [SerializeField, Min(0.01f)] private float _arriveDistance = 0.2f;

        private readonly List<Vector3> _corners = new List<Vector3>();
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private CharacterController _body;
        private StrikeAnimator _strike;
        private InputAction _move;
        private InputAction _attack;
        private InputAction _attackMouse;
        private InputAction _respawn;
        private InputAction _use;
        private InputAction _clickLeft;
        private InputAction _clickRight;
        private float _cooldown;
        private float _fallSpeed;
        private int _corner;
        private int _chased = -1;
        private Vector3 _chasedAt;
        private float _repathIn;
        private float _repickIn;
        private bool _holdingGround;
        private Vector3 _lastPosition;
        private float _stuckFor;

        /// <summary>
        /// A click on the ground walks there, on an enemy walks up to it and attacks it.
        /// </summary>
        public bool ClickToMove
        {
            get => _clickToMove;
            set
            {
                _clickToMove = value;
                if (!value)
                    Stop();
            }
        }

        /// <summary>
        /// The mouse button that moves when <see cref="ClickToMove"/> is on.
        /// </summary>
        public ClickButton MoveButton
        {
            get => _moveButton;
            set => _moveButton = value;
        }

        /// <summary>
        /// Layers a click can land on (the ground and the characters).
        /// </summary>
        public LayerMask ClickLayers
        {
            get => _clickLayers;
            set => _clickLayers = value;
        }

        /// <summary>
        /// The player is walking to a point or up to an enemy (<see cref="MoveTo"/>, <see cref="MoveToAttack"/>, a click).
        /// </summary>
        public bool HasDestination => _corner < _corners.Count || _chased >= 0;

        /// <summary>
        /// Where the walk ends: the last point of the path, or the chased enemy. Meaningful while <see cref="HasDestination"/>.
        /// </summary>
        public Vector3 Destination => _chased >= 0 ? _chasedAt : _corners.Count > 0 ? _corners[_corners.Count - 1] : transform.position;

        /// <summary>
        /// The enemy the player walks up to and attacks, or -1.
        /// </summary>
        public int ChasedTarget => _chased;

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
            _attack.AddBinding("<Gamepad>/buttonSouth");

            _attackMouse = new InputAction("Attack Mouse", InputActionType.Button);
            _attackMouse.AddBinding("<Mouse>/leftButton");

            _clickLeft = new InputAction("Click Left", InputActionType.Button);
            _clickLeft.AddBinding("<Mouse>/leftButton");

            _clickRight = new InputAction("Click Right", InputActionType.Button);
            _clickRight.AddBinding("<Mouse>/rightButton");

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
            _attackMouse.Enable();
            _clickLeft.Enable();
            _clickRight.Enable();
            _respawn.Enable();
            _use.Enable();
        }

        protected virtual void OnDisable()
        {
            _move.Disable();
            _attack.Disable();
            _attackMouse.Disable();
            _clickLeft.Disable();
            _clickRight.Disable();
            _respawn.Disable();
            _use.Disable();
            Stop();
        }

        protected virtual void OnDestroy()
        {
            _move.Dispose();
            _attack.Dispose();
            _attackMouse.Dispose();
            _clickLeft.Dispose();
            _clickRight.Dispose();
            _respawn.Dispose();
            _use.Dispose();
        }

        protected override void OnUnbound()
        {
            Stop();
        }

        protected virtual void Update()
        {
            var player = Actor;
            // A paused game: the player stands where they are
            if (player == null || World.IsPaused)
                return;

            if (!player.IsAlive())
            {
                Stop();
                if (_respawn.WasPressedThisFrame())
                    World.RevivePlayer();
                return;
            }

            _cooldown -= Time.deltaTime;
            if (player.IsStunned)
            {
                // A stunned player stands still (but still falls), then goes on where they were walking
                Move(Vector3.zero);
                return;
            }

            if (_clickToMove)
                ReadClick();
            var input = MoveInput();
            if (input.sqrMagnitude > 0.01f)
            {
                // The keys and the stick take over from a click
                Stop();
                Move(ScreenRelative(input));
            }
            else
            {
                Move(Walk());
            }

            bool mouseMoves = _clickToMove && _moveButton == ClickButton.Left;
            if (_attack.IsPressed() || (!mouseMoves && _attackMouse.IsPressed()))
                TryAttackNearest();
            if (_use.WasPressedThisFrame())
                World.Interact();
        }

        /// <summary>
        /// The keyboard or stick direction this frame, screen-relative ("up" walks away from the camera), each axis -1..1.
        /// Override to feed the player a direction of the game's own.
        /// </summary>
        protected virtual Vector2 MoveInput() => _move.ReadValue<Vector2>();

        /// <summary>
        /// Walks to a point, around obstacles on the scene's NavMesh (straight when the scene has none). A point the
        /// NavMesh cannot reach is walked to as near as it goes.
        /// </summary>
        /// <returns>False when there is no way there; the player then stops.</returns>
        public bool MoveTo(Vector3 point)
        {
            _chased = -1;
            _corner = 0;
            _stuckFor = 0f;
            _lastPosition = transform.position;
            if (GroundPath.Find(transform.position, point, _corners))
                return true;
            Stop();
            return false;
        }

        /// <summary>
        /// Walks up to an enemy, following it, and attacks it once in reach (again whenever the attack is ready, until
        /// it falls or the player moves elsewhere).
        /// </summary>
        /// <returns>False when it is not a living enemy of this player or has no view in the scene.</returns>
        public bool MoveToAttack(int target)
        {
            if (!IsBound || !IsLivingEnemy(target) || !TryGetPosition(target, out var position))
                return false;
            _chased = target;
            _chasedAt = position;
            _corners.Clear();
            _corner = 0;
            _repathIn = 0f;
            _stuckFor = 0f;
            _lastPosition = transform.position;
            _holdingGround = false;
            return true;
        }

        /// <summary>
        /// Ends a walk to a point or an enemy.
        /// </summary>
        public void Stop()
        {
            _corners.Clear();
            _corner = 0;
            _chased = -1;
            _holdingGround = false;
            _stuckFor = 0f;
        }

        /// <summary>
        /// Strikes the nearest living enemy in reach, if the attack is ready.
        /// </summary>
        /// <returns>True when it struck at an enemy (in co-op as a client, the host then deals the damage).</returns>
        public bool TryAttackNearest()
        {
            if (!IsBound || _cooldown > 0f || !TryFindTarget(out int target, out Vector3 targetPosition))
                return false;
            Strike(target, targetPosition);
            return true;
        }

        /// <summary>
        /// What the pointer is over: the ground point, and the living enemy there (-1 for none). The default casts a ray
        /// from the main camera against <see cref="ClickLayers"/> and takes an enemy whose collider was hit, or else
        /// the one nearest the hit point within the pick radius. Override for a pick of the game's own.
        /// </summary>
        /// <returns>False when the pointer is over nothing.</returns>
        protected virtual bool TryPick(Vector2 screenPosition, out Vector3 point, out int target)
        {
            point = default;
            target = -1;
            var view = Camera.main;
            if (view == null)
                return false;
            int hits = Physics.RaycastNonAlloc(view.ScreenPointToRay(screenPosition), _hits, 1000f, _clickLayers,
                QueryTriggerInteraction.Ignore);
            // The nearest hit that is not the player's own body
            Collider nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hits; i++)
            {
                var hit = _hits[i];
                if (hit.distance >= nearestDistance || hit.collider.transform.IsChildOf(transform))
                    continue;
                nearest = hit.collider;
                nearestDistance = hit.distance;
                point = hit.point;
            }
            if (nearest == null)
                return false;

            if (Views != null && Views.TryGetEntity(nearest.gameObject, out int entity) && IsLivingEnemy(entity))
            {
                target = entity;
                return true;
            }
            float best = _pickRadius * _pickRadius;
            foreach (var actor in World.Actors)
            {
                int other = actor.EntityId;
                if (!IsLivingEnemy(other) || !TryGetPosition(other, out var position))
                    continue;
                float distance = Flat(position - point).sqrMagnitude;
                if (distance > best)
                    continue;
                best = distance;
                target = other;
            }
            return true;
        }

        private void ReadClick()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            var button = _moveButton == ClickButton.Left ? _clickLeft : _clickRight;
            bool pressed = button.WasPressedThisFrame();
            if (!button.IsPressed())
                _holdingGround = false;
            if (!pressed && !_holdingGround)
                return;
            // A click on a screen's button is not a walk
            if (pressed && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            // Held on the ground: follow the pointer, a few times a second
            if (!pressed)
            {
                _repickIn -= Time.deltaTime;
                if (_repickIn > 0f)
                    return;
            }
            _repickIn = RepickSeconds;
            if (!TryPick(mouse.position.ReadValue(), out var point, out int target))
                return;
            if (pressed && target >= 0 && MoveToAttack(target))
                return;
            MoveTo(point);
            _holdingGround = true;
        }

        // The world direction to walk in this frame (shorter than 1 on the last step), zero when standing
        private Vector3 Walk()
        {
            if (_chased >= 0)
            {
                if (!IsLivingEnemy(_chased) || !TryGetPosition(_chased, out var position))
                {
                    Stop();
                    return Vector3.zero;
                }
                _chasedAt = position;
                float reach = Definition.AttackRange * 0.9f;
                if (Flat(position - transform.position).sqrMagnitude <= reach * reach)
                {
                    _corners.Clear();
                    _corner = 0;
                    _stuckFor = 0f;
                    if (_cooldown <= 0f)
                        Strike(_chased, position);
                    Face(position - transform.position, _turnSpeed);
                    return Vector3.zero;
                }
                _repathIn -= Time.deltaTime;
                if (_repathIn <= 0f || _corner >= _corners.Count)
                {
                    _repathIn = RepathSeconds;
                    if (!GroundPath.Find(transform.position, position, _corners))
                    {
                        Stop();
                        return Vector3.zero;
                    }
                    _corner = 0;
                }
            }

            while (_corner < _corners.Count
                   && Flat(_corners[_corner] - transform.position).sqrMagnitude <= _arriveDistance * _arriveDistance)
                _corner++;
            if (_corner >= _corners.Count)
            {
                // Arrived (a chase finds its way again next frame; a held button may send the player on)
                _corners.Clear();
                _corner = 0;
                return Vector3.zero;
            }

            // Pressed against a wall the NavMesh does not know of: give up instead of pushing forever
            float step = Definition.MoveSpeed * (Actor?.MoveMultiplier ?? 1f) * Time.deltaTime;
            float moved = Flat(transform.position - _lastPosition).magnitude;
            _lastPosition = transform.position;
            _stuckFor = step > 0f && moved < step * 0.1f ? _stuckFor + Time.deltaTime : 0f;
            if (_stuckFor > StuckSeconds)
            {
                Stop();
                return Vector3.zero;
            }

            var toCorner = Flat(_corners[_corner] - transform.position);
            float distance = toCorner.magnitude;
            // The last step ends on the point instead of past it
            bool last = _corner == _corners.Count - 1;
            return toCorner / distance * (last && step > distance ? distance / step : 1f);
        }

        private bool IsLivingEnemy(int entity)
        {
            var actor = World.Get(entity);
            return actor != null && actor.IsAlive() && World.AreHostile(EntityId, entity);
        }

        private void Strike(int target, Vector3 targetPosition)
        {
            _cooldown = Definition.AttackCooldown;
            var toTarget = Flat(targetPosition - transform.position);
            if (toTarget.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toTarget);
            if (_strike != null)
                _strike.Play();
            World.Attack(EntityId, target);
        }

        private static Vector3 ScreenRelative(Vector2 input)
        {
            var direction = new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            // Screen-relative: "up" walks away from the camera
            var view = Camera.main;
            if (view != null)
                direction = Quaternion.Euler(0f, view.transform.eulerAngles.y, 0f) * direction;
            return direction;
        }

        private void Move(Vector3 direction)
        {
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

    /// <summary>
    /// The mouse button that walks with click-to-move (<see cref="PlayerController.ClickToMove"/>).
    /// </summary>
    public enum ClickButton
    {
        Left,
        Right
    }
}
