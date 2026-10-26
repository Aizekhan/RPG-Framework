# Recipe: show game entities in the scene

The rules say *what exists*; the scene shows it. The pattern from Goblin Valley (`Assets/ReferenceGame`):

1. **Rules announce entities.** When the rules create or remove something visible, publish
   `XSpawnedEvent(entityId, kind)` and `XDespawnedEvent(entityId, kind)`, and expose the current list
   (whatever existed before the scene subscribed must still be shown).
2. **Prefab per kind.** Create a `PrefabCatalog` asset (**Create > RPG Framework > Spawning > Prefab Catalog**):
   key = kind (`"goblin"`), prefab, optional pooling. Reference it from a config asset or the host.
3. **Presenter** (plain C# class in the runtime assembly, created by the host in `Start`): subscribes to the two
   events. On spawn: `await spawner.SpawnAsync(kind, position, rotation)`, then `views.Bind(entityId, instance)`,
   then hand the view components their entity id and the rules API. On despawn: `views.Unbind(entityId)`, then
   `spawner.Despawn(instance)`. Copy `GoblinValleyPresenter`.
4. **View components** on the prefab read state every frame and call the rules API on input.
   They never change state themselves. See `CombatantView`, `HealthBarView`, `GoblinBrain`.
5. **Find what is under a click or a collider:** `views.TryGetEntity(hit.collider.gameObject, out int entityId)`.
6. Test it with a PlayMode test that loads the scene and waits for the views (`GoblinValleyPlayTests`).
