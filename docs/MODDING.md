# Modding

A mod is a folder with a `mod.json` and files laid out like the runtime package. Mods are read from:

| Platform | Folder |
|---|---|
| Windows | `%LOCALAPPDATA%\WC2CSharp\mods\<mod>\` (or any folder passed with `--mods <folder>`) |
| Android | `Android/data/com.wc2remake.csharp/files/mods/<mod>/` (reachable over USB), and `files/documents/mods/` |

`--no-mods` starts the desktop build without them. `samples/mods/example-balance` is a working example.

## mod.json

```json
{
  "id": "example-balance",
  "name": "Example: balance tweaks",
  "version": "1.0",
  "author": "you",
  "description": "What the mod does.",
  "priority": 0,
  "enabled": true
}
```

Mods are applied in ascending `priority` (then by id); a later mod wins over an earlier one and over
the game. `enabled: false` skips the mod.

## What a mod can change

**Game data** (`data/*.json`, `maps/*/areas.json`, string tables) is **merged** with the game's file:

* objects merge key by key, so a mod lists only what it changes;
* arrays and plain values replace the game's value;
* `null` removes a key, for example `{"countries": {"others": {"realbomber": null}}}` in `data/units.json`;
* `"$replace": true` inside an object replaces the game's object instead of merging.

```json
// mods/my-mod/data/units.json: a stronger tank for everyone, nothing else changes
{ "countries": { "others": { "tank": { "strength": 120, "maxAttack": 10 } } } }
```

```json
// mods/my-mod/lang/en/stringtable_en.json: one English text
{ "strings": { "Tank": "Medium Tank" } }
```

The field reference of every data file is in `DATA.md`.

**Scenarios and tutorial scripts** (`scenarios/*.json`) are ordered lists; a mod's file replaces the
game's file of the same name. To add a scenario, add `scenarios/<name>.json` and its entry in
`data/campaigns.json` or `data/conquests.json`. The pause box **Save** button writes the running
battle as `game.json` in the documents folder, ready to be copied into a mod as a scenario.

**Any other file** (textures, atlases, fonts, sounds, music) is replaced:

* by a file with the same package path (`gfx/ui/phone-568/mainbg-568h@2x.webp`), or
* by a file with the same name anywhere in the mod (`mainbg-568h@2x.webp`), since the game finds
  assets by their original names.

New files are simply added; data can refer to them by name (for example a unit `image`, which is an
atlas entry, or a new atlas the mod adds).

## Checking a mod

```powershell
cd WC2
dotnet run -- --verify-data --mods <folder that contains your mod>
```

lists every definition the mods change compared with the original game data, field by field. The
game log (`--log FILE`) names every mounted mod. Package paths and original names of all files are in
`index.json` at the root of the built package (`WC2/bin/Debug/net9.0/pack`).

## Limits

* Map geometry (`area<id>.bin`, `adjion<id>.bin`, `areamark<id>.raw`) and scenario area masks are
  binary; new maps need those files.
* New area types (`data/terrain.json`) are fully data driven; new installation or building kinds are
  not (their behaviour is code), but their numbers are data.
* There is no in-game mod list yet; mods are switched with `enabled` in `mod.json`.
