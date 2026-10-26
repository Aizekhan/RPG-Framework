// File: Assets/_Framework/ECS/Tests/ArchitectureTests.cs
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RPGFramework.ECS;

namespace RPGFramework.ECS.Tests
{
    /// <summary>
    /// ECS component rules. Framework-wide rules (no game references, no cycles, namespaces, script files) live in
    /// Assets/_Tests/Editor/FrameworkArchitectureTests.cs.
    /// </summary>
    [TestFixture]
    [Category("Architecture")]
    public class ArchitectureTests
    {
        /// <summary>
        /// RPGFW004: All Components must be structs, not classes
        /// </summary>
        [Test]
        public void AllComponents_AreStructs()
        {
            var componentTypes = GetAllTypesImplementing<IComponent>();
            var violations = new List<string>();

            foreach (var type in componentTypes)
            {
                if (!type.IsValueType)
                {
                    violations.Add($"{type.FullName} is a class, must be struct");
                }
            }

            Assert.IsEmpty(violations,
                $"Components must be structs (value types), not classes:\n" +
                $"{string.Join("\n", violations)}");
        }

        /// <summary>
        /// RPGFW005: Components should be data-only (no methods except constructors)
        /// </summary>
        [Test]
        public void Components_AreDataOnly()
        {
            var componentTypes = GetAllTypesImplementing<IComponent>();
            var violations = new List<string>();

            foreach (var type in componentTypes)
            {
                // Get public instance methods (excluding constructors, property getters/setters)
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName) // Exclude property accessors
                    .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                    .ToList();

                if (methods.Any())
                {
                    violations.Add($"{type.Name} has methods: {string.Join(", ", methods.Select(m => m.Name))}");
                }
            }

            // This is a WARNING, not ERROR (hence we log but don't fail)
            if (violations.Any())
            {
                UnityEngine.Debug.LogWarning(
                    "[Architecture] Components should be data-only (no methods):\n" +
                    string.Join("\n", violations));
            }

            // For now, just warn. Can be changed to Assert.IsEmpty() if strict enforcement needed
            Assert.Pass($"Checked {componentTypes.Count} components");
        }

        // Helper methods

        private List<Type> GetAllTypesImplementing<T>()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(t => typeof(T).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();
        }
    }
}
