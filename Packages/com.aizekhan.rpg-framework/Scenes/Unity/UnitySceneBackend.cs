using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace RPGFramework.Scenes.Unity
{
    /// <summary>
    /// <see cref="ISceneBackend"/> over Unity's SceneManager (scenes must be in the build settings).
    /// </summary>
    public sealed class UnitySceneBackend : ISceneBackend
    {
        public string ActiveScene => SceneManager.GetActiveScene().name;

        public bool IsLoaded(string sceneName) => SceneManager.GetSceneByName(sceneName).isLoaded;

        public async UniTask LoadAsync(string sceneName, bool additive, IProgress<float> progress, CancellationToken cancellationToken)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, additive ? LoadSceneMode.Additive : LoadSceneMode.Single);
            if (operation == null)
                throw new InvalidOperationException($"Scene '{sceneName}' could not be loaded (is it in the build settings?)");

            await operation.ToUniTask(progress, cancellationToken: cancellationToken);
        }

        public async UniTask UnloadAsync(string sceneName, CancellationToken cancellationToken)
        {
            var operation = SceneManager.UnloadSceneAsync(sceneName);
            if (operation == null)
                throw new InvalidOperationException($"Scene '{sceneName}' is not loaded");

            await operation.ToUniTask(cancellationToken: cancellationToken);
        }

        public void SetActive(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
                SceneManager.SetActiveScene(scene);
        }
    }
}
