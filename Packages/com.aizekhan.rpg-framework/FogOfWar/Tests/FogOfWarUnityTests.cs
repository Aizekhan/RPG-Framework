using NUnit.Framework;
using RPGFramework.FogOfWar.Unity;
using UnityEngine;

namespace RPGFramework.FogOfWar.Tests
{
    /// <summary>
    /// The Unity side: a team's fog as a texture, and an object that is an eye on the fog.
    /// </summary>
    public class FogOfWarUnityTests
    {
        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        [Test]
        public void TheTexture_DrawsATexelACell_AndRedrawsOnlyOnChange()
        {
            var fog = new FogOfWarMap(4, 2, teams: 2);
            using (var texture = new FogOfWarTexture(fog) { ExploredFog = 0.5f })
            {
                Assert.That(texture.Texture.width, Is.EqualTo(4));
                Assert.That(texture.Texture.format, Is.EqualTo(TextureFormat.R8));
                Assert.That(texture.Refresh(), Is.True, "the explored shade changed");
                Assert.That(texture.Refresh(), Is.False);

                fog.Explore(0, P(3, 1), 0);
                fog.AddEye(0, P(0, 0), 0);
                fog.AddEye(1, P(1, 0), 0);
                Assert.That(texture.Refresh(), Is.True);
                Assert.That(texture.Texture.GetPixelData<byte>(0).ToArray(),
                    Is.EqualTo(new byte[] { 0, 255, 255, 255, 255, 255, 255, 128 }));

                texture.Team = 1;
                Assert.That(texture.Refresh(), Is.True);
                Assert.That(texture.Texture.GetPixelData<byte>(0)[1], Is.EqualTo(0));

                var area = FogOfWarTexture.AreaOf(new FogGridSpace(-4f, 10f, 2f, 4, 2));
                Assert.That(area, Is.EqualTo(new Vector4(-4f, 10f, 0.125f, 0.25f)));
            }
        }

        [Test]
        public void AnEye_FollowsItsObject_CellByCell_AndLeavesWhenDisabled()
        {
            var fog = new FogOfWarMap(20, 20, teams: 2);
            var space = new FogGridSpace(0f, 0f, 2f, 20, 20);
            var go = new GameObject("scout");
            try
            {
                go.transform.position = new Vector3(5f, 3f, 7f);
                var eye = go.AddComponent<FogOfWarEye>();
                eye.SightRadius = 4f;
                eye.Bind(fog, space);
                Assert.That(eye.EyeId, Is.Not.Zero);
                Assert.That(fog.PositionOf(eye.EyeId), Is.EqualTo(P(2, 3)), "x and z");
                Assert.That(fog.SightOf(eye.EyeId).Radius, Is.EqualTo(2));
                Assert.That(fog.IsVisible(0, P(4, 3)), Is.True);

                go.transform.position = new Vector3(30f, 0f, 30f);
                eye.Sync();
                Assert.That(fog.PositionOf(eye.EyeId), Is.EqualTo(P(15, 15)));
                Assert.That(fog.StateOf(0, P(2, 3)), Is.EqualTo(FogState.Explored));

                eye.SightRadius = 8f;
                Assert.That(fog.SightOf(eye.EyeId).Radius, Is.EqualTo(4));
                eye.Team = 1;
                Assert.That(fog.TeamOf(eye.EyeId), Is.EqualTo(1), "joined the other team");

                eye.Unbind();
                Assert.That(fog.EyeCount, Is.Zero);
                Assert.That(eye.EyeId, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
