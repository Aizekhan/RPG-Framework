using NUnit.Framework;

namespace RPGFramework.DI.Tests
{
    [TestFixture]
    public class DIFactoryRegistrationTests
    {
        public interface IConfigured
        {
            string Name { get; }
        }

        public sealed class Configured : IConfigured
        {
            public Configured(string name) => Name = name;
            public string Name { get; }
        }

        public sealed class Consumer
        {
            public Consumer(IConfigured configured) => Configured = configured;
            public IConfigured Configured { get; }
        }

        public sealed class NeedsMissing
        {
            public NeedsMissing(Consumer consumer, System.IDisposable missing)
            {
            }
        }

        [Test]
        public void Factory_Singleton_IsCreatedOnceWithContainerArgument()
        {
            var container = new DIContainer();
            int calls = 0;
            IDIContainer seen = null;
            container.RegisterFactory<IConfigured>(c =>
            {
                calls++;
                seen = c;
                return new Configured("hero");
            });

            var first = container.Resolve<IConfigured>();
            var second = container.Resolve<IConfigured>();

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.Name, Is.EqualTo("hero"));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(seen, Is.SameAs(container));
        }

        [Test]
        public void Factory_Transient_IsCalledEveryResolve()
        {
            var container = new DIContainer();
            int calls = 0;
            container.RegisterFactory<IConfigured>(_ => new Configured("x" + ++calls), DILifetime.Transient);

            Assert.That(container.Resolve<IConfigured>(), Is.Not.SameAs(container.Resolve<IConfigured>()));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void FactoryService_SatisfiesConstructorDependencies_AndValidation()
        {
            var container = new DIContainer();
            container.RegisterFactory<IConfigured>(_ => new Configured("a"));
            container.RegisterSingleton<Consumer, Consumer>();

            Assert.That(container.IsRegistered<IConfigured>(), Is.True);
            Assert.That(container.Validate().IsValid, Is.True);
            Assert.That(container.Resolve<Consumer>().Configured.Name, Is.EqualTo("a"));
        }

        [Test]
        public void Validation_StillReportsMissingDependenciesOfTypeRegistrations()
        {
            var container = new DIContainer();
            container.RegisterFactory<IConfigured>(_ => new Configured("a"));
            container.RegisterSingleton<Consumer, Consumer>();
            container.RegisterSingleton<NeedsMissing, NeedsMissing>();

            var result = container.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues.Count, Is.EqualTo(1));
            Assert.That(result.Issues[0].DependencyType, Is.EqualTo(typeof(System.IDisposable)));
        }

        [Test]
        public void Factory_ReplacesEarlierRegistrationAndCachedInstance()
        {
            var container = new DIContainer();
            container.RegisterInstance<IConfigured>(new Configured("old"));

            container.RegisterFactory<IConfigured>(_ => new Configured("new"));

            Assert.That(container.Resolve<IConfigured>().Name, Is.EqualTo("new"));
        }
    }
}
