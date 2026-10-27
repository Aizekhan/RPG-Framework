using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Bootstrap.UnityServices;
using RPGFramework.Scenes;
using UnityEngine.SceneManagement;

namespace RPGFramework.ActionRpg.PlayTests
{
    /// <summary>
    /// A host whose scenes are made in code (tests have no scenes in the build settings): loading one creates it,
    /// lets the test fill it, makes it active and unloads the scenes before it, as a real scene change does.
    /// </summary>
    public sealed class SceneChangingHost : ActionRpgHost
    {
        /// <summary>
        /// Fills a scene as it loads (the level, and maybe a host of its own).
        /// </summary>
        public Action<string> Populate { get; set; }

        public List<string> ScenesChanged { get; } = new List<string>();

        protected override void ConfigureUnityServices(UnityServicesOptions options) =>
            options.SceneBackend = () => new CreatedScenes(this);

        protected override void OnSceneChanged(string sceneName) => ScenesChanged.Add(sceneName);

        private sealed class CreatedScenes : ISceneBackend
        {
            private readonly SceneChangingHost _host;

            public CreatedScenes(SceneChangingHost host) => _host = host;

            public string ActiveScene => SceneManager.GetActiveScene().name;

            public bool IsLoaded(string sceneName) => SceneManager.GetSceneByName(sceneName).isLoaded;

            public async UniTask LoadAsync(string sceneName, bool additive, IProgress<float> progress, CancellationToken cancellationToken)
            {
                var old = SceneManager.GetActiveScene();
                var scene = SceneManager.CreateScene(sceneName);
                SceneManager.SetActiveScene(scene);
                _host.Populate?.Invoke(sceneName);
                if (!additive)
                    await SceneManager.UnloadSceneAsync(old).ToUniTask(cancellationToken: cancellationToken);
            }

            public async UniTask UnloadAsync(string sceneName, CancellationToken cancellationToken) =>
                await SceneManager.UnloadSceneAsync(sceneName).ToUniTask(cancellationToken: cancellationToken);

            public void SetActive(string sceneName) => SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        }
    }
}
