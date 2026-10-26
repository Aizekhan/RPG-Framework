using System.Linq;
using NUnit.Framework;
using RPGFramework.ActionRpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReferenceGame.Tests
{
    public sealed class GoblinValleySceneTests
    {
        private const string ScenePath = "Assets/ReferenceGame/Scenes/GoblinValley.unity";

        [Test]
        public void Scene_HasOneHostWiredToTheSetupAndACamera()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var hosts = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GoblinValleyHost>(true))
                    .ToList();
                var missingScripts = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Count(component => component == null);

                Assert.That(hosts, Has.Count.EqualTo(1));
                Assert.That(missingScripts, Is.EqualTo(0));

                // Wired by GoblinValleyBuilder
                Assert.That(hosts[0].Setup, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameSetup>(GoblinValleyTests.SetupPath)));
                var host = new SerializedObject(hosts[0]);
                Assert.That(host.FindProperty("_camera").objectReferenceValue, Is.Not.Null);
                Assert.That(host.FindProperty("_saveProgress").boolValue, Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
