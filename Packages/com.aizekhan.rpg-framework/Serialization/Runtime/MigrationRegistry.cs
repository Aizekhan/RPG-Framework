using System;
using System.Collections.Generic;

namespace RPGFramework.Serialization
{
    /// <summary>
    /// Default implementation of IMigrationRegistry.
    /// Thread-safe registry for version migrations.
    /// </summary>
    public class MigrationRegistry : IMigrationRegistry
    {
        private readonly Dictionary<string, object> _migrations = new Dictionary<string, object>();
        private readonly object _lock = new object();

        public void RegisterMigration<TFrom, TTo>(string typeId, ushort fromVersion, ushort toVersion, IMigrator<TFrom, TTo> migrator)
            where TFrom : ISerializable
            where TTo : ISerializable
        {
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("TypeId cannot be null or empty", nameof(typeId));
            if (migrator == null)
                throw new ArgumentNullException(nameof(migrator));
            if (toVersion <= fromVersion)
                throw new ArgumentException("Target version must be greater than source version");

            var key = GetMigrationKey(typeId, fromVersion, toVersion);

            lock (_lock)
            {
                _migrations[key] = migrator;
            }
        }

        public byte[] Migrate(string typeId, byte[] data, ushort fromVersion, ushort toVersion)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("TypeId cannot be null or empty", nameof(typeId));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (toVersion <= fromVersion)
                throw new ArgumentException("Target version must be greater than source version");

            var key = GetMigrationKey(typeId, fromVersion, toVersion);

            lock (_lock)
            {
                if (!_migrations.TryGetValue(key, out var migratorObj))
                {
                    throw new InvalidOperationException(
                        $"No migration registered for {typeId} from version {fromVersion} to {toVersion}");
                }

                // Migration is stored as IMigrator<TFrom, TTo>, but we work with byte[] here
                // The actual migration happens through ISerializable interface
                var migratorType = migratorObj.GetType();
                var migrateMethod = migratorType.GetMethod("Migrate");

                if (migrateMethod == null)
                {
                    throw new InvalidOperationException($"Migrator for {typeId} does not have Migrate method");
                }

                // Get TFrom and TTo types from the IMigrator<,> interface
                var migratorInterface = migratorType.GetInterface(typeof(IMigrator<,>).Name);
                if (migratorInterface == null)
                {
                    throw new InvalidOperationException($"Migrator for {typeId} does not implement IMigrator interface");
                }

                var genericArgs = migratorInterface.GetGenericArguments();
                if (genericArgs.Length != 2)
                {
                    throw new InvalidOperationException($"Invalid migrator type for {typeId}");
                }

                var fromType = genericArgs[0];
                var toType = genericArgs[1];

                // Deserialize old data
                var oldInstance = (ISerializable)Activator.CreateInstance(fromType);
                oldInstance.Deserialize(data);

                // Perform migration
                var newInstance = migrateMethod.Invoke(migratorObj, new object[] { oldInstance });

                // Serialize new data
                return ((ISerializable)newInstance).Serialize();
            }
        }

        public bool HasMigration(string typeId, ushort fromVersion, ushort toVersion)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                return false;

            var key = GetMigrationKey(typeId, fromVersion, toVersion);

            lock (_lock)
            {
                return _migrations.ContainsKey(key);
            }
        }

        private static string GetMigrationKey(string typeId, ushort fromVersion, ushort toVersion)
        {
            return $"{typeId}:{fromVersion}->{toVersion}";
        }
    }
}
