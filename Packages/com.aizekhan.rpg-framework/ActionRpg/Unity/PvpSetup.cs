using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// The PvP part of a <see cref="GameSetup"/>: whether the game is a match, its rules, and where sides start.
    /// </summary>
    [Serializable]
    public sealed class PvpSetup
    {
        [Tooltip("Players fight each other in a match instead of playing together")]
        [SerializeField] private bool _enabled;

        [SerializeField] private PvpMode _mode = PvpMode.TeamDeathmatch;

        [Tooltip("Team names (Duel and Team Deathmatch); with faction hostility, faction ids")]
        [SerializeField] private List<string> _teams = new List<string> { "Red", "Blue" };

        [Tooltip("Defeats that win Team Deathmatch and Free For All")]
        [SerializeField, Min(1)] private int _scoreToWin = 10;

        [Tooltip("Rounds that win a Duel (2 = best of three)")]
        [SerializeField, Min(1)] private int _roundsToWin = 2;

        [Tooltip("Seconds a defeated player waits before coming back")]
        [SerializeField, Min(0f)] private float _respawnDelay = 3f;

        [Tooltip("Seconds between Duel rounds")]
        [SerializeField, Min(0f)] private float _roundBreak = 3f;

        [Tooltip("Seconds a match lasts at most (0 = no limit); not used by Duel")]
        [SerializeField, Min(0f)] private float _timeLimit;

        [Tooltip("Seconds after a match before the next starts (0 = never by itself)")]
        [SerializeField, Min(0f)] private float _restartDelay = 10f;

        [Tooltip("Where each side starts, in order (first team, second team...; in Free For All, each seat). " +
                 "Empty puts sides around a circle")]
        [SerializeField] private List<Vector3> _spawnPoints = new List<Vector3>();

        [Tooltip("Radius of that circle")]
        [SerializeField, Min(1f)] private float _arenaRadius = 8f;

        [Tooltip("Distance between teammates at a spawn point")]
        [SerializeField, Min(0.5f)] private float _spacing = 1.5f;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public PvpMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        public List<Vector3> SpawnPoints => _spawnPoints;

        public int RoundsToWin
        {
            get => _roundsToWin;
            set => _roundsToWin = Mathf.Max(1, value);
        }

        public int ScoreToWin
        {
            get => _scoreToWin;
            set => _scoreToWin = Mathf.Max(1, value);
        }

        /// <summary>
        /// The rules for <see cref="ActionRpgSettings.Pvp"/>; null when PvP is off.
        /// </summary>
        public PvpSettings ToSettings()
        {
            if (!_enabled)
                return null;
            var settings = new PvpSettings
            {
                Mode = _mode, ScoreToWin = _scoreToWin, RoundsToWin = _roundsToWin, RespawnDelay = _respawnDelay,
                RoundBreak = _roundBreak, TimeLimit = _timeLimit, RestartDelay = _restartDelay
            };
            settings.Teams.Clear();
            settings.Teams.AddRange(_teams);
            return settings;
        }

        /// <summary>
        /// Where a player of this side starts, facing the middle of the arena.
        /// </summary>
        /// <param name="side">The side's place (<see cref="PvpSettings.SideIndexOf"/>).</param>
        /// <param name="member">Which of the side's players (0 for the first).</param>
        /// <param name="sides">How many sides there are, for the circle.</param>
        public Pose SpawnPoint(int side, int member, int sides)
        {
            side = Mathf.Max(0, side);
            Vector3 point;
            if (_spawnPoints.Count > 0)
            {
                point = _spawnPoints[side % _spawnPoints.Count];
            }
            else
            {
                float angle = side * Mathf.PI * 2f / Mathf.Max(2, sides);
                point = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * _arenaRadius;
            }

            var facing = new Vector3(-point.x, 0f, -point.z);
            var rotation = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            // Teammates stand side by side: 0, +1, -1, +2, -2... spacings to the right
            int slot = (member + 1) / 2 * (member % 2 == 1 ? 1 : -1);
            return new Pose(point + rotation * Vector3.right * (slot * _spacing), rotation);
        }
    }
}
