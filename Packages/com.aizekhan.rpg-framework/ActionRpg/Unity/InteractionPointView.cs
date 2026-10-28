using System;
using System.Collections.Generic;
using RPGFramework.Interaction;
using UnityEngine;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A place in the scene players use with E: a chest, a door, a lever, a shrine. On the machine that runs the
    /// rules (solo or the co-op host) the <see cref="ActionRpgHost"/> adds it to <c>World.Interactions</c> where it
    /// stands; friends who join get it from the host. What using it does is the game's: react to
    /// <see cref="InteractedEvent"/> in your module (it runs where the rules run) and check <c>Kind</c> or
    /// <c>PointId</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPointView : MonoBehaviour
    {
        [SerializeField, Tooltip("Unique among the places in the world; the object's name when empty")]
        private string _id;

        [SerializeField, Tooltip("What sort of place it is (chest, door, lever): rules and handlers go by it")]
        private string _kind = "use";

        [SerializeField, Tooltip("What the screen shows when it is the nearest; the kind when empty")]
        private string _prompt;

        [SerializeField, Min(0f), Tooltip("How near a player has to be, in metres")]
        private float _radius = 2f;

        [SerializeField, Tooltip("It closes after the first use (a chest, a pickup)")]
        private bool _once;

        [SerializeField, Tooltip("Shown while the place is open, hidden once it is closed (a closed lid, a glow)")]
        private GameObject _shownWhileOpen;

        [SerializeField, Tooltip("Shown once the place is closed (an open chest, a pulled lever)")]
        private GameObject _shownWhenClosed;

        private static readonly List<InteractionPointView> ActiveViews = new List<InteractionPointView>();

        internal static IReadOnlyList<InteractionPointView> Active => ActiveViews;

        /// <summary>
        /// A place came into the scene (true) or left it (false).
        /// </summary>
        internal static event Action<InteractionPointView, bool> ActiveChanged;

        public string Id => string.IsNullOrWhiteSpace(_id) ? name : _id;
        public string Kind => string.IsNullOrWhiteSpace(_kind) ? "use" : _kind;

        /// <summary>
        /// The place this view added to the world (on the machine that runs the rules), or null.
        /// </summary>
        internal InteractionPoint Point { get; set; }

        /// <summary>
        /// The place as the world has it now, at the view's position.
        /// </summary>
        public InteractionPoint CreatePoint()
        {
            var position = transform.position;
            return new InteractionPoint(Id, Kind, _radius, _prompt, _once).At(position.x, position.y, position.z);
        }

        /// <summary>
        /// Shows the place open or closed (the host calls it on every machine from the world's state).
        /// </summary>
        internal void ShowOpen(bool open)
        {
            if (_shownWhileOpen != null && _shownWhileOpen.activeSelf != open)
                _shownWhileOpen.SetActive(open);
            if (_shownWhenClosed != null && _shownWhenClosed.activeSelf == open)
                _shownWhenClosed.SetActive(!open);
        }

        private void OnEnable()
        {
            ActiveViews.Add(this);
            ActiveChanged?.Invoke(this, true);
        }

        private void OnDisable()
        {
            ActiveViews.Remove(this);
            ActiveChanged?.Invoke(this, false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
