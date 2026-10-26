# Recipe: settings a designer tunes in the inspector

1. Put the numbers in a `[Serializable]` options class in `<Game>.Core` with public fields and defaults
   (`GoblinValleyOptions`). The rules take it through the constructor; the module registers it
   (`Container.RegisterInstance(options)`).
2. Create a ScriptableObject in the runtime assembly that holds the options and the asset references
   (`[CreateAssetMenu]`, `[SerializeField] private GoblinValleyOptions _rules`, `[SerializeField] private PrefabCatalog _prefabs`).
   One class per file, the file named after the class (GA09).
3. The host gets the config through a `[SerializeField]` field and passes `config.Rules` to the composition.
4. Tests build the options in code (fast and explicit); the scene uses the asset.
5. Use `[Tooltip]`, `[Min]` and `[Header]` so the inspector explains itself.
