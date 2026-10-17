# Building Potato Tweaks

## References

From the Valheim install (`Valheim/valheim_Data/Managed`):
mscorlib, netstandard, System, System.Core, assembly_valheim, assembly_utils, assembly_guiutils, UnityEngine, UnityEngine.CoreModule, UnityEngine.InputLegacyModule, UnityEngine.PhysicsModule, UnityEngine.TextRenderingModule, UnityEngine.ParticleSystemModule, UnityEngine.AnimationModule, UnityEngine.AIModule, Unity.Collections, Unity.TextMeshPro, UnityEngine.UI

From a BepInEx profile (r2modman / Gale):
- `BepInEx/core/BepInEx.dll`
- `BepInEx/core/0Harmony.dll`
- `BepInEx/plugins/ValheimModding-Jotunn/Jotunn.dll`
- `BepInEx/plugins/RandyKnapp-EpicLoot/EpicLoot.dll` (compile time only; Epic Loot stays optional when the mod runs)

StarLevelSystem and Equipment & Quick Slots are used through reflection, so they aren't needed to build.


## Compile

Any C# compiler works. Releases are built with Roslyn from the .NET 8 SDK against the game's own framework libraries:

```
csc -nologo -nostdlib -noconfig -target:library -optimize+ -deterministic -langversion:9 -out:PotatoTweaks.dll -r:<each reference above> src/*.cs
```

A Visual Studio / `dotnet build` project targeting `netstandard2.1` with the same references also works.

## Package for Thunderstore

Copy the built `PotatoTweaks.dll` into `package/plugins/`, then zip the contents of `package/` (not the folder itself) as `Potato_Tweaks-PotatoTweaks-<version>.zip`:

```
manifest.json
README.md
CHANGELOG.md
LICENSE        (copy from the repository root)
icon.png
plugins/PotatoTweaks.dll
```

## Releasing

1. Raise `Version` in `src/Plugin.cs` and `version_number` in `package/manifest.json`.
2. Add an entry to `CHANGELOG.md` (and `package/CHANGELOG.md`).
3. A new minor version (1.2 → 1.3) makes every player update; a patch (1.2.0 → 1.2.1) stays compatible.
4. After a Valheim or Epic Loot update, check the log for "is disabled": a feature whose game code changed switches itself off and says why.
