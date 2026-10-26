# Recipe: add a feature

A feature is a piece of game rules with its own state: a shop, a day/night cycle, a reputation meter.

1. **Game Kit > New Feature** (or `...GameScaffold.NewFeatureFromCommandLine -game DragonPeak -feature Shop`). You get:
   - `Scripts/Core/Shop/ShopModule.cs`: registers the rules; already added to `DragonPeakComposition`.
   - `Scripts/Core/Shop/ShopRules.cs`: state and actions; every change is published as an event.
   - `Scripts/Core/Shop/ShopEvents.cs`: `ShopChangedEvent`.
   - `Tests/ShopTests.cs`: boots the game and drives the rules.
2. Replace the example counter with the real state and actions. Keep the pattern:
   - state has private setters; it changes only through methods that check their input;
   - every change publishes a `readonly struct ...Event : IEvent` named in the past tense (`ItemBoughtEvent`);
   - dependencies come in through the constructor; register them in `ShopModule.OnConfigure`
     (`Container.RegisterFactory(c => new ShopRules(c.Resolve<IEventPublisher>(), c.Resolve<IItemRegistry>()))`);
   - a dependency on another module's service: add that module's type to `Dependencies`
     (`public override IEnumerable<Type> Dependencies => new[] { typeof(FrameworkCoreModule), typeof(ItemsModule) };`).
3. Update `ShopTests` first, then the rules. Test through the game (`GameHost` + composition), not by creating
   the rules alone, so wiring mistakes show up.
4. The feature needs a framework module the Core asmdef does not reference yet? Add the reference
   (e.g. `RPGFramework.Items.Runtime`) to `DragonPeak.Core.asmdef` and to `DragonPeak.Tests.asmdef`.
