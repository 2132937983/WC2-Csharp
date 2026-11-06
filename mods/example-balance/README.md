# Example mod: balance tweaks

This mod ships **disabled** (`"enabled": false` in `mod.json`). Set it to `true` to try it in a
development build (the desktop game mounts the repository's `mods/` folder), or copy the folder into
the game's mod folder:

* Windows: `%LOCALAPPDATA%\WC2CSharp\mods\example-balance\`
* Android: `Android/data/com.wc2remake.csharp/files/mods/example-balance/`
* or start the desktop build with `--mods <folder that contains example-balance>`

What it shows (see `MODDING.md` in the repository root for the rules):

* `data/json/units/units.json`, `data/json/cards/cards.json`, `data/json/terrain/terrain.json` are
  **merged** into the game's files: only the keys listed here change (tank strength and attack, the
  tank card price, healing in capitals).
* `data/json/text/en/stringtable_en.json` changes one English text ("Tank" becomes "Medium Tank").
* `mod.json` names the mod; `priority` orders several mods (higher wins), `enabled: false` turns it off.

`dotnet run --project Hosts/Desktop -- --verify-data --mods mods --all-mods` lists exactly what the mods change
compared with the original game data (`--all-mods` also loads disabled mods).
