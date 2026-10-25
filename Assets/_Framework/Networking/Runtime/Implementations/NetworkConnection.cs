using System;
using System.Collections.Generic;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Implementation of INetworkConnection.
    /// </summary>
    public class NetworkConnection : INetworkConnection
    {
        private readonly INetworkTransport _transport;
        private readonly INetworkSerializer _serializer;
        private readonly List<float> _rttSamples;
        private readonly int _maxRttSamples = 10;
        private long _lastPingTime;
        private int _packetsSent;
        private int _packetsReceived;
        private int _packetsLost;

        public int ConnectionId { get; }
        public ConnectionState State { get; private set; }
        public string RemoteAddress { get; }
        public float AverageRtt { get; private set; }
        public float PacketLoss { get; private set; }

        public NetworkConnection(
            int connectionId,
            string remoteAddress,
            INetworkTransport transport,
            INetworkSerializer serializer)
        {
            ConnectionId = connectionId;
            RemoteAddress = remoteAddress;
            _transport = transport;
            _serializer = serializer;
            _rttSamples = new List<float>();
            State = ConnectionState.Connected;
        }

        public void Send(INetworkMessage message, bool reliable = true)
        {
            if (State != ConnectionState.Connected)
                return;

            byte[] data = _serializer.Serialize(message);
            _transport.Send(ConnectionId, data, reliable);
            _packetsSent++;
        }

        public void Close()
        {
            State = ConnectionState.Disconnecting;
        }

        public void UpdateRtt(float rtt)
        {
            _rttSamples.Add(rtt);
            if (_rttSamples.Count > _maxRttSamples)
                _rttSamples.RemoveAt(0);

            float sum = 0;
            foreach (float sample in _rttSamples)
                sum += sample;
            AverageRtt = sum / _rttSamples.Count;
        }

        public void RecordPacketReceived()
        {
            _packetsReceived++;
            UpdatePacketLoss();
        }

        public void RecordPacketLost()
        {
            _packetsLost++;
            UpdatePacketLoss();
        }

        private void UpdatePacketLoss()
        {
            int totalPackets = _packetsSent + _packetsReceived;
            if (totalPackets > 0)
                PacketLoss = (_packetsLost / (float)totalPackets) * 100f;
        }

        public void SetState(ConnectionState state)
        {
            State = state;
        }
    }
}
