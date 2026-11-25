#!/usr/bin/env python3
"""Audit and convert scenario army references for ArmyDef flattening.

The default audit is read-only. Conversion requires an explicit output directory.
Each area's country instance ID is resolved to its faction name before mapping.
"""

from __future__ import annotations

import argparse
import collections
import copy
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


BASE_ARMY_TYPES = {
    "infantry",
    "panzer",
    "artillery",
    "rocket",
    "tank",
    "heavytank",
    "destroyer",
    "cruiser",
    "battleship",
    "aircraftcarrier",
    "carrier",
    "airstrike",
    "bomber",
    "airborne",
    "nuclearbomb",
    "eliteinfantry",
    "realbomber",
}

UNIT_TYPES = {
    "infantry": "infantry",
    "panzer": "armor",
    "artillery": "artillery",
    "rocket": "artillery",
    "tank": "armor",
    "heavytank": "armor",
    "destroyer": "navy",
    "cruiser": "navy",
    "battleship": "navy",
    "aircraftcarrier": "navy",
    "carrier": "transport",
    "airstrike": "air",
    "bomber": "air",
    "airborne": "air",
    "nuclearbomb": "air",
    "eliteinfantry": "infantry",
    "realbomber": "air",
}

LEGACY_COMBAT_UNIT_TYPES = {
    "1": "infantry",
    "2": "armor",
    "3": "artillery",
    "4": "navy",
    "5": "air",
}

# Legacy scenario spellings. Values are the middle-level army-type names.
ARMY_TYPE_ALIASES = {
    "heavy tank": "heavytank",
    "aircraft carrier": "aircraftcarrier",
    "elite infantry": "eliteinfantry",
    "nuclear bomb": "nuclearbomb",
}

# Entity variants adopted in docs/ARMY_FLATTEN_MAPPING.md. A missing pair uses the
# unprefixed base key. Values are image names in army_hd.xml without the extension.
ENTITY_VARIANTS = {
    ("am", "infantry"): ("am-infantry", "soldier_am"),
    ("cn", "infantry"): ("cn-infantry", "soldier_cn"),
    ("de", "infantry"): ("de-infantry", "soldier_de"),
    ("fr", "infantry"): ("fr-infantry", "soldier_fr"),
    ("gb", "infantry"): ("gb-infantry", "soldier_gb"),
    ("it", "infantry"): ("it-infantry", "soldier_it"),
    ("ja", "infantry"): ("ja-infantry", "soldier_ja"),
    ("ru", "infantry"): ("ru-infantry", "soldier_ru"),
    ("tw", "infantry"): ("tw-infantry", "soldier_tw"),
    ("am", "panzer"): ("am-panzer", "panzer_am"),
    ("de", "panzer"): ("de-panzer", "panzer_de"),
    ("fr", "panzer"): ("fr-panzer", "panzer_fr"),
    ("gb", "panzer"): ("gb-panzer", "panzer_gb"),
    ("it", "panzer"): ("it-panzer", "panzer_it"),
    ("ja", "panzer"): ("ja-panzer", "panzer_ja"),
    ("ru", "panzer"): ("ru-panzer", "panzer_ru"),
    ("am", "artillery"): ("am-artillery", "cannon_am"),
    ("de", "artillery"): ("de-artillery", "cannon_de"),
    ("gb", "artillery"): ("gb-artillery", "cannon_gb"),
    ("ru", "artillery"): ("ru-artillery", "cannon_ru"),
    ("de", "rocket"): ("de-rocket", "rocketlauncher_de"),
    ("ru", "rocket"): ("ru-rocket", "rocketlauncher_ru"),
    ("am", "tank"): ("am-tank", "tank_am"),
    ("de", "tank"): ("de-tank", "tank_de"),
    ("fr", "tank"): ("fr-tank", "tank_fr"),
    ("gb", "tank"): ("gb-tank", "tank_gb"),
    ("it", "tank"): ("it-tank", "tank_it"),
    ("ja", "tank"): ("ja-tank", "tank_ja"),
    ("ru", "tank"): ("ru-tank", "tank_ru"),
    ("am", "heavytank"): ("am-heavytank", "heavytank_am"),
    ("de", "heavytank"): ("de-heavytank", "heavytank_de"),
    ("fr", "heavytank"): ("fr-heavytank", "heavytank_fr"),
    ("gb", "heavytank"): ("gb-heavytank", "heavytank_gb"),
    ("ja", "heavytank"): ("ja-heavytank", "heavytank_ja"),
    ("ru", "heavytank"): ("ru-heavytank", "heavytank_ru"),
    ("de", "eliteinfantry"): ("de-eliteinfantry", "eliteinfantry_de"),
}

# These are non-entity test definitions. They are checked but never used while
# converting battle/conquest <army> nodes.
AIR_MISSION_VARIANTS = {
    ("am", "airstrike"): "am-airstrike",
    ("de", "airstrike"): "de-airstrike",
    ("am", "bomber"): "am-bomber",
    ("de", "bomber"): "de-bomber",
}

SCENARIO_NAME = re.compile(
    r"^(?:battle_.+|conquest(?:_\d+)?)\.xml$", re.IGNORECASE
)
IMAGE_NAME = re.compile(r'<Image\s+name="([^"]+\.png)"')
ARMY_TAG = re.compile(r'(<army\b[^>]*\btype\s*=\s*")([^"]+)(")', re.IGNORECASE)

FLAT_KEY_TO_ARMY_TYPE = {
    key: army_type for (_, army_type), (key, _) in ENTITY_VARIANTS.items()
}


def default_assets_directory() -> Path:
    script_directory = Path(__file__).resolve().parent
    for repository_directory in (script_directory, script_directory.parent):
        candidate = repository_directory / "WC2" / "assets"
        if candidate.is_dir():
            return candidate
    return script_directory / "WC2" / "assets"


def normalize_army_type(value: str) -> str:
    normalized = value.strip().lower()
    return ARMY_TYPE_ALIASES.get(normalized, normalized)


def flat_army_key(faction: str, army_type: str) -> str:
    variant = ENTITY_VARIANTS.get((faction, army_type))
    return variant[0] if variant else army_type


def read_atlas_images(path: Path) -> set[str]:
    # army_hd.xml is an atlas fragment with more than one top-level element, so
    # ElementTree cannot parse it as a normal XML document.
    text = path.read_text(encoding="utf-8-sig")
    return {match.group(1)[:-4] for match in IMAGE_NAME.finditer(text)}


def scenario_files(assets: Path) -> list[Path]:
    return sorted(
        (path for path in assets.iterdir() if path.is_file() and SCENARIO_NAME.match(path.name)),
        key=lambda path: path.name.lower(),
    )


def list_named(root: ET.Element, name: str) -> ET.Element | None:
    for element in root.findall("list"):
        if element.get("name") == name:
            return element
    return None


def strip_whitespace_nodes(element: ET.Element) -> None:
    if element.text is not None and not element.text.strip():
        element.text = None
    for child in element:
        strip_whitespace_nodes(child)
        if child.tail is not None and not child.tail.strip():
            child.tail = None


def audit_scenario(
    path: Path,
    counts: collections.Counter,
    errors: list[str],
    replacements: list[tuple[str, str]] | None = None,
) -> int:
    try:
        root = ET.parse(path).getroot()
    except (ET.ParseError, OSError) as exc:
        errors.append(f"{path.name}: cannot parse as scenario XML: {exc}")
        return 0

    country_list = list_named(root, "country")
    area_list = list_named(root, "area")
    if country_list is None or area_list is None:
        # Files such as conquest.xml are valid XML but are not concrete scenarios.
        return 0

    countries: dict[str, str] = {}
    for country in country_list:
        country_id = country.get("id")
        if not country_id:
            errors.append(f"{path.name}: country without id")
            continue
        if country_id in countries:
            errors.append(f"{path.name}: duplicate country id '{country_id}'")
            continue
        countries[country_id] = (country.get("name") or "").strip().lower()

    army_count = 0
    for area in area_list:
        country_id = area.get("country")
        armies = list(area.findall("army"))
        if not armies:
            continue
        if not country_id or country_id not in countries:
            errors.append(
                f"{path.name}: area {area.get('id', '?')} has armies but unknown country id "
                f"'{country_id or ''}'"
            )
            continue
        faction = countries[country_id]
        if not faction:
            errors.append(f"{path.name}: country id '{country_id}' has an empty faction name")
            continue

        for army in armies:
            legacy_type = army.get("type")
            if not legacy_type:
                errors.append(f"{path.name}: area {area.get('id', '?')} has army without type")
                continue
            normalized_type = normalize_army_type(legacy_type)
            if normalized_type in FLAT_KEY_TO_ARMY_TYPE:
                army_type = FLAT_KEY_TO_ARMY_TYPE[normalized_type]
                key = normalized_type
            elif normalized_type in BASE_ARMY_TYPES:
                army_type = normalized_type
                key = flat_army_key(faction, army_type)
            else:
                errors.append(
                    f"{path.name}: area {area.get('id', '?')} has unknown army type "
                    f"'{legacy_type}'"
                )
                continue
            counts[(faction, army_type, key)] += 1
            if replacements is not None:
                replacements.append((legacy_type, key))
            army_count += 1
    return army_count


def convert_scenario(path: Path, output: Path) -> int:
    errors: list[str] = []
    replacements: list[tuple[str, str]] = []
    army_count = audit_scenario(path, collections.Counter(), errors, replacements)
    if errors:
        raise ValueError("; ".join(errors))

    raw = path.read_bytes()
    has_bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    matches = list(ARMY_TAG.finditer(text))
    if len(matches) != len(replacements):
        raise ValueError(
            f"{path.name}: parsed {len(replacements)} scenario armies but found "
            f"{len(matches)} lexical army tags"
        )

    replacement_index = 0

    def replace_type(match: re.Match[str]) -> str:
        nonlocal replacement_index
        legacy_type, flat_key = replacements[replacement_index]
        replacement_index += 1
        if match.group(2) != legacy_type:
            raise ValueError(
                f"{path.name}: parser/lexical order mismatch at army {replacement_index}: "
                f"'{legacy_type}' != '{match.group(2)}'"
            )
        return f"{match.group(1)}{flat_key}{match.group(3)}"

    converted = ARMY_TAG.sub(replace_type, text)
    output.parent.mkdir(parents=True, exist_ok=True)
    encoded = converted.encode("utf-8")
    if has_bom:
        encoded = b"\xef\xbb\xbf" + encoded
    output.write_bytes(encoded)
    return army_count


def country_definitions(root: ET.Element, country_name: str) -> dict[str, ET.Element]:
    for country in root.findall("country"):
        if country.get("name") == country_name:
            return {
                army.get("type"): army
                for army in country.findall("army")
                if army.get("type")
            }
    return {}


def make_flat_definition(
    source: ET.Element,
    *,
    key: str,
    definition_id: int,
    army_type: str,
    image: str,
) -> ET.Element:
    result = copy.deepcopy(source)
    result.attrib.pop("unit-type", None)
    result.attrib.pop("type", None)
    result.attrib.pop("image", None)
    result.attrib.pop("country-independent", None)

    original_attributes = dict(result.attrib)
    result.attrib.clear()
    result.set("key", key)
    result.set("id", str(definition_id))
    result.set("unit-type", UNIT_TYPES[army_type])
    result.set("army-type", army_type)
    result.set("image", image)
    for name, value in original_attributes.items():
        result.set(name, value)
    for damage in result.iter("damage"):
        legacy_target = damage.get("target")
        if legacy_target in LEGACY_COMBAT_UNIT_TYPES:
            damage.attrib.pop("target")
            damage.set("target-unit-type", LEGACY_COMBAT_UNIT_TYPES[legacy_target])
    return result


def generate_flat_armydef(
    assets: Path, source_xml: Path, output: Path
) -> tuple[int, int]:
    current_path = source_xml / "armydef.xml"
    backup_path = assets / "armydef-bak.xml"
    current_root = ET.parse(current_path).getroot()
    backup_root = ET.parse(backup_path).getroot()

    type_registry: list[tuple[int, str, str, bool]] = []
    registry_element = current_root.find("army-types")
    if registry_element is None:
        raise ValueError("armydef.xml has no army-types registry")
    for element in registry_element.findall("type"):
        name = element.get("name")
        image = element.get("image")
        id_text = element.get("id")
        if not name or not image or id_text is None:
            raise ValueError("armydef.xml has an incomplete army-types entry")
        type_registry.append(
            (int(id_text), name, image, element.get("country-independent") == "true")
        )
    type_registry.sort()

    registry_names = {name for _, name, _, _ in type_registry}
    if registry_names != BASE_ARMY_TYPES:
        missing = sorted(BASE_ARMY_TYPES - registry_names)
        extra = sorted(registry_names - BASE_ARMY_TYPES)
        raise ValueError(f"army type registry mismatch; missing={missing}, extra={extra}")

    defaults = country_definitions(current_root, "others")
    cn_definitions = country_definitions(current_root, "cn")
    backup_am = country_definitions(backup_root, "am")
    backup_de = country_definitions(backup_root, "de")
    if set(defaults) != BASE_ARMY_TYPES:
        missing = sorted(BASE_ARMY_TYPES - set(defaults))
        extra = sorted(set(defaults) - BASE_ARMY_TYPES)
        raise ValueError(f"default ArmyDef mismatch; missing={missing}, extra={extra}")

    output_root = ET.Element(
        "armies", {"legacy-combat-defaults": current_root.get("legacy-combat-defaults", "false")}
    )
    output_root.append(
        ET.Comment(
            " Generated by army_flatten.py. Review this preview before replacing armydef.xml. "
        )
    )

    unit_types_element = ET.SubElement(output_root, "unit-types")
    for unit_type in dict.fromkeys(UNIT_TYPES.values()):
        ET.SubElement(unit_types_element, "type", {"key": unit_type})

    army_types_element = ET.SubElement(output_root, "army-types")
    for _, army_type, _, _ in type_registry:
        ET.SubElement(
            army_types_element,
            "type",
            {"key": army_type, "unit-type": UNIT_TYPES[army_type]},
        )

    weapon_rules = current_root.find("weapon-armor-rules")
    if weapon_rules is not None:
        copied_weapon_rules = copy.deepcopy(weapon_rules)
        strip_whitespace_nodes(copied_weapon_rules)
        output_root.append(copied_weapon_rules)

    definitions_element = ET.SubElement(output_root, "definitions")
    image_by_type: dict[str, str] = {}
    for definition_id, army_type, image_base, country_independent in type_registry:
        image = image_base if country_independent else f"{image_base}_others"
        image_by_type[army_type] = image
        definitions_element.append(
            make_flat_definition(
                defaults[army_type],
                key=army_type,
                definition_id=definition_id,
                army_type=army_type,
                image=image,
            )
        )

    next_id = max(definition_id for definition_id, _, _, _ in type_registry) + 1
    for (faction, army_type), (key, image) in ENTITY_VARIANTS.items():
        source = cn_definitions[army_type] if (faction, army_type) == ("cn", "infantry") else defaults[army_type]
        definitions_element.append(
            make_flat_definition(
                source,
                key=key,
                definition_id=next_id,
                army_type=army_type,
                image=image,
            )
        )
        next_id += 1

    for faction, backup_definitions in (("am", backup_am), ("de", backup_de)):
        for army_type in ("airstrike", "bomber"):
            key = AIR_MISSION_VARIANTS[(faction, army_type)]
            source = copy.deepcopy(defaults[army_type])
            backup = backup_definitions.get(army_type)
            if backup is None:
                raise ValueError(f"armydef-bak.xml has no ({faction}, {army_type}) definition")
            for attribute in ("strength", "movement", "minatk", "maxatk"):
                source.set(attribute, backup.get(attribute, source.get(attribute, "0")))
            definitions_element.append(
                make_flat_definition(
                    source,
                    key=key,
                    definition_id=next_id,
                    army_type=army_type,
                    image=image_by_type[army_type],
                )
            )
            next_id += 1

    generated = list(definitions_element)
    generated_keys = [element.get("key") for element in generated]
    generated_ids = [int(element.get("id", "-1")) for element in generated]
    if len(set(generated_keys)) != len(generated_keys):
        raise ValueError("generated ArmyDef keys are not unique")
    if len(set(generated_ids)) != len(generated_ids):
        raise ValueError("generated ArmyDef IDs are not unique")
    if sorted(generated_ids) != list(range(next_id)):
        raise ValueError("generated ArmyDef IDs are not contiguous from zero")

    atlas_images = read_atlas_images(assets / "army_hd.xml")
    missing_images = sorted(
        {
            element.get("image")
            for element in generated
            if element.get("image") not in atlas_images
        }
    )
    if missing_images:
        raise ValueError(f"generated ArmyDefs reference missing images: {missing_images}")

    ET.indent(output_root, space="\t")
    output.parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(output_root).write(output, encoding="utf-8", xml_declaration=True)
    return len(definitions_element), next_id - 1


def card_definition_by_key(definitions: ET.Element) -> dict[str, ET.Element]:
    result: dict[str, ET.Element] = {}
    for card in definitions.findall("card"):
        key = card.get("key")
        if not key:
            raise ValueError("carddef.xml contains a card without key")
        if key in result:
            raise ValueError(f"carddef.xml contains duplicate card key '{key}'")
        result[key] = card
    return result


def card_id(card: ET.Element) -> int:
    value = card.get("legacy-id", card.get("id"))
    if value is None:
        raise ValueError(f"card '{card.get('key', '?')}' has no id or legacy-id")
    return int(value)


def card_army_effects(card: ET.Element) -> list[ET.Element]:
    return [
        effect
        for effect in card.findall("./effects/effect")
        if effect.get("type") in {"draft-army", "airborne-deploy", "strategic-strike"}
    ]


def card_effect_army_type(effect: ET.Element) -> str:
    unit = effect.get("unit")
    if unit:
        return normalize_army_type(unit)
    if effect.get("type") != "strategic-strike":
        return ""
    payload = (effect.get("payload") or "").strip().lower()
    return {
        "1": "airstrike",
        "airstrike": "airstrike",
        "2": "bomber",
        "bomber": "bomber",
        "3": "nuclearbomb",
        "nuclear": "nuclearbomb",
        "nuclearbomb": "nuclearbomb",
    }.get(payload, "")


def flat_card_army_key(faction: str, army_type: str) -> str:
    return AIR_MISSION_VARIANTS.get(
        (faction, army_type), flat_army_key(faction, army_type)
    )


def rewritten_card_unit(card: ET.Element, faction: str) -> str | None:
    effects = card_army_effects(card)
    if not effects:
        return None
    units = {card_effect_army_type(effect) for effect in effects}
    if "" in units:
        raise ValueError(f"card '{card.get('key')}' has an army effect without a valid unit")
    if len(units) != 1:
        raise ValueError(
            f"card '{card.get('key')}' has more than one referenced army type: {sorted(units)}"
        )
    army_type = next(iter(units))
    if army_type in FLAT_KEY_TO_ARMY_TYPE:
        army_type = FLAT_KEY_TO_ARMY_TYPE[army_type]
    if army_type not in BASE_ARMY_TYPES:
        raise ValueError(f"card '{card.get('key')}' references unknown army '{army_type}'")
    return flat_card_army_key(faction, army_type)


def rewrite_card_units(card: ET.Element, flat_unit: str) -> None:
    for effect in card_army_effects(card):
        effect.set("unit", flat_unit)
    # The effect is authoritative. Keeping a legacy numeric hint here would make
    # the generated XML disagree with the flat ArmyDef ID assigned to the key.
    card.attrib.pop("relatedArmyId", None)


def country_cardtabs(root: ET.Element) -> tuple[list[str], dict[str, dict[str, list[str]]]]:
    countries = root.find("countries")
    if countries is None:
        raise ValueError("carddef.xml has no countries section")
    order: list[str] = []
    result: dict[str, dict[str, list[str]]] = {}
    for country in countries.findall("country"):
        faction = (country.get("call") or "").strip().lower()
        if not faction:
            raise ValueError("carddef.xml contains a country without call")
        if faction in result:
            raise ValueError(f"carddef.xml contains duplicate country '{faction}'")
        order.append(faction)
        tabs: dict[str, list[str]] = {}
        for tab in country.findall("./cardtabs/cardtab"):
            tab_name = tab.get("tab")
            if not tab_name:
                raise ValueError(f"country '{faction}' contains a cardtab without tab")
            if tab_name in tabs:
                raise ValueError(f"country '{faction}' contains duplicate tab '{tab_name}'")
            entries: list[str] = []
            for reference in tab.findall("card"):
                value = reference.get("key", reference.get("id"))
                if value:
                    entries.append(value)
            tabs[tab_name] = entries
        result[faction] = tabs
    if "others" not in result:
        raise ValueError("carddef.xml has no others card tabs")
    return order, result


def generate_flat_carddef(source_xml: Path, output: Path) -> tuple[int, int, int]:
    root = ET.parse(source_xml / "carddef.xml").getroot()
    definitions = root.find("definitions")
    if definitions is None:
        raise ValueError("carddef.xml has no definitions section")
    cards = card_definition_by_key(definitions)
    ids = [card_id(card) for card in cards.values()]
    if len(ids) != len(set(ids)):
        raise ValueError("carddef.xml contains duplicate card IDs")
    next_id = max(ids, default=-1) + 1

    country_order, declared_tabs = country_cardtabs(root)
    default_tabs = declared_tabs["others"]
    factions = (
        set(declared_tabs)
        | {faction for faction, _ in ENTITY_VARIANTS}
        | {faction for faction, _ in AIR_MISSION_VARIANTS}
    )

    generated_cards = [copy.deepcopy(card) for card in definitions.findall("card")]
    generated_by_key = {card.get("key"): card for card in generated_cards}
    generated_tabs: dict[str, dict[str, list[str]]] = {}
    clone_by_source_and_unit: dict[tuple[str, str], str] = {}
    clone_count = 0

    for faction in sorted(factions, key=lambda value: (value != "others", value)):
        own_tabs = declared_tabs.get(faction, {})
        output_tabs: dict[str, list[str]] = {}
        for tab_name in dict.fromkeys([*default_tabs, *own_tabs]):
            source_entries = own_tabs.get(tab_name, default_tabs.get(tab_name, []))
            converted_entries: list[str] = []
            changed = False
            for reference in source_entries:
                source = cards.get(reference)
                if source is None:
                    raise ValueError(
                        f"country '{faction}' tab '{tab_name}' references unknown card '{reference}'"
                    )
                flat_unit = rewritten_card_unit(source, faction)
                if flat_unit is None:
                    converted_entries.append(reference)
                    continue

                generated_source = generated_by_key[reference]
                current_units = {
                    normalize_army_type(effect.get("unit", ""))
                    for effect in card_army_effects(generated_source)
                    if effect.get("unit")
                }
                if current_units == {flat_unit} and all(
                    effect.get("unit") for effect in card_army_effects(generated_source)
                ):
                    converted_entries.append(reference)
                    continue

                if faction == "others" or reference.startswith(f"{faction}-"):
                    rewrite_card_units(generated_source, flat_unit)
                    clone_by_source_and_unit[(reference, flat_unit)] = reference
                    converted_entries.append(reference)
                    changed = True
                    continue

                clone_key = clone_by_source_and_unit.get((reference, flat_unit))
                if clone_key is None:
                    preferred_key = f"{faction}-{reference}"
                    if preferred_key in generated_by_key:
                        existing = generated_by_key[preferred_key]
                        existing_units = {
                            normalize_army_type(effect.get("unit", ""))
                            for effect in card_army_effects(existing)
                            if effect.get("unit")
                        }
                        if existing_units != {flat_unit}:
                            rewrite_card_units(existing, flat_unit)
                        clone_key = preferred_key
                    else:
                        clone = copy.deepcopy(source)
                        clone_key = preferred_key
                        clone.set("key", clone_key)
                        clone.attrib.pop("legacy-id", None)
                        clone.set("id", str(next_id))
                        next_id += 1
                        rewrite_card_units(clone, flat_unit)
                        generated_cards.append(clone)
                        generated_by_key[clone_key] = clone
                        clone_count += 1
                    clone_by_source_and_unit[(reference, flat_unit)] = clone_key
                converted_entries.append(clone_key)
                changed = True
            if faction == "others" or tab_name in own_tabs or changed:
                output_tabs[tab_name] = converted_entries
        generated_tabs[faction] = output_tabs

    if next_id > 128:
        raise ValueError(f"generated CardDef IDs exceed runtime limit 127 (next ID is {next_id})")

    output_root = ET.Element("cards")
    output_root.append(
        ET.Comment(
            " Generated by army_flatten.py. Review this preview before replacing carddef.xml. "
        )
    )
    output_definitions = ET.SubElement(output_root, "definitions")
    for card in generated_cards:
        output_definitions.append(card)
    output_countries = ET.SubElement(output_root, "countries")
    ordered_factions = [*country_order, *sorted(factions - set(country_order))]
    for faction in ordered_factions:
        country = ET.SubElement(output_countries, "country", {"call": faction})
        tabs_element = ET.SubElement(country, "cardtabs")
        for tab_name, entries in generated_tabs[faction].items():
            tab = ET.SubElement(tabs_element, "cardtab", {"tab": tab_name})
            for key in entries:
                ET.SubElement(tab, "card", {"key": key})

    final_cards = card_definition_by_key(output_definitions)
    final_ids = [card_id(card) for card in final_cards.values()]
    if len(final_ids) != len(set(final_ids)):
        raise ValueError("generated CardDef IDs are not unique")
    valid_army_keys = (
        BASE_ARMY_TYPES
        | {key for key, _ in ENTITY_VARIANTS.values()}
        | set(AIR_MISSION_VARIANTS.values())
    )
    for card in final_cards.values():
        for effect in card_army_effects(card):
            unit = effect.get("unit")
            if unit not in valid_army_keys:
                raise ValueError(
                    f"generated card '{card.get('key')}' references missing flat army '{unit}'"
                )
    for faction, tabs in generated_tabs.items():
        for tab_name, entries in tabs.items():
            missing = [key for key in entries if key not in final_cards]
            if missing:
                raise ValueError(
                    f"generated country '{faction}' tab '{tab_name}' has missing cards {missing}"
                )

    ET.indent(output_root, space="\t")
    output.parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(output_root).write(output, encoding="utf-8", xml_declaration=True)
    return len(final_cards), clone_count, max(final_ids, default=-1)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--assets",
        type=Path,
        default=default_assets_directory(),
        help="asset directory (default: WC2/assets beside this script)",
    )
    parser.add_argument(
        "--source-xml",
        type=Path,
        help="legacy XML input directory for generation/conversion "
        "(default: transfer-flatten/raw-xml-bak when present, otherwise --assets)",
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="print every resolved (faction, legacy type) mapping",
    )
    parser.add_argument(
        "--generate-armydef",
        type=Path,
        metavar="PATH",
        help="write a flat ArmyDef preview to PATH; never replaces armydef.xml implicitly",
    )
    parser.add_argument(
        "--generate-carddef",
        type=Path,
        metavar="PATH",
        help="write a flat CardDef preview to PATH; never replaces carddef.xml implicitly",
    )
    parser.add_argument(
        "--convert-scenarios",
        type=Path,
        metavar="DIRECTORY",
        help="write converted battle/conquest XML files to DIRECTORY",
    )
    parser.add_argument(
        "--backup-xml",
        nargs="?",
        const=Path(__file__).resolve().parent / "raw-xml-bak",
        type=Path,
        metavar="DIRECTORY",
        help="copy current carddef, armydef and scenario XML files to DIRECTORY "
        "(default: transfer-flatten/raw-xml-bak)",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    assets = args.assets.resolve()
    if not assets.is_dir():
        print(f"error: asset directory does not exist: {assets}", file=sys.stderr)
        return 2

    default_raw_xml = Path(__file__).resolve().parent / "raw-xml-bak"
    source_xml = (
        args.source_xml.resolve()
        if args.source_xml is not None
        else default_raw_xml if default_raw_xml.is_dir() else assets
    )
    if not source_xml.is_dir():
        print(f"error: source XML directory does not exist: {source_xml}", file=sys.stderr)
        return 2

    errors: list[str] = []
    atlas_path = assets / "army_hd.xml"
    try:
        atlas_images = read_atlas_images(atlas_path)
    except OSError as exc:
        print(f"error: cannot read {atlas_path}: {exc}", file=sys.stderr)
        return 2

    for (faction, army_type), (key, image) in ENTITY_VARIANTS.items():
        if image not in atlas_images:
            errors.append(
                f"mapping ({faction}, {army_type}) -> {key} references missing image '{image}.png'"
            )

    files = scenario_files(assets)
    counts: collections.Counter = collections.Counter()
    parsed_armies = 0
    for path in files:
        parsed_armies += audit_scenario(path, counts, errors)

    specialized = sum(count for (_, army_type, key), count in counts.items() if key != army_type)
    fallback = parsed_armies - specialized

    print(f"assets: {assets}")
    print(f"candidate files: {len(files)}")
    print(f"scenario armies: {parsed_armies}")
    print(f"specialized mappings: {specialized}")
    print(f"base mappings: {fallback}")
    print(f"entity variant definitions: {len(ENTITY_VARIANTS)}")
    print(f"air-mission test definitions: {len(AIR_MISSION_VARIANTS)}")
    if args.verbose:
        print("\nresolved mappings:")
        for (faction, army_type, key), count in sorted(counts.items()):
            print(f"  ({faction}, {army_type}) -> {key}: {count}")

    if errors:
        print("\nissues:", file=sys.stderr)
        for error in errors:
            print(f"  {error}", file=sys.stderr)
        return 1

    if args.backup_xml is not None:
        backup_directory = args.backup_xml.resolve()
        if backup_directory == assets:
            print("error: backup directory must not be the source asset directory", file=sys.stderr)
            return 2
        backup_sources = [assets / "carddef.xml", assets / "armydef.xml", *files]
        try:
            backup_directory.mkdir(parents=True, exist_ok=True)
            for source in backup_sources:
                destination = backup_directory / source.name
                shutil.copy2(source, destination)
                if source.read_bytes() != destination.read_bytes():
                    raise OSError(f"backup verification failed for {source.name}")
        except OSError as exc:
            print(f"error: cannot back up XML files: {exc}", file=sys.stderr)
            return 2
        print(f"backed up XML files: {len(backup_sources)} -> {backup_directory}")

    if args.convert_scenarios is not None:
        output_directory = args.convert_scenarios.resolve()
        if output_directory == assets:
            print("error: conversion output must not be the source asset directory", file=sys.stderr)
            return 2
        converted_armies = 0
        conversion_files = scenario_files(source_xml)
        try:
            for path in conversion_files:
                converted_armies += convert_scenario(path, output_directory / path.name)
        except (OSError, ValueError) as exc:
            print(f"error: cannot convert scenarios: {exc}", file=sys.stderr)
            return 2

        verification_errors: list[str] = []
        verified_armies = 0
        for path in conversion_files:
            verified_armies += audit_scenario(
                output_directory / path.name,
                collections.Counter(),
                verification_errors,
            )
        if verification_errors or verified_armies != converted_armies:
            for error in verification_errors:
                print(f"error: converted output: {error}", file=sys.stderr)
            if verified_armies != converted_armies:
                print(
                    f"error: converted {converted_armies} army references but verified "
                    f"{verified_armies}",
                    file=sys.stderr,
                )
            return 2
        print(f"converted scenarios: {len(conversion_files)} -> {output_directory}")
        print(f"converted army references: {converted_armies}")
        print(f"verified converted references: {verified_armies}")

    if args.generate_armydef is not None:
        output = args.generate_armydef.resolve()
        try:
            definition_count, maximum_id = generate_flat_armydef(assets, source_xml, output)
        except (ET.ParseError, OSError, ValueError) as exc:
            print(f"error: cannot generate flat ArmyDef: {exc}", file=sys.stderr)
            return 2
        print(f"generated: {output}")
        print(f"generated definitions: {definition_count} (IDs 0..{maximum_id})")

    if args.generate_carddef is not None:
        output = args.generate_carddef.resolve()
        try:
            definition_count, clone_count, maximum_id = generate_flat_carddef(source_xml, output)
        except (ET.ParseError, OSError, ValueError) as exc:
            print(f"error: cannot generate flat CardDef: {exc}", file=sys.stderr)
            return 2
        print(f"generated: {output}")
        print(
            f"generated card definitions: {definition_count} "
            f"({clone_count} new variants, maximum ID {maximum_id})"
        )

    if (
        args.generate_armydef is None
        and args.generate_carddef is None
        and args.convert_scenarios is None
        and args.backup_xml is None
    ):
        print("status: OK (read-only audit; no files changed)")
    else:
        print("status: OK (source assets unchanged)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
