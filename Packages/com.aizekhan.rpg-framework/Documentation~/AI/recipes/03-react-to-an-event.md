# Recipe: make one feature react to another

Example: when a quest completes, the shop unlocks a new item.

1. Find the event the other feature publishes (`QuestCompletedEvent` in `RPGFramework.Quests`). Game events live in
   `<Feature>Events.cs`; framework events are `IEvent` structs in each module.
2. Subscribe in the reacting feature's rules, keep the subscription and dispose it on shutdown:
   ```csharp
   private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

   public void Start(IEventBus bus) =>
       bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted).AddTo(_subscriptions);

   private void OnQuestCompleted(QuestCompletedEvent completed) { /* the reaction */ }

   public void Dispose() => _subscriptions.Dispose();
   ```
   Call `Start` from the module's `OnInitialize` and `Dispose` from `OnShutdown`.
3. Do **not** reference the other feature's rules class to call it. If it must be asked something, depend on an
   interface registered in the container.
4. Test: cause the event through the game and assert the reaction. Goblin Valley's
   `ClearingTheValley_RunsTheWholeEventChain` test is a full example.
