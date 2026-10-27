using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// In co-op, a player's name above their head and a ring at their feet in their party colour, so everyone can
    /// tell who is who. Hidden while playing alone. The presenter adds it to players and sets <see cref="Palette"/>.
    /// </summary>
    public sealed class PlayerTagView : ActorView
    {
        [Tooltip("Colours by party seat (first player, second, ...); set by the host's 'Party Colors'")]
        [SerializeField] private Color[] _palette = DefaultPalette();

        [SerializeField, Min(0.2f)] private float _ringSize = 1.3f;

        private Transform _label;
        private Text _name;
        private Renderer _ring;
        private Material _ringMaterial;
        private bool _shown = true;

        public Color[] Palette
        {
            get => _palette;
            set
            {
                if (value != null && value.Length > 0)
                    _palette = value;
            }
        }

        /// <summary>
        /// Blue, orange, green, purple: the first four seats unless the host sets its own.
        /// </summary>
        public static Color[] DefaultPalette() => new[]
        {
            new Color(0.25f, 0.55f, 1f), new Color(1f, 0.6f, 0.15f), new Color(0.35f, 0.85f, 0.35f), new Color(0.8f, 0.35f, 0.95f)
        };

        /// <summary>
        /// The colour of a party seat: from the palette, then for each seat past it the bright hue least like the
        /// colours before it, so a large party never shares a colour.
        /// </summary>
        public static Color ColorOf(int seat, Color[] palette)
        {
            if (seat < 0)
                return Color.white;
            int listed = palette?.Length ?? 0;
            if (seat < listed)
                return palette[seat];
            var taken = palette?.ToList() ?? new System.Collections.Generic.List<Color>();
            var colour = Color.white;
            for (int next = listed; next <= seat; next++)
            {
                colour = MostDistinct(taken);
                taken.Add(colour);
            }
            return colour;
        }

        private static Color MostDistinct(System.Collections.Generic.List<Color> taken)
        {
            const int Hues = 24;
            var best = Color.white;
            float bestDistance = -1f;
            for (int i = 0; i < Hues; i++)
            {
                var candidate = Color.HSVToRGB(i / (float)Hues, 0.75f, 1f);
                float nearest = taken.Count == 0 ? float.MaxValue : taken.Min(colour => Distance(colour, candidate));
                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = candidate;
                }
            }
            return best;
        }

        private static float Distance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        /// <summary>
        /// The name shown above the head (empty while hidden or unbound).
        /// </summary>
        public string ShownName => _shown && _name != null ? _name.text : "";

        private int _slot = int.MinValue;

        public Color ShownColor => _ringMaterial != null ? _ringMaterial.color : Color.clear;

        private void Awake()
        {
            float top = 2f;
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);
                top = bounds.max.y - transform.position.y;
            }
            BuildLabel(top + 0.85f);
            BuildRing();
            Show(false);
        }

        private void OnDestroy()
        {
            if (_ringMaterial != null)
                Destroy(_ringMaterial);
        }

        protected override void OnBound()
        {
            _slot = int.MinValue;
            Recolor();
            _shown = true;
            Show(false);
        }

        // A player's colour is their seat's, in PvP their side's (teammates share it)
        private void Recolor()
        {
            var actor = Actor;
            var pvp = World?.Settings?.Pvp;
            int slot = pvp != null && actor?.Team != null ? pvp.SideIndexOf(actor.Team) : actor?.Seat ?? -1;
            if (slot == _slot)
                return;
            _slot = slot;
            var color = ColorOf(slot, _palette);
            _name.color = color;
            _ringMaterial.color = color;
        }

        protected override void OnUnbound() => Show(false);

        private void LateUpdate()
        {
            var actor = Actor;
            if (actor != null)
                Recolor();
            bool together = actor != null && World.Actors.Count(a => a.IsPlayer) > 1;
            Show(together);
            if (!together)
                return;
            _name.text = actor.DisplayName;
            var view = Camera.main;
            if (view != null)
                _label.rotation = view.transform.rotation;
        }

        private void Show(bool shown)
        {
            if (_shown == shown || _label == null)
                return;
            _shown = shown;
            _label.gameObject.SetActive(shown);
            _ring.gameObject.SetActive(shown);
        }

        private void BuildLabel(float height)
        {
            var canvasObject = new GameObject("NameTag", typeof(RectTransform), typeof(Canvas));
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.SetParent(transform, false);
            canvasRect.localPosition = new Vector3(0f, height, 0f);
            canvasRect.sizeDelta = new Vector2(300f, 40f);
            canvasRect.localScale = Vector3.one * 0.012f;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var textObject = new GameObject("Name", typeof(RectTransform), typeof(Text), typeof(Outline));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(canvasRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            _name = textObject.GetComponent<Text>();
            _name.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _name.fontSize = 30;
            _name.fontStyle = FontStyle.Bold;
            _name.alignment = TextAnchor.MiddleCenter;
            _name.horizontalOverflow = HorizontalWrapMode.Overflow;
            _name.raycastTarget = false;
            textObject.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            _label = canvasRect;
        }

        private void BuildRing()
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PartyRing";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(_ringSize, 0.01f, _ringSize);
            _ring = ring.GetComponent<Renderer>();
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringMaterial = _ring.material;
        }
    }
}
