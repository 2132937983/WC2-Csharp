# Modding

A mod is a folder with a `mod.json` and files laid out like the asset tree (`assets/`, see
ASSET_LAYOUT.md). Mods are read from:

| Platform | Folder |
|---|---|
| Windows | `%LOCALAPPDATA%\WC2CSharp\mods\<mod>\`, `mods\` next to the program, and in a development build the repository's `mods/` folder (or only the folder passed with `--mods <folder>`) |
| Android | `Android/data/com.wc2remake.csharp/files/mods/<mod>/` (reachable over USB), and `files/documents/mods/` |

`--no-mods` starts the desktop build without them; `--all-mods` also loads mods marked
`"enabled": false`. `mods/example-balance` is a working example (shipped disabled).

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

**Game data** (`data/json/**/*.json`: units, cards, terrain, rules, string tables, ...) is **merged**
with the game's file:

* objects merge key by key, so a mod lists only what it changes;
* arrays and plain values replace the game's value;
* `null` removes a key, for example `{"countries": {"others": {"realbomber": null}}}` in `data/json/units/units.json`;
* `"$replace": true` inside an object replaces the game's object instead of merging.

```json
// mods/my-mod/data/json/units/units.json: a stronger tank for everyone, nothing else changes
{ "countries": { "others": { "tank": { "strength": 120, "maxAttack": 10 } } } }
```

```json
// mods/my-mod/data/json/text/en/stringtable_en.json: one English text
{ "strings": { "Tank": "Medium Tank" } }
```

The field reference of every data file is in `DATA_SCHEMA.md`.

**Scenarios and tutorial scripts** are the original XML files (`data/xml/scenarios/`,
`scripts/tutorial/`); a mod's file replaces the game's file by path or original name. To add a
scenario, add its XML, an entry in `data/json/scenarios/index.json` (`"file"` says where the XML is) and
its entry in `data/json/campaigns/campaigns.json` or `data/json/conquests/conquests.json`. A battle
exported with the pause box **Save** button (`game.json`, JSON scenario format, DATA_SCHEMA.md) can be
put into a mod as `data/json/scenarios/<name>.json`; it is then read instead of the XML of that name.

Every data file is checked against its schema (`data/json/schemas/`) when it loads. If a mod makes a
file invalid, the log says where (`$.countries.others.Tank.price: -5 is below the minimum 0`) and the
mod's changes to that file are ignored. The pause box **Save** button writes the running
battle as `game.json` in the documents folder, ready to be copied into a mod as a scenario.

**Any other file** (textures, atlases, fonts, sounds, music) is replaced:

* by a file with the same path (`image/ui/phone-568-hd/mainbg.webp`), or
* by a file with its original name at the mod's root (`mainbg-568h@2x.webp`), since the game finds
  assets by their original names (`assets/legacy-names.json` lists them all).

New files are simply added; data can refer to them by name (for example a unit `image`, which is an
atlas entry, or a new atlas the mod adds).

## Checking a mod

```powershell
dotnet run --project Hosts/Desktop -- --verify-data --mods <folder that contains your mod>
```

lists every definition the mods change compared with the original game data, field by field. The
game log (`--log FILE`) names every mounted mod. Paths and original names of all files are in
`generated/asset_index.json`.

## Limits

* Map geometry (`data/binary/maps/<id>/`: `area<id>.bin`, `adjion<id>.bin`, `areamark<id>.raw`) and
  scenario area masks are binary; new maps need those files.
* New area types (`data/json/terrain/terrain.json`) are fully data driven; new installation or building kinds are
  not (their behaviour is code), but their numbers are data.
* There is no in-game mod list yet; mods are switched with `enabled` in `mod.json`.
