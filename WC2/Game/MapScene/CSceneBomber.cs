namespace WC2;

/// <summary>Bomber forwarding (CSceneBomber.cpp).</summary>
public sealed partial class CScene
{
    /// <summary>Forwards an airborne command to the bomber.</summary>
    public void Airborne(int targetAreaId)
    {
        if (bomber != null)
            bomber.Airborne(targetAreaId);
    }

    /// <summary>Forwards an aircraft carrier strike to the bomber.</summary>
    public void AircraftCarrierBomb(int startAreaId, int targetAreaId)
    {
        AircraftCarrierBomb(startAreaId, targetAreaId, (int)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE);
    }

    /// <summary>Forwards an air strike launched by a map unit to the bomber.</summary>
    /// <param name="startAreaId">Area of the unit launching the strike.</param>
    /// <param name="targetAreaId">Target area.</param>
    /// <param name="payloadType">Air strike payload type.</param>
    public void AircraftCarrierBomb(int startAreaId, int targetAreaId, int payloadType)
    {
        if (bomber != null)
            bomber.AircraftCarrierBomb(startAreaId, targetAreaId, payloadType);
    }

    /// <summary>Forwards an area bombing command to the bomber.</summary>
    /// <param name="targetAreaId">Target area.</param>
    /// <param name="actionType">Bombing action type used by CBomber to pick the flow.</param>
    public void BombArea(int targetAreaId, int actionType)
    {
        if (bomber != null)
            bomber.BombArea(targetAreaId, actionType);
    }

    /// <summary>True while the bomber is executing a mission.</summary>
    public bool IsBombing()
    {
        return bomber != null && bomber.IsBombing();
    }
}
