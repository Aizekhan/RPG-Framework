using System;

namespace RPGFramework.BehaviorTree
{
    /// <summary>
    /// Behavior tree executor.
    /// Manages root node execution and blackboard.
    /// </summary>
    public class BehaviorTree
    {
        private readonly INode _rootNode;
        private readonly Blackboard _blackboard;
        private NodeStatus _lastStatus;

        public Blackboard Blackboard => _blackboard;
        public NodeStatus LastStatus => _lastStatus;

        public BehaviorTree(INode rootNode, Blackboard blackboard = null)
        {
            _rootNode = rootNode ?? throw new ArgumentNullException(nameof(rootNode));
            _blackboard = blackboard ?? new Blackboard();
            _lastStatus = NodeStatus.Success;
        }

        /// <summary>
        /// Tick the behavior tree (execute one iteration).
        /// Call this every frame/update.
        /// </summary>
        /// <returns>Current execution status</returns>
        public NodeStatus Tick()
        {
            _lastStatus = _rootNode.Execute();
            return _lastStatus;
        }

        /// <summary>
        /// Reset the entire behavior tree.
        /// </summary>
        public void Reset()
        {
            _rootNode.Reset();
            _lastStatus = NodeStatus.Success;
        }
    }
}
