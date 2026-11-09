using UnityEngine;

namespace RPGFramework.FogOfWar.Unity
{
    /// <summary>
    /// Makes an object an eye of a team on the fog: a unit, a torch, a tower. Bind it to the map from code
    /// (<see cref="Bind"/>); it follows the object, cell by cell, and leaves the fog when disabled.
    /// </summary>
    [AddComponentMenu("RPG Framework/Fog Of War Eye")]
    public sealed class FogOfWarEye : MonoBehaviour
    {
        [SerializeField] private int _team;
        [SerializeField, Min(0f), Tooltip("World units; rounded up to whole cells")] private float _sightRadius = 10f;
        [SerializeField] private SightShape _shape = SightShape.Circle;
        [SerializeField, Tooltip("Walls do not stop it: a scan, a flying scout")] private bool _throughWalls;
        [SerializeField, Tooltip("A 2D game: the ground is x and y (3D: x and z)")] private bool _groundIsXY;

        private FogOfWarMap _map;
        private FogGridSpace _space;
        private int _id;

        public int Team { get => _team; set { _team = value; Rejoin(); } }
        public float SightRadius { get => _sightRadius; set { _sightRadius = Mathf.Max(0f, value); ApplySight(); } }
        public SightShape Shape { get => _shape; set { _shape = value; ApplySight(); } }
        public bool ThroughWalls { get => _throughWalls; set { _throughWalls = value; ApplySight(); } }
        public bool GroundIsXY { get => _groundIsXY; set { _groundIsXY = value; Sync(); } }

        /// <summary>
        /// Its id on the map; 0 while not on it.
        /// </summary>
        public int EyeId => _id;

        public FogOfWarMap Map => _map;

        public void Bind(FogOfWarMap map, FogGridSpace space)
        {
            Leave();
            _map = map;
            _space = space;
            if (isActiveAndEnabled)
                Join();
        }

        /// <summary>
        /// Leaves the map (a level unloads); what it saw stays explored.
        /// </summary>
        public void Unbind()
        {
            Leave();
            _map = null;
            _space = null;
        }

        /// <summary>
        /// Moves the eye to the object's cell now (done every frame in LateUpdate).
        /// </summary>
        public void Sync()
        {
            if (_id == 0)
                return;
            _map.MoveEye(_id, CellNow());
        }

        private void OnEnable() => Join();

        private void OnDisable() => Leave();

        private void LateUpdate() => Sync();

        private void Join()
        {
            if (_map == null || _space == null || _id != 0)
                return;
            _id = _map.AddEye(_team, CellNow(), SightNow());
        }

        private void Leave()
        {
            if (_id != 0 && _map != null)
                _map.RemoveEye(_id);
            _id = 0;
        }

        private void Rejoin()
        {
            if (_id == 0)
                return;
            Leave();
            Join();
        }

        private void ApplySight()
        {
            if (_id != 0)
                _map.SetSight(_id, SightNow());
        }

        private GridPoint CellNow()
        {
            var at = transform.position;
            return _space.CellAt(at.x, _groundIsXY ? at.y : at.z);
        }

        private FogSight SightNow() => new FogSight(_space.CellsIn(_sightRadius), _shape, _throughWalls);
    }
}
