using System;

namespace RPGFramework.DI
{
    /// <summary>
    /// Attribute for marking fields, properties, constructors, and methods for dependency injection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Constructor | AttributeTargets.Method)]
    public class InjectAttribute : Attribute
    {
    }
}
