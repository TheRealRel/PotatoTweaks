# Building Potato Tweaks

Source for version 1.4.0 lives next to this file in `potato-tweaks/src/`. Packaging files are in `potato-tweaks/package/`.

## References needed

From the Valheim install (`Valheim/valheim_Data/Managed`):
mscorlib, netstandard, System, System.Core, assembly_valheim, assembly_utils, UnityEngine, UnityEngine.CoreModule, UnityEngine.InputLegacyModule, UnityEngine.PhysicsModule, UnityEngine.TextRenderingModule, Unity.TextMeshPro, UnityEngine.UI, UnityEngine.UIModule, UnityEngine.ParticleSystemModule, UnityEngine.AnimationModule, UnityEngine.AIModule, Unity.Collections, assembly_guiutils

From a BepInEx profile: `BepInEx/core/BepInEx.dll`, `BepInEx/core/0Harmony.dll`, `BepInEx/plugins/ValheimModding-Jotunn/Jotunn.dll`, `BepInEx/plugins/RandyKnapp-EpicLoot/EpicLoot.dll` (compile-time only; Epic Loot stays optional at runtime)

## Compile

Any C# compiler works. The release build used the Roslyn compiler from the .NET 8 SDK with the game's own framework libraries:

```
dotnet <sdk>/Roslyn/bincore/csc.dll -nologo -nostdlib -noconfig -target:library -optimize+ -deterministic -langversion:9 -out:PotatoTweaks.dll -r:<each reference above> src/*.cs
```

A Visual Studio / `dotnet build` project targeting `netstandard2.1` (or `net48`) with the same references also works.

## Package

Zip these at the root of the archive, named `Potato_Tweaks-PotatoTweaks-<version>.zip`:

```
manifest.json
README.md
CHANGELOG.md
LICENSE
icon.png               (256x256 PNG: the viking potato)
plugins/PotatoTweaks.dll
```

The icon is binary, so it is not stored in the project; it is inside every uploaded version on Thunderstore and in `PotatoTweaks-1.4.0-source.zip`.

## Before releasing

1. Raise `Version` in `src/Plugin.cs` and `version_number` in `manifest.json`.
2. Add a `CHANGELOG.md` entry.
3. After any Valheim update, check the game hooks listed in `claude/potato-tweaks.md` still match.
