using NUnit.Framework;

namespace RPGFramework.BehaviorTree.Tests
{
    [TestFixture]
    public class BehaviorTreeTests
    {
        // Test helper action nodes
        private class AlwaysSucceedAction : ActionNode
        {
            protected override NodeStatus OnExecute() => NodeStatus.Success;
        }

        private class AlwaysFailAction : ActionNode
        {
            protected override NodeStatus OnExecute() => NodeStatus.Failure;
        }

        private class CountingAction : ActionNode
        {
            public int ExecuteCount { get; private set; }
            private readonly NodeStatus _returnStatus;

            public CountingAction(NodeStatus returnStatus = NodeStatus.Success)
            {
                _returnStatus = returnStatus;
            }

            protected override NodeStatus OnExecute()
            {
                ExecuteCount++;
                return _returnStatus;
            }

            public override void Reset()
            {
                base.Reset();
                // Keep ExecuteCount for test verification - don't reset it
            }

            public void ResetCount()
            {
                ExecuteCount = 0;
            }
        }

        [Test]
        public void Selector_FirstChildSucceeds_ReturnsSuccess()
        {
            // Arrange
            var selector = new SelectorNode(
                new AlwaysSucceedAction(),
                new AlwaysFailAction()
            );

            // Act
            var status = selector.Execute();

            // Assert
            Assert.AreEqual(NodeStatus.Success, status);
        }

        [Test]
        public void Selector_AllChildrenFail_ReturnsFailure()
        {
            // Arrange
            var selector = new SelectorNode(
                new AlwaysFailAction(),
                new AlwaysFailAction()
            );

            // Act
            var status = selector.Execute();

            // Assert
            Assert.AreEqual(NodeStatus.Failure, status);
        }

        [Test]
        public void Sequence_AllChildrenSucceed_ReturnsSuccess()
        {
            // Arrange
            var sequence = new SequenceNode(
                new AlwaysSucceedAction(),
                new AlwaysSucceedAction()
            );

            // Act
            var status = sequence.Execute();

            // Assert
            Assert.AreEqual(NodeStatus.Success, status);
        }

        [Test]
        public void Sequence_FirstChildFails_ReturnsFailure()
        {
            // Arrange
            var sequence = new SequenceNode(
                new AlwaysFailAction(),
                new AlwaysSucceedAction()
            );

            // Act
            var status = sequence.Execute();

            // Assert
            Assert.AreEqual(NodeStatus.Failure, status);
        }

        [Test]
        public void Inverter_SuccessBecomesFailure()
        {
            // Arrange
            var inverter = new InverterNode(new AlwaysSucceedAction());

            // Act
            var status = inverter.Execute();

            // Assert
            Assert.AreEqual(NodeStatus.Failure, status);
        }

        [Test]
        public void Inverter_FailureBecomesSuccess()
        {
            // Arrange
            var inverter = new InverterNode(new AlwaysFailAction());

            // Act
            var status = inverter.Execute();

            // Assert
            Assert.AreEqual(NodeStatus.Success, status);
        }

        [Test]
        public void Repeater_RepeatsTwice()
        {
            // Arrange
            var counter = new CountingAction();
            var repeater = new RepeaterNode(counter, repeatCount: 2);

            // Act
            var status1 = repeater.Execute(); // First execution completes, child reset, count = 1
            Assert.AreEqual(NodeStatus.Running, status1);

            var status2 = repeater.Execute(); // Second execution completes, count = 2, repeat limit reached

            // Assert
            Assert.AreEqual(NodeStatus.Success, status2);
            Assert.AreEqual(2, counter.ExecuteCount);
        }

        [Test]
        public void BehaviorTree_ExecutesRootNode()
        {
            // Arrange
            var action = new AlwaysSucceedAction();
            var tree = new BehaviorTree(action);

            // Act
            var status = tree.Tick();

            // Assert
            Assert.AreEqual(NodeStatus.Success, status);
            Assert.AreEqual(NodeStatus.Success, tree.LastStatus);
        }

        [Test]
        public void BehaviorTree_Reset_ResetsAllNodes()
        {
            // Arrange
            var counter = new CountingAction();
            var tree = new BehaviorTree(counter);
            tree.Tick();
            Assert.AreEqual(1, counter.ExecuteCount);

            // Act
            tree.Reset();
            counter.ResetCount(); // Reset count for clean test
            tree.Tick();

            // Assert
            Assert.AreEqual(1, counter.ExecuteCount); // Executed once after reset
        }

        [Test]
        public void Blackboard_SetAndGetValue()
        {
            // Arrange
            var blackboard = new Blackboard();

            // Act
            blackboard.SetValue("health", 100);
            var health = blackboard.GetValue<int>("health");

            // Assert
            Assert.AreEqual(100, health);
        }

        [Test]
        public void Blackboard_TryGetValue_KeyExists()
        {
            // Arrange
            var blackboard = new Blackboard();
            blackboard.SetValue("position", 5.5f);

            // Act
            bool found = blackboard.TryGetValue<float>("position", out var value);

            // Assert
            Assert.IsTrue(found);
            Assert.AreEqual(5.5f, value);
        }

        [Test]
        public void Blackboard_TryGetValue_KeyMissing()
        {
            // Arrange
            var blackboard = new Blackboard();

            // Act
            bool found = blackboard.TryGetValue<int>("missing", out var value);

            // Assert
            Assert.IsFalse(found);
            Assert.AreEqual(0, value);
        }

        [Test]
        public void Blackboard_HasKey_ReturnsTrue()
        {
            // Arrange
            var blackboard = new Blackboard();
            blackboard.SetValue("key", "value");

            // Act & Assert
            Assert.IsTrue(blackboard.HasKey("key"));
        }

        [Test]
        public void Blackboard_Remove_RemovesKey()
        {
            // Arrange
            var blackboard = new Blackboard();
            blackboard.SetValue("key", "value");

            // Act
            bool removed = blackboard.Remove("key");

            // Assert
            Assert.IsTrue(removed);
            Assert.IsFalse(blackboard.HasKey("key"));
        }

        [Test]
        public void Blackboard_Clear_RemovesAllKeys()
        {
            // Arrange
            var blackboard = new Blackboard();
            blackboard.SetValue("key1", 1);
            blackboard.SetValue("key2", 2);

            // Act
            blackboard.Clear();

            // Assert
            Assert.IsFalse(blackboard.HasKey("key1"));
            Assert.IsFalse(blackboard.HasKey("key2"));
        }
    }
}
