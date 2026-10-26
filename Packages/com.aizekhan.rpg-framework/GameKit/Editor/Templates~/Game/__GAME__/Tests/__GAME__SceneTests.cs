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
        public void Scene_HasOneHostAndNoMissingScripts()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToList();
                Assert.That(components.OfType<__GAME__Host>().Count(), Is.EqualTo(1));
                Assert.That(components.Count(component => component == null), Is.EqualTo(0), "missing scripts");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
