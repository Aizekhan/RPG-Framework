using UnityEngine;

namespace RPGFramework.UINavigation.Unity
{
    /// <summary>
    /// How <see cref="PrefabViewFactory"/> creates a view: from a direct prefab reference, or from an asset key loaded
    /// through the asset manager (Resources, Addressables, ... depending on the registered providers).
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/UI/View Config", fileName = "ViewConfig")]
    public sealed class ViewConfig : ScriptableObject
    {
        [Tooltip("Id used with INavigationService (e.g. \"MainMenu\")")]
        [SerializeField] private string _viewId;

        [Tooltip("Prefab with a NavigableView component. Takes precedence over Asset Key")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Key loaded through IAssetManager when no prefab is assigned")]
        [SerializeField] private string _assetKey;

        [Tooltip("Keep the instance when closed and reuse it next time instead of destroying it")]
        [SerializeField] private bool _keepInstance = true;

        [Tooltip("Parent layer registered on the factory (e.g. \"Screens\", \"Modals\"); empty = default parent")]
        [SerializeField] private string _layer;

        public string ViewId => _viewId;
        public GameObject Prefab => _prefab;
        public string AssetKey => _assetKey;
        public bool KeepInstance => _keepInstance;
        public string Layer => _layer;

        public static ViewConfig Create(string viewId, GameObject prefab = null, string assetKey = null, bool keepInstance = true,
            string layer = null)
        {
            var config = CreateInstance<ViewConfig>();
            config.name = viewId;
            config._viewId = viewId;
            config._prefab = prefab;
            config._assetKey = assetKey;
            config._keepInstance = keepInstance;
            config._layer = layer;
            return config;
        }
    }
}
