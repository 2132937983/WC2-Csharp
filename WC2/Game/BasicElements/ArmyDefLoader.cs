using static WC2.armydef_internal;

namespace WC2;

/// <summary>Loads the flat global army definition table from armydef.xml.</summary>
public sealed partial class CObjectDef
{
    public void LoadArmyDef()
    {
        ResetArmyTypeRegistry();
        ReleaseArmyDef();
        var xml = new TiXmlDocument(GetPath("armydef.xml", null));
        if (!xml.LoadFile(TIXML_ENCODING_UNKNOWN)) return;

        TiXmlElement root = xml.FirstChildElement("armies");
        if (root == null) return;

        TiXmlElement unitTypesElement = root.FirstChildElement("unit-types");
        for (TiXmlElement element = unitTypesElement?.FirstChildElement("type");
             element != null; element = element.NextSiblingElement("type"))
        {
            string key = element.Attribute("key");
            if (string.IsNullOrWhiteSpace(key) || !unitTypes.Add(key))
                throw new InvalidDataException($"armydef.xml: invalid or duplicate unit-type '{key}'");
        }
        TiXmlElement armyTypesElement = root.FirstChildElement("army-types");
        for (TiXmlElement element = armyTypesElement?.FirstChildElement("type");
             element != null; element = element.NextSiblingElement("type"))
        {
            string key = element.Attribute("key");
            string unitType = element.Attribute("unit-type");
            if (string.IsNullOrWhiteSpace(key) || !unitTypes.Contains(unitType) ||
                !armyTypeUnitTypes.TryAdd(key, unitType))
                throw new InvalidDataException($"armydef.xml: invalid or duplicate army-type '{key}'");
        }

        bool useLegacyCombatDefaults = true;
        if (ParseBoolAttribute(root, "legacy-combat-defaults", out bool legacyCombatDefaults))
            useLegacyCombatDefaults = legacyCombatDefaults;
        weaponArmorDamageRates.Clear();
        ParseWeaponArmorRules(root.FirstChildElement("weapon-armor-rules"), weaponArmorDamageRates);

        TiXmlElement definitions = root.FirstChildElement("definitions");
        if (definitions == null)
            throw new InvalidDataException("armydef.xml: missing definitions");

        // CardDef is loaded after ArmyDef, so the complete key/id registry must exist first.
        for (TiXmlElement element = definitions.FirstChildElement("army");
             element != null; element = element.NextSiblingElement("army"))
        {
            string key = element.Attribute("key");
            if (string.IsNullOrWhiteSpace(key) ||
                element.QueryIntAttribute("id", out int id) != TIXML_SUCCESS || id < 0)
                throw new InvalidDataException("armydef.xml: every army requires a key and non-negative id");
            if (!RegisterArmyType(key, id, element.Attribute("image"), true))
                throw new InvalidDataException($"armydef.xml: duplicate army key or id '{key}'/{id}");
        }

        for (TiXmlElement element = definitions.FirstChildElement("army");
             element != null; element = element.NextSiblingElement("army"))
        {
            string key = element.Attribute("key");
            int id = ResolveArmyTypeId(key);
            string armyType = element.Attribute("army-type");
            string unitType = element.Attribute("unit-type");
            if (string.IsNullOrWhiteSpace(armyType) ||
                !armyTypeUnitTypes.TryGetValue(armyType, out string declaredUnitType) ||
                !string.Equals(unitType, declaredUnitType, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"armydef.xml: army '{key}' has invalid unit-type/army-type classification");
            var armyDef = new ArmyDef
            {
                name = key,
                unitType = declaredUnitType,
                id = id,
                armyType = armyType,
                imageName = element.Attribute("image") ?? key,
                imageCountryIndependent = true,
                hasImageCountryIndependent = true,
            };

            SetLegacyCombatRules(armyDef, useLegacyCombatDefaults);
            if (ParseBoolAttribute(element, "navy", out bool explicitNavy))
            {
                armyDef.isNavy = explicitNavy;
                armyDef.hasExplicitNavy = true;
            }
            armyDef.weaponType = ParseWeaponTypeMask(element.Attribute("weapon-type"));
            if (XmlValueParser.ParseUInt8Attribute(element, "armor-type", out byte armorType))
                armyDef.armorType = armorType;
            if (element.QueryIntAttribute("strength", out armyDef.hp) != TIXML_SUCCESS) armyDef.hp = 0;
            if (element.QueryIntAttribute("movement", out armyDef.movement) != TIXML_SUCCESS) armyDef.movement = 0;
            if (element.QueryIntAttribute("minatk", out armyDef.minAttack) != TIXML_SUCCESS) armyDef.minAttack = 0;
            if (element.QueryIntAttribute("maxatk", out armyDef.maxAttack) != TIXML_SUCCESS) armyDef.maxAttack = 0;
            ParseCombatRules(element.FirstChildElement("combat"), armyDef);

            if (!armyDefsById.TryAdd(id, armyDef))
                throw new InvalidDataException($"armydef.xml: duplicate army id '{id}'");
        }
    }
}
