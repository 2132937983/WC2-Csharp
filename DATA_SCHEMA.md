# Game data and its schemas

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

`data/json/conquests/conquests.json`: `{"conquests": {"<key>": {"centerX", "centerY", "flags": [...]}}}`.

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
