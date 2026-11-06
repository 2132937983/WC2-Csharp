# Game data reference (JSON)

All game data is JSON. The files live in `WC2/content/` (the authored source, in git) and end up in
the runtime package at the same relative path (`content/data/units.json` → `data/units.json`). The
original XML files stay untouched in `WC2/assets/` and are still packaged under `data/original/`,
`scenarios/`, `lang/...`, but the game reads the JSON.

The JSON files were converted from the original XML with `dotnet run -- --convert-data`.
`dotnet run -- --verify-data` (part of `tests\run-tests.ps1`) loads every type twice, from the
original XML and from the JSON, and compares the resulting definitions field by field. A deliberate
change to the JSON therefore shows up there; with `--mods <folder>` it lists exactly what a mod changes.

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
| `data/units.json` | `armydef.xml` | unit types, stats per country, combat rules, unit levels |
| `data/cards.json` | `carddef.xml` | cards per country |
| `data/commanders.json` | `commanderdef.xml` | commanders, commander levels |
| `data/portraits.json` | `generalphotos.xml` | general portraits |
| `data/campaigns.json` | `battlelist.xml` | campaign battles on the selection map |
| `data/conquests.json` | `conquestlist.xml` | conquests on the selection map |
| `data/terrain.json` | constants in the code | area types, buildings, installations |
| `data/medals.json` | constants in the code | war medal effects |
| `data/rules.json` | constants in the code | global numbers: combat dice, economy caps |
| `data/ui/options.json` | `guioptions.xml` | options page layout |
| `maps/<id>/areas.json` | `areatax<id>.xml` | area type and base tax of every area |
| `scenarios/<name>.json` | `battle_*.xml`, `conquest_*.xml`, `tutorails.xml` | scenarios |
| `scenarios/tutorial/<name>.json` | `tutorails_script*.xml` | tutorial scripts |
| `lang/<code>/stringtable_<code>[_iPad].json` | `stringtable_*.xml` | texts |
| `lang/<code>/Localizable.json` | `<lang>.lproj/Localizable.strings` | language settings |

Not converted, because they are asset formats rather than game data: texture atlases (`*.xml` next to
their images), bitmap fonts (`.fnt`), particle effects (`effect_*.xml`), map geometry (`area<id>.bin`,
`adjion<id>.bin`, `areamark<id>.raw`) and the scenario area masks (`*.bin`, referenced by
`areasEnable`).

---

## units

`data/units.json`

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

`data/cards.json`: `{"countries": {"<country>": {"<card name>": {...}}}}`, `others` for everyone else.

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

`data/commanders.json`: `{"commanders": {"<name>": {"country": "de", "rank": 9, "infantry": 2, "airforce": 1, "artillery": 1, "armour": 3, "navy": 0, "honour": 2}}, "levels": [...]}`

The six medal fields are the commander's war medal levels. `levels` lists the 15 player commander
levels: attack, defence, healing, max strength bonus (as for units), `coolDown` turns and
`medalsToNext`.

## portraits

`data/portraits.json`: `{"portraits": {"<general name>": {"file": "general_rommel.webp", "w": 113, "h": 137, "refx": 20}}}`

## campaigns and conquests

`data/campaigns.json`: `{"battles": {"<key>": {...}}}` with `age` (label text), `centerX`, `centerY`,
`ageX`, `ageY`, `victory` and `greatVictory` (turn limits, default 15 and 10), `flags` and `arrows`
(lists of `{name, x, y, rot, scale}`, `rot` in degrees).

`data/conquests.json`: `{"conquests": {"<key>": {"centerX", "centerY", "flags": [...]}}}`.

## terrain

`data/terrain.json`. The rules of the game ask these definitions instead of testing for "capital" or
"fort" in code; the shipped values reproduce the original game exactly.

`areaTypes` (keys are the names used in `maps/<id>/areas.json`; a new name with a new `id` adds a type):

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

`data/medals.json`: what the six war medals (`infantry`, `airforce`, `artillery`, `armour`, `navy`,
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

`data/rules.json`: global numbers that were constants. `combat.diceThresholds`: strength percentages;
a unit rolls one die plus one per threshold its strength is above (`[50, 25, 15, 5]`: above 50 % five
dice ... at most 5 % one). `economy.moneyCap`, `economy.industryCap`: 99999.

## map area table

`maps/<id>/areas.json`: `{"areas": {"<area id>": {"type": "capital", "tax": 30}}}`. Areas missing here
are plain areas with tax 100. The geometry (`area<id>.bin`, `adjion<id>.bin`, `areamark<id>.raw`) stays
binary.

## scenarios

`scenarios/<name>.json`, where `<name>` is the original file name without `.xml` (`battle_axis1`,
`conquest_3`, `tutorails`). A mod adds a scenario by adding a file and the matching campaign or
conquest entry.

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

The pause box **Save** button writes the running battle in this format to `game.json` in the documents
folder.

## tutorial scripts

`scenarios/tutorial/<name>.json`: `{"commands": [{"cmd": "show text", "id": 1}, ...]}`. Commands:
`sel area`, `unsel area`, `moveto area` (`id` = area), `finger at`, `finger to` (`x`, `y`),
`hide finger`, `show text` (`id` → text `tutorails <id>`), `draw rect`, `clear rect` (`x`, `y`, `w`,
`h`), `wait` (for a tap), `show image`, `hide image`, `exit`. The game picks the script by screen width
(`tutorails_script`, `-512h`, `-534h`, `-568h`, `-640h`, `_iPad`).

## texts and language settings

`lang/<code>/stringtable_<code>.json` (phone) and `stringtable_<code>_iPad.json` (tablet):
`{"strings": {"<key>": "<text>"}}`. Line breaks are real newlines (`\n`). `Localizable.json` holds the
language settings: `language` (the code used in file names) and the string table names.

## options page layout

`data/ui/options.json`: `{"phone": [...], "tablet": [...]}`, one entry per control in order, with
`element` (`image`, `text`, `button`, `slider`, `checkbox`, `level`) and that control's attributes
(`item`, `id`, `x`, `y`, `w`, `h`, images, `font`, `content`, `align`, `color`, ...). Phone coordinates
are relative to a 480 x 320 area centred at run time; tablet coordinates are absolute 1024 x 768.
