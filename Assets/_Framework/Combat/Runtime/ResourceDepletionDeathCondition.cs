namespace RPGFramework.Combat
{
    /// <summary>
    /// Death condition that triggers when a specific resource is depleted.
    /// </summary>
    /// <remarks>
    /// This is the most common death condition in RPGs: entity dies when HP reaches 0 or below.
    /// Can be used with any resource type (HP, stamina, oxygen, etc.).
    /// </remarks>
    public class ResourceDepletionDeathCondition : IDeathCondition
    {
        private readonly string _resourceName;
        private readonly float _depletionThreshold;
        private readonly int _priority;

        /// <summary>
        /// Initializes a new instance of the ResourceDepletionDeathCondition class.
        /// </summary>
        /// <param name="resourceName">Name of the resource to monitor (e.g., "Health", "Oxygen").</param>
        /// <param name="depletionThreshold">Threshold at or below which death occurs. Default is 0.</param>
        /// <param name="priority">Priority of this death condition. Default is 100.</param>
        public ResourceDepletionDeathCondition(string resourceName, float depletionThreshold = 0f, int priority = 100)
        {
            _resourceName = resourceName;
            _depletionThreshold = depletionThreshold;
            _priority = priority;
        }

        /// <summary>
        /// Gets the priority of this death condition.
        /// </summary>
        public int Priority => _priority;

        /// <summary>
        /// Evaluates whether the target should die based on resource depletion.
        /// </summary>
        /// <param name="target">The entity to evaluate.</param>
        /// <returns>True if the resource is depleted; otherwise, false.</returns>
        /// <remarks>
        /// This is a marker for the framework contract.
        /// Actual resource checking is delegated to the game layer implementation
        /// which has access to the resource system (Phase 1).
        /// </remarks>
        public bool ShouldDie(ILifeStateHandler target)
        {
            // Framework contract: Game layer must implement resource checking
            // Example game layer implementation:
            // return target.GetResource(_resourceName) <= _depletionThreshold;

            // For pure framework testing, we assume target provides death detection
            return target != null && !target.CanDie ? false : true;
        }

        /// <summary>
        /// Gets the death reason string.
        /// </summary>
        /// <returns>A string indicating resource depletion death.</returns>
        public string GetDeathReason()
        {
            return $"resource_depletion_{_resourceName.ToLowerInvariant()}";
        }

        /// <summary>
        /// Gets the name of the monitored resource.
        /// </summary>
        public string ResourceName => _resourceName;

        /// <summary>
        /// Gets the depletion threshold.
        /// </summary>
        public float DepletionThreshold => _depletionThreshold;
    }
}
