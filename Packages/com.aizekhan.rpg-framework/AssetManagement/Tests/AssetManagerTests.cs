using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RPGFramework.AssetManagement.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.AssetManagement.Tests
{
    internal sealed class FakeAsset
    {
        public FakeAsset(string key) => Key = key;
        public string Key { get; }
    }

    /// <summary>
    /// Provider whose loads either complete immediately or wait on a gate controlled by the test.
    /// </summary>
    internal sealed class FakeProvider : IAssetProvider
    {
        private readonly Dictionary<string, object> _assets = new Dictionary<string, object>();
        private readonly Dictionary<string, UniTaskCompletionSource> _gates = new Dictionary<string, UniTaskCompletionSource>();

        public FakeProvider(string name, int priority = 0)
        {
            Name = name;
            Priority = priority;
        }

        public string Name { get; }
        public int Priority { get; }
        public List<string> Loads { get; } = new List<string>();
        public List<string> Releases { get; } = new List<string>();
        public Exception Failure { get; set; }

        public FakeProvider With(string key, object asset = null)
        {
            _assets[key] = asset ?? new FakeAsset(key);
            return this;
        }

        public UniTaskCompletionSource Gate(string key)
        {
            var gate = new UniTaskCompletionSource();
            _gates[key] = gate;
            return gate;
        }

        public async UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken) where T : class
        {
            Loads.Add(key);
            if (_gates.TryGetValue(key, out var gate))
                await gate.Task;
            if (Failure != null)
                throw Failure;
            return _assets.TryGetValue(key, out var asset) ? asset as T : null;
        }

        public void Release(string key, object asset) => Releases.Add(key);
    }

    public sealed class AssetManagerTests
    {
        private static T Run<T>(UniTask<T> task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending), "expected synchronous completion");
            return task.GetAwaiter().GetResult();
        }

        [Test]
        public void Load_ReturnsHandle_LastDisposeReleasesThroughProvider()
        {
            var provider = new FakeProvider("fake").With("sword");
            var assets = new AssetManager(null, provider);

            var first = Run(assets.LoadAsync<FakeAsset>("sword"));
            var second = Run(assets.LoadAsync<FakeAsset>("sword"));

            Assert.That(first.Asset, Is.SameAs(second.Asset));
            Assert.That(provider.Loads, Has.Count.EqualTo(1));
            Assert.That(assets.GetReferenceCount("sword"), Is.EqualTo(2));

            first.Dispose();
            first.Dispose(); // idempotent
            Assert.That(provider.Releases, Is.Empty);
            Assert.That(assets.IsLoaded("sword"), Is.True);

            second.Dispose();
            CollectionAssert.AreEqual(new[] { "sword" }, provider.Releases);
            Assert.That(assets.IsLoaded("sword"), Is.False);
        }

        [Test]
        public void Providers_AreTriedByPriority_WithFallback()
        {
            var low = new FakeProvider("low", 0).With("shared").With("lowOnly");
            var high = new FakeProvider("high", 10).With("shared");
            var assets = new AssetManager(null, low, high);

            var shared = Run(assets.LoadAsync<FakeAsset>("shared"));
            var lowOnly = Run(assets.LoadAsync<FakeAsset>("lowOnly"));

            Assert.That(assets.Providers.Select(p => p.Name), Is.EqualTo(new[] { "high", "low" }));
            CollectionAssert.AreEqual(new[] { "shared", "lowOnly" }, high.Loads);
            CollectionAssert.AreEqual(new[] { "lowOnly" }, low.Loads);

            shared.Dispose();
            lowOnly.Dispose();
            CollectionAssert.AreEqual(new[] { "shared" }, high.Releases);
            CollectionAssert.AreEqual(new[] { "lowOnly" }, low.Releases);
        }

        [Test]
        public void ThrowingProvider_IsSkipped()
        {
            var broken = new FakeProvider("broken", 10) { Failure = new InvalidOperationException("io") };
            var ok = new FakeProvider("ok").With("potion");
            var assets = new AssetManager(null, broken, ok);

            var handle = Run(assets.LoadAsync<FakeAsset>("potion"));

            Assert.That(handle.Asset.Key, Is.EqualTo("potion"));
        }

        [Test]
        public void Missing_ThrowsAssetNotFound_AndCanBeRetried()
        {
            var provider = new FakeProvider("fake");
            var assets = new AssetManager(null, provider);

            var task = assets.LoadAsync<FakeAsset>("ghost");
            Assert.Throws<AssetNotFoundException>(() => task.GetAwaiter().GetResult());
            Assert.That(assets.GetReferenceCount("ghost"), Is.Zero);

            provider.With("ghost");
            Assert.That(Run(assets.LoadAsync<FakeAsset>("ghost")).Asset.Key, Is.EqualTo("ghost"));
        }

        [Test]
        public void ConcurrentLoads_ShareOneProviderCall()
        {
            var provider = new FakeProvider("fake").With("map");
            var gate = provider.Gate("map");
            var assets = new AssetManager(null, provider);

            var a = assets.LoadAsync<FakeAsset>("map");
            var b = assets.LoadAsync<FakeAsset>("map");
            Assert.That(a.Status, Is.EqualTo(UniTaskStatus.Pending));

            gate.TrySetResult();

            Assert.That(Run(a).Asset, Is.SameAs(Run(b).Asset));
            Assert.That(provider.Loads, Has.Count.EqualTo(1));
            Assert.That(assets.GetReferenceCount("map"), Is.EqualTo(2));
        }

        [Test]
        public void CancelledRequester_DoesNotLeak_AssetReleasedWhenLoadFinishes()
        {
            var provider = new FakeProvider("fake").With("boss");
            var gate = provider.Gate("boss");
            var assets = new AssetManager(null, provider);
            var cts = new CancellationTokenSource();

            var task = assets.LoadAsync<FakeAsset>("boss", cts.Token);
            cts.Cancel();
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Canceled));

            gate.TrySetResult();

            CollectionAssert.AreEqual(new[] { "boss" }, provider.Releases);
            Assert.That(assets.IsLoaded("boss"), Is.False);
        }

        [Test]
        public void CancelledRequester_DoesNotAffectOtherRequester()
        {
            var provider = new FakeProvider("fake").With("boss");
            var gate = provider.Gate("boss");
            var assets = new AssetManager(null, provider);
            var cts = new CancellationTokenSource();

            var cancelled = assets.LoadAsync<FakeAsset>("boss", cts.Token);
            var kept = assets.LoadAsync<FakeAsset>("boss");
            cts.Cancel();
            gate.TrySetResult();

            Assert.That(cancelled.Status, Is.EqualTo(UniTaskStatus.Canceled));
            var handle = Run(kept);
            Assert.That(provider.Releases, Is.Empty);
            Assert.That(assets.GetReferenceCount("boss"), Is.EqualTo(1));

            handle.Dispose();
            CollectionAssert.AreEqual(new[] { "boss" }, provider.Releases);
        }

        [Test]
        public void SameKeyRequestedAsOtherType_ThrowsInvalidCast_AndKeepsOriginalReference()
        {
            var provider = new FakeProvider("fake").With("text", "plain string");
            var assets = new AssetManager(null, provider);
            var asObject = Run(assets.LoadAsync<object>("text"));

            Assert.Throws<InvalidCastException>(() => assets.LoadAsync<FakeAsset>("text").GetAwaiter().GetResult());

            Assert.That(asObject.Asset, Is.EqualTo("plain string"));
            Assert.That(assets.GetReferenceCount("text"), Is.EqualTo(1));
        }

        [Test]
        public void Preload_ReportsProgress_DisposeReleasesAll()
        {
            var provider = new FakeProvider("fake").With("a").With("b").With("c");
            var assets = new AssetManager(null, provider);
            var reported = new List<float>();

            var group = Run(assets.PreloadAsync<FakeAsset>(new[] { "a", "b", "c" }, new SyncProgress(reported.Add)));

            Assert.That(group.Count, Is.EqualTo(3));
            Assert.That(reported.Last(), Is.EqualTo(1f));
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, assets.LoadedKeys);

            group.Dispose();
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, provider.Releases);
        }

        [Test]
        public void Preload_Failure_ReleasesEverythingAlreadyLoaded()
        {
            var provider = new FakeProvider("fake").With("a").With("c");
            var gateC = provider.Gate("c");
            var assets = new AssetManager(null, provider);

            var task = assets.PreloadAsync<FakeAsset>(new[] { "a", "missing", "c" });
            gateC.TrySetResult();

            Assert.Throws<AssetNotFoundException>(() => task.GetAwaiter().GetResult());
            CollectionAssert.AreEquivalent(new[] { "a", "c" }, provider.Releases);
            Assert.That(assets.LoadedKeys, Is.Empty);
        }

        [Test]
        public void UnloadAll_ReleasesEverything_OutstandingHandlesBecomeNoOps()
        {
            var provider = new FakeProvider("fake").With("a");
            var assets = new AssetManager(null, provider);
            var handle = Run(assets.LoadAsync<FakeAsset>("a"));

            assets.UnloadAll();
            handle.Dispose();

            CollectionAssert.AreEqual(new[] { "a" }, provider.Releases);
            Assert.That(assets.LoadedKeys, Is.Empty);
        }

        [Test]
        public void InstantiateAsync_KeepsPrefabLoadedUntilInstanceDestroyed()
        {
            var prefab = new GameObject("PrefabSource");
            try
            {
                var provider = new FakeProvider("fake").With("enemy", prefab);
                var assets = new AssetManager(null, provider);

                var task = assets.InstantiateAsync("enemy");
                var instance = Run(task);

                Assert.That(instance.GetComponent<AssetHandleOwner>(), Is.Not.Null);
                Assert.That(assets.GetReferenceCount("enemy"), Is.EqualTo(1));

                Object.DestroyImmediate(instance);

                CollectionAssert.AreEqual(new[] { "enemy" }, provider.Releases);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        private sealed class SyncProgress : IProgress<float>
        {
            private readonly Action<float> _report;
            public SyncProgress(Action<float> report) => _report = report;
            public void Report(float value) => _report(value);
        }
    }
}
