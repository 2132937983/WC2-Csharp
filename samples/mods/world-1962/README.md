# 1962: Divided World (sample mod)

[English](#english) | [中文](#chinese)

<a id="english"></a>

A total conversion built with data only: an original alternate 1962 on the Natural Earth world map,
written to test how far the engine goes without code. It adds conquest 11 and changes nothing else.

**The world.** The war in Europe ended in 1943 with armistices; the Soviet state fell back behind the
Urals and broke apart; the Pacific war ended in a truce in 1945. In 1962 four blocs and six Russian
successor states share the map:

| Faction | Members | Relations |
|---|---|---|
| Berlin Pact | Greater Germanic Reich, the Reichskommissariate (Netherlands, Norway, Ostland, Ukraine, Muscovy, Caucasus), Burgundian State, French State, General Government, Denmark, Greater Finland, Slovakia, Hungary, Romania, Bulgaria, Croatia, Serbia, Italian Empire, Hellenic State, Spanish State, German Equatorial Africa, Iran | peace with the Atlantic Alliance until round 15, with the Sphere until round 25; at war with the successor states |
| Co-Prosperity Sphere | Empire of Japan, Manchukuo, Mengjiang, the Nanjing government, Thailand, Indochina, Burma, Philippines, Indonesia, Mongolia | peace with the Atlantic Alliance until round 10; at war with the Republic of China and the Far Eastern Republic |
| Atlantic Alliance | United States, United Kingdom, Canada, Australia, South Africa, Portugal, Republic of China, Mexico, Brazil, Colombia, Venezuela, Ecuador, Peru, Chile, Liberia | |
| Non-Aligned Movement | Indian Union, Turkey, Sweden, Switzerland, Ireland, Afghanistan, Saudi Arabia, Tibet, Argentina, Bolivia, Paraguay | at peace with everyone |
| six successor states | Soviet Union (Sverdlovsk), Orenburg Cossack Host, Siberian Republic, Far Eastern Republic, Alash Autonomy, Emirate of Bukhara | each a faction of its own: at war with each other and the Pact, at peace with the Atlantic Alliance and (but for the Far East) the Sphere |

Borders follow wartime plans (the Reichskommissariate, Himmler's Burgundy, Greater Finland, the
Co-Prosperity Sphere, the Arkhangelsk-Astrakhan line). The 25 countries the original game knows keep its
flags and units; the 40 new ones have flags drawn from simple designs and use the common unit art. The map
ends at 26 degrees south, as the original's does, so southern South America and Africa are only partly on it.

## Playing it

* **Windows**: copy this folder into `%LOCALAPPDATA%\WC2CSharp\mods\` (or into `mods\` next to `WC2.exe`).
  From the repository: `dotnet run --project Hosts/Desktop -- --mods samples/mods`.
* **Android**: copy this folder to `Android/data/com.wc2remake.csharp/files/mods/world-1962/`.
* Start the game, **Conquest**, the 11th entry, *1962 DIVIDED WORLD*. The world map
  (`map-natural-earth`) is built into the game and is needed.
* To switch it off: delete the folder, or set `"enabled": false` in `mod.json`.

Victory: defeat every country at war with you or bound to be; the Non-Aligned (at peace with everyone for
good) need not fall. When a truce ends, a commander announces it at the start of your round.

## Changing it

Everything here is generated from `source/world-1962.json`: the countries (names, factions, tags, colours,
flags, capitals, garrisons, fleets), the factions, the relations and the rules that decide who owns which
province. Edit it and build again:

```powershell
dotnet run --project Tools -- build-scenario --config samples/mods/world-1962/source/world-1962.json
```

then restart the game. The rules file format is in `MODDING.md`, section 4 ("A total conversion"); the
factions and relations in the scenario are described in `DATA_SCHEMA.md` ("Factions and relations").
A flag can also be a PNG of your own next to the rules (`"flag": "myflag.png"`).

| File | What |
|---|---|
| `mod.json` | id, name, priority, enabled |
| `data/json/scenarios/conquest_11.json` | the scenario |
| `conquest_11.bin` | area switches (all on) |
| `data/json/conquests/conquests.json` | the conquest list entry (wraps left-right, no fog of war) |
| `data/json/countries/countries.json` | the new countries |
| `flag_<id>.png`, `button_<id>@2x.png`, `sflag_<id>@2x.png` | their flags, list buttons, preview flags |
| `button_conquest11@2x.png` | the conquest list button |
| `data/json/text/...` | texts in English and Chinese (English in the other languages) |
| `source/world-1962.json` | the rules |

---

<a id="chinese"></a>

# 1962：分裂的世界（示例模组）

一个只用数据完成的全面改造：在 Natural Earth 世界地图上原创的 1962 年架空历史，用来测试引擎在不写代码的情况下
能走多远。它新增征服 11，不改变其他任何内容。

**世界设定。** 欧洲战争以 1943 年的停战告终；苏维埃国家退到乌拉尔山以东并分裂；太平洋战争以 1945 年的停战
结束。1962 年，四大阵营和六个俄国继承国瓜分了地图：

| 阵营 | 成员 | 外交关系 |
|---|---|---|
| 柏林公约 | 大日耳曼帝国、各帝国专员辖区（荷兰、挪威、东方、乌克兰、莫斯科、高加索）、勃艮第国、法兰西国、波兰总督府、丹麦、大芬兰、斯洛伐克、匈牙利、罗马尼亚、保加利亚、克罗地亚、塞尔维亚、意大利帝国、希腊国、西班牙国、德属赤道非洲、伊朗 | 与大西洋联盟和平至第 15 回合，与共荣圈和平至第 25 回合；与各继承国交战 |
| 大东亚共荣圈 | 大日本帝国、满洲国、蒙疆、南京国民政府、泰国、印度支那、缅甸、菲律宾、印度尼西亚、蒙古 | 与大西洋联盟和平至第 10 回合；与中华民国和远东共和国交战 |
| 大西洋联盟 | 美国、英国、加拿大、澳大利亚、南非、葡萄牙、中华民国、墨西哥、巴西、哥伦比亚、委内瑞拉、厄瓜多尔、秘鲁、智利、利比里亚 | |
| 不结盟运动 | 印度联邦、土耳其、瑞典、瑞士、爱尔兰、阿富汗、沙特阿拉伯、西藏、阿根廷、玻利维亚、巴拉圭 | 与所有国家和平 |
| 六个继承国 | 苏联（斯维尔德洛夫斯克）、奥伦堡哥萨克军、西伯利亚共和国、远东共和国、阿拉什自治国、布哈拉埃米尔国 | 各自为一个阵营：彼此交战并与柏林公约交战，与大西洋联盟和平，与共荣圈和平（远东共和国除外） |

边界依据战时的各种计划划定（各帝国专员辖区、希姆莱的勃艮第、大芬兰、大东亚共荣圈、阿尔汉格尔斯克-阿斯特拉罕
线）。原版游戏认识的 25 个国家保留游戏自己的国旗和单位；40 个新国家的国旗由简单的设计绘制，单位使用通用美术。
地图与原版一样只到南纬 26 度，因此南美洲和非洲的南部只有一部分在地图上。

## 游玩

* **Windows**：把本文件夹复制到 `%LOCALAPPDATA%\WC2CSharp\mods\`（或 `WC2.exe` 旁边的 `mods\`）。
  在代码仓库中：`dotnet run --project Hosts/Desktop -- --mods samples/mods`。
* **Android**：把本文件夹复制到 `Android/data/com.wc2remake.csharp/files/mods/world-1962/`。
* 启动游戏，进入 **征服**，第 11 项 *1962 DIVIDED WORLD*。需要世界地图（`map-natural-earth`），它已内置在
  游戏中。
* 关闭方法：删除该文件夹，或在 `mod.json` 中设置 `"enabled": false`。

胜利条件：击败所有正在或将要与你交战的国家；不结盟国家（与所有国家永久和平）无需消灭。停战结束时，会有一位将领
在你的回合开始时宣布。

## 修改

这里的一切都由 `source/world-1962.json` 生成：国家（名称、阵营、标签、颜色、国旗、首都、守军、舰队）、阵营、
外交关系，以及决定每个省份归属的规则。编辑它，然后重新构建：

```powershell
dotnet run --project Tools -- build-scenario --config samples/mods/world-1962/source/world-1962.json
```

然后重启游戏。规则文件的格式见 `MODDING.md` 第 4 节（“全面改造”）；剧本中的阵营和外交关系见 `DATA_SCHEMA.md`
（“阵营与外交关系”）。国旗也可以是放在规则文件旁边的你自己的 PNG（`"flag": "myflag.png"`）。

| 文件 | 内容 |
|---|---|
| `mod.json` | id、名称、优先级、是否启用 |
| `data/json/scenarios/conquest_11.json` | 剧本 |
| `conquest_11.bin` | 地区开关（全部开启） |
| `data/json/conquests/conquests.json` | 征服列表条目（左右循环，没有战争迷雾） |
| `data/json/countries/countries.json` | 新国家 |
| `flag_<id>.png`、`button_<id>@2x.png`、`sflag_<id>@2x.png` | 它们的国旗、列表按钮、预览小旗 |
| `button_conquest11@2x.png` | 征服列表按钮 |
| `data/json/text/...` | 英文和中文文本（其他语言使用英文） |
| `source/world-1962.json` | 规则 |
