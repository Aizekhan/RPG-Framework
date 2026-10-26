using System;
using System.IO;
using NUnit.Framework;
using RPGFramework.Serialization;

namespace RPGFramework.DeltaCompression.Tests
{
    /// <summary>
    /// A sender and a receiver, each with its own <see cref="DeltaSerializer"/>: whatever the state does (stays, changes
    /// a little, grows, shrinks), the receiver rebuilds it exactly, and a small change costs far less than the state.
    /// A delta built on a state the receiver never got (a lost packet) is refused instead of producing a wrong state,
    /// and a broken packet is refused with a clear error.
    /// </summary>
    public sealed class DeltaSerializerTests
    {
        private sealed class Blob : ISerializable
        {
            public byte[] Bytes = Array.Empty<byte>();

            public Blob()
            {
            }

            public Blob(byte[] bytes) => Bytes = (byte[])bytes.Clone();

            public byte[] Serialize() => (byte[])Bytes.Clone();

            public void Deserialize(byte[] data) => Bytes = (byte[])data.Clone();
        }

        private const int Entity = 7;

        private DeltaSerializer _sender;
        private DeltaSerializer _receiver;

        [SetUp]
        public void SetUp()
        {
            _sender = new DeltaSerializer();
            _receiver = new DeltaSerializer();
        }

        private byte[] Send(byte[] state, int entity = Entity) => _sender.CreateDelta(entity, new Blob(state));

        private byte[] Receive(byte[] packet, int entity = Entity) => _receiver.ApplyDelta<Blob>(entity, packet).Bytes;

        private static byte[] Pattern(int length, int seed = 0)
        {
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
                bytes[i] = (byte)((i * 31 + seed) % 251);
            return bytes;
        }

        [Test]
        public void TheFirstUpdate_RebuildsTheStateExactly()
        {
            var state = Pattern(300);

            Assert.That(Receive(Send(state)), Is.EqualTo(state));
        }

        [Test]
        public void ASmallChange_CostsFarLessThanTheState_AndRebuildsExactly()
        {
            var state = Pattern(1000);
            Receive(Send(state));

            // A position (a float) and a health value changed
            state[100] ^= 0x11;
            state[101] ^= 0x22;
            state[102] ^= 0x33;
            state[600] ^= 0x44;
            var packet = Send(state);

            Assert.That(packet.Length, Is.LessThan(100), "a few changed bytes of a thousand");
            Assert.That(Receive(packet), Is.EqualTo(state));
        }

        [Test]
        public void AnUnchangedState_CostsAlmostNothing()
        {
            var state = Pattern(1000);
            Receive(Send(state));

            var packet = Send(state);

            Assert.That(packet.Length, Is.LessThan(20));
            Assert.That(Receive(packet), Is.EqualTo(state));
        }

        [Test]
        public void AStateThatGrows_RebuildsExactly()
        {
            var state = Pattern(200);
            Receive(Send(state));

            var grown = new byte[220]; // an item was added to the inventory
            Array.Copy(state, grown, state.Length);
            for (int i = 200; i < grown.Length; i++)
                grown[i] = (byte)(i + 1);

            Assert.That(Receive(Send(grown)), Is.EqualTo(grown));
        }

        [Test]
        public void AStateThatShrinks_RebuildsExactly()
        {
            var state = Pattern(200);
            Receive(Send(state));

            var shrunk = new byte[180]; // an item was removed
            Array.Copy(state, shrunk, shrunk.Length);
            shrunk[5] ^= 0xFF;

            Assert.That(Receive(Send(shrunk)), Is.EqualTo(shrunk));
        }

        [Test]
        public void AStateThatChangesCompletely_StillRebuildsExactly()
        {
            Receive(Send(Pattern(100, seed: 1)));
            var other = Pattern(130, seed: 2);

            Assert.That(Receive(Send(other)), Is.EqualTo(other));
        }

        [Test]
        public void ADeltaBuiltOnAStateTheReceiverNeverGot_IsRefused_NotMisapplied()
        {
            var a = new byte[64];
            Receive(Send(a));

            var b = (byte[])a.Clone();
            b[0] = 1;
            Send(b); // lost on the way

            var c = (byte[])b.Clone();
            c[1] = 2;
            var packet = Send(c);

            var error = Assert.Throws<InvalidOperationException>(() => Receive(packet));
            Assert.That(error.Message, Does.Contain("full state"));

            // The receiver keeps what it had; the sender starts over with a full state and both agree again
            _sender.ClearState(Entity);
            Assert.That(Receive(Send(c)), Is.EqualTo(c));
            var d = (byte[])c.Clone();
            d[2] = 3;
            Assert.That(Receive(Send(d)), Is.EqualTo(d));
        }

        [Test]
        public void ADeltaWithNoStateToBuildOn_IsRefused()
        {
            var state = Pattern(50);
            Receive(Send(state));
            state[0] ^= 1;
            var packet = Send(state);
            _receiver.ClearAllStates();

            Assert.Throws<InvalidOperationException>(() => Receive(packet));
        }

        [Test]
        public void BrokenPackets_AreRefusedWithAClearError()
        {
            var full = Send(Pattern(100));
            Receive(full);
            var state = Pattern(100);
            state[10] ^= 1;
            var delta = Send(state);

            Assert.Throws<InvalidDataException>(() => Receive(Array.Empty<byte>()));
            Assert.Throws<InvalidDataException>(() => Receive(new byte[] { 9, 0, 0, 0 }), "an unknown kind");
            Assert.Throws<InvalidDataException>(() => Receive(Cut(full, 20)), "a cut full state");
            Assert.Throws<InvalidDataException>(() => Receive(Cut(delta, delta.Length - 1)), "a cut delta");

            // Still in step afterwards
            Assert.That(Receive(delta), Is.EqualTo(state));
        }

        [Test]
        public void Entities_AreTrackedSeparately()
        {
            var first = Pattern(80, seed: 1);
            var second = Pattern(80, seed: 2);
            Receive(Send(first, 1), 1);
            Receive(Send(second, 2), 2);

            first[3] ^= 0x0F;
            second[70] ^= 0xF0;

            Assert.That(Receive(Send(second, 2), 2), Is.EqualTo(second));
            Assert.That(Receive(Send(first, 1), 1), Is.EqualTo(first));
        }

        [Test]
        public void AnEmptyState_RoundTrips()
        {
            Receive(Send(Pattern(10)));

            Assert.That(Receive(Send(Array.Empty<byte>())), Is.Empty);
            Assert.That(Receive(Send(Pattern(10))), Is.EqualTo(Pattern(10)));
        }

        private static byte[] Cut(byte[] packet, int length)
        {
            var cut = new byte[length];
            Array.Copy(packet, cut, length);
            return cut;
        }
    }
}
