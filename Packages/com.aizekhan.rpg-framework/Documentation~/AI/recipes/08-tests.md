# Recipe: tests

| What | How | Example |
|---|---|---|
| Rules | EditMode: `new GameHost(logger)`, `host.Start(<Game>Composition.Configure)`, resolve the rules, act, assert the state and the published events, and no errors in a `MemoryLogSink` | `<Feature>Tests`, `GoblinValleyTests` |
| Per-frame rules | tick `host.Update(dt)` in a loop | `GoblinValleyWaveTests` |
| Scene wiring | EditMode: open the scene additively, assert the host and its serialized references | `<Game>SceneTests` |
| Playing | PlayMode asmdef (empty `includePlatforms`), `EditorSceneManager.LoadSceneAsyncInPlayMode` inside `#if UNITY_EDITOR`, wait for the views, drive public methods | `GoblinValleyPlayTests` |
| Architecture | one class deriving `GameArchitectureTests` | `<Game>ArchitectureTests` |

Run `unity test . --mode EditMode --output results.xml` and `--mode PlayMode`, and read failures in the XML.
Never weaken or delete a failing test to make it pass: fix the code, or explain why the test is wrong.
