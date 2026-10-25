using System;

namespace RPGFramework.Serialization
{
    /// <summary>
    /// Attribute for stable type identification across assemblies and refactoring.
    /// Use this instead of Type.FullName to avoid fragile string-based type resolution.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class SerializableTypeAttribute : Attribute
    {
        /// <summary>
        /// Stable type identifier (e.g., "rpg.component.health.v1").
        /// Must be unique across the application.
        /// </summary>
        public string TypeId { get; }

        /// <summary>
        /// Schema version for this type.
        /// Increment when breaking changes occur.
        /// </summary>
        public ushort Version { get; }

        public SerializableTypeAttribute(string typeId, ushort version = 1)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("TypeId cannot be null or empty", nameof(typeId));

            TypeId = typeId;
            Version = version;
        }
    }
}
