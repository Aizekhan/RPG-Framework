using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RPGFramework.ActionCombat.Tests
{
    public class CombatAssetTests
    {
        private AnimationClip _clip;
        private AttackAsset _attack;
        private GameObject _object;

        [TearDown]
        public void TearDown()
        {
            if (_clip != null)
                Object.DestroyImmediate(_clip);
            if (_attack != null)
                Object.DestroyImmediate(_attack);
            if (_object != null)
                Object.DestroyImmediate(_object);
        }

        [Test]
        public void PhasesFromAClip_SplitItsLengthAtTheMarks()
        {
            var (windUp, active, recovery) = AttackAsset.PhasesFromClip(1.2f, 2f, 0.25f, 0.5f);

            Assert.That(windUp, Is.EqualTo(0.15f).Within(1e-5f));
            Assert.That(active, Is.EqualTo(0.15f).Within(1e-5f));
            Assert.That(recovery, Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void AnAttackWithAClip_TakesItsPhasesFromTheClipsEvents()
        {
            _clip = ClipOf(1f, (0.4f, AttackAsset.ActiveStartEvent), (0.6f, AttackAsset.ActiveEndEvent));
            _attack = ScriptableObject.CreateInstance<AttackAsset>();
            _attack.name = "slash";
            var serialized = new SerializedObject(_attack);
            serialized.FindProperty("_clip").objectReferenceValue = _clip;
            serialized.FindProperty("_maxCharge").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var definition = _attack.Definition;

            Assert.That(definition.Id, Is.EqualTo("slash"));
            Assert.That(definition.WindUp, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(definition.Active, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(definition.Recovery, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(definition.CanCharge, Is.True);
        }

        [Test]
        public void ADodgeClip_GivesTheDodgesTimeAndInvulnerability()
        {
            _clip = ClipOf(0.8f, (0.1f, Combatant.InvulnerableStartEvent), (0.5f, Combatant.InvulnerableEndEvent));
            _object = new GameObject("hero");
            var combatant = _object.AddComponent<Combatant>();
            var serialized = new SerializedObject(combatant);
            serialized.FindProperty("_dodgeClip").objectReferenceValue = _clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var settings = combatant.BuildSettings();

            Assert.That(settings.DodgeSeconds, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(settings.InvulnerableFrom, Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(settings.InvulnerableUntil, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void AMoveAddedThroughSerializedProperty_IsReportedAsAProblem()
        {
            var behaviour = ScriptableObject.CreateInstance<EnemyBehaviourAsset>();
            try
            {
                var serialized = new SerializedObject(behaviour);
                var moves = serialized.FindProperty("_moves");
                moves.arraySize++;
                var move = moves.GetArrayElementAtIndex(0);
                move.FindPropertyRelative("Weight").floatValue = 0f;
                move.FindPropertyRelative("Next").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var problems = behaviour.Problems();

                Assert.That(problems, Has.Some.Contains("has no attack"));
                Assert.That(problems, Has.Some.Contains("to itself"));
                Assert.That(problems, Has.Some.Contains("No move can be chosen"));
            }
            finally
            {
                Object.DestroyImmediate(behaviour);
            }
        }

        // FG-31: moves come from the weapon (clip, motion, sweep, commit, hold), the monster adds its own
        [Test]
        public void AMonster_TakesItsWeaponsMoves_WithTheirMotionSweepCommitAndHold()
        {
            _attack = ScriptableObject.CreateInstance<AttackAsset>();
            _attack.name = "dagger stab";
            var attack = new SerializedObject(_attack);
            attack.FindProperty("_holdPoint").floatValue = 0.6f;
            var motions = attack.FindProperty("_motions");
            motions.arraySize = 1;
            motions.GetArrayElementAtIndex(0).FindPropertyRelative("Distance").floatValue = 0.8f;
            var sweep = attack.FindProperty("_sweep");
            sweep.arraySize = 2;
            var segment = sweep.GetArrayElementAtIndex(0);
            segment.FindPropertyRelative("ArcFrom").floatValue = -15f;
            segment.FindPropertyRelative("ArcTo").floatValue = 15f;
            segment.FindPropertyRelative("Radius").floatValue = 1.2f;
            attack.ApplyModifiedPropertiesWithoutUndo();

            var dagger = ScriptableObject.CreateInstance<WeaponMoveSet>();
            var goblin = ScriptableObject.CreateInstance<EnemyBehaviourAsset>();
            try
            {
                var set = new SerializedObject(dagger);
                set.FindProperty("_weaponKind").stringValue = "dagger";
                var moves = set.FindProperty("_moves");
                moves.arraySize = 1;
                var move = moves.GetArrayElementAtIndex(0);
                move.FindPropertyRelative("Attack").objectReferenceValue = _attack;
                move.FindPropertyRelative("MaxRange").floatValue = 1.4f;
                move.FindPropertyRelative("Weight").floatValue = 1f;
                move.FindPropertyRelative("Next").intValue = -1;
                move.FindPropertyRelative("Opener").boolValue = true;
                move.FindPropertyRelative("CommitShare").floatValue = 0.75f;
                move.FindPropertyRelative("HoldUntilInRange").boolValue = true;
                move.FindPropertyRelative("MaxHold").floatValue = 1f;
                set.ApplyModifiedPropertiesWithoutUndo();
                goblin.Weapon = dagger;

                var built = goblin.Build().Moves.Single();

                Assert.That(dagger.WeaponKind, Is.EqualTo("dagger"));
                Assert.That(built.Attack.Id, Is.EqualTo("dagger stab"));
                Assert.That(built.CommitRange, Is.EqualTo(1.05f).Within(1e-4f));
                Assert.That(built.HoldUntilInRange, Is.True);
                Assert.That(built.Attack.HoldPoint, Is.EqualTo(0.6f).Within(1e-4f));
                Assert.That(built.Attack.Motions.Single().Distance, Is.EqualTo(0.8f).Within(1e-4f));
                Assert.That(built.Attack.Sweep.Count, Is.EqualTo(1), "the segment without a radius is left out");
                Assert.That(_attack.Problems(), Has.Some.Contains("Sweep segment 1"));
                Assert.That(goblin.Problems(), Is.Empty, "the weapon's opener counts");
            }
            finally
            {
                Object.DestroyImmediate(dagger);
                Object.DestroyImmediate(goblin);
            }
        }

        private static AnimationClip ClipOf(float length, params (float Time, string Name)[] events)
        {
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, length, 1f));
            var marks = new AnimationEvent[events.Length];
            for (int i = 0; i < events.Length; i++)
                marks[i] = new AnimationEvent { time = events[i].Time, functionName = events[i].Name };
            AnimationUtility.SetAnimationEvents(clip, marks);
            return clip;
        }
    }
}
