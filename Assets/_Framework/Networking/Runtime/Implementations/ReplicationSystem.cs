using System;
using System.Collections.Generic;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Implementation of IReplicationSystem.
    /// Manages entity replication across the network.
    /// </summary>
    public class ReplicationSystem : IReplicationSystem
    {
        private readonly Dictionary<int, INetworkEntity> _entities;
        private readonly List<INetworkEntity> _entitiesReadOnly;
        private float _timeSinceLastReplication;
        private readonly NetworkConfig _config;

        public ReplicationSystem(NetworkConfig config)
        {
            _config = config;
            _entities = new Dictionary<int, INetworkEntity>();
            _entitiesReadOnly = new List<INetworkEntity>();
        }

        public void RegisterEntity(INetworkEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (_entities.ContainsKey(entity.NetworkId))
                throw new InvalidOperationException($"Entity with NetworkId {entity.NetworkId} already registered");

            _entities[entity.NetworkId] = entity;
            _entitiesReadOnly.Add(entity);
        }

        public void UnregisterEntity(int networkId)
        {
            if (_entities.TryGetValue(networkId, out var entity))
            {
                _entities.Remove(networkId);
                _entitiesReadOnly.Remove(entity);
            }
        }

        public INetworkEntity GetEntity(int networkId)
        {
            _entities.TryGetValue(networkId, out var entity);
            return entity;
        }

        public IReadOnlyList<INetworkEntity> GetAllEntities()
        {
            return _entitiesReadOnly;
        }

        public void ReplicateToClients(INetworkManager networkManager, float deltaTime)
        {
            if (networkManager.Role != NetworkRole.Server)
                return;

            _timeSinceLastReplication += deltaTime;
            float replicationInterval = 1f / _config.ReplicationRate;

            if (_timeSinceLastReplication < replicationInterval)
                return;

            _timeSinceLastReplication = 0;

            foreach (var entity in _entities.Values)
            {
                ReplicateEntity(networkManager, entity);
            }
        }

        private void ReplicateEntity(INetworkManager networkManager, INetworkEntity entity)
        {
            // Determine which clients need this entity
            var relevantConnections = DetermineRelevantConnections(networkManager, entity);

            foreach (var connection in relevantConnections)
            {
                SendEntityUpdate(networkManager, connection.ConnectionId, entity);
            }
        }

        private IEnumerable<INetworkConnection> DetermineRelevantConnections(
            INetworkManager networkManager,
            INetworkEntity entity)
        {
            var connections = new List<INetworkConnection>();

            foreach (var connection in networkManager.Connections)
            {
                // Apply replication mode rules
                switch (entity.ReplicationMode)
                {
                    case ReplicationMode.OwnerOnly:
                        if (connection.ConnectionId == entity.OwnerId)
                            connections.Add(connection);
                        break;

                    case ReplicationMode.ExceptOwner:
                        if (connection.ConnectionId != entity.OwnerId)
                            connections.Add(connection);
                        break;

                    case ReplicationMode.FullState:
                    case ReplicationMode.Delta:
                        connections.Add(connection);
                        break;

                    case ReplicationMode.InterestManaged:
                        // TODO: Implement interest management (spatial, etc.)
                        connections.Add(connection);
                        break;
                }
            }

            return connections;
        }

        private void SendEntityUpdate(INetworkManager networkManager, int connectionId, INetworkEntity entity)
        {
            INetworkMessage message;

            if (_config.UseDeltaCompression && entity.ReplicationMode == ReplicationMode.Delta)
            {
                byte[] deltaData = entity.SerializeDelta();
                if (deltaData != null && deltaData.Length > 0)
                {
                    message = new EntityDeltaUpdateMessage
                    {
                        NetworkId = entity.NetworkId,
                        DeltaData = deltaData
                    };
                }
                else
                {
                    return; // No changes to replicate
                }
            }
            else
            {
                byte[] stateData = entity.SerializeState();
                message = new EntityStateUpdateMessage
                {
                    NetworkId = entity.NetworkId,
                    StateData = stateData
                };
            }

            networkManager.Send(connectionId, message, reliable: true);
        }

        public void ApplyReplication(int networkId, byte[] stateData, bool isDelta)
        {
            if (!_entities.TryGetValue(networkId, out var entity))
                return;

            if (isDelta)
            {
                entity.ApplyDelta(stateData);
            }
            else
            {
                entity.DeserializeState(stateData);
            }
        }

        public void SpawnEntity(int networkId, int ownerId, string prefabId, byte[] initialState)
        {
            // This is typically handled by game-specific logic
            // The framework just sends the spawn message
            // Game code will instantiate the actual entity and register it
        }

        public void DespawnEntity(int networkId)
        {
            UnregisterEntity(networkId);
        }
    }
}
