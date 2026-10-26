using System;

namespace RPGFramework.BehaviorTree
{
    /// <summary>
    /// Base class for condition nodes (leaf nodes that check conditions).
    /// Game layer inherits from this to implement specific conditions.
    /// </summary>
    public abstract class ConditionNode : INode
    {
        public NodeStatus Execute()
        {
            return CheckCondition() ? NodeStatus.Success : NodeStatus.Failure;
        }

        public void Reset()
        {
            // Conditions are stateless, no reset needed
        }

        /// <summary>
        /// Check the condition.
        /// Override with actual condition logic.
        /// </summary>
        /// <returns>True if condition is met</returns>
        protected abstract bool CheckCondition();
    }
}
