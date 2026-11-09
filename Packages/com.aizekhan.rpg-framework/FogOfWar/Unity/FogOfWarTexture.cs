using System;
using UnityEngine;

namespace RPGFramework.FogOfWar.Unity
{
    /// <summary>
    /// A team's fog as a one-channel texture, a texel per cell: 1 unexplored, <see cref="ExploredFog"/> explored, 0
    /// visible; bilinear, so edges come out soft. <see cref="Refresh"/> each frame redraws only when the fog changed.
    /// The game's shader (a full-screen pass, a projector, a plane over the map, a minimap) darkens by it;
    /// <see cref="SetGlobals"/> gives every shader the texture and where it lies:
    /// <c>uv = (world.xz - _FogOfWarArea.xy) * _FogOfWarArea.zw</c>.
    /// </summary>
    public sealed class FogOfWarTexture : IDisposable
    {
        public const string TextureProperty = "_FogOfWarTex";
        public const string AreaProperty = "_FogOfWarArea";

        private readonly FogOfWarMap _map;
        private readonly byte[] _pixels;
        private long _drawn = -1;
        private float _drawnExplored = -1f;
        private int _team;

        public FogOfWarTexture(FogOfWarMap map, int team = 0)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _pixels = new byte[map.CellCount];
            Team = team;
            Texture = new Texture2D(map.Width, map.Height, TextureFormat.R8, false)
            {
                name = "Fog of war",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Refresh();
        }

        public Texture2D Texture { get; private set; }

        /// <summary>
        /// Whose view it draws (the local player's team; a spectator switches).
        /// </summary>
        public int Team
        {
            get => _team;
            set
            {
                _map.ViewVersion(value);
                _team = value;
                _drawn = -1;
            }
        }

        /// <summary>
        /// How dark explored ground is drawn, 0..1 (1: as dark as unexplored).
        /// </summary>
        public float ExploredFog { get; set; } = 0.6f;

        /// <summary>
        /// Redraws when the team's view changed since the last draw.
        /// </summary>
        /// <returns>True when it redrew.</returns>
        public bool Refresh()
        {
            if (Texture == null)
                throw new ObjectDisposedException(nameof(FogOfWarTexture));
            long version = _map.ViewVersion(_team);
            if (version == _drawn && ExploredFog == _drawnExplored)
                return false;
            _map.WriteStates(_team, _pixels, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(ExploredFog) * 255f), 0);
            Texture.SetPixelData(_pixels, 0);
            Texture.Apply(false);
            _drawn = version;
            _drawnExplored = ExploredFog;
            return true;
        }

        /// <summary>
        /// Sets the texture and its world area as global shader values.
        /// </summary>
        public void SetGlobals(FogGridSpace space, string textureProperty = TextureProperty,
            string areaProperty = AreaProperty)
        {
            if (space == null)
                throw new ArgumentNullException(nameof(space));
            Shader.SetGlobalTexture(textureProperty, Texture);
            Shader.SetGlobalVector(areaProperty, AreaOf(space));
        }

        /// <summary>
        /// The grid's corner (xy) and one over its world size (zw).
        /// </summary>
        public static Vector4 AreaOf(FogGridSpace space) => new Vector4(space.OriginX, space.OriginY,
            1f / (space.Width * space.CellSize), 1f / (space.Height * space.CellSize));

        public void Dispose()
        {
            if (Texture == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(Texture);
            else
                UnityEngine.Object.DestroyImmediate(Texture);
            Texture = null;
        }
    }
}
