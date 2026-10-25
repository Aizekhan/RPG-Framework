using System;
using System.Collections.Generic;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Manages replication of network entities across the network.
    /// </summary>
    public interface IReplicationSystem
    {
        /// <summary>
        /// Register a network entity for replication.
        /// </summary>
        void RegisterEntity(INetworkEntity entity);

        /// <summary>
        /// Unregister a network entity.
        /// </summary>
        void UnregisterEntity(int networkId);

        /// <summary>
        /// Get entity by network ID.
        /// </summary>
        INetworkEntity GetEntity(int networkId);

        /// <summary>
        /// Get all registered entities.
        /// </summary>
        IReadOnlyList<INetworkEntity> GetAllEntities();

        /// <summary>
        /// Replicate all entities to clients (server only).
        /// Called periodically by the network manager.
        /// </summary>
        void ReplicateToClients(INetworkManager networkManager, float deltaTime);

        /// <summary>
        /// Apply a replication update from server (client only).
        /// </summary>
        void ApplyReplication(int networkId, byte[] stateData, bool isDelta);

        /// <summary>
        /// Spawn a network entity on all clients.
        /// </summary>
        void SpawnEntity(int networkId, int ownerId, string prefabId, byte[] initialState);

        /// <summary>
        /// Despawn a network entity on all clients.
        /// </summary>
        void DespawnEntity(int networkId);
    }
}
