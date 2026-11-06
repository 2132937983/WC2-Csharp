namespace WC2;

/// <summary>Confirmation box shown before an attack is launched (GUIAttackBox.cpp).</summary>
public sealed class GUIAttackBox : GUIElement
{
    public GUIButtonEx buttonOk;
    public GUIButton buttonCancel;
    public ecImage imageAttackBoard;
    public ecImage imageStartCountry;
    public ecImage imageTargetCountry;
    public int startAreaId;
    public int targetAreaId;

    public GUIAttackBox() { }

    /// <summary>Releases the images held directly; the two buttons belong to the child tree.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageAttackBoard = null;
        imageStartCountry = null;
        imageTargetCountry = null;
        base.Dispose();
    }

    /// <summary>Initializes the board, the two flag placeholders and the OK / cancel buttons.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        imageAttackBoard = new ecImage(s_TextureRes.GetImage("attack_board.png"));
        imageStartCountry = new ecImage(g_GameRes.GetFlagImage("flag_de.png"));
        imageTargetCountry = new ecImage(g_GameRes.GetFlagImage("flag_de.png"));

        var okButton = new GUIButtonEx();
        buttonOk = okButton;
        var buttonRect = new GUIRect(179.0f, 143.0f, 64.0f, 32.0f);
        okButton.Init("green_normal.png", "green_normal.png", buttonRect, null);
        okButton.SetImageText("buttontext_ok.png", "buttontext_ok_press.png");
        if (Layout.Tablet)
        {
            okButton.imagePos[0] = 11.0f;
            okButton.imagePos[1] = -5.0f;
        }
        else
        {
            okButton.imagePos[0] = 13.0f;
            okButton.imagePos[1] = 0.0f;
        }
        AddChild(okButton, true);

        buttonRect = new GUIRect(13.0f, 143.0f, 64.0f, 32.0f);
        buttonCancel = GUIManager.Instance().AddButton("buttontext_cancel.png", "buttontext_cancel_press.png", buttonRect, this, null);
        buttonCancel.SetBackground("gray_normal.png");
    }

    /// <summary>Button, touch and back key handling of the attack box.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            // the children (buttons) see the touch first, then the box swallows it (modal)
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.BACK_PRESSED && (flags & SHOWN) != 0)
        {
            OnEvent(Event.Button(buttonCancel));
            return true;
        }

        if (evt.type == Event.GUI &&
            evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
            !ReferenceEquals(evt.info.gui.ptr, buttonOk) &&
            ReferenceEquals(evt.info.gui.ptr, buttonCancel))
        {
            Hide();
        }

        return base.OnEvent(evt);
    }

    /// <summary>Draws the box, the flags of both sides and the info of the leading units.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absRect);

        imageAttackBoard.Render(absRect.x, absRect.y);
        imageStartCountry.Render(absRect.x + 37.0f, absRect.y + 50.0f);

        float armyY = absRect.y + 120.0f;
        CArea startArea = g_Scene.GetArea(startAreaId);
        CArmy startArmy = startArea?.GetArmy(0);
        if (startArmy != null)
        {
            int startLevel = startArmy.level;
            if ((startArmy.cards & (1 << (int)ARMY_CARD.COMMANDER)) != 0)
                startLevel = startArmy.country.GetCommanderLevel();

            CCountry startCountry = startArea.country;
            g_GameRes.RenderUIAttackArmy(
                startCountry.name,
                absRect.x + 60.0f,
                armyY,
                startArmy.basicAbilities.id,
                startArmy.hp,
                startArmy.GetMaxStrength(),
                startArmy.movement,
                startArmy.cards,
                startLevel,
                startCountry.alliance,
                startCountry.ai);
        }

        imageTargetCountry.Render(absRect.x + 175.0f, absRect.y + 50.0f);

        CArea targetArea = g_Scene.GetArea(targetAreaId);
        CArmy targetArmy = targetArea?.GetArmy(0);
        if (targetArmy != null)
        {
            int targetLevel = targetArmy.level;
            if ((targetArmy.cards & (1 << (int)ARMY_CARD.COMMANDER)) != 0)
                targetLevel = targetArmy.country.GetCommanderLevel();

            CCountry targetCountry = targetArea.country;
            g_GameRes.RenderUIDefendArmy(
                targetCountry.name,
                absRect.x + 208.0f,
                armyY,
                targetArmy.basicAbilities.id,
                targetArmy.hp,
                targetArmy.GetMaxStrength(),
                targetArmy.movement,
                targetArmy.cards,
                targetLevel,
                targetCountry.alliance,
                targetCountry.ai);
        }
    }

    /// <summary>Sets the start and target of the attack and refreshes both flags.</summary>
    public void SetAttack(int startAreaIdIn, int targetAreaIdIn)
    {
        startAreaId = startAreaIdIn;
        targetAreaId = targetAreaIdIn;

        CArea startArea = g_Scene.GetArea(startAreaIdIn);
        CArea targetArea = g_Scene.GetArea(targetAreaIdIn);

        string flagName = Sprintf("flag_%s.png", startArea.country.name);
        imageStartCountry.Init(g_GameRes.GetFlagImage(flagName));

        flagName = Sprintf("flag_%s.png", targetArea.country.name);
        imageTargetCountry.Init(g_GameRes.GetFlagImage(flagName));
    }
}
