# Game data and its schemas

**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

The game's definitions are JSON. The files live in `assets/data/json/` (the authored source, in git)
and end up in the runtime package at the same path. The original XML files stay untouched in
`assets/data/xml/` and are still packaged; the game reads the JSON, except for the **scenarios and
tutorial scripts, which stay the original XML files**: an index JSON names and summarises them.

Special behaviour stays in code, selected by a type, trait or action id (an area type's `capital` or
`port` trait, an installation id, a card type); every number those rules use is in the data.

## Schemas and checks

Every data file has a JSON Schema (draft 2020-12) in `assets/data/json/schemas/`:

| Schema | Files |
|---|---|
| `units.schema.json` | `units/units.json` |
| `cards.schema.json` | `cards/cards.json` |
| `commanders.schema.json`, `portraits.schema.json` | `commanders/commanders.json`, `commanders/portraits.json` |
| `campaigns.schema.json`, `conquests.schema.json` | `campaigns/campaigns.json`, `conquests/conquests.json` |
| `terrain.schema.json` | `terrain/terrain.json` |
| `medals.schema.json`, `rules.schema.json` | `rules/medals.json`, `rules/rules.json` |
| `countries.schema.json` | `countries/countries.json` |
| `audio.schema.json` | `audio/audio.json` |
| `layout-profiles.schema.json`, `options-layout.schema.json` | `layout/profiles.json`, `layout/options.json` |
| `map-areas.schema.json` | `maps/<id>/areas.json` |
| `scenario-index.schema.json` | `scenarios/index.json` |
| `text.schema.json` | `text/<code>/*.json` |
| `ai.schema.json` | `ai/profiles.json` |

* **At load time** (`JsonData.LoadMerged`, `Engine/IO/JsonSchema.cs`) the merged file (game plus
  mods) is checked against its schema. Problems are logged with their JSON path
  (`data/json/cards/cards.json: $.countries.others.Tank.price: -5 is below the minimum 0`). When only
  the mods' version is invalid, the mods' changes to that file are dropped and the game's own data is
  used.
* `dotnet run --project Hosts/Desktop -- --verify-data` (part of `Tests/run-tests.ps1`) checks every data file
  against its schema, then loads every type twice, from the original XML (or the built-in tables) and
  from the JSON, and compares the resulting definitions field by field. A deliberate change to the JSON
  therefore shows up there; with `--mods <folder>` it lists exactly what a mod changes.
* `dotnet run --project Hosts/Desktop -- --convert-data` regenerates the JSON from the originals (files whose
  content does not change are left alone).

Records reject unknown fields (`additionalProperties: false`), so a misspelt field is an error;
registries (objects keyed by a name) accept any key.

## Conventions

* Comments (`//`, `/* */`) and trailing commas are allowed.
* Numbers may also be written as strings and the other way round; values are parsed exactly like the
  original XML attributes (an integer field accepts `"12"`, `12` and `12.0`).
* Registries are objects keyed by name or id (`"units": {"tank": {...}}`), never arrays, so a mod can
  change one entry. Ordered lists (scenario countries, tutorial commands) are arrays.
* Every file has `"format": 1` and an `"about"` text; both are ignored by the game.
* Mods merge into these files: objects merge key by key, arrays and values replace, `null` removes a
  key, `"$replace": true` replaces a whole object. See `MODDING.md`.

| File | Replaces | Content |
|---|---|---|
| `data/json/units/units.json` | `armydef.xml` | unit types, stats per country, combat rules, unit levels |
| `data/json/cards/cards.json` | `carddef.xml` | cards per country |
| `data/json/commanders/commanders.json` | `commanderdef.xml` | commanders, commander levels |
| `data/json/commanders/portraits.json` | `generalphotos.xml` | general portraits |
| `data/json/campaigns/campaigns.json` | `battlelist.xml` | campaign battles on the selection map |
| `data/json/conquests/conquests.json` | `conquestlist.xml` | conquests on the selection map |
| `data/json/terrain/terrain.json` | constants in the code | area types, buildings, installations |
| `data/json/rules/medals.json` | constants in the code | war medal effects |
| `data/json/rules/rules.json` | constants in the code | global numbers: combat dice, economy caps |
| `data/json/layout/options.json` | `guioptions.xml` | options page layout |
| `data/json/layout/profiles.json` | constants in the code | screen layouts (aspect ratio buckets) |
| `data/json/audio/audio.json` | file names in the code | which sound plays for what |
| `data/json/countries/countries.json` | lists in the code | countries with their own unit art, flag, badge |
| `data/json/ai/profiles.json` | constants in the AI code | AI profiles: classic (the original), standard, hard |
| `data/json/maps/<id>/areas.json` | `areatax<id>.xml` | area type and base tax of every area |
| `data/json/scenarios/index.json` | (new) | index of the scenario XML files and tutorial scripts |
| `data/json/text/<code>/stringtable_<code>[_iPad].json` | `stringtable_*.xml` | texts |
| `data/json/text/<code>/Localizable.json` | `<lang>.lproj/Localizable.strings` | language settings |

Not converted, because they are asset formats rather than game data: texture atlases (`*.xml` next to
their images), bitmap fonts (`.fnt`), particle effects (`effect_*.xml`), map geometry (`area<id>.bin`,
`adjion<id>.bin`, `areamark<id>.raw`) and the scenario area masks (`*.bin`, referenced by
`areasEnable`).

---

## units

`data/json/units/units.json`

```json
{
  "legacyCombatDefaults": false,
  "weaponArmorRules": { "1": { "2": "150%", "3": 0.8 } },
  "unitTypes": {
    "infantry": { "id": 0, "image": "soldier" },
    "carrier":  { "id": 10, "image": "transportship", "countryIndependent": true }
  },
  "countries": {
    "others": {
      "tank": { "unitType": 2, "strength": 110, "movement": 2, "minAttack": 2, "maxAttack": 9, "armorType": 2,
                "combat": { "traits": ["cross-entrenchment"],
                            "attack": { "mode": 0, "engagement": 0, "range": { "shape": "graph", "min": 0, "max": 100 } },
                            "result": { "movementRestoreOnKill": 1 } } }
    },
    "cn": { "infantry": { ... } }
  },
  "levels": [ { "attack": 0, "defence": 0, "turnRecovery": 0, "instantRecovery": 0, "maxHpPercent": 0, "expToNext": 90 }, ... ]
}
```

* `legacyCombatDefaults`: when true, units without explicit combat data get the hard-coded rules of
  the remake's first version. The shipped data sets it to false; everything is explicit.
* `weaponArmorRules`: weapon type (1..32) → armor type (1..255) → damage rate (`"150%"`, `1.5`,
  `"inf"`). A unit with several weapon types uses the best rate.
* `unitTypes`: name → stable `id`, atlas image base name (`image`; country suffixes are appended unless
  `countryIndependent`). Names used in scenarios but not listed get a dynamic id from 1000.
* `countries`: country id → unit type → definition. `others` is used for every country without its own
  entry for that type.

Unit definition fields:

| Field | Meaning |
|---|---|
| `strength`, `movement`, `minAttack`, `maxAttack` | base stats (missing = 0) |
| `unitType` | combat class: 0 not selectable, 1 infantry, 2 armour, 3 artillery, 4 navy (0..255) |
| `navy` | drawn as a ship / transport at sea; independent of the combat class |
| `weaponType` | weapon type list (`"1,3"`, `"1 3"`, `"1|3"`) |
| `armorType` | armor type 0..255 |
| `image`, `countryIndependent` | overrides of the unit type's image for this country |
| `combat` | combat rules, below |

`combat`:

| Field | Meaning |
|---|---|
| `traits` | list of `cross-entrenchment` (keeps movement when attacking an entrenchment), `secondary-attack`, `recv_fort_second_counter` (takes a second counter from forts), `cannot-occupy` |
| `canOccupy` | readable form of `cannot-occupy`; wins over the trait |
| `aiNoCounterBonus` | AI target score when the target cannot counter (default 300) |
| `attack` | `mode` 0 close / 1 projectile, harassment / 2 air strike (or `direct`, `projectile`, `airstrike`, `infiltration`); `payload` for air strikes: 1 `airstrike`, 2 `bomber`, 3 `nuclear`; `attackCost` movement cost of a special attack (`"inf"` clears all movement); `engagement` 0 close / 1 projectile; `range`; `damage` |
| `counter` | `inherit: "attack"` copies the attack; otherwise only `range` is read (air strike attackers counter in close combat); `damage` |
| `range` | `shape` `graph` (1000 = one area step) or `circle` (1000 = map distance 100); `min`, `max`, `radius` (forces a circle), `maxInclusive`, `radiusSource: "country"` (air strike radius of the owner) |
| `damage` | list of `{target: <unitType>, phase: "attack"|"counter"|..., rate: "130%"}` conditional damage rates; under `attack`/`counter` the phase defaults to that phase |
| `result` | `constructionLevelDegrade` and `constructionLevelChance` (% chance to lower the target's building), `movementRestoreOnKill` |

`levels`: the five unit levels (0..4): attack and defence bonus, healing per turn, healing on level up,
max strength bonus in %, and the experience needed for the next level (scaled by max strength / 100).

## cards

`data/json/cards/cards.json`: `{"countries": {"<country>": {"<card name>": {...}}}}`, `others` for everyone else.

| Field | Meaning |
|---|---|
| `id` | card id (0..28; 28 is the elite infantry card) |
| `type` | `army`, `navy`, `airforce`, `development`, `strategy` |
| `price`, `industry` | cost in money and industry |
| `round` | turns before it can be used again |
| `tech` | tech level needed |
| `relatedArmyId` | unit type id the card drafts (default: the card id) |
| `maxTimes` | uses per game (0 = unlimited) |
| `image`, `intro` | card picture (atlas name) and text key |

The draft order of the AI follows the price, then industry, then tech, per country.

## commanders

`data/json/commanders/commanders.json`: `{"commanders": {"<name>": {"country": "de", "rank": 9, "infantry": 2, "airforce": 1, "artillery": 1, "armour": 3, "navy": 0, "honour": 2}}, "levels": [...]}`

The six medal fields are the commander's war medal levels. `levels` lists the 15 player commander
levels: attack, defence, healing, max strength bonus (as for units), `coolDown` turns and
`medalsToNext`.

## portraits

`data/json/commanders/portraits.json`: `{"portraits": {"<general name>": {"file": "general_rommel.webp", "w": 113, "h": 137, "refx": 20}}}`

## campaigns and conquests

`data/json/campaigns/campaigns.json`: `{"battles": {"<key>": {...}}}` with `age` (label text), `centerX`, `centerY`,
`ageX`, `ageY`, `victory` and `greatVictory` (turn limits, default 15 and 10), `flags` and `arrows`
(lists of `{name, x, y, rot, scale}`, `rot` in degrees).

`data/json/conquests/conquests.json`: `{"conquests": {"<key>": {"centerX", "centerY", "flags": [...]}}}`, and two
options of the conquest's map:

* `fogOfWar` (default `false`): armies are hidden in the areas the player's alliance neither holds nor
  touches (only the drawing; the AI is not affected);
* `leftRightWrap` (default `false`): the map repeats horizontally: the camera scrolls past the left and
  right edge, and moves, arrows, airstrike ranges and distances take the short way over the seam. The
  map must span its whole width (the world map: `mods/map-natural-earth`, MAP_PIPELINE.md).

Both are read when the game starts or loads the conquest. The world map's conquest 10 (WORLD 1939) has
`"fogOfWar": false, "leftRightWrap": true`.

## terrain

`data/json/terrain/terrain.json`. The rules of the game ask these definitions instead of testing for "capital" or
"fort" in code; the shipped values reproduce the original game exactly.

`areaTypes` (keys are the names used in `data/json/maps/<id>/areas.json`; a new name with a new `id` adds a type):

| Field | Meaning |
|---|---|
| `id` | number stored in saves (0 normal, 1 capital, 2 port, 3 large city, 4 normal city) |
| `cityLevel`, `industryLevel` | levels the area has without buildings; a new city / industry starts one level higher, and a damaged one disappears when it falls back to this level |
| `healing` | strength regained per turn by units with movement left (plus `healingPerLevel` per level) |
| `navyHealingMultiplier` | factor for ships (ports: 2) |
| `defence` | extra defence for units here (the larger of this and the installation's applies) |
| `capital`, `port`, `keyArea` | counts as capital; ships can be drafted / port drawn; counts for the "core" defeat type |
| `encirclementNeedsGarrison` | an enemy area of this type closes an encirclement only with units in it |
| `medalBonus` | added to the medal roll of fights here |
| `ai` | AI target values: `construction`, `airStrike`, `nuclear`, `airborne` |

`constructions` (`city`, `industry`, `airport`): `maxLevel`, `perLevel` (tax per city level, industry
per industry level), `healingLevel` (the area heals as at least this level; airport 1).

`installations` (`fort`, `entrenchment`, `antiaircraft`, `radar`): `defence`, `minCounterRange`
(fort: defenders counter at least one area far), `stopsAttackers` (entrenchment: attackers lose their
movement unless `cross-entrenchment`), `secondCounter` (fort), `halvesRangedDamage` (radar),
`airStrikeDamagePercent` (anti-aircraft 30, radar 50), `protectsFromBombing`, `ai.airStrikePenalty`.

`rules`: `healingPerLevel` (3), `seaMaxMovement` (1), and the AI level weights of air strike and
airborne targets.

## medals

`data/json/rules/medals.json`: what the six war medals (`infantry`, `airforce`, `artillery`, `armour`, `navy`,
`commerce`) do. The kinds of effect are code, their numbers are data; the shipped values reproduce the
original.

| Field | Meaning |
|---|---|
| `upgradeCost.base`, `upgradeCost.perLevel` | medals needed to raise a war medal: base + perLevel x current level |
| `commerce.incomePercentPerLevel` | extra taxes and industry per commerce medal level (10) |
| `airforce.attackBonus`, `attackBonusFromLevel` | air strike attack bonus and the level it starts at |
| `airforce.radius`, `radiusUpgraded`, `radiusUpgradedFromLevel` | air strike radius in map units (300, 400 from level 2) |
| `airforce.noCooldownFromLevel` | air force cards have no cool-down from this level (3) |
| `draftBonuses` | ordered list: a unit drafted with a card gets `upgrades` level-ups and `addTactics` (`assault`, `defence`, `carrier`) when the owner's `medal` is at `level` or above; `cards` limits it to card ids, `cardType` to a card type |

## rules

`data/json/rules/rules.json`: global numbers that were constants. `combat.diceThresholds`: strength percentages;
a unit rolls one die plus one per threshold its strength is above (`[50, 25, 15, 5]`: above 50 % five
dice ... at most 5 % one). `economy.moneyCap`, `economy.industryCap`: 99999.

## map area table

`data/json/maps/<id>/areas.json`: `{"areas": {"<area id>": {"type": "capital", "tax": 30}}}`. Areas missing here
are plain areas with tax 100. The geometry (`area<id>.bin`, `adjion<id>.bin`, `areamark<id>.raw`) stays
binary.

A generated mesh map (MAP_PIPELINE.md) adds per area `fit` (the radius in map units of the largest circle
around the army anchor inside the area) and a top-level `display` object: `maxZoom` (the largest camera zoom;
the classic maps have 1), `unitFitRadius` and `minUnitScale` (an area whose `fit` is below `unitFitRadius`
draws its units, flag and buildings scaled by `fit / unitFitRadius`, never below `minUnitScale`),
`markerZoom` (below this on-screen unit scale, camera zoom x unit scale, armies show as a flag of constant
size and buildings are left out) and `flags` (`all`: a flag in every owned area, the classic look;
`armies`: only where units or a building stand), `terrain` (the background picture: tiles `file` with `{0}`
for the index, `columns` x `rows` over the whole map, and `ownerAlpha`, the opacity of the owners' colours
over it) and `detail` (the paper grain: `file`, a tile of `units` map units, from the camera zoom `minZoom`).
A sea area of type `port` is a harbour, as in the original: ships are built and healed there, and the AI
plans its navy from it. A generated map may store more than 16 neighbours per area: `adjion<id>.bin`
version 2 ("WCAJ", 2, area count, most neighbours, then per area the count and the neighbours).

## scenarios

The scenarios stay the original XML files (`data/xml/scenarios/battle_*.xml`, `conquest_*.xml`,
`tutorails.xml`; their area masks in `data/binary/scenarios/`). `data/json/scenarios/index.json`
names each one and summarises it; `--convert-data` generates it from the XML and `--verify-data`
checks that it still matches:

```json
{ "scenarios": {
    "battle_axis1": { "file": "data/xml/scenarios/battle_axis1.xml", "mode": "campaign", "series": "axis",
                      "level": 1, "map": 1, "areaMask": "data/binary/scenarios/battle_axis1.bin",
                      "countries": [ {"id": "de1", "alliance": 1, "player": true}, {"id": "pl", "alliance": 3} ],
                      "areas": 120, "dialogues": 4 } },
  "tutorialScripts": { "tutorails_script-568h": { "file": "scripts/tutorial/tutorails_script-568h.xml" } } }
```

* `file`: what the game reads for this scenario name (the campaign and conquest lists name scenarios
  by their original file names, `battle_axis1.xml`). A mod adds a scenario by adding its XML anywhere,
  an entry here and the matching campaign or conquest entry.
* `mode` `campaign` / `conquest` / `tutorial`, `series` and `level` from the name; `map`, `countries`
  (turn order, alliance, the player's country), `areas` and `dialogues` (counts) from the XML.

### Exported battles (optional JSON scenario format)

The pause box **Save** button writes the running battle to `game.json` in the documents folder in the
JSON form below. Put into a mod as `data/json/scenarios/<name>.json`, it is read instead of the XML of
that name. The game itself ships no JSON scenarios.

```json
{
  "map": 1, "areasEnable": "battle_axis1.bin",
  "countries": [ {"id": "de", "name": "de", "commander": "Guderian", "ai": 0, "alliance": "a", "defeated": "land",
                  "money": 300, "industry": 200, "techLevel": 3, "taxFactor": 1.35, "r": 71, "g": 87, "b": 99, "a": 90} ],
  "areas": [ {"id": 512, "country": "de", "construction": "city", "level": 2, "installation": "fort",
              "units": [ {"type": "infantry", "level": 1, "cards": 0} ]} ],
  "dialogues": [ {"commander": "Guderian", "index": 1, "atRound": 1, "left": true} ]
}
```

* `countries` are in turn order. `alliance` `a`, `b`, `c` are the sides; anything else (`n`, `d`) is
  neutral. `defeated`: `land` (default), `army`, `core`. `ai` defaults to 1 (in conquests every
  country but the player's is AI anyway). Colour defaults to 255, `techLevel` to 1, `taxFactor` to 1.
* `areas`: `construction` `city`/`industry`/`airport` with `level` (≤ 0 means none), `installation`
  `fort`/`entrenchment`/`antiaircraft`/`radar`, `units` with `type` (a `units.json` type), `level`
  (default 1) and `cards` (tactic card bits).
* `dialogues`: commander dialogue `index` shown at `atRound`, portrait on the left or right.

## tutorial scripts

The original XML (`scripts/tutorial/tutorails_script*.xml`, listed in the scenario index). A mod may
ship a JSON version as `data/json/scenarios/tutorial/<name>.json`:
`{"commands": [{"cmd": "show text", "id": 1}, ...]}`. Commands:
`sel area`, `unsel area`, `moveto area` (`id` = area), `finger at`, `finger to` (`x`, `y`),
`hide finger`, `show text` (`id` → text `tutorails <id>`), `draw rect`, `clear rect` (`x`, `y`, `w`,
`h`), `wait` (for a tap), `show image`, `hide image`, `exit`. The game picks the script by screen width
(`tutorails_script`, `-512h`, `-534h`, `-568h`, `-640h`, `_iPad`).

## texts and language settings

`data/json/text/<code>/stringtable_<code>.json` (phone) and `stringtable_<code>_iPad.json` (tablet):
`{"strings": {"<key>": "<text>"}}`. Line breaks are real newlines (`\n`). `Localizable.json` holds the
language settings: `language` (the code used in file names) and the string table names.

## options page layout

`data/json/layout/options.json`: `{"phone": [...], "tablet": [...]}`, one entry per control in order, with
`element` (`image`, `text`, `button`, `slider`, `checkbox`, `level`) and that control's attributes
(`item`, `id`, `x`, `y`, `w`, `h`, images, `font`, `content`, `align`, `color`, ...). Phone coordinates
are relative to a 480 x 320 area centred at run time; tablet coordinates are absolute 1024 x 768.

## layout profiles

`data/json/layout/profiles.json`: `{"profiles": [{"id": "phone-568", "aspectAbove": 1.721875,
"width": 568, "height": 320, "widthMode": 2, "artVariant": "phone-568-hd"}, ...]}`. Tried in order; the
first profile whose aspect range (screen width / height, exclusive bounds `aspectAbove`,
`aspectBelow`) contains the screen wins. `width` x `height` is the logical screen the UI is laid out in,
`widthMode` the original's mode (2 phone, 3 tablet), `artVariant` the art folder it prefers. The
built-in profiles are the original buckets (640/568/534/480 x 320, 1024 x 768 below 1.40625).

## audio

`data/json/audio/audio.json`:

* `music`: `menu`, `battle` (one track picked at random per battle), `defeat`; each a list of files.
* `ui`: interface sounds by role: `click`, `buy`, `commanderLevelUp`, `mainMenu`, `explosion`.
* `effects`: the in-game effects by id (`draft`, `move`, `occupy`, `cannon`, `rocket`, `fire`, `exp`,
  `strike`, `select`, `cancel`, `ship`, `celebrate`, `after_war`, `buff`, `build`, `card_interface`,
  `gas`, `lv_up`, `machine_gun`, `naval_gun`, `pop`, `supply`).
* `preload`: sounds loaded when the game starts.

File names are original names or package paths; a mod can point a role at its own file.

## countries

`data/json/countries/countries.json`: `{"countries": {"gb": {"commanderMedal": true}, "ca": {}, ...},
"commonMedals": ["common_1", "common_2", "common_3"]}`. The ids whose unit art (`<unit>_<id>.png` in
the unit atlas) and flag (`flag_<id>.png`) the game looks up; `others` is the fallback art.
`commanderMedal` countries have an AI commander badge (`medal_<id>.png`), the others use the common
badges. Scenarios refer to these ids, with a digit for several countries of one nation (`de1`, `de2`).

## ai

`data/json/ai/profiles.json`: `{"default": "standard", "profiles": {"classic": {...}, "standard": {"base": "classic", ...}, "hard": {"base": "standard", ...}}}`.
The AI code (`Game/AI`, original names) decides with these numbers; `classic` holds the original
game's numbers and reproduces its decisions exactly (the regression goldens run with `--ai classic`).
A profile names a `base` and lists only what it changes. `default` is the profile new games use;
`--ai NAME` on the desktop overrides it.

Every unit's choice is the highest scored action (stay, move to a neighbour, attack, march towards a
target); the fields are the numbers of those scores:

* area value: `taxWeight`, `industryWeight`, `areaTypeValue` (by area type id: normal, capital, port,
  large city, normal city), `installationValue` (none, fort, entrenchment, anti-aircraft, radar);
* staying: `stayNoEnemyDivisorPerUnit`, `stayThreatenedMultiplier`;
* moving: `moveEnemyBonus`, `moveNeutralBonus`, `landingBonus`, `movePlayerBonus`, `moveEmptyOwnAreaPercent`;
* attacking: `attackEnemyExtra`, `attackNeutralExtra`, `navyFortPenalty`, `navyShoreBonus`,
  `assaultBonus`, `targetDefencePenalty`, `commanderBonus`, `carrierTargetBonus`, `attackPlayerBonus`,
  `attackStackHpPercent` (percent of the hp behind the defender's front unit that counts against the
  attack; classic 0: only the front unit counts);
* marching: `pathStayIfAreaValueAbove`, `pathTechFactor`, `pathBase`, `pathStrongHp`, `pathStrongBonus`,
  `pathFullHealthBonus`, `pathNavyBonus`, `pathAssaultBonus`, `pathCommanderBonus`, `pathLevelBonus`;
* defending (all 0 in classic): an own area's defence weight is the sum of `defendCapital`,
  `defendPort`, `defendAirfield`, `defendCity` and `defendHome` (an area the country owned when the
  battle started) for what it is; its threat is the hp of the enemy units around it divided by the
  hp of its own units, capped at `threatCapPercent`. Staying in it gains weight × threat ×
  `defendStayPercent` / 100, moving into it gains weight × threat × `defendReinforcePercent` / 100,
  and taking the last unit out of a threatened own capital costs `capitalLastUnitPenalty`;
* cards: `cardBands` (the random card category bands, percent), `cardReservePercent` (no card once
  money or industry fell below this percent of the turn's start), `cardAlwaysBuyMultiple`,
  `cardRefusePercent` (refusal chance with money up to 2x, up to 3x, above 3x the price).

`standard` holds and reinforces threatened capitals, ports, airfields, cities and home areas, never
strips a threatened capital, weighs whole enemy stacks and buys cards more readily. `hard` adds a
stronger push against the player. Tests: `Tests/ai_defends_marseille_standard` (France reinforces
Marseille and still holds it after Italy's first assault; the classic AI loses it there),
`ai_standard_autoplay`, and the unit tests in `Tests/Unit/AiProfileTests.cs`.

---

<a id="chinese"></a>

# 游戏数据及其模式（schema）

游戏的定义数据都是 JSON。这些文件位于 `assets/data/json/`（人工编写的源数据，纳入 git），并以相同路径进入运行时游戏资源包。原版 XML 文件原封不动地保留在 `assets/data/xml/` 中，并且仍会被打包；游戏读取的是 JSON，但**剧本和教程脚本例外，它们仍使用原版 XML 文件**：由一个索引 JSON 为它们命名并给出摘要。

特殊行为仍保留在代码中，通过类型、特性（trait）或动作 id 来选择（地区类型的 `capital` 或 `port` 特性、设施 id、卡牌类型）；这些规则用到的每个数值都在数据中。

## 模式（schema）与检查

每个数据文件在 `assets/data/json/schemas/` 中都有一个 JSON Schema（draft 2020-12）：

| 模式（schema） | 文件 |
|---|---|
| `units.schema.json` | `units/units.json` |
| `cards.schema.json` | `cards/cards.json` |
| `commanders.schema.json`, `portraits.schema.json` | `commanders/commanders.json`, `commanders/portraits.json` |
| `campaigns.schema.json`, `conquests.schema.json` | `campaigns/campaigns.json`, `conquests/conquests.json` |
| `terrain.schema.json` | `terrain/terrain.json` |
| `medals.schema.json`, `rules.schema.json` | `rules/medals.json`, `rules/rules.json` |
| `countries.schema.json` | `countries/countries.json` |
| `audio.schema.json` | `audio/audio.json` |
| `layout-profiles.schema.json`, `options-layout.schema.json` | `layout/profiles.json`, `layout/options.json` |
| `map-areas.schema.json` | `maps/<id>/areas.json` |
| `scenario-index.schema.json` | `scenarios/index.json` |
| `text.schema.json` | `text/<code>/*.json` |
| `ai.schema.json` | `ai/profiles.json` |

* **加载时**（`JsonData.LoadMerged`，`Engine/IO/JsonSchema.cs`），合并后的文件（游戏数据加上模组（mod））会按其模式进行检查。发现的问题会连同其 JSON 路径一起记录到日志中（`data/json/cards/cards.json: $.countries.others.Tank.price: -5 is below the minimum 0`）。如果只有模组的版本无效，则丢弃模组对该文件的修改，改用游戏自身的数据。
* `dotnet run --project Hosts/Desktop -- --verify-data`（属于 `Tests/run-tests.ps1` 的一部分）会按模式检查每个数据文件，然后将每种类型加载两次——一次从原版 XML（或内置表）加载，一次从 JSON 加载——并逐字段比较得到的定义。因此对 JSON 的有意修改会在这里显现出来；加上 `--mods <folder>` 时，它会准确列出某个模组改动了什么。
* `dotnet run --project Hosts/Desktop -- --convert-data` 从原版文件重新生成 JSON（内容没有变化的文件保持不动）。

记录（record）拒绝未知字段（`additionalProperties: false`），因此拼错的字段名会报错；注册表（以名称为键的对象）接受任意键。

## 约定

* 允许使用注释（`//`、`/* */`）和尾随逗号。
* 数字也可以写成字符串，反之亦然；值的解析方式与原版 XML 属性完全相同（整数字段接受 `"12"`、`12` 和 `12.0`）。
* 注册表是以名称或 id 为键的对象（`"units": {"tank": {...}}`），绝不是数组，这样模组可以只修改其中一个条目。有序列表（剧本中的国家、教程命令）是数组。
* 每个文件都有 `"format": 1` 和一段 `"about"` 文本；游戏会忽略这两者。
* 模组会合并进这些文件：对象逐键合并，数组和值整体替换，`null` 删除一个键，`"$replace": true` 替换整个对象。参见 `MODDING.md`。

| 文件 | 取代 | 内容 |
|---|---|---|
| `data/json/units/units.json` | `armydef.xml` | 单位类型、各国属性、战斗规则、单位等级 |
| `data/json/cards/cards.json` | `carddef.xml` | 各国的卡牌 |
| `data/json/commanders/commanders.json` | `commanderdef.xml` | 将领、将领等级 |
| `data/json/commanders/portraits.json` | `generalphotos.xml` | 将领肖像 |
| `data/json/campaigns/campaigns.json` | `battlelist.xml` | 选择地图上的战役关卡 |
| `data/json/conquests/conquests.json` | `conquestlist.xml` | 选择地图上的征服 |
| `data/json/terrain/terrain.json` | 代码中的常量 | 地区类型、建筑、设施 |
| `data/json/rules/medals.json` | 代码中的常量 | 战争勋章的效果 |
| `data/json/rules/rules.json` | 代码中的常量 | 全局数值：战斗骰子、经济上限 |
| `data/json/layout/options.json` | `guioptions.xml` | 选项页面布局 |
| `data/json/layout/profiles.json` | 代码中的常量 | 屏幕布局（按宽高比分档） |
| `data/json/audio/audio.json` | 代码中的文件名 | 什么情况下播放哪个声音 |
| `data/json/countries/countries.json` | 代码中的列表 | 拥有自己的单位美术、旗帜、徽章的国家 |
| `data/json/ai/profiles.json` | AI 代码中的常量 | AI 配置：classic（原版）、standard、hard |
| `data/json/maps/<id>/areas.json` | `areatax<id>.xml` | 每个地区的地区类型和基础税收 |
| `data/json/scenarios/index.json` | （新增） | 剧本 XML 文件和教程脚本的索引 |
| `data/json/text/<code>/stringtable_<code>[_iPad].json` | `stringtable_*.xml` | 文本 |
| `data/json/text/<code>/Localizable.json` | `<lang>.lproj/Localizable.strings` | 语言设置 |

以下内容没有转换，因为它们是资源格式而不是游戏数据：纹理图集（与其图像放在一起的 `*.xml`）、位图字体（`.fnt`）、粒子效果（`effect_*.xml`）、地图几何数据（`area<id>.bin`、`adjion<id>.bin`、`areamark<id>.raw`）以及剧本的地区掩码（`*.bin`，由 `areasEnable` 引用）。

---

## 单位（units）

`data/json/units/units.json`

```json
{
  "legacyCombatDefaults": false,
  "weaponArmorRules": { "1": { "2": "150%", "3": 0.8 } },
  "unitTypes": {
    "infantry": { "id": 0, "image": "soldier" },
    "carrier":  { "id": 10, "image": "transportship", "countryIndependent": true }
  },
  "countries": {
    "others": {
      "tank": { "unitType": 2, "strength": 110, "movement": 2, "minAttack": 2, "maxAttack": 9, "armorType": 2,
                "combat": { "traits": ["cross-entrenchment"],
                            "attack": { "mode": 0, "engagement": 0, "range": { "shape": "graph", "min": 0, "max": 100 } },
                            "result": { "movementRestoreOnKill": 1 } } }
    },
    "cn": { "infantry": { ... } }
  },
  "levels": [ { "attack": 0, "defence": 0, "turnRecovery": 0, "instantRecovery": 0, "maxHpPercent": 0, "expToNext": 90 }, ... ]
}
```

* `legacyCombatDefaults`：为 true 时，没有显式战斗数据的单位会使用重制版第一个版本中硬编码的规则。随附的数据将其设为 false；所有内容都是显式给出的。
* `weaponArmorRules`：武器类型（1..32）→ 护甲类型（1..255）→ 伤害倍率（`"150%"`、`1.5`、`"inf"`）。拥有多种武器类型的单位使用其中最高的倍率。
* `unitTypes`：名称 → 稳定的 `id`、图集图像基础名（`image`；除非设置了 `countryIndependent`，否则会追加国家后缀）。在剧本中使用但未在此列出的名称会获得一个从 1000 开始的动态 id。
* `countries`：国家 id → 单位类型 → 定义。对于某一类型没有自己条目的国家，一律使用 `others`。

单位定义字段：

| 字段 | 含义 |
|---|---|
| `strength`, `movement`, `minAttack`, `maxAttack` | 基础属性（缺省为 0） |
| `unitType` | 战斗类别：0 不可选择，1 步兵，2 装甲，3 炮兵，4 海军（0..255） |
| `navy` | 在海上绘制为舰船 / 运输船；与战斗类别无关 |
| `weaponType` | 武器类型列表（`"1,3"`、`"1 3"`、`"1|3"`） |
| `armorType` | 护甲类型 0..255 |
| `image`, `countryIndependent` | 针对该国家覆盖单位类型的图像设置 |
| `combat` | 战斗规则，见下文 |

`combat`：

| 字段 | 含义 |
|---|---|
| `traits` | 由以下各项组成的列表：`cross-entrenchment`（攻击堑壕时保留移动力）、`secondary-attack`、`recv_fort_second_counter`（会受到要塞的第二次反击）、`cannot-occupy` |
| `canOccupy` | `cannot-occupy` 的易读形式；优先于该特性 |
| `aiNoCounterBonus` | 目标无法反击时的 AI 目标评分（默认 300） |
| `attack` | `mode` 0 近战 / 1 投射、骚扰 / 2 空袭（或 `direct`、`projectile`、`airstrike`、`infiltration`）；空袭使用的 `payload`：1 `airstrike`、2 `bomber`、3 `nuclear`；`attackCost` 特殊攻击的移动力消耗（`"inf"` 清空全部移动力）；`engagement` 0 近战 / 1 投射；`range`；`damage` |
| `counter` | `inherit: "attack"` 复制攻击的设置；否则只读取 `range`（空袭类攻击者以近战方式反击）；`damage` |
| `range` | `shape` 为 `graph`（1000 = 一个地区步长）或 `circle`（1000 = 地图距离 100）；`min`、`max`、`radius`（强制为圆形）、`maxInclusive`、`radiusSource: "country"`（使用所属国家的空袭半径） |
| `damage` | 由 `{target: <unitType>, phase: "attack"|"counter"|..., rate: "130%"}` 组成的条件伤害倍率列表；位于 `attack`/`counter` 之下时，phase 默认取所在的那个阶段 |
| `result` | `constructionLevelDegrade` 和 `constructionLevelChance`（降低目标建筑等级的概率，%）、`movementRestoreOnKill` |

`levels`：五个单位等级（0..4）：攻击和防御加成、每回合恢复量、升级时的恢复量、最大兵力加成（%），以及升到下一级所需的经验（按最大兵力 / 100 缩放）。

## 卡牌（cards）

`data/json/cards/cards.json`：`{"countries": {"<country>": {"<card name>": {...}}}}`，其他所有国家使用 `others`。

| 字段 | 含义 |
|---|---|
| `id` | 卡牌 id（0..28；28 是精锐步兵卡） |
| `type` | `army`、`navy`、`airforce`、`development`、`strategy` |
| `price`, `industry` | 金钱和工业的花费 |
| `round` | 再次可用之前需要等待的回合数 |
| `tech` | 所需的科技等级 |
| `relatedArmyId` | 该卡牌征召的单位类型 id（默认：卡牌 id） |
| `maxTimes` | 每局可使用的次数（0 = 无限制） |
| `image`, `intro` | 卡牌图片（图集名称）和文本键 |

AI 的征召顺序按国家分别依价格、然后工业、然后科技排列。

## 将领（commanders）

`data/json/commanders/commanders.json`：`{"commanders": {"<name>": {"country": "de", "rank": 9, "infantry": 2, "airforce": 1, "artillery": 1, "armour": 3, "navy": 0, "honour": 2}}, "levels": [...]}`

六个勋章字段是该将领的战争勋章等级。`levels` 列出玩家将领的 15 个等级：攻击、防御、恢复、最大兵力加成（与单位相同）、`coolDown` 回合数以及 `medalsToNext`。

## 肖像（portraits）

`data/json/commanders/portraits.json`：`{"portraits": {"<general name>": {"file": "general_rommel.webp", "w": 113, "h": 137, "refx": 20}}}`

## 战役与征服（campaigns 和 conquests）

`data/json/campaigns/campaigns.json`：`{"battles": {"<key>": {...}}}`，包含 `age`（标签文本）、`centerX`、`centerY`、`ageX`、`ageY`、`victory` 和 `greatVictory`（回合限制，默认分别为 15 和 10）、`flags` 和 `arrows`（由 `{name, x, y, rot, scale}` 组成的列表，`rot` 以度为单位）。

`data/json/conquests/conquests.json`：`{"conquests": {"<key>": {"centerX", "centerY", "flags": [...]}}}`，以及该征服地图的两个选项：

* `fogOfWar`（默认 `false`）：玩家所在同盟既未占领也不相邻的地区中的部队会被隐藏（只影响绘制；AI 不受影响）；
* `leftRightWrap`（默认 `false`）：地图在水平方向上循环：镜头可以越过左右边缘，移动、箭头、空袭范围和距离都走跨越接缝的近路。地图必须覆盖整个宽度（世界地图：`mods/map-natural-earth`，见 MAP_PIPELINE.md）。

两者在游戏开始或读取该征服时读取。世界地图的征服 10（WORLD 1939）设置为 `"fogOfWar": false, "leftRightWrap": true`。

## 地形（terrain）

`data/json/terrain/terrain.json`。游戏规则通过查询这些定义来做判断，而不是在代码中检测“capital”或“fort”；随附的数值精确再现原版游戏。

`areaTypes`（键是 `data/json/maps/<id>/areas.json` 中使用的名称；用一个新名称加一个新 `id` 即可新增一种类型）：

| 字段 | 含义 |
|---|---|
| `id` | 存档中保存的数字（0 普通，1 首都，2 港口，3 大城市，4 普通城市） |
| `cityLevel`, `industryLevel` | 该地区在没有建筑时具有的等级；新建的城市 / 工业从比它高一级开始，受损的城市 / 工业在退回到该等级时消失 |
| `healing` | 仍有剩余移动力的单位每回合恢复的兵力（每级再加 `healingPerLevel`） |
| `navyHealingMultiplier` | 舰船的恢复系数（港口：2） |
| `defence` | 位于此处的单位获得的额外防御（此值与设施的防御取较大者生效） |
| `capital`, `port`, `keyArea` | 算作首都；可以征召舰船 / 绘制港口；计入 “core” 失败类型 |
| `encirclementNeedsGarrison` | 该类型的敌方地区只有在其中有单位时才能闭合包围圈 |
| `medalBonus` | 加到在此处发生的战斗的勋章掷骰上 |
| `ai` | AI 目标值：`construction`、`airStrike`、`nuclear`、`airborne` |

`constructions`（`city`、`industry`、`airport`）：`maxLevel`、`perLevel`（每级城市的税收、每级工业的工业产出）、`healingLevel`（该地区至少按此等级恢复；机场为 1）。

`installations`（`fort`、`entrenchment`、`antiaircraft`、`radar`）：`defence`、`minCounterRange`（要塞：防守方的反击距离至少为一个地区）、`stopsAttackers`（堑壕：攻击方会失去移动力，除非具有 `cross-entrenchment`）、`secondCounter`（要塞）、`halvesRangedDamage`（雷达）、`airStrikeDamagePercent`（防空 30，雷达 50）、`protectsFromBombing`、`ai.airStrikePenalty`。

`rules`：`healingPerLevel`（3）、`seaMaxMovement`（1），以及空袭目标和空降目标的 AI 等级权重。

## 勋章（medals）

`data/json/rules/medals.json`：六种战争勋章（`infantry`、`airforce`、`artillery`、`armour`、`navy`、`commerce`）的作用。效果的种类由代码实现，其数值则是数据；随附的数值精确再现原版。

| 字段 | 含义 |
|---|---|
| `upgradeCost.base`, `upgradeCost.perLevel` | 提升一级战争勋章所需的勋章数：base + perLevel x 当前等级 |
| `commerce.incomePercentPerLevel` | 每级商业勋章带来的额外税收和工业（10） |
| `airforce.attackBonus`, `attackBonusFromLevel` | 空袭攻击加成及其开始生效的等级 |
| `airforce.radius`, `radiusUpgraded`, `radiusUpgradedFromLevel` | 以地图单位计的空袭半径（300，从 2 级起为 400） |
| `airforce.noCooldownFromLevel` | 从此等级起空军卡牌没有冷却（3） |
| `draftBonuses` | 有序列表：当所属国家的 `medal` 达到 `level` 或以上时，用卡牌征召的单位获得 `upgrades` 次升级以及 `addTactics`（`assault`、`defence`、`carrier`）；`cards` 将其限定于指定的卡牌 id，`cardType` 限定于某一卡牌类型 |

## 规则（rules）

`data/json/rules/rules.json`：原先是常量的全局数值。`combat.diceThresholds`：兵力百分比；单位掷一个骰子，其兵力每高于一个阈值就再多掷一个（`[50, 25, 15, 5]`：高于 50 % 时掷五个骰子……不超过 5 % 时只掷一个）。`economy.moneyCap`、`economy.industryCap`：99999。

## 地图地区表

`data/json/maps/<id>/areas.json`：`{"areas": {"<area id>": {"type": "capital", "tax": 30}}}`。此处未列出的地区是税收为 100 的普通地区。几何数据（`area<id>.bin`、`adjion<id>.bin`、`areamark<id>.raw`）仍保持二进制格式。

生成的网格地图（见 MAP_PIPELINE.md）会为每个地区增加 `fit`（以地区内军队锚点为圆心、完全位于地区内的最大圆的半径，单位为地图单位），并在顶层增加 `display` 对象：`maxZoom`（镜头最大缩放；经典地图为 1）、`unitFitRadius` 和 `minUnitScale`（`fit` 小于 `unitFitRadius` 的地区，其部队、旗帜和建筑按 `fit / unitFitRadius` 缩小绘制，但不小于 `minUnitScale`）、`markerZoom`（屏幕上的部队比例，即镜头缩放 × 部队比例，低于该值时军队显示为固定大小的旗帜，不绘制建筑）`flags`（`all`：每个有归属的地区都插旗，即经典样式；`armies`：只在有部队或建筑的地区插旗）、`terrain`（背景图：图块 `file`，其中 `{0}` 为序号，`columns` × `rows` 覆盖整张地图，以及 `ownerAlpha`，即叠加其上的归属色的不透明度）和 `detail`（纸张纹理：`file`，每块 `units` 地图单位，从镜头缩放 `minZoom` 起显示）。类型为 `port` 的海上地区就是港口，与原版一致：在那里建造和修理舰船，AI 也据此规划海军。生成的地图每个地区可以存储超过 16 个邻居：`adjion<id>.bin` 版本 2（"WCAJ"、2、地区数、最多邻居数，然后是每个地区的邻居数和各邻居）。

## 剧本（scenarios）

剧本仍然是原版 XML 文件（`data/xml/scenarios/battle_*.xml`、`conquest_*.xml`、`tutorails.xml`；其地区掩码位于 `data/binary/scenarios/`）。`data/json/scenarios/index.json` 为每个剧本命名并给出摘要；`--convert-data` 从 XML 生成它，`--verify-data` 检查它是否仍与 XML 一致：

```json
{ "scenarios": {
    "battle_axis1": { "file": "data/xml/scenarios/battle_axis1.xml", "mode": "campaign", "series": "axis",
                      "level": 1, "map": 1, "areaMask": "data/binary/scenarios/battle_axis1.bin",
                      "countries": [ {"id": "de1", "alliance": 1, "player": true}, {"id": "pl", "alliance": 3} ],
                      "areas": 120, "dialogues": 4 } },
  "tutorialScripts": { "tutorails_script-568h": { "file": "scripts/tutorial/tutorails_script-568h.xml" } } }
```

* `file`：游戏针对该剧本名称所读取的文件（战役和征服列表以剧本的原版文件名引用剧本，例如 `battle_axis1.xml`）。模组要新增一个剧本，需要把它的 XML 放在任意位置，在此处添加一个条目，并添加对应的战役或征服条目。
* `mode`（`campaign` / `conquest` / `tutorial`）、`series` 和 `level` 取自名称；`map`、`countries`（回合顺序、同盟、玩家的国家）、`areas` 和 `dialogues`（数量）取自 XML。

### 导出的战斗（可选的 JSON 剧本格式）

暂停框中的 **保存**（Save）按钮会将正在进行的战斗以下面的 JSON 形式写入文档文件夹中的 `game.json`。将其作为 `data/json/scenarios/<name>.json` 放入模组后，游戏会读取它来代替同名的 XML。游戏本身不附带任何 JSON 剧本。

```json
{
  "map": 1, "areasEnable": "battle_axis1.bin",
  "countries": [ {"id": "de", "name": "de", "commander": "Guderian", "ai": 0, "alliance": "a", "defeated": "land",
                  "money": 300, "industry": 200, "techLevel": 3, "taxFactor": 1.35, "r": 71, "g": 87, "b": 99, "a": 90} ],
  "areas": [ {"id": 512, "country": "de", "construction": "city", "level": 2, "installation": "fort",
              "units": [ {"type": "infantry", "level": 1, "cards": 0} ]} ],
  "dialogues": [ {"commander": "Guderian", "index": 1, "atRound": 1, "left": true} ]
}
```

* `countries` 按回合顺序排列。`alliance` 为 `a`、`b`、`c` 时表示各方阵营；其他任何值（`n`、`d`）表示中立。`defeated`：`land`（默认）、`army`、`core`。`ai` 默认为 1（在征服模式中，除玩家之外的每个国家本来就都是 AI）。颜色默认为 255，`techLevel` 默认为 1，`taxFactor` 默认为 1。
* `areas`：`construction` 为 `city`/`industry`/`airport`，配合 `level`（≤ 0 表示没有）；`installation` 为 `fort`/`entrenchment`/`antiaircraft`/`radar`；`units` 包含 `type`（`units.json` 中的一个类型）、`level`（默认 1）和 `cards`（战术卡牌位标志）。
* `dialogues`：在第 `atRound` 回合显示的将领对话 `index`，肖像位于左侧或右侧。

## 教程脚本

原版 XML（`scripts/tutorial/tutorails_script*.xml`，列在剧本索引中）。模组可以以 `data/json/scenarios/tutorial/<name>.json` 的形式提供 JSON 版本：`{"commands": [{"cmd": "show text", "id": 1}, ...]}`。命令有：`sel area`、`unsel area`、`moveto area`（`id` = 地区）、`finger at`、`finger to`（`x`、`y`）、`hide finger`、`show text`（`id` → 文本 `tutorails <id>`）、`draw rect`、`clear rect`（`x`、`y`、`w`、`h`）、`wait`（等待一次点击）、`show image`、`hide image`、`exit`。游戏根据屏幕宽度选择脚本（`tutorails_script`、`-512h`、`-534h`、`-568h`、`-640h`、`_iPad`）。

## 文本与语言设置

`data/json/text/<code>/stringtable_<code>.json`（手机）和 `stringtable_<code>_iPad.json`（平板）：`{"strings": {"<key>": "<text>"}}`。换行是真正的换行符（`\n`）。`Localizable.json` 保存语言设置：`language`（文件名中使用的代码）以及字符串表的名称。

## 选项页面布局

`data/json/layout/options.json`：`{"phone": [...], "tablet": [...]}`，按顺序每个控件一个条目，包含 `element`（`image`、`text`、`button`、`slider`、`checkbox`、`level`）以及该控件的属性（`item`、`id`、`x`、`y`、`w`、`h`、图像、`font`、`content`、`align`、`color`……）。手机坐标相对于运行时居中放置的 480 x 320 区域；平板坐标是 1024 x 768 下的绝对坐标。

## 布局配置

`data/json/layout/profiles.json`：`{"profiles": [{"id": "phone-568", "aspectAbove": 1.721875, "width": 568, "height": 320, "widthMode": 2, "artVariant": "phone-568-hd"}, ...]}`。按顺序逐个尝试；宽高比范围（屏幕宽度 / 高度，开区间边界 `aspectAbove`、`aspectBelow`）包含当前屏幕的第一个配置胜出。`width` x `height` 是 UI 进行布局时所用的逻辑屏幕，`widthMode` 是原版的模式（2 手机，3 平板），`artVariant` 是它优先使用的美术文件夹。内置配置就是原版的分档（640/568/534/480 x 320，以及宽高比低于 1.40625 时的 1024 x 768）。

## 音频（audio）

`data/json/audio/audio.json`：

* `music`：`menu`、`battle`（每场战斗随机选择一首曲目）、`defeat`；每项都是一个文件列表。
* `ui`：按用途划分的界面音效：`click`、`buy`、`commanderLevelUp`、`mainMenu`、`explosion`。
* `effects`：按 id 划分的游戏内音效（`draft`、`move`、`occupy`、`cannon`、`rocket`、`fire`、`exp`、`strike`、`select`、`cancel`、`ship`、`celebrate`、`after_war`、`buff`、`build`、`card_interface`、`gas`、`lv_up`、`machine_gun`、`naval_gun`、`pop`、`supply`）。
* `preload`：游戏启动时预先加载的声音。

文件名可以是原版名称或游戏资源包中的路径；模组可以将某个用途指向它自己的文件。

## 国家（countries）

`data/json/countries/countries.json`：`{"countries": {"gb": {"commanderMedal": true}, "ca": {}, ...}, "commonMedals": ["common_1", "common_2", "common_3"]}`。这些是游戏据以查找单位美术（单位图集中的 `<unit>_<id>.png`）和旗帜（`flag_<id>.png`）的 id；`others` 是后备美术。`commanderMedal` 国家拥有 AI 将领徽章（`medal_<id>.png`），其他国家使用通用徽章。剧本引用这些 id，同一国家在剧本中出现多个时会附加一位数字（`de1`、`de2`）。

## AI（ai）

`data/json/ai/profiles.json`：`{"default": "standard", "profiles": {"classic": {...}, "standard": {"base": "classic", ...}, "hard": {"base": "standard", ...}}}`。AI 代码（`Game/AI`，沿用原版名称）依据这些数值做出决策；`classic` 保存原版游戏的数值，并精确再现其决策（回归测试基准（golden）以 `--ai classic` 运行）。一个配置指定一个 `base`，并且只列出它所修改的内容。`default` 是新游戏使用的配置；在桌面版上用 `--ai NAME` 可以覆盖它。

每个单位的选择都是评分最高的行动（原地停留、移动到相邻地区、攻击、朝目标行军）；下列字段就是这些评分中所用的数值：

* 地区价值：`taxWeight`、`industryWeight`、`areaTypeValue`（按地区类型 id：普通、首都、港口、大城市、普通城市）、`installationValue`（无、要塞、堑壕、防空、雷达）；
* 停留：`stayNoEnemyDivisorPerUnit`、`stayThreatenedMultiplier`；
* 移动：`moveEnemyBonus`、`moveNeutralBonus`、`landingBonus`、`movePlayerBonus`、`moveEmptyOwnAreaPercent`；
* 攻击：`attackEnemyExtra`、`attackNeutralExtra`、`navyFortPenalty`、`navyShoreBonus`、`assaultBonus`、`targetDefencePenalty`、`commanderBonus`、`carrierTargetBonus`、`attackPlayerBonus`、`attackStackHpPercent`（防守方最前方单位身后那些单位的 hp 中，有多大百分比计入对此次攻击的不利评估；classic 为 0：只计最前方的单位）；
* 行军：`pathStayIfAreaValueAbove`、`pathTechFactor`、`pathBase`、`pathStrongHp`、`pathStrongBonus`、`pathFullHealthBonus`、`pathNavyBonus`、`pathAssaultBonus`、`pathCommanderBonus`、`pathLevelBonus`；
* 防守（在 classic 中全部为 0）：己方地区的防守权重是按其属性对应的 `defendCapital`、`defendPort`、`defendAirfield`、`defendCity` 和 `defendHome`（战斗开始时该国已拥有的地区）之和；其威胁值是周围敌方单位的 hp 除以该地区内己方单位的 hp，上限为 `threatCapPercent`。留在该地区可获得 权重 × 威胁 × `defendStayPercent` / 100，移入该地区可获得 权重 × 威胁 × `defendReinforcePercent` / 100，而将受威胁的己方首都中的最后一个单位撤出则要付出 `capitalLastUnitPenalty` 的代价；
* 卡牌：`cardBands`（随机卡牌类别的区间，百分比）、`cardReservePercent`（一旦金钱或工业跌破本回合开始时数值的这一百分比，就不再购买卡牌）、`cardAlwaysBuyMultiple`、`cardRefusePercent`（金钱不超过价格 2 倍、不超过 3 倍、超过 3 倍时的拒绝购买概率）。

`standard` 会坚守并增援受威胁的首都、港口、机场、城市和本土地区，从不抽空受威胁的首都，会权衡整个敌方单位堆叠，并且更愿意购买卡牌。`hard` 在此基础上加强了对玩家的进攻。测试：`Tests/ai_defends_marseille_standard`（法国增援马赛，并在意大利的第一次进攻之后仍守住该地；classic AI 在那里会丢失马赛）、`ai_standard_autoplay`，以及 `Tests/Unit/AiProfileTests.cs` 中的单元测试。
