using System;

namespace RPGFramework.BehaviorTree
{
    /// <summary>
    /// Inverter (NOT) decorator - inverts child result.
    /// Success becomes Failure, Failure becomes Success.
    /// Running passes through unchanged.
    /// </summary>
    public class InverterNode : INode
    {
        private readonly INode _child;

        public InverterNode(INode child)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
        }

        public NodeStatus Execute()
        {
            var status = _child.Execute();

            return status switch
            {
                NodeStatus.Success => NodeStatus.Failure,
                NodeStatus.Failure => NodeStatus.Success,
                NodeStatus.Running => NodeStatus.Running,
                _ => throw new InvalidOperationException($"Unknown node status: {status}")
            };
        }

        public void Reset()
        {
            _child.Reset();
        }
    }
}
