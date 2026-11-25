namespace WC2;

/// <summary>Storage and lookup of the army definitions (ArmyDefStore.cpp).</summary>
public sealed partial class CObjectDef
{
    /// <summary>Releases the global flat army definition table.</summary>
    public void ReleaseArmyDef()
    {
        armyDefsById.Clear();
        unitTypes.Clear();
        armyTypeUnitTypes.Clear();
    }

    /// <summary>The global ArmyDef identified by its flat id.</summary>
    public ArmyDef GetArmyDef(int armyType) => armyDefsById.GetValueOrDefault(armyType);
}
