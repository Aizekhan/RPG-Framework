using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace __GAME__.Tests
{
    public sealed class __GAME__SceneTests
    {
        private const string ScenePath = "Assets/__GAME__/Scenes/__GAME__.unity";

        [Test]
        public void Scene_HasOneHostWithAGoodSetupAndNoMissingScripts()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToList();
                var hosts = components.OfType<__GAME__Host>().ToList();
                Assert.That(hosts.Count, Is.EqualTo(1));
                Assert.That(hosts[0].Setup, Is.Not.Null, "drag the Game Setup asset into the host's Setup field");
                Assert.That(hosts[0].Setup.Validate(), Is.Empty);
                Assert.That(components.Count(component => component == null), Is.EqualTo(0), "missing scripts");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
