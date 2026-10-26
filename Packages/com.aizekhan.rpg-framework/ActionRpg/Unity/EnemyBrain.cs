using UnityEngine;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// An enemy runs at the player, keeps a little distance from other enemies and strikes when in reach.
    /// Speed, reach and attack rate come from its <see cref="CharacterDefinition"/>.
    /// </summary>
    public class EnemyBrain : ActorView
    {
        [SerializeField, Min(0f)] private float _turnSpeed = 540f;

        [Tooltip("Enemies closer than this push each other apart")]
        [SerializeField, Min(0f)] private float _personalSpace = 1.1f;

        private StrikeAnimator _strike;
        private float _cooldown;

        protected virtual void Awake() => _strike = GetComponent<StrikeAnimator>();

        protected override void OnBound()
        {
            // Out of step with each other, so a pack does not strike as one
            _cooldown = Random.Range(0.3f, 1f) * Definition.AttackCooldown;
        }

        protected virtual void Update()
        {
            var self = Actor;
            var player = World?.Player;
            if (self == null || !self.IsAlive() || player == null || !player.IsAlive()
                || !TryGetPosition(World.PlayerId, out var playerPosition))
                return;

            var toPlayer = Flat(playerPosition - transform.position);
            float distance = toPlayer.magnitude;
            Face(toPlayer, _turnSpeed);

            var step = Separation();
            if (distance > Definition.AttackRange * 0.9f)
                step += toPlayer / Mathf.Max(distance, 0.001f);
            transform.position += Vector3.ClampMagnitude(step, 1f) * (Definition.MoveSpeed * Time.deltaTime);

            _cooldown -= Time.deltaTime;
            if (distance <= Definition.AttackRange && _cooldown <= 0f)
            {
                _cooldown = Definition.AttackCooldown;
                if (_strike != null)
                    _strike.Play();
                World.Attack(EntityId, World.PlayerId);
            }
        }

        private Vector3 Separation()
        {
            var push = Vector3.zero;
            foreach (int other in World.LivingEnemies)
            {
                if (other == EntityId || !TryGetPosition(other, out var position))
                    continue;
                var away = Flat(transform.position - position);
                float distance = away.magnitude;
                if (distance < _personalSpace && distance > 0.001f)
                    push += away / distance * (1f - distance / _personalSpace);
            }
            return push;
        }
    }
}
