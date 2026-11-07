# Example mod: balance tweaks

**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

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

---

<a id="chinese"></a>

# 示例模组：平衡性调整

本模组发布时处于**禁用**状态（`mod.json` 中的 `"enabled": false`）。将其设为 `true` 即可在开发版本中试用（桌面版游戏会挂载仓库中的 `mods/` 文件夹），或者将该文件夹复制到游戏的模组文件夹中：

* Windows：`%LOCALAPPDATA%\WC2CSharp\mods\example-balance\`
* Android：`Android/data/com.wc2remake.csharp/files/mods/example-balance/`
* 或者以 `--mods <folder that contains example-balance>` 启动桌面版（参数为包含 example-balance 的文件夹）

本模组演示的内容（规则见仓库根目录下的 `MODDING.md`）：

* `data/json/units/units.json`、`data/json/cards/cards.json`、`data/json/terrain/terrain.json` 会被**合并**到游戏的文件中：只有其中列出的键会改变（坦克的兵力和攻击力、坦克卡牌的价格、首都中的治疗量）。
* `data/json/text/en/stringtable_en.json` 修改了一条英文文本（“Tank” 改为 “Medium Tank”）。
* `mod.json` 为模组命名；`priority` 决定多个模组之间的顺序（数值高者优先），`enabled: false` 将其关闭。

`dotnet run --project Hosts/Desktop -- --verify-data --mods mods --all-mods` 会精确列出各模组相对于原版游戏数据所做的修改（`--all-mods` 还会加载已禁用的模组）。
