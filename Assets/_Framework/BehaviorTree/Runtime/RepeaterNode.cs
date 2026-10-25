using System;

namespace RPGFramework.BehaviorTree
{
    /// <summary>
    /// Repeater decorator - repeats child execution N times or infinitely.
    /// Returns Running until repeat count reached.
    /// Returns child's final status when complete.
    /// </summary>
    public class RepeaterNode : INode
    {
        private readonly INode _child;
        private readonly int _repeatCount; // -1 = infinite
        private int _currentCount;

        /// <summary>
        /// Create repeater with fixed count.
        /// </summary>
        /// <param name="child">Child node to repeat</param>
        /// <param name="repeatCount">Number of times to repeat (-1 for infinite)</param>
        public RepeaterNode(INode child, int repeatCount = -1)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
            _repeatCount = repeatCount;
            _currentCount = 0;
        }

        public NodeStatus Execute()
        {
            if (_repeatCount >= 0 && _currentCount >= _repeatCount)
            {
                return NodeStatus.Success;
            }

            var status = _child.Execute();

            // If child finished (Success or Failure), reset and increment counter
            if (status != NodeStatus.Running)
            {
                _child.Reset();
                _currentCount++;

                // Check if we've reached repeat limit
                if (_repeatCount >= 0 && _currentCount >= _repeatCount)
                {
                    Reset();
                    return NodeStatus.Success;
                }

                // Continue repeating
                return NodeStatus.Running;
            }

            return NodeStatus.Running;
        }

        public void Reset()
        {
            _currentCount = 0;
            _child.Reset();
        }
    }
}
