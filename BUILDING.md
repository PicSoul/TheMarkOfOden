# Building from source

## What you need

- .NET SDK 8 or newer
- Valheim installed
- BepInEx installed, either into the Valheim folder or into a mod manager profile

No copy of the game's assemblies is included in this repository, and none should ever be committed.
The build reads them from your own installation.

## Setup

Copy `Local.props.example` to `Local.props` and set the paths for your machine:

```xml
<Project>
  <PropertyGroup>
    <ValheimInstall>C:\Program Files (x86)\Steam\steamapps\common\Valheim</ValheimInstall>
    <BepInExCore>$(AppData)\r2modmanPlus-local\Valheim\profiles\Default\BepInEx\core</BepInExCore>
    <DeployProfile>Default</DeployProfile>
  </PropertyGroup>
</Project>
```

`Local.props` is gitignored, so your paths stay out of the repository. All three are optional:
the build falls back to the `VALHEIM_INSTALL` and `BEPINEX_CORE` environment variables, then to the
default Steam location. If a path is wrong the build stops with a message saying which one.

`DeployProfile` is the only one that changes behaviour rather than just locating files: set it and every
build drops the DLL straight into that r2modman profile, which is the fastest way to iterate. Leave it
out and the build only produces a DLL.

## Build

```
dotnet build MarkOfOden.sln -c Release
```

The game's assemblies are publicized at build time by `BepInEx.AssemblyPublicizer.MSBuild`, so the code
can reach private members such as `MonsterAI.m_targetCreature` and `BaseAI.Flee`. Nothing publicized is
written back to your game folder.

## Packaging

```
.\build.ps1              # build, verify, validate, zip
.\build.ps1 -Install     # also copy into the local profile
```

The script refuses to package if the version in `manifest.json`, the `.csproj` and `Plugin.cs` disagree,
or if `CHANGELOG.md` has no section for it. Thunderstore versions are immutable once uploaded, so these
are worth catching first.

It also runs `tools/PatchCheck` against your installed game. That resolves every Harmony target named by
a string, which the compiler cannot check and which throws at startup if wrong, taking the whole mod down
with it. Run it yourself after a Valheim update:

```
dotnet run --project tools/PatchCheck/patchcheck.csproj -c Release -- `
  src/MarkOfOden/bin/Release/MarkOfOden.dll `
  "<valheim>/valheim_Data/Managed" `
  "<bepinex>/core"
```

It works on any mod's assembly, not just this one, which makes it a quick way to see whether some other
mod still lines up with the current game build.

## How the mod is put together

Three layers, deliberately independent:

- **`Marks/`** — what a player has done. Boss credit arrives from the game's own kill registration, and
  species kills are read from the history Valheim already keeps per character. `MarkSync` publishes the
  result to the player's ZDO.
- **`Fear/`** — what a creature makes of it. `FearEvaluator` is the single decision point; `CreatureTiers`
  works out how dangerous each creature believes itself to be, from game data rather than a fixed list.
- **`Patches/`** — where it touches the game. Postfixes only.

### Why it hooks where it does

Creature AI runs on whichever client owns the creature, which is usually not the client of the player
being feared. So a player's mark is published to their own ZDO and read from there, the same way vanilla
handles crown mode.

There are no transpilers. Every patch is a prefix or postfix, so another mod patching the same method
still runs, and a changed method body cannot silently break a pattern match. `FearMe`, the closest
existing mod, reaches its flee logic by matching IL inside `MonsterAI.UpdateAI`; that method changed
shape in Valheim 1.0.

Two patches do their work by assigning a plain field rather than changing behaviour — the name plate
distance in particular — which keeps them out of the way of HUD mods.

### If the game updates

Run `build.ps1`. If a patch target has moved, PatchCheck names it before you ever launch the game.

## License

MIT. See `LICENSE`.
