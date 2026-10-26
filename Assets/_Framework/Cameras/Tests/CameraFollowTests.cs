using NUnit.Framework;
using UnityEngine;

namespace RPGFramework.Cameras.Tests
{
    public sealed class CameraFollowTests
    {
        private GameObject _camera;
        private GameObject _target;
        private CameraFollow _follow;

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Camera");
            _target = new GameObject("Target");
            _follow = _camera.AddComponent<CameraFollow>();
            _follow.SetTarget(_target.transform);
            _follow.Offset = new Vector3(0f, 5f, -10f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camera);
            Object.DestroyImmediate(_target);
        }

        [Test]
        public void SnapToTarget_PlacesCameraAtTargetPlusOffset()
        {
            _target.transform.position = new Vector3(3f, 0f, 4f);

            _follow.SnapToTarget();

            Assert.That(_camera.transform.position, Is.EqualTo(new Vector3(3f, 5f, -6f)));
        }

        [Test]
        public void Boundaries_ClampTheHorizontalPlane()
        {
            _follow.SetBoundaries(new Vector2(-5f, -5f), new Vector2(5f, 5f));
            _target.transform.position = new Vector3(100f, 0f, 100f);

            _follow.SnapToTarget();
            Assert.That(_camera.transform.position, Is.EqualTo(new Vector3(5f, 5f, 5f)));

            _follow.DisableBoundaries();
            _follow.SnapToTarget();
            Assert.That(_camera.transform.position, Is.EqualTo(new Vector3(100f, 5f, 90f)));
        }

        [Test]
        public void NotFollowing_LeavesCameraInPlace()
        {
            _follow.IsFollowing = false;
            _target.transform.position = new Vector3(50f, 0f, 0f);

            _follow.UpdateCamera();

            Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
        }
    }
}
