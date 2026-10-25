namespace RPGFramework.BehaviorTree
{
    /// <summary>
    /// Execution status of a behavior tree node.
    /// </summary>
    public enum NodeStatus
    {
        /// <summary>
        /// Node is currently executing and not yet finished.
        /// </summary>
        Running,

        /// <summary>
        /// Node execution succeeded.
        /// </summary>
        Success,

        /// <summary>
        /// Node execution failed.
        /// </summary>
        Failure
    }
}
