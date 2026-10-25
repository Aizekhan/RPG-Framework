using System;

namespace RPGFramework.Serialization
{
    /// <summary>
    /// Registry for mapping stable type IDs to CLR types.
    /// Enables polymorphic deserialization without Type.FullName fragility.
    /// </summary>
    public interface ITypeRegistry
    {
        /// <summary>
        /// Registers a type with its stable type ID.
        /// </summary>
        void RegisterType<T>(string typeId, ushort version = 1);

        /// <summary>
        /// Registers a type using its SerializableTypeAttribute.
        /// </summary>
        void RegisterType<T>();

        /// <summary>
        /// Gets the CLR type for a given type ID and version.
        /// </summary>
        Type GetType(string typeId, ushort version);

        /// <summary>
        /// Gets the CLR type for a given type ID (latest version).
        /// </summary>
        Type GetType(string typeId);

        /// <summary>
        /// Gets the stable type ID for a given CLR type.
        /// </summary>
        string GetTypeId<T>();

        /// <summary>
        /// Gets the stable type ID for a given CLR type.
        /// </summary>
        string GetTypeId(Type type);

        /// <summary>
        /// Gets the version for a given CLR type.
        /// </summary>
        ushort GetVersion<T>();

        /// <summary>
        /// Gets the version for a given CLR type.
        /// </summary>
        ushort GetVersion(Type type);

        /// <summary>
        /// Checks if a type ID is registered.
        /// </summary>
        bool IsRegistered(string typeId, ushort version);

        /// <summary>
        /// Checks if a CLR type is registered.
        /// </summary>
        bool IsRegistered<T>();
    }
}
