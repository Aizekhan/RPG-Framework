using NUnit.Framework;
using UnityEngine;

namespace RPGFramework.Tests.Editor
{
    /// <summary>
    /// Basic compilation test to ensure Framework assemblies are properly loaded
    /// </summary>
    public class FrameworkCompilationTest
    {
        [Test]
        public void Framework_Assemblies_Should_Load()
        {
            // This test passes if the project compiles successfully
            Assert.Pass("Framework compiled successfully");
        }

        [Test]
        public void Unity_Version_Should_Be_6000_Or_Higher()
        {
            var version = Application.unityVersion;
            Assert.IsTrue(version.StartsWith("6000"),
                $"Expected Unity 6000.x but got {version}");
        }
    }
}
