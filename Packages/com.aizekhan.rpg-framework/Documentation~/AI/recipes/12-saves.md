# Recipe: saving the game, and changing what you save later

Games on `ActionRpgHost` save the player's progress already: tick **Save Progress** on the host. It saves on level-up,
finished quests, every **Autosave Seconds** (60) and on quit, into `Application.persistentDataPath/Saves/<Game Setup>`.

| Task | Do |
|---|---|
| Save your game's own state (a shop's coins, opened chests, a world flag) | Write a `SaveParticipant<TState>` (below) and register it: override `ConfigureSaves(ActionRpgSaves saves)` on your host and call `saves.Register(new ShopSave(shop))`. It is saved and loaded with the progress, in its own section. |
| Save outside `ActionRpgHost` | `new SaveGameCoordinator(new SaveSystem(folder, slots, new JsonSaveSerializer(), new SaveValidator(version)))`, `Register` your participants, then `Save(slot, metadata)` / `Load(slot)`. |
| Add a member to a saved type | Just add it, with a sensible default (`public int Coins { get; set; } = 0;`). Old saves load; the new member keeps its default. No version needed. |
| Remove a member | Just remove it. Old saves load; the removed member is skipped. |
| Rename or retype a member, or split one | Raise `Version` on the participant and override `Upgrade` (below). Without it, the old value is lost silently (it no longer matches a member). |
| Rename `SaveKey` | Don't: existing saves lose that section. Keep the key and change the type behind it. |
| Delete progress | Host context menu **Delete Saved Progress**, or `host.Saves.Delete()`. |

## A participant

```csharp
public sealed class ShopState
{
    public int Coins { get; set; }
    public List<string> Bought { get; set; } = new List<string>();
}

public sealed class ShopSave : SaveParticipant<ShopState>
{
    private readonly Shop _shop;
    public ShopSave(Shop shop) => _shop = shop;

    public override string SaveKey => "mygame.shop";          // stable forever
    protected override ShopState CaptureState() => new ShopState { Coins = _shop.Coins, Bought = _shop.Bought.ToList() };
    protected override void RestoreState(ShopState state) => _shop.Load(state.Coins, state.Bought);
}
```

The state is a plain class: public properties with setters (or public fields), lists, dictionaries, enums (saved by
name), `DateTime`, `Guid`, numbers and strings of any language. No references to live objects (save ids instead), no
`UnityEngine.Object`s. Keep it in the game's Core assembly, next to the rules it saves.

## Changing the shape: versions

Version 1 saved `Gold` as whole coins; version 2 keeps `Copper` (100 per gold). Keep the old shape as a class, raise
`Version`, and convert in `Upgrade`:

```csharp
public sealed class ShopStateV1 { public int Gold { get; set; } }   // as version 1 saved it; never change it again

public override int Version => 2;

protected override ShopState Upgrade(int savedVersion, byte[] data, ISaveSerializer serializer)
{
    var old = serializer.Deserialize<ShopStateV1>(data);
    return new ShopState { Copper = old.Gold * 100L };
}
```

For several versions, convert step by step (`savedVersion == 1` → 2 → 3). Test it: save with the old participant,
load with the new one (the framework's `SaveVersioningTests` show the pattern).

## What the framework does for you

- **A save from a newer build** (its section version is above yours) is refused before anything changes, and
  `ActionRpgSaves` stops saving so the newer save is not overwritten. The player sees a line in the game log.
- **The previous save is kept** beside the latest (`autosave.previous.sav`). If the latest is damaged (checksum) or
  cannot be read, the previous one loads instead and the game log says so.
- **A save nothing can read** is moved aside (`autosave.unreadable-<time>.sav`) and the game starts fresh, so it is
  never overwritten by the next save.
- Writes go through a temporary file, so a crash mid-save leaves the last save intact.
