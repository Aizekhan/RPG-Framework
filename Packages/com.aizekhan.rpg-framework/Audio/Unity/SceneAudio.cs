using UnityEngine;

namespace RPGFramework.Audio.Unity
{
    /// <summary>
    /// Sound for a scene without a game host, such as a main menu: an <see cref="IAudioService"/> over the cues and
    /// music of an <see cref="AudioLibrary"/>, at the volumes the player kept (PlayerPrefs, the same keys as
    /// <see cref="AudioModule"/>), ticked in real time. It starts <see cref="Music"/> when it starts and stops its sounds
    /// when it is destroyed (its sources live under it, so they go with the scene).
    /// </summary>
    [AddComponentMenu("RPG Framework/Scene Audio")]
    public sealed class SceneAudio : MonoBehaviour
    {
        [Tooltip("The cues and music this scene plays")]
        [SerializeField] private AudioLibrary _library;

        [Tooltip("The music track played on start (empty: none)")]
        [SerializeField] private string _music = "";

        [Tooltip("Seconds the music fades in")]
        [SerializeField, Min(0f)] private float _musicFade = 1f;

        private AudioService _service;
        private UnityAudioOutput _output;
        private IVolumeStore _volumes;
        private Transform _root;

        /// <summary>
        /// The library; setting it from code (after AddComponent) builds the service again.
        /// </summary>
        public AudioLibrary Library
        {
            get => _library;
            set
            {
                _library = value;
                Release();
            }
        }

        /// <summary>
        /// The music track played when the service is built.
        /// </summary>
        public string Music
        {
            get => _music;
            set => _music = value ?? "";
        }

        public float MusicFade
        {
            get => _musicFade;
            set => _musicFade = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Where volumes are kept (null: PlayerPrefs); set it before the service is built.
        /// </summary>
        public IVolumeStore Volumes
        {
            get => _volumes;
            set
            {
                _volumes = value;
                Release();
            }
        }

        /// <summary>
        /// The scene's audio, built on first use (null without a library). Building it starts <see cref="Music"/>.
        /// </summary>
        public IAudioService Audio => _service ?? Build();

        private void Start() => Build();

        private void Update() => _service?.Update(Time.unscaledDeltaTime);

        private void OnDestroy() => Release();

        private IAudioService Build()
        {
            if (_service != null || _library == null)
                return _service;
            _root = new GameObject("Audio").transform;
            _root.SetParent(transform, false);
            _output = new UnityAudioOutput(_library, _root);
            _service = new AudioService(_output, null, _volumes ?? new PlayerPrefsVolumeStore());
            AudioModule.RegisterCues(_service, _library);
            if (!string.IsNullOrEmpty(_music))
                _service.PlayMusic(_music, _musicFade);
            return _service;
        }

        private void Release()
        {
            _service?.Dispose();
            _output?.Dispose();
            _service = null;
            _output = null;
            if (_root != null)
            {
                if (Application.isPlaying)
                    Destroy(_root.gameObject);
                else
                    DestroyImmediate(_root.gameObject);
            }
            _root = null;
        }
    }
}
