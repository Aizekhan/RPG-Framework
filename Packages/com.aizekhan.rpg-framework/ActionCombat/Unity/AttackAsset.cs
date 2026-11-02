using UnityEngine;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// An attack set up in the inspector (<see cref="AttackDefinition"/>). Its phases come from the seconds here, or
    /// from an animation clip: the clip's length (at <see cref="ClipSpeed"/>) split by the normalized marks
    /// Active Start / Active End, or by animation events named <c>ActiveStart</c> and <c>ActiveEnd</c> on the clip.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG Framework/Action Combat/Attack", fileName = "Attack")]
    public sealed class AttackAsset : ScriptableObject
    {
        public const string ActiveStartEvent = "ActiveStart";
        public const string ActiveEndEvent = "ActiveEnd";

        [Tooltip("The game's name for it; empty: the asset's name")]
        [SerializeField] private string _id = "";

        [Header("Phases (seconds), used when there is no clip")]
        [SerializeField, Min(0f)] private float _windUp = 0.25f;
        [SerializeField, Min(0f)] private float _active = 0.15f;
        [SerializeField, Min(0f)] private float _recovery = 0.35f;

        [Header("Phases from a clip")]
        [Tooltip("The attack's animation: its length gives the phases")]
        [SerializeField] private AnimationClip _clip;

        [Tooltip("Playback speed of the clip in the animator state")]
        [SerializeField, Min(0.01f)] private float _clipSpeed = 1f;

        [Tooltip("Where the hit window opens, as a share of the clip (an ActiveStart animation event wins)")]
        [SerializeField, Range(0f, 1f)] private float _activeStart = 0.35f;

        [Tooltip("Where the hit window closes, as a share of the clip (an ActiveEnd animation event wins)")]
        [SerializeField, Range(0f, 1f)] private float _activeEnd = 0.55f;

        [Header("Hit")]
        [SerializeField, Min(0f)] private float _damageMultiplier = 1f;

        [Tooltip("Poise it takes from the target; 0 never staggers")]
        [SerializeField, Min(0f)] private float _stagger = 1f;

        [Tooltip("Off: hyper armour, a hit does not stop this attack")]
        [SerializeField] private bool _interruptible = true;

        [Tooltip("What blocking one blow costs the defender (stamina)")]
        [SerializeField, Min(0f)] private float _blockCost = 1f;

        [Tooltip("The game's tag bits for the damage rules (AttackContext.Tags)")]
        [SerializeField] private int _tags;

        [Tooltip("What dealt the hit, as the game numbers it (AttackContext.SourceId)")]
        [SerializeField] private int _sourceId;

        [Header("Charge (hold to charge; a bow's draw)")]
        [Tooltip("Longest charge that counts, seconds; 0: cannot be charged")]
        [SerializeField, Min(0f)] private float _maxCharge;

        [Tooltip("A release before this is the quick attack")]
        [SerializeField, Min(0f)] private float _minCharge = 0.2f;

        [SerializeField, Min(0f)] private float _maxChargeMultiplier = 2f;

        [Tooltip("Share of the charge (0..1) → share of the extra damage (0..1)")]
        [SerializeField] private AnimationCurve _chargeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Played by a release under Min Charge; empty: this attack uncharged")]
        [SerializeField] private AttackAsset _quickAttack;

        [Header("Projectile")]
        [Tooltip("It flies (ProjectileLauncher fires it when the active phase starts); the flight is its hit window")]
        [SerializeField] private bool _projectile;

        [SerializeField, Min(0f)] private float _projectileLifetime = 5f;

        [Header("Cost and parry")]
        [Tooltip("Stamina starting the attack costs (CombatArena.UseAttackStock); an attacker that cannot pay does not start it. 0: free")]
        [SerializeField, Min(0f)] private float _staminaCost;

        [Tooltip("Off: it cannot be parried")]
        [SerializeField] private bool _parryable = true;

        [Header("Warp (pulls the attacker to its target and turns it to face it; Max Distance and Max Angle 0: none)")]
        [Tooltip("The phase and its part (From/Until; Until 0: to its end), how far it may pull and turn, the gap it keeps (0: the caller's)")]
        [SerializeField] private AttackWarp _warp = new AttackWarp(AttackPhase.WindUp, 0f, 0f);

        [Header("Hold (an enemy's wide swing waits raised while it closes in)")]
        [Tooltip("Where a held wind-up waits, as a share of the wind-up: 1 = just before the blow")]
        [SerializeField, Range(0f, 1f)] private float _holdPoint = 1f;

        [Header("Motion (how the attack moves its attacker)")]
        [Tooltip("Per phase: approach, retreat, strafe or leap, the distance over a part of the phase (Until 0: to its end)")]
        [SerializeField] private System.Collections.Generic.List<AttackMotion> _motions = new System.Collections.Generic.List<AttackMotion>();

        [Header("Sweep (the weapon's volume over the active window, for contact without physics)")]
        [Tooltip("Segments in time (From/Until of the window; Until 0: to its end), each an arc around the attacker: degrees from its forward (right +), radii, height above its feet (both 0: any)")]
        [SerializeField] private System.Collections.Generic.List<SweepSegment> _sweep = new System.Collections.Generic.List<SweepSegment>();

        [Header("Animator")]
        [Tooltip("Trigger set when the attack starts (CombatAnimatorBridge); empty: the bridge's default")]
        [SerializeField] private string _animatorTrigger = "";

        private AttackDefinition _definition;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public AnimationClip Clip => _clip;
        public float ClipSpeed => _clipSpeed;
        public string AnimatorTrigger => _animatorTrigger;

        /// <summary>
        /// The rules' attack, built once (and again after a change in the inspector).
        /// </summary>
        public AttackDefinition Definition => _definition ??= Build(new System.Collections.Generic.HashSet<AttackAsset>());

        /// <summary>
        /// The phases of an attack from a clip: wind-up up to the start mark, active between the marks, recovery after.
        /// </summary>
        /// <param name="length">The clip's length in seconds.</param>
        /// <param name="speed">Its playback speed.</param>
        /// <param name="start">Where the hit window opens (0..1 of the clip).</param>
        /// <param name="end">Where it closes (0..1, after the start).</param>
        public static (float WindUp, float Active, float Recovery) PhasesFromClip(float length, float speed, float start, float end)
        {
            float seconds = Mathf.Max(0f, length) / Mathf.Max(0.0001f, speed);
            start = Mathf.Clamp01(start);
            end = Mathf.Clamp(end, start, 1f);
            return (seconds * start, seconds * (end - start), seconds * (1f - end));
        }

        /// <summary>
        /// The clip's <c>ActiveStart</c>/<c>ActiveEnd</c> animation events as shares of the clip, where present.
        /// </summary>
        public static (float Start, float End) MarksOf(AnimationClip clip, float start, float end)
        {
            if (clip == null || clip.length <= 0f)
                return (start, end);
            foreach (var animationEvent in clip.events)
            {
                if (animationEvent.functionName == ActiveStartEvent)
                    start = animationEvent.time / clip.length;
                else if (animationEvent.functionName == ActiveEndEvent)
                    end = animationEvent.time / clip.length;
            }
            return (start, end);
        }

        private AttackDefinition Build(System.Collections.Generic.HashSet<AttackAsset> building)
        {
            building.Add(this);
            float windUp = _windUp, active = _active, recovery = _recovery;
            if (_clip != null)
            {
                var (start, end) = MarksOf(_clip, _activeStart, _activeEnd);
                (windUp, active, recovery) = PhasesFromClip(_clip.length, _clipSpeed, start, end);
            }

            var curve = _chargeCurve;
            var definition = new AttackDefinition(Id, windUp, active, recovery)
            {
                DamageMultiplier = _damageMultiplier,
                Stagger = _stagger,
                Interruptible = _interruptible,
                BlockCost = _blockCost,
                Tags = _tags,
                SourceId = _sourceId,
                MaxCharge = _maxCharge,
                MinCharge = _minCharge,
                MaxChargeMultiplier = _maxChargeMultiplier,
                ChargeCurve = curve != null && curve.length > 0 ? share => curve.Evaluate(share) : (System.Func<float, float>)null,
                Projectile = _projectile,
                ProjectileLifetime = _projectileLifetime,
                HoldPoint = _holdPoint,
                StaminaCost = _staminaCost,
                Parryable = _parryable
            };
            if (_warp.Problem() == null)
                definition.Warp = _warp;
            // A broken row is left out (Problems names it) rather than failing the whole attack
            definition.Motions = _motions.FindAll(motion => motion.Problem() == null);
            definition.Sweep = _sweep.FindAll(segment => segment.Problem() == null);
            // A quick attack that leads back here would never end
            if (_quickAttack != null && !building.Contains(_quickAttack))
                definition.QuickAttack = _quickAttack._definition ?? _quickAttack.Build(building);
            return definition;
        }

        /// <summary>
        /// What is wrong with its warp, motions and sweep segments (they are left out of the attack). The inspector logs
        /// these as warnings.
        /// </summary>
        public System.Collections.Generic.List<string> Problems()
        {
            var problems = new System.Collections.Generic.List<string>();
            string warp = _warp.Problem();
            if (warp != null)
                problems.Add("Warp: " + warp);
            for (int i = 0; i < _motions.Count; i++)
            {
                string wrong = _motions[i].Problem();
                if (wrong != null)
                    problems.Add($"Motion {i}: {wrong}");
            }
            for (int i = 0; i < _sweep.Count; i++)
            {
                string wrong = _sweep[i].Problem();
                if (wrong != null)
                    problems.Add($"Sweep segment {i}: {wrong}");
            }
            return problems;
        }

        private void OnValidate()
        {
            _definition = null;
            foreach (var problem in Problems())
                Debug.LogWarning($"[Attack] {name}: {problem}", this);
        }
    }
}
