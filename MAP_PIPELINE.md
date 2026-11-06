# Map pipeline

The classic map of the game (map 1: hand-drawn tiles, area geometry in `data/binary/maps/1/`) stays as
it is, and every campaign and conquest keeps using it. Next to it, maps can now be generated from
real geography: `WC2.Tools build-map` turns Natural Earth GeoJSON into a **map pack**, a data pack in
`assets/data/maps/<id>/` that nothing uses unless a mode or a mod selects it. Deleting the folder
reverts it completely.

## Running it

```powershell
.\Tools\map\build-natural-earth.ps1          # downloads the source data once (about 50 MB, Tools\map\source,
                                             # not in git) and rebuilds assets\data\maps\natural-earth
# or by hand:
dotnet Tools\bin\Debug\net9.0\WC2.Tools.dll build-map --config Tools\map\natural-earth.json --source <dir> --out <dir>
```

It takes about 5 seconds and is deterministic: the same sources and configuration give byte-identical
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

## Not done yet: playing on a generated map

The pack holds everything the rules need (regions, adjacency, terrain types, ownership, cities, ports,
airfields). Playing on it still needs, in the game:

* a scene that draws regions from `regions.raster` (country colours, borders, the unit and building
  anchors at each region's `pixel`) instead of the classic tile art and area masks;
* the classic area geometry formats (`area1.bin`, `adjion1.bin`, `areamark1.raw`) or a reader that
  builds `CScene`'s area table and adjacency from the pack;
* a scenario for it (starting armies and money per country; the pack already gives ownership and
  capitals) and a menu entry or mod that selects it.

Until then the pack is data only; the classic map and its campaigns are untouched.
