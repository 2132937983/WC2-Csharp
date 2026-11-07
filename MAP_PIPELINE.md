# Map pipeline
**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

The classic map of the game (map 1: hand-drawn tiles, area geometry in `data/binary/maps/1/`) stays as
it is, and every campaign and conquest keeps using it. Next to it, `WC2.Tools build-map` generates a
**world base map** from real geography and history:

* a **map pack** (`assets/data/maps/natural-earth/`, data: regions, adjacency, owners in every era);
* a **playable map mod** (`mods/map-natural-earth/`: the map as map 2, drawn from vertex buffers, with the
  conquest **WORLD 1939**).

The base map is **one map for every era**: its regions follow the borders of 1910, 1914, 1936, 1939,
1945, 1949 and today at the same time, so a scenario of any of these years only says who owns which
region. It has **7 327 land provinces** (about the granularity of Age of History II; 1939 Austria has
about 15, Czechoslovakia about 10, Poland about 70) and 1 145 sea zones, inland seas and canals
included. It is drawn in the style of European War 4 and World Conqueror 3: natural colours and shaded
relief under translucent owner colours, paper grain, an ocean with depth and coastal wave lines.
Disabling or deleting the mod reverts everything.

## Running it

```powershell
.\Tools\map\build-natural-earth.ps1          # downloads the sources once (about 95 MB, Tools\map\source,
                                             # not in git), rebuilds assets\data\maps\natural-earth and
                                             # mods\map-natural-earth
# or by hand:
dotnet Tools\bin\Debug\net9.0\WC2.Tools.dll build-map --config Tools\map\natural-earth.json --source <dir> `
    --out assets\data\maps\natural-earth --classic-out mods\map-natural-earth [--preview-dir <dir>]
```

It takes about two minutes and is deterministic. Delete `mods\map-natural-earth` before rebuilding it
(the export does not remove files of an earlier map), and rebuild the game afterwards (the mod is built
into the package). `--preview-dir` also draws every era (`era-<year>.png`) and close-ups (`previewCrops`).

## Sources

| File | Source | Used for |
|---|---|---|
| `ne_10m_admin_1_states_provinces.geojson` | Natural Earth (public domain) | provinces: the building blocks of the regions; today's country (`adm0_a3`) |
| `ne_50m_populated_places.geojson` | Natural Earth | capitals of today (`ADM0CAP`), cities by population (`POP_MAX`) |
| `ne_10m_populated_places.geojson` | Natural Earth | the names of the provinces (the largest town inside) |
| `ne_10m_ports.geojson` | Natural Earth | ports (`scalerank`) |
| `ne_10m_airports.geojson` | Natural Earth | airfields (`type`, `scalerank`) |
| `ne_10m_lakes.geojson`, `ne_10m_lakes_historic.geojson` | Natural Earth | the large lakes (the province layer covers them as land) and the Aral Sea of 1960 |
| `ne2/NE2_50M_SR_W/NE2_50M_SR_W.tif` | Natural Earth II with shaded relief and water, 1:50m (public domain) | the terrain picture: natural colours and relief |
| `CShapes-2.0.geojson` | CShapes 2.0, ETH Zurich (Schvitz, Rüegger, Girardin, Cederman, Weidmann, Gleditsch), CC BY-NC-SA 4.0, icr.ethz.ch/data/cshapes | the borders of states and colonies from 1886 to 2019, each with its dates, Gleditsch-Ward code (`gwcode`) and capital |

CShapes is licensed for non-commercial use with attribution and share-alike: the map pack and the map
mod are derived from it and carry the same terms (the game earns nothing; see `map.json` `about`).

## How the base map is made

1. **Provinces and water.** Every Natural Earth province inside the bounds is drawn into an
   equirectangular raster (4500 pixels across the 360 degrees, 12.5 per degree), without antialiasing;
   then the water is cut out of it (`water`, see below): the large lakes and the straits and canals.
2. **Eras.** For every era with a `date`, the CShapes units valid that day (states and colonies) are drawn
   into a raster of their own; the era `naturalearth` (today) takes the province's country. Land no unit
   covers takes the `fallback` unit of its Natural Earth country, else the province's main unit, else the
   nearest; inside a province, a fragment of another unit smaller than `minFragmentKm2` or nowhere
   `minFragmentWidthPixels` thick (two sources tracing one border apart) joins its neighbour.
3. **Signatures.** A pixel's signature is its unit in every era plus the occupation zone a scenario gives
   its province (`provinceOwners`). A province splits into parts of one signature each.
4. **Regions of the right size.** Every part has a target area from `detail`: its country, else the first
   zone that contains it, else `defaultKm2` (finest in Europe and East Asia, coarse in deserts, Siberia and
   the Arctic). Parts merge **only with neighbours of the same signature**, smallest first, until they reach
   `mergeBelow` x the target, so no era's border is ever merged away. A group above `splitAbove` x the
   target is cut into cells of about the target area: seeds by farthest point sampling, grown through the
   group (a cell never crosses the group's edge, so every era's border stays) and relaxed three times. What
   stays below `minIslandKm2` joins a land neighbour (an enclave) or is left out (an island).
5. **Islands and the seam.** Island groups farther than `islandSplitDegrees` from their region become
   regions of their own when large enough (Novaya Zemlya, the Kurils, ...); a wrapping map splits regions at
   the seam.
6. **Sea zones of irregular shape.** Seeds spaced by the distance from the coast (small zones along the
   coasts, large ones in the open ocean), grown through the water and relaxed; lakes large enough get a
   zone; the seam is a zone border.
7. **Adjacency** over the seam on a wrapping map, plus `straits` (links between sea zones where the
   water is too narrow even for a channel: the Kiel Canal, Bab-el-Mandeb, the rivers between the Great
   Lakes); a region left touching nothing (an island in a small lake) is linked to the nearest region
   across the water.
8. **Features.** Capitals of today and of the game's countries in the other eras (`homeUnits`), cities,
   ports (a port region keeps its land type; its harbour is the sea zone off it, see below), airfields.
   A province is named after the largest town inside it, else after its Natural Earth province with a
   compass point.
9. **Owners**: every land region records its unit in every era (`regions.json` `owners`, `map.json` `eras`).

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
  // transportCards (default true): units on a coast start with a transport ship
  // the countries: alliance a/b/c (n or d: neutral, passive), commander, colour, optional garrison,
  // fleets, money, industry, techlevel
  "players": { "de": { "alliance": "a", "commander": "Guderian", "color": [71, 87, 99] } }
}
```

Units no `owners` entry names start unclaimed; in the era `naturalearth` the `countries` table maps
today's countries. The game knows 31 countries with flags; a historical state without one is given to the
nearest game country (WORLD 1939: Austria and Czechoslovakia to Germany, Korea and Taiwan to Japan, the
colonies to their empires, Mongolia to the Soviet Union, China to the Republic of China `tw`) or left
unclaimed.

## Lakes, straits and canals

The province layer covers lakes as land, and straits a few kilometres wide close in a raster of 9 km
pixels. `water` therefore cuts them out of the provinces at both resolutions (the region raster and the
outline raster), before anything else:

* **lakes**: Natural Earth lakes up to `maxScalerank` 2 (the Great Lakes, Baikal, Ladoga, Onega,
  Balkhash, Victoria, ...) and, from the historical lakes, the Aral Sea of 1960. They get sea zones like
  the oceans: inland seas, navigable by ships built in their harbours and by transports from their shores
  (the Caspian Sea was water already). The five Great Lakes are joined by their rivers (`straits`).
* **channels**: the Bosporus, the Dardanelles, the Suez and Panama canals, Gibraltar, the Øresund and
  the Strait of Kerch as lines `widthPixels` (2.5, about 20 km) wide. The sea zones on both sides connect
  through them, so fleets sail from the Mediterranean to the Black Sea, the Red Sea and the Pacific; land
  units cannot walk across (a province on both banks, Istanbul, still holds both).

`MapPackTests` checks it: from the mid Atlantic the Black Sea, the Sea of Marmara, the Red Sea, the Pacific
off Panama, the Gulf of Finland and the Sea of Azov are reached by sea; Lake Superior, Lake Michigan, Baikal,
the Aral Sea, Ladoga and the Caspian are inland seas, and Superior reaches Ontario.

## Ships and transports (as in the original)

The rules are the original game's (checked against the decompiled original): a land unit enters a sea
zone only when it carries a **transport ship** (army card 2, the Transport card bought in the card shop,
or given by the navy medal from level 1 to every newly drafted land unit); at sea it is drawn as a
transport. Warships are built in an own **harbour**: a sea area of type `port`. The original's 48 port
areas are all sea areas; the export therefore marks the sea zone off every port city (its longest
shared border) as a harbour (180 on the world map), with the port icon on that coast and tax 2. Like
many units of the original conquests, starting units on a coast come with a transport ship. The AI
buys transport cards, embarks and lands with the original logic: after four rounds of WORLD 1939 it has
about 270 transports and 70 warships at sea (`Tests/world_autoplay.expect`).

## The map as a mesh

With `"mesh": true` the export writes `map<id>.mesh` instead of image tiles and region masks: every border
between two regions is traced once from a raster at twice the region raster's resolution, simplified
(Douglas-Peucker, `tolerance`) and smoothed (Chaikin, `smoothing`) once, and both regions build their outline
from that same chain, so neighbours meet without gaps; the outlines (holes included) are triangulated with
LibTessDotNet (MIT). Regions and borders are stored in vertical strips (`chunkWidth`).

The game (`Game/MapScene/MapMesh.cs`) keeps the triangles in one vertex buffer and the borders in one line
buffer; an area's colour follows its owner, a border is drawn as a national border when the owners on its
two sides differ and as a province border otherwise; only the strips in view are drawn, in two draw calls.
Selection, fights and the new-round flash draw single areas or one country's areas (an index buffer per
owner change) on top. Each copy of a wrapping map draws its background clipped to the map, so the copies
left and right of the seam never cover each other.

## The look of the map

`classicExport.terrain` makes the export draw a terrain picture (`Tools/MapTerrainExport.cs`), 7200 x 2160
pixels in 8 x 4 tiles (`terrain2_<n>.webp`, uploaded as RGB565, loaded when first seen):

* **land**: Natural Earth II with shaded relief (green lowlands, brown and grey mountains with their light
  and shadow, sand deserts, white ice), graded towards parchment (`parchmentMix`);
* **water**: a gradient from shallow water at the coast to deep ocean (`shallow`, `deep`, `depthPixels`),
  faint wave lines following the coast (`waveLines`) and a pale foam edge; lakes and channels alike.

The game draws the picture, then the owners' colours over it at `ownerAlpha` (0.38, so the relief shows
through; unowned land and the sea are not tinted), then a paper grain (`paper2.png`, a tile of 600 map
units, fading in from zoom 0.12), then the borders twice: a soft stroke one pixel aside at 45 % and the
sharp line (national borders dark, province borders light, coasts dark). The world map: about 900 000
triangles, 40 000 border chains; textures at the start of WORLD 1939 106 MB (215 MB with the old tiles and
masks), 18 draw calls per frame.

## Units, zoom and markers

The map has 100 map units per degree (36 000 x 10 800); Europe's provinces are about 100 x 60 units.
`areas.json` gives every area its `fit` radius and the map a `display` object (DATA_SCHEMA.md): the units,
flag and buildings of an area smaller than `unitFitRadius` are scaled down to fit (never below
`minUnitScale`; a unit moving in shrinks around itself, so its path stays true); the camera zooms in up to
`maxZoom` (3); below the camera zoom `markerZoom` every province's armies and buildings become one flag
of nearly constant size on screen (a little smaller in a small province), the same threshold for every
province, so nothing on screen vanishes or collapses into dots; flags stand only where armies or buildings
are (`flags: "armies"`), the colours show the owners.

## The configuration (`Tools/map/natural-earth.json`)

| Field | World base map |
|---|---|
| `bounds`, `wrap` | west -180, east 180 (wrapping), north 82, south -26 |
| `raster.width` | 4500 (12.5 pixels per degree) |
| `eras` | 1910-01-01, 1914-07-01, 1936-01-01, 1939-09-01, 1945-09-02, 1949-12-31 (CShapes), 2018 (`naturalearth`) |
| `historicalBorders` | `file`, `minFragmentKm2` 1500, `minFragmentWidthPixels` 2, `fallback` |
| `water` | `lakes` (file, `maxScalerank` 2), `historicLakes` (file, names: the Aral Sea), `channels` (name, path lon/lat, `widthPixels`) |
| `homeUnits` | game country -> its CShapes units (capitals of every era) |
| `regions` | `minIslandKm2` 1500, `islandSplitDegrees` 4, `islandClusterDegrees` 1, `maxPartDistanceDegrees` 25 |
| `detail` | `defaultKm2` 22 000, `mergeBelow` 0.5, `splitAbove` 1.6; zones from 5 000 km2 (western and central Europe) to 60 000 (northern Canada); countries (Japan, Korea 5 000, Greenland 150 000) |
| `sea` | `coastRadiusDegrees` 1.8, `oceanRadiusDegrees` 14, `growth` 0.9, `relaxIterations` 2, `minSeaPixels` 300 |
| `straits` | links between sea zones: Kiel Canal, Bab-el-Mandeb, the rivers between the Great Lakes (the others are channels) |
| `cities`, `ports`, `airfields` | `minPopulation` 1 000 000, `largeCityPopulation` 5 000 000; `maxScalerank` 5 and 4 |
| `classicExport` | `mapId` 2, `scale` 8 (map units per region raster pixel), `detailRaster` 2, `mesh` true, `meshSettings`, `areaTax` `area` (1 per 25 000 km2), `terrain` (the look), `display`, `scenarios` |
| `countries` | Natural Earth `ADM0_A3` -> game ids |

## The map pack (`assets/data/maps/natural-earth/`)

| File | Content |
|---|---|
| `map.json` | id, name, bounds, projection, `wrap`, raster size, per country its regions and capital, `eras` (source, units with names and capitals), the configuration |
| `regions.json` | one region per line: `id`, `name`, `sea` or `country` + `type`, `city`, `port`, `airfield`, `coastal`, `provinces`, `owners`, `center`, `pixel`, `areaKm2`, `neighbours` |
| `regions.raster` | the region of every pixel (deflate compressed uint16) |
| `preview.png` | regions coloured by their 1939 unit |

7 327 land regions, 1 145 sea zones, 161 capitals (5 of earlier eras), 237 ports, 150 airfields, 9 strait links.

## Playing on the generated map

| File | Read by | Content |
|---|---|---|
| `area2.bin` | `CScene.LoadAreaData` | per area: box, army, building and installation anchors, land or sea (36 000 x 10 800 map units) |
| `adjion2.bin` | `CScene.LoadAdjoin` | every neighbour of every area (version 2: "WCAJ", up to 17 here; the original's format keeps 16, which dropped the shortest borders of a coastal sea zone, straits first) |
| `areamark2.raw` | `CAreaMark` | the area under every 8 x 8 block of map units (4500 x 1350 cells) |
| `data/json/maps/2/areas.json` | `LoadAreaTax`, `MapDisplay` | type, tax and `fit` of every area (harbours: sea areas of type `port`), the `display` settings |
| `map2.mesh` | `CMapMesh` | the areas as triangles and the borders as lines (12 MB) |
| `terrain2_<n>.webp`, `paper2.png` | `CMapMesh` | the terrain picture (32 tiles) and the paper grain |
| `conquest_10.xml`, `conquest_10.bin` | `LoadBattle` | WORLD 1939: 27 countries, 6 177 owned areas, armies in capitals and cities (transport ships on the coasts), 61 destroyers in harbours |
| `data/json/conquests/conquests.json`, texts, `button_conquest10@2x.png` | the conquest menu | conquest 10 with `fogOfWar: false` and `leftRightWrap: true` |

The mod is built into the package (`assets/build.json` `builtInMods`) and appears as **WORLD 1939** on the
second page of the conquest list, with a live preview; `--scenario conquest_10.xml --country fr` or
`startconquest conquest_10.xml fr` start it directly. One AI round of 23 countries takes about 0.7 to 0.95
s on the desktop (fixed step).

## Checks

* `Tests/Unit/MapPackTests.cs`: the pack (consistency, wrapping, thousands of provinces, the units of every
  era, historical splits, polar coverage, the seam, capitals of every era, Marseille); the pipeline on a
  synthetic world (a historical border, a sliver, a harbour, transports, the conquest options); the mesh
  covers every region exactly (a lake as a hole, an enclave) and the game reads it.
* `Tests/Unit/CameraTests.cs`: the zoom limits and wrapping.
* `Tests/world_menu` and `Tests/world_autoplay` (start, the seam, zoomed out, zoomed out at the seam,
  zoomed in, markers, four AI rounds, the naval count `WRN` checked by `world_autoplay.expect`).

### Still to do

* The menu texts of the world conquests are English in every language.
* Only WORLD 1939 is a conquest; the other eras need only a `scenarios` entry.
* Occupations are by whole province; CShapes has no wartime occupations.
* A few tiny regions have no outline in the mesh (smaller than the simplification): they can be picked but
  are not drawn.
* The terrain picture is 20 pixels per degree: soft when zoomed in far (the paper grain hides some of it).
* Balance: start money, taxes and armies are simple rules of the export, not tuned by play.

---

<a id="chinese"></a>
# 地图生成流程

游戏的经典地图（地图 1：手绘地图贴图块，地区几何数据在 `data/binary/maps/1/`）保持不变，所有战役和征服
仍然使用它。在它之外，`WC2.Tools build-map` 根据真实的地理和历史生成一张**世界底图**：

* 一个**地图包**（`assets/data/maps/natural-earth/`，数据：区域、相邻关系、各时代的归属）；
* 一个**可玩的地图模组**（`mods/map-natural-earth/`：地图 2，由顶点缓冲绘制，附带征服 **WORLD 1939**）。

这张底图是**所有时代共用的一张地图**：它的区域同时遵循 1910、1914、1936、1939、1945、1949 年和今天的
边界，因此任何一年的剧本只需说明哪个区域归谁。它有 **7 327 个陆地省份**（与《文明时代 2》相当；1939 年
奥地利约 15 个、捷克斯洛伐克约 10 个、波兰约 70 个）和 1 145 个海区（包括内海和运河）。地图采用《欧陆战争 4》
和《世界征服者 3》的风格绘制：自然色彩和地形晕渲之上叠加半透明的归属色、纸张纹理，海洋带有深浅和沿岸波纹。
关闭或删除该模组即可完全恢复原样。

## 运行

```powershell
.\Tools\map\build-natural-earth.ps1          # downloads the sources once (about 95 MB, Tools\map\source,
                                             # not in git), rebuilds assets\data\maps\natural-earth and
                                             # mods\map-natural-earth
# or by hand:
dotnet Tools\bin\Debug\net9.0\WC2.Tools.dll build-map --config Tools\map\natural-earth.json --source <dir> `
    --out assets\data\maps\natural-earth --classic-out mods\map-natural-earth [--preview-dir <dir>]
```

脚本会下载一次源数据（约 95 MB，存放在 `Tools\map\source`，不进 git），然后重建地图包和模组。整个过程约
两分钟，结果是确定性的。重建前请先删除 `mods\map-natural-earth`（导出不会删除旧地图留下的文件），之后
重新构建游戏（该模组内置在游戏资源包中）。`--preview-dir` 还会绘制每个时代（`era-<年份>.png`）和局部
放大图（`previewCrops`）。

## 数据来源

| 文件 | 来源 | 用途 |
|---|---|---|
| `ne_10m_admin_1_states_provinces.geojson` | Natural Earth（公有领域） | 省份：区域的基本单元；今天的所属国家（`adm0_a3`） |
| `ne_50m_populated_places.geojson` | Natural Earth | 今天的首都（`ADM0CAP`）、按人口确定的城市（`POP_MAX`） |
| `ne_10m_populated_places.geojson` | Natural Earth | 省份的名称（省内最大的城镇） |
| `ne_10m_ports.geojson` | Natural Earth | 港口（`scalerank`） |
| `ne_10m_airports.geojson` | Natural Earth | 机场（`type`、`scalerank`） |
| `ne_10m_lakes.geojson`、`ne_10m_lakes_historic.geojson` | Natural Earth | 大型湖泊（省份图层把它们当作陆地）和 1960 年的咸海 |
| `ne2/NE2_50M_SR_W/NE2_50M_SR_W.tif` | Natural Earth II 带地形晕渲和水体，1:50m（公有领域） | 地形底图：自然色彩与地形起伏 |
| `CShapes-2.0.geojson` | CShapes 2.0，苏黎世联邦理工学院（Schvitz、Rüegger、Girardin、Cederman、Weidmann、Gleditsch），CC BY-NC-SA 4.0，icr.ethz.ch/data/cshapes | 1886 年至 2019 年各国家和殖民地的边界，每个都带有日期、Gleditsch-Ward 代码（`gwcode`）和首都 |

CShapes 的许可要求非商业使用、署名并以相同方式共享：地图包和地图模组由它派生，适用同样的条款（本游戏
不盈利；见 `map.json` 的 `about`）。

## 底图的生成步骤

1. **省份与水域。** 边界范围内的每个 Natural Earth 省份被绘制到一张等距圆柱投影的栅格中（360 度共 4500
   像素，每度 12.5 像素），不做抗锯齿；然后从中挖出水域（`water`，见下文）：大型湖泊以及海峡和运河。
2. **时代。** 对每个带 `date` 的时代，把当天有效的 CShapes 单元（国家和殖民地）绘制到各自的栅格中；时代
   `naturalearth`（今天）直接使用省份所属的国家。没有单元覆盖的陆地，使用其 Natural Earth 国家的
   `fallback` 单元，否则使用该省的主要单元，再否则使用最近的单元；在一个省份内部，属于其他单元、面积小于
   `minFragmentKm2` 或任何地方都没有 `minFragmentWidthPixels` 那么厚的碎片（两个数据源描绘同一条边界时的
   偏差）并入相邻碎片。
3. **签名。** 一个像素的签名是它在每个时代所属的单元，再加上剧本给其省份指定的占领区（`provinceOwners`）。
   一个省份被拆分为若干部分，每部分只有一种签名。
4. **大小合适的区域。** 每个部分都从 `detail` 得到一个目标面积：先看其国家，否则看包含它的第一个区域
   范围，再否则用 `defaultKm2`（欧洲和东亚最细，沙漠、西伯利亚和北极较粗）。各部分**只与签名相同的
   相邻部分合并**，从最小的开始，直到达到目标面积的 `mergeBelow` 倍，因此任何时代的边界都不会被合并掉。
   超过目标面积 `splitAbove` 倍的组被切成面积约等于目标的单元：用最远点采样放置种子，在组内生长（单元
   不会越过组的边缘，因此每个时代的边界都保留），并松弛三次。仍小于 `minIslandKm2` 的部分并入相邻陆地
   （飞地）或被舍弃（岛屿）。
5. **岛屿与接缝。** 距离所属区域超过 `islandSplitDegrees` 的岛群在足够大时成为独立区域（新地岛、千岛
   群岛……）；循环地图在接缝处拆分区域。
6. **形状不规则的海区。** 种子间距随离岸距离增大（沿岸小海区、远洋大海区），在水中生长并松弛；足够大
   的湖泊有自己的海区；接缝是海区边界。
7. **相邻关系**：循环地图上跨越接缝，另加 `straits`（水道太窄、连通道也画不出的海区之间的连接：基尔运河、
   曼德海峡、五大湖之间的河流）；不与任何区域相邻的区域（小湖中的岛屿）与水对岸最近的区域相连。
8. **要素。** 今天的首都以及游戏中国家在其他时代的首都（`homeUnits`）、城市、港口（港口区域保持其陆地
   类型；它的港口是其外侧的海区，见下文）、机场。省份以其内最大的城镇命名，否则以其 Natural Earth 省份
   名加方位命名。
9. **归属**：每个陆地区域记录它在每个时代所属的单元（`regions.json` 的 `owners`，`map.json` 的 `eras`）。

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
  // transportCards (default true): units on a coast start with a transport ship
  // the countries: alliance a/b/c (n or d: neutral, passive), commander, colour, optional garrison,
  // fleets, money, industry, techlevel
  "players": { "de": { "alliance": "a", "commander": "Guderian", "color": [71, 87, 99] } }
}
```

其中 `owners` 把该时代的 CShapes 单元映射到游戏国家；`provinceOwners` 列出由其单元以外的国家控制的省份；
`transportCards`（默认 true）让沿海的初始部队带有运输船；`players` 是剧本中的国家（阵营 a/b/c，n 或 d 表示
中立、不行动；将领、颜色，以及可选的驻军、舰队、资金、工业、科技等级）。`owners` 中没有列出的单元开局
无主；在时代 `naturalearth` 中由 `countries` 表映射今天的国家。游戏认识 31 个有国旗的国家；没有国旗的
历史国家交给最接近的游戏国家（WORLD 1939：奥地利和捷克斯洛伐克归德国，朝鲜和台湾归日本，殖民地归各自
的帝国，蒙古归苏联，中国归中华民国 `tw`），或者保持无主。

## 湖泊、海峡与运河

省份图层把湖泊当作陆地，而几公里宽的海峡在 9 公里一像素的栅格中会被封住。因此 `water` 在两种分辨率（区域
栅格和轮廓栅格）上、在其他步骤之前，把它们从省份中挖出：

* **湖泊**：`maxScalerank` 不超过 2 的 Natural Earth 湖泊（五大湖、贝加尔湖、拉多加湖、奥涅加湖、巴尔喀什湖、
  维多利亚湖……），以及历史湖泊中 1960 年的咸海。它们与海洋一样获得海区：内海，可由在其港口建造的舰船以及从
  其沿岸出发的运输船航行（里海本来就是水域）。五大湖由各自之间的河流连接（`straits`）。
* **通道**：博斯普鲁斯海峡、达达尼尔海峡、苏伊士运河和巴拿马运河、直布罗陀海峡、厄勒海峡和刻赤海峡，画成
  `widthPixels`（2.5，约 20 公里）宽的线。两侧的海区通过它们相连，舰队可以从地中海驶入黑海、红海和太平洋；
  陆军不能步行跨越（横跨两岸的省份，如伊斯坦布尔，仍同时拥有两岸）。

`MapPackTests` 会检查这些：从大西洋中部出发，可以经海路到达黑海、马尔马拉海、红海、巴拿马外的太平洋、芬兰湾
和亚速海；苏必利尔湖、密歇根湖、贝加尔湖、咸海、拉多加湖和里海是内海，并且苏必利尔湖可以到达安大略湖。

## 舰船与运输船（与原版一致）

规则就是原版游戏的规则（已与反编译的原版核对）：陆军只有带着**运输船**（部队卡牌 2，即在卡牌商店购买的
运输卡，或者海军勋章 1 级起给每个新征召的陆军部队）才能进入海区；在海上它显示为运输船。战舰在己方的
**港口**建造：类型为 `port` 的海上地区。原版的 48 个港口地区全部是海上地区；因此导出时把每座港口城市外侧
的海区（共享边界最长的那个）标记为港口（世界地图上共 180 个），港口图标画在那段海岸上，税收为 2。与原版
征服中的许多部队一样，沿海的初始部队带有运输船。AI 用原版逻辑购买运输卡、登船和登陆：WORLD 1939 进行
四个回合后，海上约有 270 艘运输船和 70 艘战舰（见 `Tests/world_autoplay.expect`）。

## 以网格绘制地图

`"mesh": true` 时，导出写出 `map<id>.mesh`，而不是图片贴图块和区域掩码：两个区域之间的每条边界，都在
两倍于区域栅格分辨率的栅格上只追踪一次，只简化（Douglas-Peucker，`tolerance`）和平滑（Chaikin，
`smoothing`）一次，两个区域都用同一条链构建轮廓，因此相邻区域之间没有缝隙；轮廓（包括孔洞）用
LibTessDotNet（MIT）三角化。区域和边界按竖条（`chunkWidth`）存储。

游戏（`Game/MapScene/MapMesh.cs`）把三角形放在一个顶点缓冲中，把边界放在一个线条缓冲中；地区的颜色随
归属变化，两侧归属不同的边界画成国界，否则画成省界；只绘制视野内的竖条，共两次绘制调用。选中、战斗和
新回合闪烁在上面单独绘制某个地区或某个国家的全部地区（每次归属变化建一个索引缓冲）。循环地图的每个副本都
把背景裁剪到地图范围内绘制，因此接缝左右的副本不会互相覆盖。

## 地图的外观

`classicExport.terrain` 让导出绘制一张地形底图（`Tools/MapTerrainExport.cs`），7200 × 2160 像素，分成 8 × 4 块
（`terrain2_<n>.webp`，以 RGB565 上传，首次可见时加载）：

* **陆地**：带地形晕渲的 Natural Earth II（绿色低地、带光影的棕色和灰色山地、沙色沙漠、白色冰雪），向羊皮纸
  色调调和（`parchmentMix`）；
* **水域**：从海岸处的浅水到远洋深水的渐变（`shallow`、`deep`、`depthPixels`），沿海岸的淡淡波纹线
  （`waveLines`）和浅色的浪花边；湖泊和通道相同。

游戏先画这张底图，再以 `ownerAlpha`（0.38，让地形透出；无主陆地和海洋不着色）叠加归属色，然后是纸张纹理
（`paper2.png`，每块 600 地图单位，从缩放 0.12 起渐显），最后把边界画两遍：偏移一个像素、45% 透明度的柔和描边，
以及清晰的线条（国界深色、省界浅色、海岸线深色）。世界地图：约 90 万个三角形、4 万条边界链；WORLD 1939 开局
时纹理占用 106 MB（旧的贴图块和掩码为 215 MB），每帧 18 次绘制调用。

## 部队、缩放与标记

地图每度 100 个地图单位（36 000 × 10 800）；欧洲的省份约为 100 × 60 单位。`areas.json` 给每个地区一个
`fit` 半径，并给地图一个 `display` 对象（见 DATA_SCHEMA.md）：小于 `unitFitRadius` 的地区，其部队、旗帜和
建筑缩小到能放进地区（不小于 `minUnitScale`；正在移入的部队围绕自身缩小，因此移动路径保持正确）；镜头
最多放大到 `maxZoom`（3）；镜头缩放低于 `markerZoom` 时，每个省份的军队和建筑显示为一面在屏幕上大小几乎
不变的旗帜（小省份略小），所有省份使用同一个阈值，因此屏幕上的东西不会消失或缩成小点；只在有军队或建筑的
地方插旗（`flags: "armies"`），颜色表示归属。

## 配置（`Tools/map/natural-earth.json`）

| 字段 | 世界底图 |
|---|---|
| `bounds`、`wrap` | 西 -180、东 180（左右循环），北 82，南 -26 |
| `raster.width` | 4500（每度 12.5 像素） |
| `eras` | 1910-01-01、1914-07-01、1936-01-01、1939-09-01、1945-09-02、1949-12-31（CShapes），2018（`naturalearth`） |
| `historicalBorders` | `file`、`minFragmentKm2` 1500、`minFragmentWidthPixels` 2、`fallback` |
| `water` | `lakes`（文件，`maxScalerank` 2）、`historicLakes`（文件，名称：咸海）、`channels`（名称，经纬度路径，`widthPixels`） |
| `homeUnits` | 游戏国家 → 它的 CShapes 单元（各时代的首都） |
| `regions` | `minIslandKm2` 1500、`islandSplitDegrees` 4、`islandClusterDegrees` 1、`maxPartDistanceDegrees` 25 |
| `detail` | `defaultKm2` 22 000、`mergeBelow` 0.5、`splitAbove` 1.6；区域范围从 5 000 km2（西欧和中欧）到 60 000（加拿大北部）；国家（日本、朝鲜半岛 5 000，格陵兰 150 000） |
| `sea` | `coastRadiusDegrees` 1.8、`oceanRadiusDegrees` 14、`growth` 0.9、`relaxIterations` 2、`minSeaPixels` 300 |
| `straits` | 海区之间的连接：基尔运河、曼德海峡、五大湖之间的河流（其余的是通道） |
| `cities`、`ports`、`airfields` | `minPopulation` 1 000 000、`largeCityPopulation` 5 000 000；`maxScalerank` 分别为 5 和 4 |
| `classicExport` | `mapId` 2、`scale` 8（每个区域栅格像素的地图单位）、`detailRaster` 2、`mesh` true、`meshSettings`、`areaTax` `area`（每 25 000 km2 为 1）、`terrain`（外观）、`display`、`scenarios` |
| `countries` | Natural Earth 的 `ADM0_A3` → 游戏 id |

## 地图包（`assets/data/maps/natural-earth/`）

| 文件 | 内容 |
|---|---|
| `map.json` | id、名称、边界、投影、`wrap`、栅格尺寸、每个国家的区域数和首都、`eras`（来源、各单元的名称和首都）、所用的配置 |
| `regions.json` | 每行一个区域：`id`、`name`、`sea` 或 `country` + `type`、`city`、`port`、`airfield`、`coastal`、`provinces`、`owners`、`center`、`pixel`、`areaKm2`、`neighbours` |
| `regions.raster` | 每个像素所属的区域（deflate 压缩的 uint16） |
| `preview.png` | 按 1939 年单元着色的区域 |

7 327 个陆地区域、1 145 个海区、161 个首都（其中 5 个来自更早的时代）、237 个港口、150 个机场、9 条海峡连接。

## 在生成的地图上游戏

| 文件 | 读取者 | 内容 |
|---|---|---|
| `area2.bin` | `CScene.LoadAreaData` | 每个地区：范围、部队/建筑/设施锚点、陆地或海洋（36 000 × 10 800 地图单位） |
| `adjion2.bin` | `CScene.LoadAdjoin` | 每个地区的全部邻居（版本 2："WCAJ"，这里最多 17 个；原版格式只保留 16 个，会丢掉沿海海区最短的边界，首先是海峡） |
| `areamark2.raw` | `CAreaMark` | 每个 8 × 8 地图单位方块下的地区（4500 × 1350 个单元格） |
| `data/json/maps/2/areas.json` | `LoadAreaTax`、`MapDisplay` | 每个地区的类型、税收和 `fit`（港口：类型为 `port` 的海上地区），以及 `display` 设置 |
| `map2.mesh` | `CMapMesh` | 地区的三角形和边界线（12 MB） |
| `terrain2_<n>.webp`、`paper2.png` | `CMapMesh` | 地形底图（32 块）和纸张纹理 |
| `conquest_10.xml`、`conquest_10.bin` | `LoadBattle` | WORLD 1939：27 个国家、6 177 个有归属的地区、首都和城市中的部队（沿海部队带运输船）、港口中的 61 艘驱逐舰 |
| `data/json/conquests/conquests.json`、文本、`button_conquest10@2x.png` | 征服菜单 | 征服 10，`fogOfWar: false`、`leftRightWrap: true` |

该模组内置在游戏资源包中（`assets/build.json` 的 `builtInMods`），在征服列表第二页显示为 **WORLD 1939**，
带实时预览；`--scenario conquest_10.xml --country fr` 或 `startconquest conquest_10.xml fr` 可以直接开始。
桌面版上 23 个国家的一轮 AI 约需 0.7 到 0.95 秒（固定步长）。

## 检查

* `Tests/Unit/MapPackTests.cs`：地图包（一致性、左右循环、数千个省份、各时代的单元、历史边界拆分、北极
  覆盖、接缝、各时代的首都、马赛）；在合成小世界上运行的生成流程（一条历史边界、一个细碎片、一个港口、
  运输船、征服选项）；网格精确覆盖每个区域（湖泊为孔洞、一块飞地），并且游戏能读取它。
* `Tests/Unit/CameraTests.cs`：缩放限制和左右循环。
* `Tests/world_menu` 和 `Tests/world_autoplay`（开局、接缝、最大缩小、在接缝处最大缩小、放大、标记、四轮 AI，
  以及由 `world_autoplay.expect` 检查的海军统计 `WRN`）。

### 待办

* 世界征服的菜单文本在所有语言中都是英文。
* 目前只有 WORLD 1939 是征服；其他时代只需一个 `scenarios` 条目。
* 占领以整个省份为单位；CShapes 没有战时占领数据。
* 网格中有少数极小的区域没有轮廓（小于简化精度）：可以选中，但不会绘制。
* 地形底图为每度 20 像素：放大很多时显得柔和（纸张纹理遮住了一部分）。
* 平衡：初始资金、税收和部队是导出时的简单规则，尚未经过实际游戏调整。
