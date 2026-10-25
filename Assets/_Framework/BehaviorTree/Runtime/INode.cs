namespace RPGFramework.BehaviorTree
{
    /// <summary>
    /// Base interface for all behavior tree nodes.
    /// Framework contract with no engine or game dependencies.
    /// </summary>
    public interface INode
    {
        /// <summary>
        /// Execute the node logic.
        /// Called every tick while node is active.
        /// </summary>
        /// <returns>Current execution status</returns>
        NodeStatus Execute();

        /// <summary>
        /// Reset the node to initial state.
        /// Called when node needs to restart execution.
        /// </summary>
        void Reset();
    }
}
