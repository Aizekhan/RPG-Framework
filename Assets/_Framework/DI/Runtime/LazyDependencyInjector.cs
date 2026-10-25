using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGFramework.DI
{
    /// <summary>
    /// Dependency injector for LazyMonoBehaviour components.
    /// Automatically scans scenes and injects dependencies into components with [Inject] attributes.
    /// Survives scene loading (DontDestroyOnLoad).
    /// </summary>
    public class LazyDependencyInjector : MonoBehaviour, IDependencyInjector
    {
        [SerializeField]
        [Tooltip("Automatically scan and inject when new scenes are loaded")]
        private bool scanOnSceneLoad = true;

        [SerializeField]
        [Tooltip("Scan and inject in Start()")]
        private bool scanOnStart = true;

        [SerializeField]
        [Tooltip("Log injection activity to console")]
        private bool logInjections = true;

        private IDIContainer container;
        private bool isInitialized = false;

        /// <summary>
        /// Initializes the injector with a DI container.
        /// </summary>
        public void Initialize(IDIContainer container)
        {
            this.container = container;
            isInitialized = true;

            if (logInjections)
                Debug.Log("[DI] LazyDependencyInjector initialized");

            // Initial scan if configured
            if (scanOnStart)
                ScanAndInjectInScene();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (scanOnSceneLoad)
                SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (isInitialized)
                ScanAndInjectInScene();
        }

        private void OnDestroy()
        {
            if (scanOnSceneLoad)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Scans the scene and injects dependencies into LazyMonoBehaviour components.
        /// </summary>
        public void ScanAndInjectInScene()
        {
            if (!isInitialized)
            {
                Debug.LogError("[DI] LazyDependencyInjector not initialized. Call Initialize() before ScanAndInjectInScene().");
                return;
            }

            var monoBehaviours = FindObjectsByType<LazyMonoBehaviour>(FindObjectsSortMode.None);
            int injectedCount = 0;

            foreach (var component in monoBehaviours)
            {
                if (!component.AreDependenciesInjected)
                {
                    component.InjectWith(container);
                    injectedCount++;
                }
            }

            if (logInjections && injectedCount > 0)
                Debug.Log($"[DI] Injected dependencies into {injectedCount} LazyMonoBehaviour components");
        }

        /// <summary>
        /// Injects dependencies into a specific object instance.
        /// </summary>
        public void InjectDependencies(object instance)
        {
            if (!isInitialized)
            {
                Debug.LogError("[DI] LazyDependencyInjector not initialized. Call Initialize() before InjectDependencies().");
                return;
            }

            container.InjectDependencies(instance);
        }

        /// <summary>
        /// Injects dependencies into all MonoBehaviour components in the scene.
        /// Alias for ScanAndInjectInScene().
        /// </summary>
        public void InjectDependenciesInScene()
        {
            ScanAndInjectInScene();
        }
    }
}
