using System;

namespace RPGFramework.BehaviorTree
{
    /// <summary>
    /// Base class for action nodes (leaf nodes that perform actions).
    /// Game layer inherits from this to implement specific actions.
    /// </summary>
    public abstract class ActionNode : INode
    {
        protected bool _isRunning;

        public NodeStatus Execute()
        {
            if (!_isRunning)
            {
                OnStart();
                _isRunning = true;
            }

            var status = OnExecute();

            if (status != NodeStatus.Running)
            {
                OnEnd();
                _isRunning = false;
            }

            return status;
        }

        public virtual void Reset()
        {
            if (_isRunning)
            {
                OnEnd();
                _isRunning = false;
            }
        }

        /// <summary>
        /// Called once when action starts.
        /// Override for initialization logic.
        /// </summary>
        protected virtual void OnStart() { }

        /// <summary>
        /// Called every tick while action is running.
        /// Override with actual action logic.
        /// </summary>
        /// <returns>Action execution status</returns>
        protected abstract NodeStatus OnExecute();

        /// <summary>
        /// Called once when action ends (success or failure).
        /// Override for cleanup logic.
        /// </summary>
        protected virtual void OnEnd() { }
    }
}
