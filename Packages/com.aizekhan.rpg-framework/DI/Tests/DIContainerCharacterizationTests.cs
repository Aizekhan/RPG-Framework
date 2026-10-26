using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace RPGFramework.DI.Tests
{
    /// <summary>
    /// Behaviour contract of DIContainer/DIScope, ported from MythHunter's reflection-based
    /// characterization suite (Tests/DI/DIContainerCharacterizationTests.cs).
    /// </summary>
    public sealed class DIContainerCharacterizationTests
    {
        [Test]
        public void ScopedRegistration_CachesByServiceTypeWithinScope()
        {
            var container = new DIContainer();
            container.RegisterScoped<ITestService, TestService>();

            Assert.That(container.Resolve<ITestService>(), Is.SameAs(container.Resolve<ITestService>()));
        }

        [Test]
        public void ResolveByType_ReturnsResolvedServiceInsteadOfRegistrationMetadata()
        {
            var container = new DIContainer();
            container.RegisterTransient<ITestService, TestService>();

            Assert.That(container.Resolve(typeof(ITestService)), Is.InstanceOf<ITestService>());
        }

        [Test]
        public void TypeBasedRegistrationCheck_IncludesRegisteredInstances()
        {
            var container = new DIContainer();
            var instance = new TestService();
            container.RegisterInstance<ITestService>(instance);

            Assert.That(container.IsRegistered(typeof(ITestService)), Is.True);
            Assert.That(container.Resolve(typeof(ITestService)), Is.SameAs(instance));
        }

        [Test]
        public void ScopeDisposal_DisposesScopedServiceExactlyOnce()
        {
            ScopedDisposableService.DisposeCount = 0;
            var container = new DIContainer();
            container.RegisterScoped<IScopedDisposableService, ScopedDisposableService>();

            var scope = container.CreateScope("disposable");
            container.SetCurrentScope(scope);
            var first = container.Resolve<IScopedDisposableService>();
            Assert.That(container.Resolve<IScopedDisposableService>(), Is.SameAs(first));

            scope.Dispose();

            Assert.That(ScopedDisposableService.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void LazySingleton_ResolveLazyAndResolveShareOneInstance()
        {
            var container = new DIContainer();
            container.RegisterLazySingleton<ITestService, TestService>();

            var lazy = container.ResolveLazy<ITestService>();

            Assert.That(lazy.Value, Is.SameAs(lazy.Value));
            Assert.That(container.Resolve<ITestService>(), Is.SameAs(lazy.Value));
        }

        [Test]
        public void SetCurrentScope_OnWorkerThread_DoesNotReplaceMainThreadScope()
        {
            var container = new DIContainer();
            container.RegisterScoped<ITestService, TestService>();
            container.SetCurrentScope(container.CreateScope("main"));
            var mainInstance = container.Resolve<ITestService>();

            ITestService workerInstance = null;
            ITestService workerSecond = null;
            Exception workerError = null;

            var worker = new Thread(() =>
            {
                try
                {
                    container.SetCurrentScope(container.CreateScope("worker"));
                    workerInstance = container.Resolve<ITestService>();
                    workerSecond = container.Resolve<ITestService>();
                }
                catch (Exception exception)
                {
                    workerError = exception;
                }
            });
            worker.Start();
            worker.Join();

            Assert.That(workerError, Is.Null);
            Assert.That(workerInstance, Is.Not.Null);
            Assert.That(workerSecond, Is.SameAs(workerInstance));
            Assert.That(workerInstance, Is.Not.SameAs(mainInstance));
            Assert.That(container.Resolve<ITestService>(), Is.SameAs(mainInstance));
        }

        [Test]
        public void ChildScope_HasIndependentScopedInstance_AndDoesNotDisposeParentInstance()
        {
            ScopedDisposableService.DisposeCount = 0;
            var container = new DIContainer();
            container.RegisterScoped<IScopedDisposableService, ScopedDisposableService>();

            var parentScope = container.CreateScope("parent");
            container.SetCurrentScope(parentScope);
            var parentInstance = (ScopedDisposableService)container.Resolve<IScopedDisposableService>();

            var childScope = parentScope.CreateChildScope("child");
            container.SetCurrentScope(childScope);
            var childInstance = (ScopedDisposableService)container.Resolve<IScopedDisposableService>();
            Assert.That(childInstance, Is.Not.SameAs(parentInstance));

            childScope.Dispose();
            Assert.That(childInstance.IsDisposed, Is.True);
            Assert.That(parentInstance.IsDisposed, Is.False);
            Assert.That(ScopedDisposableService.DisposeCount, Is.EqualTo(1));

            container.SetCurrentScope(parentScope);
            Assert.That(container.Resolve<IScopedDisposableService>(), Is.SameAs(parentInstance));
            parentScope.Dispose();
            Assert.That(ScopedDisposableService.DisposeCount, Is.EqualTo(2));
        }

        [Test]
        public void ScopeDisposal_UsesReverseCreationOrder_AndContinuesAfterDisposeThrows()
        {
            DisposalProbe.Order.Clear();
            var container = new DIContainer();
            container.RegisterScoped<IFirstDisposable, FirstDisposable>();
            container.RegisterScoped<IThrowingDisposable, ThrowingDisposable>();

            var scope = container.CreateScope("order");
            container.SetCurrentScope(scope);
            container.Resolve<IFirstDisposable>();
            container.Resolve<IThrowingDisposable>();

            scope.Dispose();

            CollectionAssert.AreEqual(new[] { "throwing", "first" }, DisposalProbe.Order);
        }

        [Test]
        public void ScopeDisposal_DoesNotDisposeSameSharedInstanceTwice()
        {
            SharedDualService.DisposeCount = 0;
            var container = new DIContainer();
            var shared = new SharedDualService();
            container.RegisterInstance<IFirstDisposable>(shared);
            container.RegisterInstance<IThrowingDisposable>(shared);

            var scope = container.CreateScope("shared");
            var first = scope.GetOrCreateInstance<IFirstDisposable>();
            var second = scope.GetOrCreateInstance<IThrowingDisposable>();
            Assert.That(second, Is.SameAs(first));

            scope.Dispose();

            Assert.That(SharedDualService.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void Container_ResolvesItselfAsIDIContainer()
        {
            var container = new DIContainer();

            Assert.That(container.Resolve<IDIContainer>(), Is.SameAs(container));
        }

        [Test]
        public void Constructor_MarkedWithInject_IsPreferredOverFirstConstructor()
        {
            var container = new DIContainer();
            container.RegisterInstance<ITestService>(new TestService());
            container.RegisterTransient<MultiConstructor, MultiConstructor>();

            Assert.That(container.Resolve<MultiConstructor>().UsedInjectConstructor, Is.True);
        }

        // ---------- Validate ----------

        [Test]
        public void Validate_ValidGraph_HasNoIssues()
        {
            var container = new DIContainer();
            container.RegisterSingleton<ITestService, TestService>();
            container.RegisterTransient<IConsumer, Consumer>();

            var result = container.Validate();

            Assert.That(result.IsValid, Is.True, result.ToString());
            Assert.DoesNotThrow(result.ThrowIfInvalid);
        }

        [Test]
        public void Validate_ReportsMissingConstructorDependency()
        {
            var container = new DIContainer();
            container.RegisterTransient<IConsumer, Consumer>();

            var issue = container.Validate().Issues.Single();

            Assert.That(issue.Kind, Is.EqualTo(DIValidationIssueKind.MissingDependency));
            Assert.That(issue.ServiceType, Is.EqualTo(typeof(IConsumer)));
            Assert.That(issue.DependencyType, Is.EqualTo(typeof(ITestService)));
        }

        [Test]
        public void Validate_ReportsCircularDependencyOnceWithPath()
        {
            var container = new DIContainer();
            container.RegisterSingleton<ICycleA, CycleA>();
            container.RegisterSingleton<ICycleB, CycleB>();

            var result = container.Validate();

            var cycle = result.Issues.Single(i => i.Kind == DIValidationIssueKind.CircularDependency);
            StringAssert.Contains("ICycleA -> ICycleB -> ICycleA", cycle.Message);
            Assert.Throws<InvalidOperationException>(result.ThrowIfInvalid);
        }

        [Test]
        public void Validate_CoversLazySingletonsAndInstances()
        {
            var container = new DIContainer();
            container.RegisterLazySingleton<IConsumer, Consumer>();

            Assert.That(container.Validate().Issues.Single().DependencyType, Is.EqualTo(typeof(ITestService)));

            container.RegisterInstance<ITestService>(new TestService());
            Assert.That(container.Validate().IsValid, Is.True);
        }

        [Test]
        public void Validate_DoesNotCreateInstances()
        {
            CountingService.Created = 0;
            var container = new DIContainer();
            container.RegisterSingleton<CountingService, CountingService>();

            container.Validate();

            Assert.That(CountingService.Created, Is.EqualTo(0));
        }

        [Test]
        public void OptionalConstructorParameter_UsesDefaultWhenUnregistered_AndServiceWhenRegistered()
        {
            var container = new DIContainer();
            container.RegisterTransient<OptionalConsumer, OptionalConsumer>();

            var withoutService = container.Resolve<OptionalConsumer>();
            Assert.That(withoutService.Service, Is.Null);
            Assert.That(withoutService.Limit, Is.EqualTo(7));
            Assert.That(container.Validate().IsValid, Is.True);

            var service = new TestService();
            container.RegisterInstance<ITestService>(service);
            Assert.That(container.Resolve<OptionalConsumer>().Service, Is.SameAs(service));
        }

        // ---------- test types ----------

        private interface ITestService
        {
        }

        private sealed class OptionalConsumer
        {
            public OptionalConsumer(ITestService service = null, int limit = 7)
            {
                Service = service;
                Limit = limit;
            }

            public ITestService Service { get; }
            public int Limit { get; }
        }

        private sealed class TestService : ITestService
        {
        }

        private interface IConsumer
        {
        }

        private sealed class Consumer : IConsumer
        {
            public Consumer(ITestService service)
            {
            }
        }

        private interface ICycleA
        {
        }

        private interface ICycleB
        {
        }

        private sealed class CycleA : ICycleA
        {
            public CycleA(ICycleB b)
            {
            }
        }

        private sealed class CycleB : ICycleB
        {
            public CycleB(ICycleA a)
            {
            }
        }

        private sealed class CountingService
        {
            public static int Created;

            public CountingService()
            {
                Created++;
            }
        }

        private sealed class MultiConstructor
        {
            public MultiConstructor()
            {
            }

            [Inject]
            public MultiConstructor(ITestService service)
            {
                UsedInjectConstructor = true;
            }

            public bool UsedInjectConstructor { get; }
        }

        private interface IScopedDisposableService : IDisposable
        {
        }

        private sealed class ScopedDisposableService : IScopedDisposableService
        {
            public static int DisposeCount;
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
                DisposeCount++;
            }
        }

        private static class DisposalProbe
        {
            public static readonly List<string> Order = new List<string>();
        }

        private interface IFirstDisposable : IDisposable
        {
        }

        private interface IThrowingDisposable : IDisposable
        {
        }

        private sealed class FirstDisposable : IFirstDisposable
        {
            public void Dispose() => DisposalProbe.Order.Add("first");
        }

        private sealed class ThrowingDisposable : IThrowingDisposable
        {
            public void Dispose()
            {
                DisposalProbe.Order.Add("throwing");
                throw new InvalidOperationException("dispose failure");
            }
        }

        private sealed class SharedDualService : IFirstDisposable, IThrowingDisposable
        {
            public static int DisposeCount;

            public void Dispose() => DisposeCount++;
        }
    }
}
