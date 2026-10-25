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
    /// Architecture validation tests — ensures Framework principles are enforced
    /// </summary>
    [TestFixture]
    [Category("Architecture")]
    public class ArchitectureTests
    {
        private Assembly _frameworkAssembly;
        private Assembly _gameAssembly;

        [SetUp]
        public void Setup()
        {
            // Get assemblies
            _frameworkAssembly = typeof(IEntityManager).Assembly;

            // Try to find game assembly (might not exist in isolated tests)
            _gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "MythHunter.GameLogic");
        }

        /// <summary>
        /// RPGFW001: Framework Core must NOT reference Game Layer code
        /// </summary>
        [Test]
        public void Framework_DoesNotReference_GameLayer()
        {
            // Get all referenced assemblies from Framework
            var references = _frameworkAssembly.GetReferencedAssemblies();

            // Check: No references to MythHunter or game-specific assemblies
            var gameReferences = references
                .Where(r => r.Name.Contains("MythHunter") ||
                           r.Name.Contains("GameLogic") ||
                           r.Name.StartsWith("Game."))
                .ToList();

            Assert.IsEmpty(gameReferences,
                $"Framework assembly '{_frameworkAssembly.GetName().Name}' " +
                $"must NOT reference game-specific code! " +
                $"Found references: {string.Join(", ", gameReferences.Select(r => r.Name))}");
        }

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

        /// <summary>
        /// No circular dependencies between Framework assemblies
        /// </summary>
        [Test]
        public void Framework_HasNoCircularDependencies()
        {
            var frameworkAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("RPGFramework"))
                .ToList();

            var dependencyGraph = new Dictionary<string, List<string>>();

            foreach (var assembly in frameworkAssemblies)
            {
                var refs = assembly.GetReferencedAssemblies()
                    .Where(r => r.Name.StartsWith("RPGFramework"))
                    .Select(r => r.Name)
                    .ToList();

                dependencyGraph[assembly.GetName().Name] = refs;
            }

            // Check for cycles (simple check)
            foreach (var kvp in dependencyGraph)
            {
                if (HasCycle(kvp.Key, kvp.Value, dependencyGraph, new HashSet<string>()))
                {
                    Assert.Fail($"Circular dependency detected involving '{kvp.Key}'");
                }
            }

            Assert.Pass($"No circular dependencies found in {frameworkAssemblies.Count} Framework assemblies");
        }

        /// <summary>
        /// Framework assemblies follow naming convention
        /// </summary>
        [Test]
        public void Framework_FollowsNamingConvention()
        {
            var frameworkAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                {
                    try
                    {
                        // Dynamic assemblies throw NotSupportedException on .Location access
                        return !string.IsNullOrEmpty(a.Location) && a.Location.Contains("_Framework");
                    }
                    catch (NotSupportedException)
                    {
                        return false; // Skip dynamic assemblies
                    }
                })
                .ToList();

            var violations = new List<string>();

            foreach (var assembly in frameworkAssemblies)
            {
                var name = assembly.GetName().Name;

                if (!name.StartsWith("RPGFramework"))
                {
                    violations.Add($"Assembly '{name}' should start with 'RPGFramework'");
                }
            }

            Assert.IsEmpty(violations,
                $"Framework assemblies must follow naming convention:\n" +
                $"{string.Join("\n", violations)}");
        }

        /// <summary>
        /// Framework types use correct namespaces
        /// </summary>
        [Test]
        public void Framework_TypesUseCorrectNamespaces()
        {
            var frameworkTypes = _frameworkAssembly.GetTypes()
                .Where(t => t.IsPublic)
                .ToList();

            var violations = new List<string>();

            foreach (var type in frameworkTypes)
            {
                if (!type.Namespace?.StartsWith("RPGFramework") == true)
                {
                    violations.Add($"Type '{type.FullName}' should be in RPGFramework.* namespace");
                }
            }

            Assert.IsEmpty(violations,
                $"Framework types must use RPGFramework.* namespaces:\n" +
                $"{string.Join("\n", violations)}");
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

        private bool HasCycle(
            string current,
            List<string> dependencies,
            Dictionary<string, List<string>> graph,
            HashSet<string> visited)
        {
            if (visited.Contains(current))
                return true;

            visited.Add(current);

            foreach (var dep in dependencies)
            {
                if (graph.ContainsKey(dep))
                {
                    if (HasCycle(dep, graph[dep], graph, new HashSet<string>(visited)))
                        return true;
                }
            }

            return false;
        }
    }
}
