using System;

namespace RPGFramework.Systems
{
    /// <summary>
    /// A unit of game logic driven by a <see cref="ISystemRegistry"/>.
    /// Per-frame work is opted into through the stage interfaces
    /// (<see cref="IUpdateSystem"/>, <see cref="IFixedUpdateSystem"/>, <see cref="ILateUpdateSystem"/>),
    /// so systems without per-frame work cost nothing per frame.
    /// </summary>
    public interface ISystem : IDisposable
    {
        void Initialize();
    }

    public interface IUpdateSystem : ISystem
    {
        void Update(float deltaTime);
    }

    public interface IFixedUpdateSystem : ISystem
    {
        void FixedUpdate(float fixedDeltaTime);
    }

    public interface ILateUpdateSystem : ISystem
    {
        void LateUpdate(float deltaTime);
    }

    /// <summary>
    /// A system that only runs while the current phase (see <see cref="IPhaseProvider"/>) is one of its phases.
    /// </summary>
    public interface IPhaseFilteredSystem : ISystem
    {
        bool IsActiveInPhase(string phaseId);
    }

    /// <summary>
    /// Reference priorities; higher runs first. Games may use any int values in between.
    /// </summary>
    public static class SystemPriorities
    {
        public const int Core = 1000;
        public const int Input = 800;
        public const int Network = 700;
        public const int Simulation = 500;
        public const int Gameplay = 300;
        public const int Presentation = 100;
        public const int Diagnostics = 0;
    }
}
