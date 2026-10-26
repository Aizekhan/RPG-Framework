using System.Collections.Generic;

namespace RPGFramework.BehaviorTree
{
    /// <summary>
    /// Sequence (AND) node - executes children in order until one fails.
    /// Returns Success if all children succeed.
    /// Returns Failure if any child fails.
    /// Returns Running if current child is running.
    /// </summary>
    public class SequenceNode : INode
    {
        private readonly List<INode> _children;
        private int _currentChildIndex;

        public SequenceNode()
        {
            _children = new List<INode>();
            _currentChildIndex = 0;
        }

        public SequenceNode(params INode[] children) : this()
        {
            _children.AddRange(children);
        }

        public void AddChild(INode child)
        {
            _children.Add(child);
        }

        public NodeStatus Execute()
        {
            while (_currentChildIndex < _children.Count)
            {
                var status = _children[_currentChildIndex].Execute();

                if (status == NodeStatus.Failure)
                {
                    Reset();
                    return NodeStatus.Failure;
                }

                if (status == NodeStatus.Running)
                {
                    return NodeStatus.Running;
                }

                // Success - move to next child
                _currentChildIndex++;
            }

            // All children succeeded
            Reset();
            return NodeStatus.Success;
        }

        public void Reset()
        {
            _currentChildIndex = 0;
            foreach (var child in _children)
            {
                child.Reset();
            }
        }
    }
}
