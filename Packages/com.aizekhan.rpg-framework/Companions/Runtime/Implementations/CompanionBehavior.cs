namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Default implementation of ICompanionBehavior.
    /// Manages companion AI, commands, and combat behavior.
    /// </summary>
    public class CompanionBehavior : ICompanionBehavior
    {
        private CompanionCommand _currentCommand;
        private bool _isInCombat;
        private float _followDistance;
        private bool _autoAttackEnabled;
        private int _currentTargetId;

        public CompanionCommand CurrentCommand => _currentCommand;
        public bool IsInCombat => _isInCombat;
        public float FollowDistance
        {
            get => _followDistance;
            set => _followDistance = value;
        }
        public bool AutoAttackEnabled
        {
            get => _autoAttackEnabled;
            set => _autoAttackEnabled = value;
        }

        public CompanionBehavior()
        {
            _currentCommand = CompanionCommand.Follow;
            _isInCombat = false;
            _followDistance = 3.0f;
            _autoAttackEnabled = true;
            _currentTargetId = -1;
        }

        public void Update(float deltaTime)
        {
            // AI behavior logic would be implemented here
            // This would handle:
            // - Following logic
            // - Combat target selection
            // - Patrol path following
            // - Auto-attack behavior
            // - Threat detection

            // For now, this is a placeholder for the AI system
            // In a full implementation, this would integrate with a navigation/pathfinding system
        }

        public void ExecuteCommand(CompanionCommand command)
        {
            _currentCommand = command;

            switch (command)
            {
                case CompanionCommand.Stay:
                case CompanionCommand.Stop:
                    _isInCombat = false;
                    _currentTargetId = -1;
                    break;
                case CompanionCommand.Attack:
                    _isInCombat = true;
                    break;
                case CompanionCommand.Return:
                    _isInCombat = false;
                    _currentTargetId = -1;
                    _currentCommand = CompanionCommand.Follow;
                    break;
            }
        }

        public void AttackTarget(int targetEntityId)
        {
            _currentTargetId = targetEntityId;
            _currentCommand = CompanionCommand.Attack;
            _isInCombat = true;
        }

        public void MoveTo(float x, float y, float z)
        {
            // Movement logic would be implemented here
            // This would interface with a pathfinding/navigation system
        }

        public void ResetToIdle()
        {
            _currentCommand = CompanionCommand.Follow;
            _isInCombat = false;
            _currentTargetId = -1;
        }

        public void EnterCombat(int targetId)
        {
            _isInCombat = true;
            _currentTargetId = targetId;
        }

        public void ExitCombat()
        {
            _isInCombat = false;
            _currentTargetId = -1;
        }

        public int GetCurrentTarget() => _currentTargetId;
    }
}
