# Map pipeline
**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

The classic map of the game (map 1: hand-drawn tiles, area geometry in `data/binary/maps/1/`) stays as
it is, and every campaign and conquest keeps using it. Next to it, `WC2.Tools build-map` generates a
**world base map** from real geography and history:

* a **map pack** (`assets/data/maps/natural-earth/`, data: regions, adjacency, owners in every era);
* a **playable map mod** (`mods/map-natural-earth/`: the map as map 2 in the scene's formats, with the
  conquest **WORLD 1939**).

The base map is **one map for every era**: its regions follow the borders of 1910, 1914, 1936, 1939,
1945, 1949 and today at the same time, so a scenario of any of these years only has to say who owns
which region. Disabling or deleting the mod reverts everything.

## Running it

```powershell
.\Tools\map\build-natural-earth.ps1          # downloads the sources once (about 75 MB, Tools\map\source,
                                             # not in git), rebuilds assets\data\maps\natural-earth and
                                             # mods\map-natural-earth
# or by hand:
dotnet Tools\bin\Debug\net9.0\WC2.Tools.dll build-map --config Tools\map\natural-earth.json --source <dir> `
    --out assets\data\maps\natural-earth --classic-out mods\map-natural-earth [--preview-dir <dir>]
```

It takes about two and a half minutes and is deterministic: the same sources and configuration give
the same files. Delete `mods\map-natural-earth` before rebuilding it (the export does not remove files
of an earlier, larger map). `--preview-dir` also draws every era (`era-<year>.png`) and close-ups
(`previewCrops`) for checking borders.

## Sources

| File | Source | Used for |
|---|---|---|
| `ne_10m_admin_1_states_provinces.geojson` | Natural Earth (public domain) | provinces: the building blocks of the regions; today's country (`adm0_a3`) |
| `ne_50m_populated_places.geojson` | Natural Earth | capitals of today (`ADM0CAP`), cities by population (`POP_MAX`) |
| `ne_10m_ports.geojson` | Natural Earth | ports (`scalerank`) |
| `ne_10m_airports.geojson` | Natural Earth | airfields (`type`, `scalerank`) |
| `CShapes-2.0.geojson` | CShapes 2.0, ETH Zurich (Schvitz, Rüegger, Girardin, Cederman, Weidmann, Gleditsch), CC BY-NC-SA 4.0, icr.ethz.ch/data/cshapes | the borders of states and colonies from 1886 to 2019, each with its start and end date, its Gleditsch-Ward code (`gwcode`) and capital |

Natural Earth comes from the `nvkelso/natural-earth-vector` repository. CShapes is licensed for
non-commercial use with attribution and share-alike: the map pack and the map mod are derived from it
and carry the same terms (the game earns nothing; see `map.json` `about`).

## How the base map is made

1. **Provinces.** Every Natural Earth province inside the bounds is drawn into an equirectangular
   raster (4500 pixels across the 360 degrees, 12.5 per degree), one colour per province, without
   antialiasing.
2. **Eras.** For every era with a `date`, the CShapes units valid on that day (states and colonies:
   "Austria-Hungary", "India", "Korea", "Algeria", ...) are drawn into a raster of their own; the era
   `naturalearth` (today) takes the province's country. On the land of every era:
   * land no unit covers takes the unit configured for its Natural Earth country (`fallback`:
     Greenland, Svalbard, Hong Kong, Kuwait), else the province's main unit, else the nearest unit;
   * inside a province, a fragment of another unit than the province's main one that is smaller than
     `minFragmentKm2` or nowhere `minFragmentWidthPixels` thick joins the neighbouring fragment: that is
     the two sources tracing the same border a few kilometres apart, not a real border.
3. **Signatures.** A pixel's signature is its unit in every era, plus the occupation zone a scenario
   gives its province (`provinceOwners`). A province splits into parts of one signature each.
4. **Regions.** Parts merge with touching parts **of the same signature** only, smallest first, until a
   region has `minAreaKm2` and a country (today's) has at most `maxPerCountry` regions
   (`regionOverrides` make Europe finer). So no era's border is ever merged away. What stays below
   `minIslandKm2` joins a neighbour when it touches land (an enclave loses its own borders) and is left
   out when it is an island.
5. **Islands and the seam.** A group of islands (parts closer than `islandClusterDegrees`) farther than
   `islandSplitDegrees` from its region's main body becomes a region of its own when it has
   `minIslandKm2` (Novaya Zemlya, Franz Josef Land, the Kurils, ...); smaller groups farther than
   `maxPartDistanceDegrees` are left out. On a wrapping map (`wrap`), a region with pixels near both
   edges is split at the seam into two.
6. **Sea zones of irregular shape.** Seeds are placed on the water, `coastRadiusDegrees` apart along
   the coasts and farther apart the more open the water (radius + `growth` x distance from the coast, at
   most `oceanRadiusDegrees`): small zones along the coasts, large ones in the open ocean. Zones grow
   from the seeds through the water (a zone never crosses land) and are relaxed `relaxIterations` times
   (each seed moves to its zone's middle), which gives polygon shapes rather than a grid. Water no seed
   reaches (lakes, enclosed seas) gets its own seed when it has `minSeaPixels`; smaller zones join a
   neighbour. The seam of a wrapping map is a zone border.
7. **Adjacency.** Two regions are neighbours when their pixels touch (4-neighbourhood) along at least
   `minBorderPixels`, over the seam on a wrapping map; `straits` link the sea zones at two points where
   the raster closes a waterway (Bosporus, Gibraltar, the Suez and Panama canals, ...).
8. **Features.** Capitals of today (the most populous place marked `ADM0CAP` per country), and the
   capitals the game's countries had in the other eras (`homeUnits`: Saint Petersburg, Istanbul,
   Calcutta, Bonn, ...) make a region a `capital`; places above `minPopulation` a `normal city` or
   `large city`; ports (`scalerank` up to `maxScalerank`) make a coastal region a `port`; major airports
   give it an airfield. The area types are those of `data/json/terrain/terrain.json`.
9. **Owners.** Every land region records its unit in every era (`owners` in `regions.json`; the units'
   names and capitals per era are in `map.json` `eras`).

## One map for every era: making a scenario

A scenario is an entry of `classicExport.scenarios`; the export turns it into a conquest:

```jsonc
{
  "number": 11, "file": "conquest_11.xml", "era": "1914", "title": "WORLD 1914",
  "centerLon": 15, "centerLat": 48, "fogOfWar": false, "leftRightWrap": true,
  "intro": "...{country}...", "victory": "Take the capitals of your enemies.",
  "names": { "hu": "Austria-Hungary" },
  // CShapes units (gwcode, see map.json eras -> "1914" -> units) -> the game's country
  "owners": { "de": ["255"], "hu": ["300"], "ru": ["365"], "tr": ["640"], "gb": ["200", "750"] },
  // provinces held by another country than their unit (Natural Earth iso_3166_2 or name)
  "provinceOwners": { },
  // the countries of the scenario: alliance a/b/c (n or d: neutral, passive), commander, colour,
  // optional garrison (extra infantry in capitals and large cities), fleets, money, industry, techlevel
  "players": { "de": { "alliance": "a", "commander": "Guderian", "color": [71, 87, 99] } }
}
```

Units no `owners` entry names start unclaimed. In the era `naturalearth` (today) the `countries` table
maps Natural Earth countries to game ids, so a 2018 scenario needs no `owners`. A region keeps one owner;
the borders inside the base map are the union of all eras, so owners never have to split a region.

The game knows 31 countries with flags (`data/json/countries/countries.json`); a historical state
without one is either given to the nearest game country in `owners` (WORLD 1939: Austria and
Czechoslovakia to Germany, Korea and Taiwan to Japan, the colonies to their empires, Mongolia to the
Soviet Union, China to the Republic of China `tw`) or left unclaimed (neutral land without armies).

## The configuration (`Tools/map/natural-earth.json`)

| Field | World base map |
|---|---|
| `bounds`, `wrap` | west -180, east 180 (wrapping), north 82 (the Russian polar islands, all of northern Europe), south -26 (southern Madagascar, northern Australia) |
| `raster.width` | 4500 (12.5 pixels per degree, about 9 km at the equator) |
| `eras` | 1910-01-01, 1914-07-01, 1936-01-01, 1939-09-01, 1945-09-02, 1949-12-31 (CShapes), 2018 (`"source": "naturalearth"`) |
| `historicalBorders` | `file`, `minFragmentKm2` 4000, `minFragmentWidthPixels` 3, `fallback` (Natural Earth country -> gwcode) |
| `homeUnits` | game country -> its CShapes units (capitals of every era) |
| `regions` | `minAreaKm2` 40 000, `maxPerCountry` 30, `minIslandKm2` 2 500, `islandSplitDegrees` 6, `islandClusterDegrees` 1.5, `maxPartDistanceDegrees` 25; Europe and Japan finer (`regionOverrides`), Russia up to 60 regions |
| `sea` | `coastRadiusDegrees` 1.8, `oceanRadiusDegrees` 14, `growth` 0.9, `relaxIterations` 2, `minSeaPixels` 300 |
| `straits` | Bosporus, Dardanelles, Gibraltar, Suez Canal, Panama Canal, Kiel Canal, Øresund, Kerch, Bab-el-Mandeb |
| `cities`, `ports`, `airfields` | `minPopulation` 1 000 000, `largeCityPopulation` 5 000 000; `maxScalerank` 5 and 4; airfield types `major`, `major and military` |
| `previewEra`, `previewCrops` | the era the pack's preview shows (1939); close-ups for `--preview-dir` |
| `classicExport` | `mapId` 2, `scale` 2 (9000 x 2700 map units), `modId`, `modName`, `scenarios` |
| `countries` | Natural Earth `ADM0_A3` -> game ids (`DEU` -> `de`, `USA` -> `am`, ...); others keep their ISO code |

## The map pack (`assets/data/maps/natural-earth/`)

| File | Content |
|---|---|
| `map.json` | id, name, bounds, projection, `wrap`, raster size, per country (today) its regions and capital, `eras` (source and units with names and capitals), the configuration used (schema `map-pack.schema.json`) |
| `regions.json` | one region per line: `id`, `name`, `sea` or `country` (today) + `type`, `city`, `port`, `airfield`, `coastal`, `provinces`, `owners` (era -> unit), `center` (lon, lat), `pixel`, `areaKm2`, `neighbours` (schema `map-regions.schema.json`) |
| `regions.raster` | the region of every pixel: `WCRG`, version, width, height, then deflate compressed uint16 (region id + 1, 0 = none) |
| `preview.png` | regions coloured by their 1939 unit, region borders, capitals (yellow) and other key areas (white) |

The pack: 1074 land regions, 1116 sea zones, 156 capitals (4 of earlier eras), 186 ports, 129
airfields, 7 straits. `Data/MapPack.cs` reads a pack through the asset layer (mods can replace or
extend it), answers "which region is at this longitude / latitude" (across the seam) and checks it (ids,
symmetric adjacency, every land region owned, connected and known in every era, ports only on coasts).

## Playing on the generated map

`--classic-out DIR` writes the map in the formats the game's scene reads for the classic map, as
**map 2**, with the conquests of `scenarios`, into a mod: `mods/map-natural-earth/` (5.5 MB).

| File | Read by | Content |
|---|---|---|
| `area2.bin` | `CScene.LoadAreaData` | per area: zone image box, army, building and installation anchors, land or sea; 9000 x 2700 map units (the provinces are drawn again at that resolution) |
| `adjion2.bin` | `CScene.LoadAdjoin` | up to 16 neighbours per area (the longest borders, straits and the seam included), kept only when both sides keep them |
| `areamark2.raw` | `CAreaMark` | the area under every 8 x 8 block of map units (touch picking) |
| `data/json/maps/2/areas.json` | `LoadAreaTax` | the type and base tax of **every** area; sea zones are listed as `normal` with tax 0 like the original's (an area missing there would load as land) |
| `map2_<n>.png` | `CBackground` | 108 background tiles of 500 units: land, sea, coasts, region and faint sea zone borders; no country borders (the background serves every era); seamless across the seam |
| `m2_zone<n>_1.png/.xml` | `InitAreaImage` | 13 atlas pages with the region masks, tinted with the owner's colour |
| `generated/texture_formats.json` | `ecGraphics` | tiles uploaded as RGB565, masks as Alpha8 |
| `conquest_10.xml`, `conquest_10.bin` | `LoadBattle` | WORLD 1939: 27 countries, 902 owned areas, armies in capitals and cities, 60 destroyers at the ports, every area enabled |
| `data/json/conquests/conquests.json`, `data/json/text/<lang>/...`, `button_conquest10@2x.png` | the conquest menu | the entry "conquest 10" with `fogOfWar: false` and `leftRightWrap: true`, its title, country names, intros and victory texts (English in every language), its button |

The mod is **built into the package** (`assets/build.json` `builtInMods`: it ships inside the APK and
next to the desktop program and is mounted like any mod; `--no-mods` leaves it out, a mod folder with
the same id replaces it). The conquest list has **WORLD 1939** on its second page, with a live preview.
On the desktop it can also start right away: `dotnet run --project Hosts/Desktop -- --scenario
conquest_10.xml --country fr`, or from a script with `startconquest conquest_10.xml fr`.

In the game the map wraps left-right (the camera scrolls past the date line; moves, arrows, airstrike
ranges and distances take the short way over the seam), zooming out stops when the map fills the screen
height, and land units that enter a sea zone board transports as on the classic map. Texture memory at
the start of WORLD 1939: 215 MB on the desktop (108 tiles 54 MB, 13 zone pages 52 MB).

## Checks

* `Tests/Unit/MapPackTests.cs`: the pack is consistent and wraps, with its seven eras; units by era
  (Vienna in Austria-Hungary in 1914, Warsaw Russian in 1910 and Polish in 1939, Helsinki, Seoul);
  Lviv and Krakow are separate regions (the 1945 border); places lie in the right countries (Cape
  Farewell, Franz Josef Land, Svalbard, Chukotka east of the date line, southern Madagascar, Darwin;
  the capitals, Saint Petersburg as an earlier capital); Marseille is a French port; and the pipeline on
  a synthetic world with a historical border that splits a province, a sliver that does not, a scenario
  of that era and the exported files (sea zones in the area table, the conquest options).
* `Tests/Unit/CameraTests.cs`: the zoom limit and wrapping.
* `Tests/world_menu` (menu, preview, game) and `Tests/world_autoplay` (WORLD 1939 as France, the seam at
  the Bering Strait, fully zoomed out, four rounds of the standard AI).

### Still to do

* The menu texts of the world conquests are English in every language.
* Only WORLD 1939 is a conquest; the other eras are in the base map and need only a `scenarios` entry.
* Occupations are by whole province (Japan in China, 1939); CShapes has no wartime occupations.
* Memory: all 108 tiles are loaded at the start; loading only the visible ones would save most of it.
* Balance: start money, taxes and armies are simple rules of the export, not tuned by play.

---

<a id="chinese"></a>
# 地图生成流程

游戏的经典地图（地图 1：手绘地图贴图块，地区几何数据在 `data/binary/maps/1/`）保持不变，所有战役和征服
仍然使用它。在它之外，`WC2.Tools build-map` 根据真实的地理和历史生成一张**世界底图**：

* 一个**地图包**（`assets/data/maps/natural-earth/`，数据：区域、相邻关系、各时代的归属）；
* 一个**可玩的地图模组**（`mods/map-natural-earth/`：以场景格式输出的地图 2，附带征服 **WORLD 1939**）。

这张底图是**所有时代共用的一张地图**：它的区域同时遵循 1910、1914、1936、1939、1945、1949 年和今天的
边界，因此这些年份中任何一年的剧本只需要说明哪个区域归谁。关闭或删除该模组即可完全恢复原样。

## 运行

```powershell
.\Tools\map\build-natural-earth.ps1          # downloads the sources once (about 75 MB, Tools\map\source,
                                             # not in git), rebuilds assets\data\maps\natural-earth and
                                             # mods\map-natural-earth
# or by hand:
dotnet Tools\bin\Debug\net9.0\WC2.Tools.dll build-map --config Tools\map\natural-earth.json --source <dir> `
    --out assets\data\maps\natural-earth --classic-out mods\map-natural-earth [--preview-dir <dir>]
```

脚本会下载一次源数据（约 75 MB，存放在 `Tools\map\source`，不进 git），然后重建
`assets\data\maps\natural-earth` 和 `mods\map-natural-earth`。整个过程约两分半钟，结果是确定性的：相同的
源数据和配置得到相同的文件。重建前请先删除 `mods\map-natural-earth`（导出不会删除更早、更大的地图留下的
文件）。`--preview-dir` 还会绘制每个时代（`era-<年份>.png`）和局部放大图（`previewCrops`），用于检查边界。

## 数据来源

| 文件 | 来源 | 用途 |
|---|---|---|
| `ne_10m_admin_1_states_provinces.geojson` | Natural Earth（公有领域） | 省份：区域的基本单元；今天的所属国家（`adm0_a3`） |
| `ne_50m_populated_places.geojson` | Natural Earth | 今天的首都（`ADM0CAP`）、按人口确定的城市（`POP_MAX`） |
| `ne_10m_ports.geojson` | Natural Earth | 港口（`scalerank`） |
| `ne_10m_airports.geojson` | Natural Earth | 机场（`type`、`scalerank`） |
| `CShapes-2.0.geojson` | CShapes 2.0，苏黎世联邦理工学院（Schvitz、Rüegger、Girardin、Cederman、Weidmann、Gleditsch），CC BY-NC-SA 4.0，icr.ethz.ch/data/cshapes | 1886 年至 2019 年各国家和殖民地的边界，每个都带有起止日期、Gleditsch-Ward 代码（`gwcode`）和首都 |

Natural Earth 数据取自 `nvkelso/natural-earth-vector` 仓库。CShapes 的许可要求非商业使用、署名并以相同
方式共享：地图包和地图模组由它派生，适用同样的条款（本游戏不盈利；见 `map.json` 的 `about`）。

## 底图的生成步骤

1. **省份。** 边界范围内的每个 Natural Earth 省份被绘制到一张等距圆柱投影的栅格中（360 度共 4500 像素，
   每度 12.5 像素），每个省份一种颜色，不做抗锯齿。
2. **时代。** 对每个带 `date` 的时代，把当天有效的 CShapes 单元（国家和殖民地：“Austria-Hungary”、
   “India”、“Korea”、“Algeria”……）绘制到各自的栅格中；时代 `naturalearth`（今天）直接使用省份所属的
   国家。在每个时代的陆地上：
   * 没有单元覆盖的陆地，先使用为其 Natural Earth 国家配置的单元（`fallback`：格陵兰、斯瓦尔巴、香港、
     科威特），否则使用该省份的主要单元，再否则使用最近的单元；
   * 在一个省份内部，如果某个碎片属于不同于该省主要单元的单元，并且面积小于 `minFragmentKm2`，或者任何
     地方都没有 `minFragmentWidthPixels` 那么厚，它就并入相邻的碎片：这是两个数据源相距几公里描绘同一条
     边界造成的，而不是真实的边界。
3. **签名。** 一个像素的签名是它在每个时代所属的单元，再加上某个剧本给它所在省份指定的占领区
   （`provinceOwners`）。一个省份会被拆分为若干部分，每部分只有一种签名。
4. **区域。** 各部分只与**签名相同**且相邻的部分合并，从最小的开始，直到区域达到 `minAreaKm2`、并且每个
   （今天的）国家最多有 `maxPerCountry` 个区域（`regionOverrides` 让欧洲更细）。因此任何时代的边界都不会
   被合并掉。仍小于 `minIslandKm2` 的部分，如果与陆地相连就并入邻居（飞地失去自己的边界），如果是岛屿
   就舍弃。
5. **岛屿与接缝。** 一组岛屿（彼此距离小于 `islandClusterDegrees` 的部分）若距离所属区域主体超过
   `islandSplitDegrees`，且面积达到 `minIslandKm2`，就成为独立的区域（新地岛、法兰士约瑟夫地群岛、
   千岛群岛……）；更小且距离超过 `maxPartDistanceDegrees` 的岛组被舍弃。在左右循环的地图（`wrap`）上，
   在两侧边缘附近都有像素的区域会在接缝处一分为二。
6. **形状不规则的海区。** 在水面上放置种子：沿海岸间隔 `coastRadiusDegrees`，水域越开阔间隔越大（半径
   + `growth` × 离岸距离，最多 `oceanRadiusDegrees`）：沿岸是小海区，远洋是大海区。海区从种子出发在水中
   生长（海区不会跨越陆地），并松弛 `relaxIterations` 次（每个种子移到其海区的中心），因此得到的是多边形
   而不是方格。种子到达不了的水域（湖泊、封闭的海）若达到 `minSeaPixels` 就获得自己的种子；更小的海区
   并入邻居。循环地图的接缝是海区边界。
7. **相邻关系。** 两个区域的像素沿至少 `minBorderPixels` 的长度相接（4 邻域）即为邻居，在循环地图上跨越
   接缝同样成立；`straits` 在栅格封闭了水道的两点之间连接海区（博斯普鲁斯、直布罗陀、苏伊士运河和
   巴拿马运河……）。
8. **要素。** 今天的首都（每个国家标为 `ADM0CAP` 的人口最多的地点），以及游戏中的国家在其他时代的首都
   （`homeUnits`：圣彼得堡、伊斯坦布尔、加尔各答、波恩……）使区域成为 `capital`；人口超过 `minPopulation`
   的地点使区域成为 `normal city` 或 `large city`；港口（`scalerank` 不超过 `maxScalerank`）使沿海区域
   成为 `port`；主要机场给区域一个机场。地区类型就是 `data/json/terrain/terrain.json` 中的类型。
9. **归属。** 每个陆地区域记录它在每个时代所属的单元（`regions.json` 中的 `owners`；各时代单元的名称和
   首都在 `map.json` 的 `eras` 中）。

## 一张地图适用所有时代：制作剧本

一个剧本就是 `classicExport.scenarios` 中的一项；导出时会把它变成一个征服：

```jsonc
{
  "number": 11, "file": "conquest_11.xml", "era": "1914", "title": "WORLD 1914",
  "centerLon": 15, "centerLat": 48, "fogOfWar": false, "leftRightWrap": true,
  "intro": "...{country}...", "victory": "Take the capitals of your enemies.",
  "names": { "hu": "Austria-Hungary" },
  // CShapes units (gwcode, see map.json eras -> "1914" -> units) -> the game's country
  "owners": { "de": ["255"], "hu": ["300"], "ru": ["365"], "tr": ["640"], "gb": ["200", "750"] },
  // provinces held by another country than their unit (Natural Earth iso_3166_2 or name)
  "provinceOwners": { },
  // the countries of the scenario: alliance a/b/c (n or d: neutral, passive), commander, colour,
  // optional garrison (extra infantry in capitals and large cities), fleets, money, industry, techlevel
  "players": { "de": { "alliance": "a", "commander": "Guderian", "color": [71, 87, 99] } }
}
```

其中：`owners` 把该时代的 CShapes 单元（gwcode，见 `map.json` 的 `eras` → `"1914"` → `units`）映射到游戏
国家；`provinceOwners` 列出由其单元以外的国家控制的省份（Natural Earth 的 `iso_3166_2` 或名称）；
`players` 是剧本中的国家：阵营 a/b/c（n 或 d 表示中立、不行动）、将领、颜色，以及可选的 `garrison`
（首都和大城市额外的步兵）、`fleets`、`money`、`industry`、`techlevel`。

`owners` 中没有列出的单元开局无主。在时代 `naturalearth`（今天）中，`countries` 表把 Natural Earth 国家
映射为游戏 id，因此 2018 年的剧本不需要 `owners`。每个区域只有一个归属者；底图中的边界是所有时代边界的
并集，所以归属永远不需要拆分区域。

游戏认识 31 个有国旗的国家（`data/json/countries/countries.json`）；没有国旗的历史国家，要么在 `owners`
中交给最接近的游戏国家（WORLD 1939：奥地利和捷克斯洛伐克归德国，朝鲜和台湾归日本，殖民地归各自的帝国，
蒙古归苏联，中国归中华民国 `tw`），要么保持无主（没有军队的中立土地）。

## 配置（`Tools/map/natural-earth.json`）

| 字段 | 世界底图 |
|---|---|
| `bounds`、`wrap` | 西 -180、东 180（左右循环），北 82（俄罗斯北极群岛，以及整个北欧），南 -26（马达加斯加南部、澳大利亚北部） |
| `raster.width` | 4500（每度 12.5 像素，赤道处约 9 公里） |
| `eras` | 1910-01-01、1914-07-01、1936-01-01、1939-09-01、1945-09-02、1949-12-31（CShapes），2018（`"source": "naturalearth"`） |
| `historicalBorders` | `file`、`minFragmentKm2` 4000、`minFragmentWidthPixels` 3、`fallback`（Natural Earth 国家 → gwcode） |
| `homeUnits` | 游戏国家 → 它的 CShapes 单元（用于各时代的首都） |
| `regions` | `minAreaKm2` 40 000、`maxPerCountry` 30、`minIslandKm2` 2 500、`islandSplitDegrees` 6、`islandClusterDegrees` 1.5、`maxPartDistanceDegrees` 25；欧洲和日本更细（`regionOverrides`），俄罗斯最多 60 个区域 |
| `sea` | `coastRadiusDegrees` 1.8、`oceanRadiusDegrees` 14、`growth` 0.9、`relaxIterations` 2、`minSeaPixels` 300 |
| `straits` | 博斯普鲁斯、达达尼尔、直布罗陀、苏伊士运河、巴拿马运河、基尔运河、厄勒海峡、刻赤、曼德海峡 |
| `cities`、`ports`、`airfields` | `minPopulation` 1 000 000、`largeCityPopulation` 5 000 000；`maxScalerank` 分别为 5 和 4；机场类型 `major`、`major and military` |
| `previewEra`、`previewCrops` | 地图包预览图显示的时代（1939）；`--preview-dir` 的局部放大图 |
| `classicExport` | `mapId` 2、`scale` 2（9000 × 2700 地图单位）、`modId`、`modName`、`scenarios` |
| `countries` | Natural Earth 的 `ADM0_A3` → 游戏 id（`DEU` → `de`、`USA` → `am`……）；其他国家保留其 ISO 代码 |

## 地图包（`assets/data/maps/natural-earth/`）

| 文件 | 内容 |
|---|---|
| `map.json` | id、名称、边界、投影、`wrap`、栅格尺寸、每个（今天的）国家的区域数和首都、`eras`（来源以及各单元的名称和首都）、所用的配置（模式 `map-pack.schema.json`） |
| `regions.json` | 每行一个区域：`id`、`name`、`sea` 或 `country`（今天）+ `type`、`city`、`port`、`airfield`、`coastal`、`provinces`、`owners`（时代 → 单元）、`center`（经度、纬度）、`pixel`、`areaKm2`、`neighbours`（模式 `map-regions.schema.json`） |
| `regions.raster` | 每个像素所属的区域：`WCRG`、版本、宽、高，然后是 deflate 压缩的 uint16（区域 id + 1，0 表示无） |
| `preview.png` | 按 1939 年单元着色的区域、区域边界、首都（黄色）和其他重要地区（白色） |

地图包：1074 个陆地区域、1116 个海区、156 个首都（其中 4 个来自更早的时代）、186 个港口、129 个机场、
7 条海峡连接。`Data/MapPack.cs` 通过资源层读取地图包（模组可以替换或扩展它），回答“这个经纬度上是哪个
区域”（可跨越接缝），并检查它（id、对称的相邻关系、每个陆地区域都有归属、相互连通、在每个时代都有已知的
单元、港口只在海岸上）。

## 在生成的地图上游戏

`--classic-out DIR` 把地图以游戏场景读取经典地图的格式写成**地图 2**，连同 `scenarios` 中的征服一起输出
为模组：`mods/map-natural-earth/`（5.5 MB）。

| 文件 | 读取者 | 内容 |
|---|---|---|
| `area2.bin` | `CScene.LoadAreaData` | 每个地区：区域图像范围，部队、建筑和设施的锚点，陆地或海洋；9000 × 2700 地图单位（省份按此分辨率重新绘制） |
| `adjion2.bin` | `CScene.LoadAdjoin` | 每个地区最多 16 个邻居（最长的边界，包括海峡和接缝），只保留双方都保留的 |
| `areamark2.raw` | `CAreaMark` | 每个 8 × 8 地图单位方块下的地区（触摸选取） |
| `data/json/maps/2/areas.json` | `LoadAreaTax` | **每个**地区的类型和基础税收；海区与原版一样以 `normal`、税收 0 列出（表中缺少的地区会被当作陆地加载） |
| `map2_<n>.png` | `CBackground` | 108 块 500 单位的背景贴图：陆地、海洋、海岸线、区域边界和淡淡的海区边界；没有国界（背景供所有时代共用）；跨越接缝无缝衔接 |
| `m2_zone<n>_1.png/.xml` | `InitAreaImage` | 13 页区域掩码图集，用归属国的颜色着色 |
| `generated/texture_formats.json` | `ecGraphics` | 贴图块以 RGB565 上传，掩码以 Alpha8 上传 |
| `conquest_10.xml`、`conquest_10.bin` | `LoadBattle` | WORLD 1939：27 个国家、902 个有归属的地区、首都和城市中的部队、港口处的 60 艘驱逐舰、所有地区启用 |
| `data/json/conquests/conquests.json`、`data/json/text/<lang>/...`、`button_conquest10@2x.png` | 征服菜单 | 条目“conquest 10”，带 `fogOfWar: false` 和 `leftRightWrap: true`，以及标题、国家名称、开场介绍和胜利条件文本（所有语言都是英文）、按钮 |

该模组**内置在游戏资源包中**（`assets/build.json` 的 `builtInMods`：它随 APK 以及桌面程序一起发布，并像
其他模组一样加载；`--no-mods` 不加载它，同 id 的模组文件夹会替换它）。征服列表第二页有 **WORLD 1939**，
带实时预览。在桌面版上也可以直接开始：`dotnet run --project Hosts/Desktop -- --scenario conquest_10.xml
--country fr`，或在测试脚本中用 `startconquest conquest_10.xml fr`。

游戏中地图左右循环（镜头可以越过日期变更线；移动、箭头、空袭范围和距离都走跨越接缝的近路），缩小到地图
填满屏幕高度时停止，进入海区的陆军会像在经典地图上一样登上运输船。WORLD 1939 开局时的纹理内存：桌面版
215 MB（108 块贴图 54 MB，13 页区域图集 52 MB）。

## 检查

* `Tests/Unit/MapPackTests.cs`：地图包一致且可循环，包含七个时代；各时代的单元（1914 年维也纳属奥匈帝国，
  华沙 1910 年属俄国、1939 年属波兰，赫尔辛基，首尔）；利沃夫和克拉科夫是不同的区域（1945 年的边界）；
  地点位于正确的国家（法韦尔角、法兰士约瑟夫地群岛、斯瓦尔巴、日期变更线以东的楚科奇、马达加斯加南部、
  达尔文；各首都，以及作为早期首都的圣彼得堡）；马赛是法国港口；以及在一个合成小世界上运行的生成流程：
  一条历史边界拆分了一个省份、一个细碎片没有拆分、该时代的一个剧本，以及导出的文件（区域表中的海区、
  征服选项）。
* `Tests/Unit/CameraTests.cs`：缩放限制和左右循环。
* `Tests/world_menu`（菜单、预览、游戏）和 `Tests/world_autoplay`（以法国进行 WORLD 1939，白令海峡处的
  接缝，最大缩小，标准 AI 进行四个回合）。

### 待办

* 世界征服的菜单文本在所有语言中都是英文。
* 目前只有 WORLD 1939 是征服；其他时代已经在底图中，只需要一个 `scenarios` 条目。
* 占领以整个省份为单位（1939 年日本在中国）；CShapes 没有战时占领数据。
* 内存：开局时加载全部 108 块贴图；只加载可见的贴图块可以节省大部分。
* 平衡：初始资金、税收和部队是导出时的简单规则，尚未经过实际游戏调整。
