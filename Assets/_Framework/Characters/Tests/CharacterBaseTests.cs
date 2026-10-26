using NUnit.Framework;
using UnityEngine;

namespace RPGFramework.Characters.Tests
{
    public sealed class CharacterBaseTests
    {
        private GameObject _host;
        private TestCharacter _character;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Hero");
            _character = _host.AddComponent<TestCharacter>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        [Test]
        public void NewCharacter_IsAliveAndUnbound()
        {
            Assert.That(_character.IsAlive, Is.True);
            Assert.That(_character.EntityId, Is.EqualTo(-1));
            Assert.That(_character.Level, Is.EqualTo(1));
            Assert.That(_character, Is.InstanceOf<ICharacter>());
        }

        [Test]
        public void DieAndRevive_ToggleStateAndCallHooks()
        {
            _character.Die();
            Assert.That(_character.IsAlive, Is.False);
            Assert.That(_character.Deaths, Is.EqualTo(1));

            _character.Revive();
            Assert.That(_character.IsAlive, Is.True);
            Assert.That(_character.Revives, Is.EqualTo(1));
        }

        [Test]
        public void SetEntityId_BindsTheCharacterToAnEntity()
        {
            _character.SetEntityId(42);

            Assert.That(_character.EntityId, Is.EqualTo(42));
        }
    }
}
