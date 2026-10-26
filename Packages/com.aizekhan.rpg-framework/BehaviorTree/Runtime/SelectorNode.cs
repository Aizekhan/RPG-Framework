using System.Collections.Generic;

namespace RPGFramework.BehaviorTree
{
    /// <summary>
    /// Selector (OR) node - executes children until one succeeds.
    /// Returns Success if any child succeeds.
    /// Returns Failure if all children fail.
    /// Returns Running if current child is running.
    /// </summary>
    public class SelectorNode : INode
    {
        private readonly List<INode> _children;
        private int _currentChildIndex;

        public SelectorNode()
        {
            _children = new List<INode>();
            _currentChildIndex = 0;
        }

        public SelectorNode(params INode[] children) : this()
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

                if (status == NodeStatus.Success)
                {
                    Reset();
                    return NodeStatus.Success;
                }

                if (status == NodeStatus.Running)
                {
                    return NodeStatus.Running;
                }

                // Failure - try next child
                _currentChildIndex++;
            }

            // All children failed
            Reset();
            return NodeStatus.Failure;
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
