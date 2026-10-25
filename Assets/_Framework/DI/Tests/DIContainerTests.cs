using NUnit.Framework;
using RPGFramework.DI;
using System;

namespace RPGFramework.DI.Tests
{
    [TestFixture]
    public class DIContainerTests
    {
        private DIContainer _container;

        [SetUp]
        public void Setup()
        {
            _container = new DIContainer();
        }

        [TearDown]
        public void TearDown()
        {
            _container = null;
        }

        #region Basic Registration and Resolution

        [Test]
        public void RegisterAndResolve_Transient_CreatesNewInstanceEachTime()
        {
            // Arrange
            _container.RegisterTransient<ITestService, TestService>();

            // Act
            var instance1 = _container.Resolve<ITestService>();
            var instance2 = _container.Resolve<ITestService>();

            // Assert
            Assert.IsNotNull(instance1);
            Assert.IsNotNull(instance2);
            Assert.AreNotSame(instance1, instance2);
        }

        [Test]
        public void RegisterAndResolve_Singleton_ReturnsSameInstance()
        {
            // Arrange
            _container.RegisterSingleton<ITestService, TestService>();

            // Act
            var instance1 = _container.Resolve<ITestService>();
            var instance2 = _container.Resolve<ITestService>();

            // Assert
            Assert.IsNotNull(instance1);
            Assert.IsNotNull(instance2);
            Assert.AreSame(instance1, instance2);
        }

        [Test]
        public void RegisterInstance_ReturnsRegisteredInstance()
        {
            // Arrange
            var expectedInstance = new TestService();
            _container.RegisterInstance<ITestService>(expectedInstance);

            // Act
            var resolvedInstance = _container.Resolve<ITestService>();

            // Assert
            Assert.AreSame(expectedInstance, resolvedInstance);
        }

        [Test]
        public void Resolve_UnregisteredType_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
            {
                _container.Resolve<ITestService>();
            });
        }

        [Test]
        public void IsRegistered_RegisteredType_ReturnsTrue()
        {
            // Arrange
            _container.RegisterSingleton<ITestService, TestService>();

            // Act
            bool isRegistered = _container.IsRegistered<ITestService>();

            // Assert
            Assert.IsTrue(isRegistered);
        }

        [Test]
        public void IsRegistered_UnregisteredType_ReturnsFalse()
        {
            // Act
            bool isRegistered = _container.IsRegistered<ITestService>();

            // Assert
            Assert.IsFalse(isRegistered);
        }

        #endregion

        #region Constructor Injection

        [Test]
        public void Resolve_WithConstructorDependency_InjectsDependency()
        {
            // Arrange
            _container.RegisterSingleton<ITestService, TestService>();
            _container.RegisterSingleton<ITestServiceWithDependency, TestServiceWithDependency>();

            // Act
            var service = _container.Resolve<ITestServiceWithDependency>();

            // Assert
            Assert.IsNotNull(service);
            Assert.IsNotNull(service.Dependency);
        }

        #endregion

        #region Field Injection

        [Test]
        public void InjectDependencies_InjectsFieldWithAttribute()
        {
            // Arrange
            _container.RegisterSingleton<ITestService, TestService>();
            var target = new TestClassWithFieldInjection();

            // Act
            _container.InjectDependencies(target);

            // Assert
            Assert.IsNotNull(target.Service);
        }

        #endregion

        #region Property Injection

        [Test]
        public void InjectDependencies_InjectsPropertyWithAttribute()
        {
            // Arrange
            _container.RegisterSingleton<ITestService, TestService>();
            var target = new TestClassWithPropertyInjection();

            // Act
            _container.InjectDependencies(target);

            // Assert
            Assert.IsNotNull(target.Service);
        }

        #endregion

        #region Scoped Dependencies

        [Test]
        public void RegisterAndResolve_Scoped_SameInstanceWithinScope()
        {
            // Arrange
            _container.RegisterScoped<ITestService, TestService>();
            using (var scope = _container.CreateScope())
            {
                _container.SetCurrentScope(scope);

                // Act
                var instance1 = _container.Resolve<ITestService>();
                var instance2 = _container.Resolve<ITestService>();

                // Assert
                Assert.IsNotNull(instance1);
                Assert.IsNotNull(instance2);
                Assert.AreSame(instance1, instance2);
            }
        }

        [Test]
        public void RegisterAndResolve_Scoped_DifferentInstancesInDifferentScopes()
        {
            // Arrange
            _container.RegisterScoped<ITestService, TestService>();

            ITestService instance1;
            ITestService instance2;

            // Act
            using (var scope1 = _container.CreateScope())
            {
                _container.SetCurrentScope(scope1);
                instance1 = _container.Resolve<ITestService>();
            }

            using (var scope2 = _container.CreateScope())
            {
                _container.SetCurrentScope(scope2);
                instance2 = _container.Resolve<ITestService>();
            }

            // Assert
            Assert.IsNotNull(instance1);
            Assert.IsNotNull(instance2);
            Assert.AreNotSame(instance1, instance2);
        }

        #endregion

        #region Lazy Dependencies

        [Test]
        public void RegisterLazySingleton_DoesNotCreateInstanceImmediately()
        {
            // Arrange
            _container.RegisterLazySingleton<ITestService, TestService>();

            // Act
            var lazy = _container.ResolveLazy<ITestService>();

            // Assert
            Assert.IsNotNull(lazy);
            Assert.IsFalse(lazy.IsValueCreated);
        }

        [Test]
        public void RegisterLazySingleton_CreatesInstanceOnFirstAccess()
        {
            // Arrange
            _container.RegisterLazySingleton<ITestService, TestService>();

            // Act
            var lazy = _container.ResolveLazy<ITestService>();
            var instance = lazy.Value;

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsTrue(lazy.IsValueCreated);
        }

        #endregion

        #region Test Classes

        public interface ITestService
        {
            void DoSomething();
        }

        public class TestService : ITestService
        {
            public void DoSomething() { }
        }

        public interface ITestServiceWithDependency
        {
            ITestService Dependency { get; }
        }

        public class TestServiceWithDependency : ITestServiceWithDependency
        {
            public ITestService Dependency { get; }

            public TestServiceWithDependency(ITestService dependency)
            {
                Dependency = dependency;
            }
        }

        public class TestClassWithFieldInjection
        {
            [Inject]
            public ITestService Service;
        }

        public class TestClassWithPropertyInjection
        {
            [Inject]
            public ITestService Service { get; set; }
        }

        #endregion
    }
}
