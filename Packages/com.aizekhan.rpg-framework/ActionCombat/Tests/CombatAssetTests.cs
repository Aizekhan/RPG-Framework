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
