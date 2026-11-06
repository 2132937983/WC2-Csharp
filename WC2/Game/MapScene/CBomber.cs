namespace WC2;

/// <summary>Plane / air strike animation (CBomber.cpp).</summary>
public sealed class CBomber
{
    public float[] planePos = new float[2];
    public ecImage[] planeImages = new ecImage[2];
    public ecImage x10Image;
    public int startAreaId;
    public int targetAreaId;
    public float flySpeed;
    public float planeXPosLimit;
    public float[] targetPos = new float[2];
    public float[] arrivedTargetPos = new float[2];
    public float effectTimer;
    public float effectSpeed;
    public bool bombing;
    public bool bombOrAirborne;
    public bool effectComplete;
    public bool planeArrived;
    public bool isAirborne;
    public int actionType;
    public int airStrikeFireCount;
    // Air strikes launched by a unit no longer use the CFight type 4 marker.
    public bool unitAirstrike;
    public int airStrikePayload;

    /// <summary>Constructs the bomber animation controller and initialises its image references.</summary>
    public CBomber()
    {
        planeImages[0] = null;
        planeImages[1] = null;
        x10Image = null;
        unitAirstrike = false;
        airStrikePayload = (int)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE;
    }

    /// <summary>Destructor equivalent: releases the image objects held by the animation.</summary>
    public void Release()
    {
        planeImages[0] = null;
        planeImages[1] = null;
        x10Image = null;
    }

    /// <summary>Loads the plane images and sets up the initial animation state.</summary>
    public void Init()
    {
        ecImageAttr fighterImageAttr = g_GameRes.textureResArmy.GetImage("fighter.png");
        planeImages[0] = fighterImageAttr != null ? new ecImage(fighterImageAttr) : null;

        ecImageAttr bomberImageAttr = g_GameRes.textureResArmy.GetImage("bomber.png");
        planeImages[1] = bomberImageAttr != null ? new ecImage(bomberImageAttr) : null;

        startAreaId = -1;
        targetAreaId = -1;
        bombing = false;
        bombOrAirborne = false;
        planeArrived = false;
        airStrikeFireCount = 0;
        unitAirstrike = false;
        airStrikePayload = (int)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE;
    }

    /// <summary>
    /// Starts an air strike, bombing or nuclear bomb animation on an area.
    /// The plane start and exit limits take both the camera view and the target position into
    /// account so the plane keeps enough flight distance even when the target is at the screen edge.
    /// </summary>
    /// <param name="targetAreaId">Target area id.</param>
    /// <param name="actionType">Air strike action type.</param>
    public void BombArea(int targetAreaId, int actionType)
    {
        this.targetAreaId = targetAreaId;

        CArea targetArea = g_Scene.GetArea(targetAreaId);
        targetPos[0] = targetArea.info.armyPos[0];
        targetPos[1] = targetArea.info.armyPos[1];
        planePos[1] = targetPos[1];

        float visibleHalfWidth = (ecGraphics.Instance().screenW * 0.5f) / g_Scene.camera.scale;
        float viewLeftStart = g_Scene.camera.centerPos[0] - visibleHalfWidth - 100.0f;
        float targetStart = targetPos[0] - 400.0f;
        planePos[0] = viewLeftStart;
        if (planePos[0] > targetStart)
            planePos[0] = targetStart;

        flySpeed = 800.0f;
        this.actionType = actionType;
        unitAirstrike = false;
        airStrikePayload = actionType;
        bombing = true;
        bombOrAirborne = true;
        effectComplete = false;
        planeArrived = false;
        isAirborne = false;
        airStrikeFireCount = 0;

        float viewRightLimit = g_Scene.camera.centerPos[0] + visibleHalfWidth + 100.0f;
        float targetLimit = targetPos[0] + 400.0f;
        planeXPosLimit = viewRightLimit;
        if (planeXPosLimit < targetLimit)
            planeXPosLimit = targetLimit;
    }

    /// <summary>Starts the air strike animation launched by an aircraft carrier.</summary>
    public void AircraftCarrierBomb(int startAreaId, int targetAreaId)
    {
        AircraftCarrierBomb(startAreaId, targetAreaId, (int)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE);
    }

    /// <summary>Starts the air strike animation launched by a map unit with the given payload.</summary>
    /// <param name="startAreaId">Area of the unit launching the strike.</param>
    /// <param name="targetAreaId">Target area.</param>
    /// <param name="payloadType">Air strike payload type.</param>
    public void AircraftCarrierBomb(int startAreaId, int targetAreaId, int payloadType)
    {
        BombArea(targetAreaId, payloadType);
        this.startAreaId = startAreaId;
        unitAirstrike = true;
        airStrikePayload = payloadType;
    }

    /// <summary>
    /// Starts the airborne drop animation on an area. Airborne shares the flight path with bombing
    /// but plays the parachute effect on arrival and uses the airborne card instead of the strike damage.
    /// </summary>
    public void Airborne(int targetAreaId)
    {
        this.targetAreaId = targetAreaId;

        CArea targetArea = g_Scene.GetArea(targetAreaId);
        targetPos[0] = targetArea.info.armyPos[0];
        targetPos[1] = targetArea.info.armyPos[1];
        planePos[1] = targetPos[1];

        float visibleHalfWidth = (ecGraphics.Instance().screenW * 0.5f) / g_Scene.camera.scale;
        float viewLeftStart = g_Scene.camera.centerPos[0] - visibleHalfWidth - 100.0f;
        float targetStart = targetPos[0] - 400.0f;
        planePos[0] = viewLeftStart;
        if (planePos[0] > targetStart)
            planePos[0] = targetStart;

        flySpeed = 800.0f;
        bombing = true;
        bombOrAirborne = true;
        effectComplete = false;
        planeArrived = false;
        isAirborne = true;
        unitAirstrike = false;
        airStrikePayload = (int)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE;
        airStrikeFireCount = 0;

        float viewRightLimit = g_Scene.camera.centerPos[0] + visibleHalfWidth + 100.0f;
        float targetLimit = targetPos[0] + 400.0f;
        planeXPosLimit = viewRightLimit;
        if (planeXPosLimit < targetLimit)
            planeXPosLimit = targetLimit;
    }

    /// <summary>True while the plane is flying or has arrived and the hit effect is still pending.</summary>
    public bool IsBombing()
    {
        return bombing || planeArrived;
    }

    /// <summary>Draws the flying plane.</summary>
    public void Render()
    {
        if (!bombing)
            return;

        ecImage planeImage;
        int payloadType = unitAirstrike ? airStrikePayload : actionType;
        if (payloadType == (int)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE)
            planeImage = planeImages[0];
        else
            planeImage = planeImages[1];

        if (planeImage != null)
            planeImage.Render(planePos[0], planePos[1]);
    }

    /// <summary>
    /// Advances the strike/airborne animation and commits the card or combat result once the hit
    /// effect finished. Animation and rules are deliberately separate: only presentation changes
    /// before the plane reaches the target; UseCard / CFight.ApplyResult run after the effect timer,
    /// so the player never sees unit or area changes before the visual hit.
    /// </summary>
    public void Update(float deltaTimeS)
    {
        if (!bombing)
            return;
        // X position before the update, used to detect crossing the target this frame.
        float originPosX = planePos[0];
        planePos[0] += deltaTimeS * flySpeed;
        // The bombing state only ends when the plane left the screen after the effect was applied.
        if (planePos[0] > planeXPosLimit && effectComplete)
            bombing = false;
        if (!isAirborne)
        {
            // Normal and carrier air strikes spawn five machine gun bursts in a fixed window before the target.
            int payloadType = unitAirstrike ? airStrikePayload : actionType;
            if (payloadType == (int)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE)
            {
                if (airStrikeFireCount <= 4 && planePos[0] > (targetPos[0] - 150.0f + airStrikeFireCount * 50.0f))
                {
                    ecEffect newEffect = ecEffectManager.Instance().AddEffect("effect_airstrike.xml", true);
                    newEffect.FireAt(targetPos[0] - 50.0f + (float)airStrikeFireCount * 20.0f, planePos[1] + (float)(GetRand() % 10) - 5.0f, 0.0f);
                    if (airStrikeFireCount == 0)
                    {
                        // One burst plays the machine gun sound once; the later effects only keep the visuals continuous.
                        g_SoundRes.PlayCharSE(MACHINE_GUN_WAV);
                    }
                    airStrikeFireCount++;
                }
            }
        }
        if (!planeArrived)
        {
            if (originPosX < targetPos[0] && planePos[0] >= targetPos[0])
            {
                planeArrived = true;
                arrivedTargetPos[0] = targetPos[0];
                arrivedTargetPos[1] = planePos[1];
                // After arrival the effectTimer delays the hit effect; the plane keeps flying right.
                effectSpeed = 0.0f;
                effectTimer = 50.0f;
                if (isAirborne)
                {
                    // Airborne has no strafing: spawn the parachute on arrival.
                    ecEffectManager.Instance().AddEffect("effect_parachute.xml", true)
                        .FireAt(targetPos[0], planePos[1] - 60.0f, 0.0f);
                }
            }
            return;
        }
        // Airborne and bombing use different effect accelerations, i.e. different hit/landing delays.
        float effectAcceleration;
        if (isAirborne)
            effectAcceleration = 250.0f;
        else
            effectAcceleration = 1500.0f;
        // effectTimer decreases with a growing speed: the slow-then-fast delay curve of the original.
        effectSpeed += deltaTimeS * effectAcceleration;
        float remainingEffectTime = effectTimer - (effectSpeed * deltaTimeS);
        if (remainingEffectTime > 0.0f)
        {
            effectTimer = remainingEffectTime;
            return;
        }
        effectTimer = 0.0f;
        planeArrived = false;
        effectComplete = true;
        // Only after the visual effect query the acting country and convert the animation type to the rule action.
        CCountry currentCountry = g_GameManager.GetCurCountry();
        if (currentCountry != null)
        {
            CardDef cardDef;
            if (isAirborne)
            {
                // The airborne card itself drops the unit; no CFight strike damage.
                cardDef = CObjectDef.Instance().GetCardDef(AIRBORNE_FORCE_CARD);
                if (cardDef != null)
                    currentCountry.UseCard(cardDef, targetAreaId, 0);
                return;
            }
            // The three cards and the carrier strike all apply damage, building destruction and texts through a temporary CFight.
            var fight = new CFight();
            if (unitAirstrike)
            {
                // Unit attacks no longer mark their origin with the CFight type 4.
                fight.AirStrikesAttack(startAreaId, targetAreaId, airStrikePayload);
                fight.ApplyResult();
                return;
            }
            switch (actionType)
            {
                case 1:
                    // actionType 1/2/3 map to the air strike, bomber and nuclear bomb cards.
                    cardDef = CObjectDef.Instance().GetCardDef(AIR_STRIKE_CARD);
                    break;
                case 2:
                    cardDef = CObjectDef.Instance().GetCardDef(BOMBER_CARD);
                    break;
                case 3:
                    cardDef = CObjectDef.Instance().GetCardDef(NUCLEAR_BOMB_CARD);
                    break;
                default:
                    return;
            }
            currentCountry.UseCard(cardDef, targetAreaId, 0);
            fight.AirStrikesAttack(currentCountry, targetAreaId, actionType);
            fight.ApplyResult();
        }
    }
}
