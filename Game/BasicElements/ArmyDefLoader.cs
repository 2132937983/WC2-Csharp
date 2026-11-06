using static WC2.armydef_internal;

namespace WC2;

/// <summary>Loads armydef.xml (ArmyDefLoader.cpp).</summary>
public sealed partial class CObjectDef
{
    /// <summary>
    /// Reads the army definitions from armydef.xml. Several parts of this code are for future use.
    /// </summary>
    public void LoadArmyDef()
    {
        // rebuild the name/id table on every reload so no stale dynamic ids survive a hot reload
        ResetArmyTypeRegistry();
        var Xml = new TiXmlDocument(GetPath("armydef.xml", null));
        // xml loaded
        if (Xml.LoadFile(TIXML_ENCODING_UNKNOWN))
        {
            // root element
            TiXmlElement pXmlElement = Xml.FirstChildElement("armies");
            if (pXmlElement != null)
            {
                bool useLegacyCombatDefaults = true;
                if (ParseBoolAttribute(pXmlElement, "legacy-combat-defaults", out bool legacyCombatDefaults))
                    useLegacyCombatDefaults = legacyCombatDefaults;
                weaponArmorDamageRates.Clear();
                ParseWeaponArmorRules(pXmlElement.FirstChildElement("weapon-armor-rules"), weaponArmorDamageRates);

                // optional explicit mapping, e.g.
                // <army-types><type name="super_tank" id="1000"/></army-types>
                TiXmlElement armyTypesElement = pXmlElement.FirstChildElement("army-types");
                if (armyTypesElement != null)
                {
                    for (TiXmlElement typeElement = armyTypesElement.FirstChildElement("type");
                         typeElement != null;
                         typeElement = typeElement.NextSiblingElement("type"))
                    {
                        string typeName = typeElement.Attribute("name");
                        if (typeName != null &&
                            typeElement.QueryIntAttribute("id", out int typeId) == TIXML_SUCCESS)
                        {
                            bool countryIndependent = false;
                            if (ParseBoolAttribute(typeElement, "country-independent", out bool countryIndependentValue))
                                countryIndependent = countryIndependentValue;
                            RegisterArmyType(typeName, typeId, typeElement.Attribute("image"), countryIndependent);
                        }
                    }
                }

                for (TiXmlElement listElement = pXmlElement.FirstChildElement();
                     listElement != null; listElement = listElement.NextSiblingElement())
                {
                    string CountryName = listElement.Attribute("name");
                    if (CountryName != null)
                    {
                        var tmp_list_here = new ArmyDefList();
                        for (TiXmlElement ArmyListElement = listElement.FirstChildElement();
                             ArmyListElement != null;
                             ArmyListElement = ArmyListElement.NextSiblingElement())
                        {
                            string ArmyTypeStr = ArmyListElement.Attribute("type");
                            if (ArmyTypeStr != null)
                            {
                                var tmp_army_def = new ArmyDef();
                                tmp_army_def.name = ArmyTypeStr;
                                tmp_army_def.id = ResolveArmyTypeId(ArmyTypeStr);
                                // a country army may force its own unit image base name;
                                // otherwise CGameRes falls back to the global army-types mapping
                                string imageName = ArmyListElement.Attribute("image");
                                if (!string.IsNullOrEmpty(imageName))
                                {
                                    tmp_army_def.imageName = imageName;
                                }
                                tmp_army_def.imageCountryIndependent = false;
                                tmp_army_def.hasImageCountryIndependent =
                                    ParseBoolAttribute(ArmyListElement, "country-independent", out bool imageCountryIndependent);
                                if (tmp_army_def.hasImageCountryIndependent)
                                    tmp_army_def.imageCountryIndependent = imageCountryIndependent;
                                byte unitType = ParseCombatUnitType(
                                    ArmyListElement.Attribute("unit-type"), ARMY_UNIT_TYPE_UNSPECIFIED);
                                SetLegacyCombatRules(tmp_army_def, useLegacyCombatDefaults, unitType);
                                // the navy rendering flag is independent of the combat class;
                                // when absent the legacy fallback set above remains
                                if (ParseBoolAttribute(ArmyListElement, "navy", out bool explicitNavy))
                                {
                                    tmp_army_def.isNavy = explicitNavy;
                                    tmp_army_def.hasExplicitNavy = true;
                                }
                                tmp_army_def.armorType = 0;
                                tmp_army_def.weaponType = ParseWeaponTypeMask(ArmyListElement.Attribute("weapon-type"));
                                if (ParseUInt8Attribute(ArmyListElement, "armor-type", out byte armorType))
                                    tmp_army_def.armorType = armorType;
                                int hp, max_attack, movement, min_attack;
                                if (ArmyListElement.QueryIntAttribute("strength", out hp) != TIXML_SUCCESS) hp = 0;
                                if (ArmyListElement.QueryIntAttribute("movement", out movement) != TIXML_SUCCESS) movement = 0;
                                if (ArmyListElement.QueryIntAttribute("minatk", out min_attack) != TIXML_SUCCESS) min_attack = 0;
                                if (ArmyListElement.QueryIntAttribute("maxatk", out max_attack) != TIXML_SUCCESS) max_attack = 0;
                                tmp_army_def.hp = hp;
                                tmp_army_def.movement = movement;
                                tmp_army_def.maxAttack = max_attack;
                                tmp_army_def.minAttack = min_attack;
                                ParseCombatRules(ArmyListElement.FirstChildElement("combat"), tmp_army_def);
                                tmp_list_here.def[tmp_army_def.id] = tmp_army_def;
                            }
                        }
                        mapArmyDefList[CountryName] = tmp_list_here;
                    }
                }
            }
        }
    }
}
