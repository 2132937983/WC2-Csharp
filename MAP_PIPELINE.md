# Map pipeline

The classic map of the game (map 1: hand-drawn tiles, area geometry in `data/binary/maps/1/`) stays as
it is, and every campaign and conquest keeps using it. Next to it, maps can now be generated from
real geography: `WC2.Tools build-map` turns Natural Earth GeoJSON into a **map pack** (a data pack in
`assets/data/maps/<id>/`) and into a **playable map mod** (`mods/map-natural-earth/`: the map as map
2 with a world conquest). Disabling or deleting the mod reverts it completely.

## Running it

```powershell
.\Tools\map\build-natural-earth.ps1          # downloads the source data once (about 50 MB, Tools\map\source,
                                             # not in git), rebuilds assets\data\maps\natural-earth and
                                             # mods\map-natural-earth
# or by hand:
dotnet Tools\bin\Debug\net9.0\WC2.Tools.dll build-map --config Tools\map\natural-earth.json --source <dir> --out <dir>
```

It takes about 5 seconds (50 with the map mod) and is deterministic: the same sources and configuration give byte-identical
files.

## Sources

Natural Earth (public domain, naturalearthdata.com), GeoJSON from the `nvkelso/natural-earth-vector`
repository:

| File | Used for |
|---|---|
| `ne_10m_admin_1_states_provinces.geojson` | provinces: the building blocks of the regions, their country (`adm0_a3`) |
| `ne_50m_populated_places.geojson` | capitals (`ADM0CAP`), cities by population (`POP_MAX`) |
| `ne_10m_ports.geojson` | ports (`scalerank`) |
| `ne_10m_airports.geojson` | airfields (`type`, `scalerank`) |

## How a map is made

1. **Rasterize.** Every province inside the bounds is drawn into an equirectangular raster (4096
   pixels across), one colour per province, without antialiasing. The raster gives each province its
   area, centre, coastline and neighbours without polygon clipping.
2. **Merge into regions.** Within a country, the smallest province group joins its smallest touching
   group until every region has `minAreaKm2` and the country has at most `maxPerCountry` regions
   (`regionOverrides` make Europe finer). Islands smaller than `minIslandKm2` are left out (water).
3. **Sea zones.** The water is cut by a grid of `cellDegrees`; each cell's connected water parts
   become sea zones (a cell with a peninsula gets two). Parts smaller than `minSeaPixels` join the
   neighbouring zone they share the longest border with.
4. **Adjacency.** Two regions are neighbours when their pixels touch (4-neighbourhood) along at
   least `minBorderPixels`. Land–land, land–sea and sea–sea neighbours come out of the same pass;
   adjacency is symmetric.
5. **Ownership and features.** A region belongs to the country of its provinces; Natural Earth's
   `ADM0_A3` maps to the game's country ids (`countries` in the configuration, e.g. `DEU` -> `de`,
   `USA` -> `am`), other countries keep their ISO code. A country's capital is the region of its most
   populous place marked as capital (type `capital`); places above `minPopulation` make a region a
   `normal city` or `large city`; ports (`scalerank` up to `maxScalerank`) make a coastal region a
   port; major airports give it an airfield. The area types are the ones of
   `data/json/terrain/terrain.json`, so the terrain rules apply unchanged.

## The configuration (`Tools/map/natural-earth.json`)

| Field | Natural Earth world |
|---|---|
| `bounds` | west -170, east 180, north 62 (southern Greenland: Cape Farewell is at 59.8 N), south -26 (southern Madagascar at 25.6 S, northern Australia) |
| `raster.width` | 4096 (about 9.5 km per pixel at the equator) |
| `regions` | `minAreaKm2` 40 000, `maxPerCountry` 30, `minIslandKm2` 2 500; Europe and Japan finer (`regionOverrides`), Russia up to 45 regions |
| `sea` | `cellDegrees` 8, `minSeaPixels` 600 |
| `cities` | `minPopulation` 1 000 000, `largeCityPopulation` 5 000 000 |
| `ports`, `airfields` | `maxScalerank` 5 and 4; airfield types `major`, `major and military` |

## The map pack (`assets/data/maps/natural-earth/`)

| File | Content |
|---|---|
| `map.json` | id, name, bounds, projection, raster size and pixels per degree, per country the number of regions and its capital region, the configuration used (schema `map-pack.schema.json`) |
| `regions.json` | one region per line: `id`, `name`, `sea` or `country` + `type`, `city` (name, population), `port` and `airfield` (names), `coastal`, `provinces` (ISO 3166-2 codes), `center` (lon, lat), `pixel`, `areaKm2`, `neighbours` (schema `map-regions.schema.json`) |
| `regions.raster` | the region of every pixel: `WCRG`, version, width, height, then deflate compressed uint16 (region id + 1, 0 = no region) |
| `preview.png` | regions coloured by country, region borders, capitals (yellow) and other key areas (white) |

The Natural Earth pack: 966 land regions in 167 countries, 380 sea zones, 153 capitals, 174 ports,
126 airfields (about 750 KB in all). `Data/MapPack.cs` reads a pack through the asset layer (so mods
can replace or extend it), answers "which region is at this longitude / latitude" and checks it
(ids, symmetric adjacency, every land region owned and connected, ports only on coasts, one capital
per country).

## Checks

* `Tests/Unit/MapPackTests.cs`: the pack is consistent; places lie in the right countries (Cape
  Farewell in Greenland, southern Madagascar, Darwin, and Paris, London, Berlin, Moscow, Tokyo,
  Washington as capitals); Marseille is a French port on the sea; and the pipeline itself, run on a
  small synthetic world: provinces merge, a too small island is dropped, two countries touch, the
  capital, port and airfield land in the right regions.
* `--verify-data` and the unit tests check `map.json` and `regions.json` against their schemas; the
  asset checker includes the pack.

## Playing on the generated map

`build-map --classic-out DIR` (the `classicExport` section of the configuration) also writes the map in
the formats the game's scene already reads for the classic map, as **map 2**, together with a
conquest, into a mod: `mods/map-natural-earth/` (3.2 MB, enabled; the build script writes it).

| File | Read by | Content |
|---|---|---|
| `area2.bin` | `CScene.LoadAreaData` | per area: zone image position and size, army, building and installation anchors (the pixel farthest from the region's edge), land or sea; map units = raster pixels x 2 (8192 x 2060) |
| `adjion2.bin` | `CScene.LoadAdjoin` | up to 16 neighbours per area: the longest borders, kept only when both sides keep them |
| `areamark2.raw` | `CAreaMark` | the area under every 8 x 8 block of map units (touch picking) |
| `data/json/maps/2/areas.json` | `LoadAreaTax` | area types and base taxes (3 to 31: size, city, capital) |
| `map2_<n>.png` | `CBackground` | 85 background tiles of 500 units: land, sea, coast, country and region lines |
| `m2_zone<n>_1.png/.xml` | `InitAreaImage` | 7 atlas pages with the region masks (sprite `<id>.png`), tinted with the owner's colour |
| `generated/texture_formats.json` | `ecGraphics` | tiles uploaded as RGB565, masks as Alpha8 (merged with the game's own list) |
| `conquest_10.xml`, `conquest_10.bin` | `LoadBattle` | the conquest: 31 countries (`players`: side, commander, colour), their regions, armies in capitals and cities, every area enabled; the rest of the land starts unowned |
| `data/json/conquests/conquests.json`, `data/json/text/<lang>/stringtable_<lang>[_iPad].json`, `button_conquest10@2x.png` | the conquest menu | the entry "conquest 10" (merged into the game's list), its title, the country names, intros and victory texts (English in every language), and its button |

The mod is **built into the package** (`assets/build.json` `builtInMods`: it ships inside the APK and
next to the desktop program under `mods/map-natural-earth/` and is mounted like any mod; `--no-mods`
leaves it out, a mod folder with the same id replaces it, removing the line from `build.json` ships
the game without it). So the conquest list has a tenth entry, **WORLD (NATURAL EARTH)** (second page),
with a live preview of the map and the 31 countries to choose from. On the desktop it can also start
right away: `dotnet run --project Hosts/Desktop -- --scenario conquest_10.xml --country fr` (any of
the 31 countries), or from a script with `startconquest conquest_10.xml fr`. On Android, copy the mod
folder to `Android/data/com.wc2remake.csharp/files/mods/` only to replace the built-in copy. The classic map and every campaign and conquest are untouched; removing or
disabling the mod reverts everything. Texture memory at the start of the world conquest: 168 MB
(desktop).

`Tests/world_menu` opens the world conquest through the menu (list, preview, game);
`Tests/world_autoplay` starts it as France and lets the standard AI play four rounds on every side
(golden dumps, reference screenshots, hit tests); the unit tests check the exported files of a
synthetic world (record sizes, symmetric adjacency, area ids in the mask, scenario, formats).

### Still to do

* The menu texts of the world conquest are English in every language; the main-menu minimaps are the
  classic map's.
* Finer art: the region shapes come from the raster (one map unit = half a raster pixel), so coasts
  and borders show steps when zoomed in; drawing the province polygons directly into the tiles would
  smooth them.
* Sea zones are a plain grid; ports have no fleets at the start; the scenario has no dialogues.
* Balance: start money, taxes and armies are simple rules of the export, not tuned by play.
