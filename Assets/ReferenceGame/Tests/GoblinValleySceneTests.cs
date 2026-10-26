using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReferenceGame.Tests
{
    public sealed class GoblinValleySceneTests
    {
        private const string ScenePath = "Assets/ReferenceGame/Scenes/GoblinValley.unity";

        [Test]
        public void Scene_HasExactlyOneHostWithItsScript()
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

                // Wired by GoblinValleyBuilder: rules and prefabs, and a camera to follow the hero
                var host = new UnityEditor.SerializedObject(hosts[0]);
                var config = host.FindProperty("_config").objectReferenceValue as GoblinValleyConfig;
                Assert.That(config, Is.Not.Null);
                Assert.That(config.Prefabs, Is.Not.Null);
                var keys = config.Prefabs.Entries.Where(e => e.Prefab != null).Select(e => e.Key);
                Assert.That(keys, Is.SupersetOf(new[] { GoblinValleyContent.PlayerKind, GoblinValleyContent.GoblinKind }));
                Assert.That(host.FindProperty("_camera").objectReferenceValue, Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
