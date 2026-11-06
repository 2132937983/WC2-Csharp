namespace WC2;

/// <summary>Animation and rendering of an area (CAreaPresentation.cpp).</summary>
public sealed partial class CArea
{
    /// <summary>
    /// Advances the drafting, move-in and move-to-front animations and commits their state when done.
    /// </summary>
    /// <param name="time">seconds since the last frame</param>
    public void Update(float time)
    {
        if (armyDrafting != null)
        {
            float nextOffset = draftingArmyOffset + time * 320.0f;
            if (nextOffset > 0.0f)
            {
                draftingArmyOffset = 0.0f;
                AddArmy(armyDrafting, false);
                armyDrafting = null;
                g_Scene.AdjacentAreasEncirclement(id);
            }
            else
            {
                draftingArmyOffset = nextOffset;
            }
        }

        if (armyMovingIn != null)
        {
            CArmy movingArmy = armyMovingIn;
            float stepX = time * movingInArmySpeed[0];
            float stepY = time * movingInArmySpeed[1];

            movingInArmyOffset[0] += stepX;
            movingInArmyOffset[1] += stepY;

            if (MathF.Abs(movingInArmyOffset[0]) <= MathF.Abs(stepX))
            {
                movingInArmyOffset[0] = 0.0f;
                movingInArmySpeed[0] = 0.0f;
            }
            if (MathF.Abs(movingInArmyOffset[1]) <= MathF.Abs(stepY))
            {
                movingInArmyOffset[1] = 0.0f;
                movingInArmySpeed[1] = 0.0f;
            }

            if (movingInArmyOffset[0] == 0.0f && movingInArmyOffset[1] == 0.0f)
            {
                AddArmy(movingArmy, false);
                armyMovingIn = null;

                if (movingInArmyOccupy)
                {
                    g_SoundRes.PlayCharSE(OCCUPY_WAV);
                    movingInArmyOccupy = false;
                }

                if (movingInArmyComplain)
                {
                    if (complainCommanderName != null)
                    {
                        string dialogueKey = Sprintf("commander complain %ld", lrand48() % 2 + 1);
                        var gameState = (CGameState)CStateManager.Instance().GetStatePtr(GAME);
                        gameState.ShowDialogue(dialogueKey, complainCommanderName, false);
                    }
                    movingInArmyComplain = false;
                }

                if (armyCount == 1)
                {
                    g_Scene.AdjacentAreasEncirclement(id);
                }
            }
        }

        if (armyMovingToFront != null)
        {
            float nextAngle = movingArmyToFrontAngle + time * 12.0f;
            if (nextAngle >= 5.96902597f)
            {
                movingArmyToFrontAngle = 5.969f;
                AddArmy(armyMovingToFront, false);
                armyMovingToFront = null;
                g_Scene.ResetTarget();
            }
            else
            {
                movingArmyToFrontAngle = nextAngle;
            }
        }
    }

    /// <summary>Renders one unit of the area with its hp, movement, cards and commander medal.</summary>
    /// <param name="x">map x of the unit</param>
    /// <param name="y">map y of the unit</param>
    /// <param name="armyCountIn">stack depth to draw</param>
    /// <param name="armyIn">the unit</param>
    public void RenderArmy(float x, float y, int armyCountIn, CArmy armyIn)
    {
        if (armyIn == null || country == null || armyIn.basicAbilities == null) return;

        uint color;
        if (armyIn.movement > 0)
        {
            if (armyIn.morale == (int)MORALE_UPSET) color = 0xFFFF4040U;
            else if (armyIn.morale == (int)MORALE_HIGH) color = 0xFF4040FFU;
            else color = 0xFFFFFFFFU;
        }
        else
        {
            if (armyIn.morale == (int)MORALE_UPSET) color = 0xFFC04040U;
            else if (armyIn.morale == (int)MORALE_HIGH) color = 0xFF4040C0U;
            else color = 0xFFC0C0C0U;
        }

        bool isNavy = armyIn.IsNavy();
        g_GameRes.RenderArmyWithNavy(
            country.name,
            country.alliance,
            armyCountIn,
            x,
            y,
            armyIn.basicAbilities.id,
            color,
            info.sea,
            isNavy,
            armyIn.direction);

        if (isNavy) y += 8.0f;
        if (info.sea && (armyIn.cards & (1 << (int)CARRIER)) != 0) y += 4.0f;

        g_GameRes.RenderArmyInfo(
            armyCountIn,
            x,
            y,
            armyIn.hp,
            armyIn.GetMaxStrength(),
            armyIn.movement,
            armyIn.cards,
            armyIn.level);

        if ((armyIn.cards & (1 << (int)COMMANDER)) != 0)
        {
            if (country.ai)
            {
                g_GameRes.RenderAICommanderMedal(armyCountIn, x, y, country.name, country.alliance);
            }
            else
            {
                g_GameRes.RenderCommanderMedal(armyCountIn, x, y, country.GetCommanderLevel());
            }
        }
    }

    /// <summary>Renders the port, the construction and the installation of the area.</summary>
    public void RenderBuilding()
    {
        float constructionX = (float)info.constructionPos[0];
        float constructionY = (float)info.constructionPos[1];

        if (info.enable && info.areaType == PORT)
        {
            g_GameRes.RenderPort(constructionX, constructionY);
        }

        if (country != null)
        {
            g_GameRes.RenderConstruction(
                (int)info.constructionType,
                info.constructionLevel,
                constructionX,
                constructionY);
            g_GameRes.RenderInstallation(
                (int)installationType,
                (float)info.installationPos[0],
                (float)info.installationPos[1]);
        }
    }

    /// <summary>
    /// Renders the flag, the stationed units and the animating units of the area.
    /// The flag is drawn before or after the units depending on its y so the depth order stays right.
    /// </summary>
    public void Render()
    {
        if (country == null) return;

        float armyX = (float)info.armyPos[0];
        float armyY = (float)info.armyPos[1];
        float flagX = (float)info.constructionPos[0];
        float flagY = (float)info.constructionPos[1];

        if (info.constructionPos[1] <= info.armyPos[1])
        {
            g_GameRes.RenderFlag(country.name, flagX, flagY);
        }

        if (armyCount > 0)
        {
            RenderArmy(armyX, armyY, armyCount, army[0]);
        }

        if (armyDrafting != null)
        {
            RenderArmy(armyX, armyY + (int)draftingArmyOffset, 1, armyDrafting);
        }

        if (armyMovingIn != null)
        {
            RenderArmy(armyX + movingInArmyOffset[0], armyY + movingInArmyOffset[1], 1, armyMovingIn);
        }

        if (armyMovingToFront != null)
        {
            float angle = movingArmyToFrontAngle;
            float xOffset = MathF.Cos(angle) * 12.0f - 12.0f;
            float yRadius = angle > 3.14159265f ? 24.0f : 12.0f;
            float yOffset = MathF.Sin(angle) * yRadius;
            RenderArmy(armyX + xOffset, armyY + yOffset, 1, armyMovingToFront);
        }

        if (info.constructionPos[1] > info.armyPos[1])
        {
            g_GameRes.RenderFlag(country.name, flagX, flagY);
        }
    }
}
