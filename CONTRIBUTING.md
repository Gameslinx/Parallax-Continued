# Contributing to Parallax Continued

This guide covers setting up a development environment for the Parallax Continued plugin.

## Repository layout

| Path | Contents |
|------|----------|
| `Mod Source/Parallax/` | The C# plugin (`ParallaxContinued.dll`). |
| `Mod Source/GameData/` | The mod's GameData contents: configs, shaders, textures, models. |
| `Assets/`, `ProjectSettings/`, `Packages/` | The Unity project used to author shaders and asset bundles. |

Most code changes only need `Mod Source/`. You only need Unity if you are
changing shaders or building asset bundles.

## Prerequisites

- A copy of **Kerbal Space Program 1.12.x**. The Steam install is detected
  automatically.
- The **.NET SDK** (6.0 or newer) or **Visual Studio** with the *Game development with Unity*
  workload (see `.vsconfig`). Rider also works.
- **Unity 2019.4.18f1**, only if you are working on shaders or asset bundles.

## Installing dependencies

The plugin compiles against the game and the following mods, which must be
installed in your KSP `GameData` folder:

- [Harmony 2](https://github.com/KSPModdingLibs/HarmonyKSP) (`000_Harmony`)
- [Kopernicus](https://github.com/Kopernicus/Kopernicus)
- [KSPBurst](https://github.com/KSPModdingLibs/KSPBurst) (`000_KSPBurst`)
- [KSPTextureLoader](https://github.com/KSPModdingLibs/KSPTextureLoader)

The easiest way to get them is [CKAN](https://github.com/KSP-CKAN/CKAN).
Alternatively, the build can install them for you: with `ckan` on your `PATH`, build with
`-p:KSPBT_InstallCKANDependencies=true`.

You will also want the texture packs described in the [README](README.md) installed
if you want to run the mod in game.

## Building

The project uses [KSPBuildTools](https://github.com/KSPModdingLibs/KSPBuildTools),
which supplies the references to the game's assemblies, so there are no
hardcoded paths to edit.

```
dotnet build "Mod Source/Parallax/Parallax.csproj"
```

or open `Mod Source/Parallax.sln` in your IDE and build.

### Locating your KSP install

KSPBuildTools looks for the game in this order:

1. the `KSPBT_GameRoot` MSBuild property,
2. the `KSP_ROOT` environment variable,
3. the default Steam location (`C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program` on Windows).

If your game is elsewhere, set `KSP_ROOT`, or create `Mod Source/Parallax.props.user`:

```xml
<Project>
  <PropertyGroup>
    <KSPBT_GameRoot>D:\Games\Kerbal Space Program</KSPBT_GameRoot>
  </PropertyGroup>
</Project>
```

### Output

A build installs `ParallaxContinued.dll` straight into
`<KSP>/GameData/ParallaxContinued/Plugins/`, so you can launch the game
immediately afterwards. This assumes the rest of the mod (shaders, configs,
etc.) is already installed there, either from a release or by copying
`Mod Source/GameData/ParallaxContinued` over.

To write the DLL to `Mod Source/GameData/ParallaxContinued/Plugins/` instead,
build with `-p:KSPBT_InstallToGame=false`.

Intermediate output (`obj/`, `bin/`) is not tracked by git.

## Working on shaders and asset bundles

Open the repository root in Unity 2019.4.18f1. The compiled shader bundles
(`*.unity3d`) live in `Mod Source/GameData/ParallaxContinued/Shaders/` and are
committed. Rebuild all three platform variants (`windows`, `linux`,
`macosx`) when you change a shader.

## Submitting changes

- Branch off `master` and open a pull request against it.
- Keep pull requests focused, and describe how you tested the change in game.
- Make sure `dotnet build` succeeds with no new errors.
- Don't commit build output such as `obj/` or `bin/`.
