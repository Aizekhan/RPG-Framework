using System;
using System.Collections.Generic;

namespace RPGFramework.Serialization
{
    /// <summary>
    /// Default implementation of ITypeRegistry.
    /// Thread-safe registry for stable type identification.
    /// </summary>
    public class TypeRegistry : ITypeRegistry
    {
        private readonly Dictionary<string, Dictionary<ushort, Type>> _typeIdToType = new Dictionary<string, Dictionary<ushort, Type>>();
        private readonly Dictionary<Type, TypeInfo> _typeToInfo = new Dictionary<Type, TypeInfo>();
        private readonly object _lock = new object();

        private struct TypeInfo
        {
            public string TypeId;
            public ushort Version;
        }

        public void RegisterType<T>(string typeId, ushort version = 1)
        {
            RegisterType(typeof(T), typeId, version);
        }

        public void RegisterType<T>()
        {
            var type = typeof(T);
            var attr = (SerializableTypeAttribute)Attribute.GetCustomAttribute(type, typeof(SerializableTypeAttribute));

            if (attr == null)
                throw new InvalidOperationException($"Type {type.FullName} does not have SerializableTypeAttribute");

            RegisterType(type, attr.TypeId, attr.Version);
        }

        private void RegisterType(Type type, string typeId, ushort version)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("TypeId cannot be null or empty", nameof(typeId));

            lock (_lock)
            {
                // Register typeId -> type mapping
                if (!_typeIdToType.TryGetValue(typeId, out var versionMap))
                {
                    versionMap = new Dictionary<ushort, Type>();
                    _typeIdToType[typeId] = versionMap;
                }

                if (versionMap.ContainsKey(version))
                {
                    var existing = versionMap[version];
                    if (existing != type)
                        throw new InvalidOperationException($"TypeId '{typeId}' version {version} is already registered to {existing.FullName}");
                    return; // Already registered
                }

                versionMap[version] = type;

                // Register type -> typeId mapping
                _typeToInfo[type] = new TypeInfo { TypeId = typeId, Version = version };
            }
        }

        public Type GetType(string typeId, ushort version)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("TypeId cannot be null or empty", nameof(typeId));

            lock (_lock)
            {
                if (_typeIdToType.TryGetValue(typeId, out var versionMap))
                {
                    if (versionMap.TryGetValue(version, out var type))
                        return type;
                }
            }

            throw new InvalidOperationException($"Type '{typeId}' version {version} is not registered");
        }

        public Type GetType(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("TypeId cannot be null or empty", nameof(typeId));

            lock (_lock)
            {
                if (_typeIdToType.TryGetValue(typeId, out var versionMap))
                {
                    // Get latest version
                    ushort maxVersion = 0;
                    Type latestType = null;

                    foreach (var kvp in versionMap)
                    {
                        if (kvp.Key > maxVersion)
                        {
                            maxVersion = kvp.Key;
                            latestType = kvp.Value;
                        }
                    }

                    if (latestType != null)
                        return latestType;
                }
            }

            throw new InvalidOperationException($"Type '{typeId}' is not registered");
        }

        public string GetTypeId<T>()
        {
            return GetTypeId(typeof(T));
        }

        public string GetTypeId(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            lock (_lock)
            {
                if (_typeToInfo.TryGetValue(type, out var info))
                    return info.TypeId;
            }

            throw new InvalidOperationException($"Type {type.FullName} is not registered");
        }

        public ushort GetVersion<T>()
        {
            return GetVersion(typeof(T));
        }

        public ushort GetVersion(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            lock (_lock)
            {
                if (_typeToInfo.TryGetValue(type, out var info))
                    return info.Version;
            }

            throw new InvalidOperationException($"Type {type.FullName} is not registered");
        }

        public bool IsRegistered(string typeId, ushort version)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                return false;

            lock (_lock)
            {
                if (_typeIdToType.TryGetValue(typeId, out var versionMap))
                    return versionMap.ContainsKey(version);
            }

            return false;
        }

        public bool IsRegistered<T>()
        {
            lock (_lock)
            {
                return _typeToInfo.ContainsKey(typeof(T));
            }
        }
    }
}
