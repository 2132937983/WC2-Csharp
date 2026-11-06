# Example mod: balance tweaks

Copy this folder into the game's mod folder to try it:

* Windows: `%LOCALAPPDATA%\WC2CSharp\mods\example-balance\`
* Android: `Android/data/com.wc2remake.csharp/files/mods/example-balance/`
* or start the desktop build with `--mods <folder that contains example-balance>`

What it shows (see `docs/MODDING.md` for the rules):

* `data/units.json`, `data/cards.json`, `data/terrain.json` are **merged** into the game's files: only
  the keys listed here change (tank strength and attack, the tank card price, healing in capitals).
* `lang/en/stringtable_en.json` changes one English text ("Tank" becomes "Medium Tank").
* `mod.json` names the mod; `priority` orders several mods (higher wins), `enabled: false` turns it off.

`dotnet run -- --verify-data --mods <folder>` lists exactly what a mod changes compared with the
original game data.
