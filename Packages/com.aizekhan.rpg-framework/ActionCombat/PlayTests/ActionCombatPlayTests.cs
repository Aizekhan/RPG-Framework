using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Combat;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPGFramework.ActionCombat.PlayTests
{
    public class ActionCombatPlayTests
    {
        private const int Hero = 1;
        private const int Monster = 2;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private CombatArena _arena;
        private RecordingDamage _damage;
        private Combatant _hero;

        [SetUp]
        public void SetUp()
        {
            _arena = Make("arena").AddComponent<CombatArena>();
            _damage = new RecordingDamage();
            _arena.UseDamage(_damage);

            _hero = Make("hero").AddComponent<Combatant>();
            _hero.UseEntityId(Hero);

            var monster = Make("monster");
            monster.transform.position = new Vector3(0f, 0f, 1f);
            monster.AddComponent<CapsuleCollider>();
            monster.AddComponent<Hurtbox>();
            monster.AddComponent<Combatant>().UseEntityId(Monster);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _objects)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }
            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator AWeaponHitbox_LandsOnceInTheActiveWindow()
        {
            var sword = Make("sword");
            sword.transform.SetParent(_hero.transform, false);
            sword.transform.localPosition = new Vector3(0f, 0f, 1f);
            sword.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.3f, 1f);
            sword.AddComponent<Hitbox>();

            // The blade already rests in the monster: nothing lands before the window
            for (int i = 0; i < 3; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(_damage.Hits, Is.Empty);

            _arena.Rules.Attack(Hero, new AttackDefinition("slash", 0.05f, 0.5f, 0.1f));
            float until = Time.time + 1f;
            while (_damage.Hits.Count == 0 && Time.time < until)
                yield return new WaitForFixedUpdate();
            for (int i = 0; i < 5; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(_damage.Hits, Is.EqualTo(new[] { Monster }), "once per target per swing");
            Assert.That(_arena.Rules.StateOf(Monster), Is.EqualTo(CombatState.HitReact));
        }

        [UnityTest]
        public IEnumerator AProjectile_FliesIntoABody_AndEndsItsSwing()
        {
            _arena.Rules.Attack(Hero, new AttackDefinition("arrow", 0f, 0.05f, 0f) { Projectile = true });
            int swing = _arena.Rules.SwingOf(Hero);
            var arrow = Make("arrow").AddComponent<CombatProjectile>();
            arrow.transform.position = new Vector3(0f, 0f, -3f);
            yield return new WaitForFixedUpdate();

            arrow.Launch(_hero, swing, Vector3.forward, 40f, 2f);
            // One long step: the sweep still meets the body it passes
            arrow.Step(0.25f);

            Assert.That(_damage.Hits, Is.EqualTo(new[] { Monster }));
            Assert.That(arrow.IsFlying, Is.False);
            Assert.That(_arena.Rules.IsSwingLive(swing), Is.False);
        }

        [UnityTest]
        public IEnumerator TwoEnemies_RunUpToADummy_AndStrikeIt()
        {
            _arena.AddEnemyTarget(Hero);
            var dummyBody = _hero.gameObject;
            dummyBody.AddComponent<CapsuleCollider>();
            dummyBody.AddComponent<Hurtbox>();

            var places = new[] { new Vector3(4f, 0f, 0f), new Vector3(-4f, 0f, 0f) };
            for (int i = 0; i < places.Length; i++)
            {
                var enemy = Make("enemy " + i);
                enemy.transform.position = places[i];
                var brain = enemy.AddComponent<CombatEnemyAI>();
                var behaviour = new EnemyBehaviour { NoticeSeconds = 0f, ViewAngle = 360f, RecoveryPause = 0.2f };
                behaviour.Moves.Add(new EnemyMove(new AttackDefinition("slash", 0.15f, 0.25f, 0.2f), 0f, 1.6f));
                brain.UseBehaviour(behaviour);
                enemy.GetComponent<Combatant>().UseEntityId(20 + i);

                var sword = Make("sword " + i);
                sword.transform.SetParent(enemy.transform, false);
                sword.transform.localPosition = new Vector3(0f, 0f, 0.9f);
                sword.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.3f, 1.2f);
                sword.AddComponent<Hitbox>();
            }

            float until = Time.time + 8f;
            while (Time.time < until && !(_damage.Attackers.Contains(20) && _damage.Attackers.Contains(21)))
                yield return null;

            Assert.That(_damage.Attackers, Has.Member(20).And.Member(21), "both ran up and struck");
            Assert.That(_damage.Hits, Has.Member(Hero));
            Assert.That(_arena.Timeline.Entries, Has.Some.Matches<TimelineEntry>(e => e.Kind == TimelineKind.Decision));
        }

        private GameObject Make(string name)
        {
            var created = new GameObject(name);
            _objects.Add(created);
            return created;
        }

        private sealed class RecordingDamage : ICombatDamage
        {
            public readonly List<int> Hits = new List<int>();
            public readonly List<int> Attackers = new List<int>();

            public bool CanHit(int attackerId, int targetId) => true;

            public float Deal(int attackerId, int targetId, AttackContext context)
            {
                Hits.Add(targetId);
                Attackers.Add(attackerId);
                return 5f;
            }
        }
    }
}
