using UnityEngine;

namespace ReferenceGame
{
    /// <summary>
    /// A goblin runs at the hero, keeps a little distance from other goblins and strikes when in reach.
    /// </summary>
    public sealed class GoblinBrain : CombatantView
    {
        [SerializeField, Min(0f)] private float _moveSpeed = 3.2f;
        [SerializeField, Min(0f)] private float _turnSpeed = 540f;
        [SerializeField, Min(0f)] private float _attackRange = 1.6f;
        [SerializeField, Min(0f)] private float _attackCooldown = 1.2f;

        [Tooltip("Goblins closer than this push each other apart")]
        [SerializeField, Min(0f)] private float _personalSpace = 1.1f;

        private StrikeAnimator _strike;
        private float _cooldown;

        private void Awake() => _strike = GetComponent<StrikeAnimator>();

        protected override void OnBound()
        {
            // Out of step with each other, so a pack does not strike as one
            _cooldown = Random.Range(0.3f, 1f) * _attackCooldown;
        }

        private void Update()
        {
            var self = Combatant;
            var hero = Game?.Player;
            if (self == null || !self.IsAlive() || hero == null || !hero.IsAlive()
                || !TryGetPosition(Game.PlayerId, out var heroPosition))
                return;

            var toHero = heroPosition - transform.position;
            toHero.y = 0f;
            float distance = toHero.magnitude;
            Face(toHero, _turnSpeed);

            var step = Separation();
            if (distance > _attackRange * 0.9f)
                step += toHero / Mathf.Max(distance, 0.001f);
            transform.position += Vector3.ClampMagnitude(step, 1f) * (_moveSpeed * Time.deltaTime);

            _cooldown -= Time.deltaTime;
            if (distance <= _attackRange && _cooldown <= 0f)
            {
                _cooldown = _attackCooldown;
                if (_strike != null)
                    _strike.Play();
                Game.Attack(EntityId, Game.PlayerId);
            }
        }

        private Vector3 Separation()
        {
            var push = Vector3.zero;
            foreach (int other in Game.LivingGoblins)
            {
                if (other == EntityId || !TryGetPosition(other, out var position))
                    continue;
                var away = transform.position - position;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance < _personalSpace && distance > 0.001f)
                    push += away / distance * (1f - distance / _personalSpace);
            }
            return push;
        }
    }
}
