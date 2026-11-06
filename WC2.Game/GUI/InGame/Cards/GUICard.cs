namespace WC2;

/// <summary>One card of the buy card lists (GUICard.cpp).</summary>
public sealed class GUICard : GUIElement
{
    public CardDef cardDef;
    /// <summary>Shared with CGameRes; not released by the card.</summary>
    public ecImage imageCardShadow;
    /// <summary>Shared with CGameRes; not released by the card.</summary>
    public ecImage imageCardCommon;
    public readonly ecText textPrice = new();
    public readonly ecText textIndustry = new();
    public readonly ecText textAttack = new();
    public readonly ecText textHp = new();
    public readonly ecText textMovement = new();
    public ecImage imageCard;
    public ecImage imageMarkTime;
    public readonly ecText textCoolDown = new();
    public ecImage imageMarkTechRequest;
    public ecImage[] imageTechnology = new ecImage[5];
    public int iTouched;
    public int touchIndex;
    public bool touched;
    public bool showShadow;
    public float cardAlpha;
    public int coolDown;
    public int technology;
    public int researchLevel;
    public bool notGrey;

    public GUICard() { }

    /// <summary>Releases the card face, the state marks and the tech level images.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageCard = null;
        imageMarkTime = null;
        imageMarkTechRequest = null;
        for (int i = 0; i < imageTechnology.Length; i++) imageTechnology[i] = null;
        base.Dispose();
    }

    /// <summary>Color of the price text.</summary>
    public void SetPriceColor(uint color) => textPrice.SetColor(color);

    /// <summary>Price value shown on the card.</summary>
    public void SetPrice(int price) => textPrice.SetText(Sprintf("%d", price));

    /// <summary>Industry requirement shown on the card.</summary>
    public void SetIndustry(int industry) => textIndustry.SetText(Sprintf("%d", industry));

    /// <summary>Color of the industry text.</summary>
    public void SetIndustryColor(uint color) => textIndustry.SetColor(color);

    /// <summary>Press, leave and release handling; a valid tap sends a button event.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH)
            return base.OnEvent(evt);

        if ((flags & (ACTIVE | SHOWN)) != (ACTIVE | SHOWN)) return false;
        if ((flags & ENABLED) == 0) return false;

        switch (evt.info.touch.type)
        {
            case Event.Info.TouchInfo.BEGIN:
                if (CheckInRect(evt.info.touch.pos[0], evt.info.touch.pos[1]))
                {
                    touched = true;
                    iTouched = 1;
                    touchIndex = evt.info.touch.index;
                }
                break;

            case Event.Info.TouchInfo.MOVE:
                if (touched &&
                    touchIndex == evt.info.touch.index &&
                    !CheckInRect(evt.info.touch.pos[0], evt.info.touch.pos[1]))
                {
                    touched = false;
                    iTouched = 0;
                }
                break;

            case Event.Info.TouchInfo.END:
                if (touched && touchIndex == evt.info.touch.index)
                {
                    touched = false;
                    iTouched = 0;
                    CCSoundBox.GetInstance().PlaySE(AudioDefs.Ui("click"));
                    base.OnEvent(Event.Button(this));
                }
                break;
        }

        return base.OnEvent(evt);
    }

    /// <summary>
    /// Renders the card: half transparent shadow, available / unavailable tint, cool down and
    /// tech limits, price and (troop cards only) the attack, hp and movement summary.
    /// </summary>
    public override void OnRender()
    {
        GetAbsRect(out var tmp_rect);
        uint alphaBits = (uint)(cardAlpha * 255.0f) << 24;
        if (imageCardShadow != null && showShadow)
        {
            // the shadow alpha shares cardAlpha with the card fade
            imageCardShadow.SetColor(alphaBits + 0xFFFFFFu, -1);
            imageCardShadow.RenderEx(tmp_rect.x, tmp_rect.y, 0.0f, 2.0f, 0.0f);
        }
        uint tmp_uint;
        if (imageCard != null)
        {
            if (notGrey)
            {
                // an available card switches to the pressed tint while held; unavailable cards are grey
                if (iTouched != 0)
                    tmp_uint = alphaBits + 0x00D2D2D2u; // 13762770 + 53760
                else
                    tmp_uint = alphaBits + 0x00FFFFFFu; // 16711935 + 65280
            }
            else
            {
                tmp_uint = alphaBits + 0x00808080u; // 8421504
            }
            if (imageCardCommon != null)
            {
                imageCardCommon.SetColor(tmp_uint, -1);
                imageCardCommon.Render(tmp_rect.x, tmp_rect.y);
            }
            imageCard.SetColor(tmp_uint, -1);
            imageCard.Render(tmp_rect.x, tmp_rect.y + 7.0f);
            if (researchLevel > 0)
            {
                // the research card shows the current research level, not the required tech level
                if (researchLevel <= g_GameRes.imageCardResearch.Length && g_GameRes.imageCardResearch[researchLevel - 1] != null)
                {
                    g_GameRes.imageCardResearch[researchLevel - 1].SetColor(tmp_uint, -1);
                    g_GameRes.imageCardResearch[researchLevel - 1].Render(tmp_rect.x + 50.0f, tmp_rect.y + 46.0f);
                }
            }
            if (coolDown > 0)
            {
                // cool down and tech requirement can coexist; both marks are stacked in order
                imageMarkTime.Render(tmp_rect.x + 50.0f, tmp_rect.y + 75.0f);
                textCoolDown.SetText(Sprintf("%d", coolDown));
                textCoolDown.DrawText(tmp_rect.x + 48.0f, tmp_rect.y + 64.0f, 2);
            }
            if (technology > 1 && technology <= 5)
            {
                imageMarkTechRequest.Render(tmp_rect.x + 50.0f, tmp_rect.y + 75.0f);
                imageTechnology[technology - 1].Render(tmp_rect.x + 50.0f, tmp_rect.y + 65.0f);
            }
        }
        textPrice.DrawText(tmp_rect.x + 45.0f, tmp_rect.y + 130.0f, 1);
        textIndustry.DrawText(tmp_rect.x + 88.0f, tmp_rect.y + 130.0f, 1);
        if (cardDef.type <= NAVY_TYPE_CARD)
        {
            // only land / navy draft cards show the three basic values of the related ArmyDef
            if (notGrey)
            {
                textAttack.SetColor(0xFF4BA8E4u);
                textHp.SetColor(0xFF4BA8E4u);
                textMovement.SetColor(0xFF4BA8E4u);
            }
            else
            {
                textAttack.SetColor(0xFF255472u);
                textHp.SetColor(0xFF255472u);
                textMovement.SetColor(0xFF255472u);
            }
            textAttack.DrawText(tmp_rect.x + 26.0f, tmp_rect.y + 108.0f, 2);
            textHp.DrawText(tmp_rect.x + 61.0f, tmp_rect.y + 108.0f, 2);
            textMovement.DrawText(tmp_rect.x + 90.0f, tmp_rect.y + 108.0f, 2);
        }
    }

    /// <summary>
    /// Initializes the static resources and the default texts from the CardDef. Price, industry,
    /// cool down and tech availability are overridden per country by GUIBuyCard.ResetCardState.
    /// </summary>
    public void Init(CardDef card_in, in GUIRect rect_in)
    {
        cardDef = card_in;
        rect.x = rect_in.x;
        rect.y = rect_in.y;
        rect.w = rect_in.w;
        rect.h = rect_in.h;
        // the card face uses the resource name of the CardDef; shadow and common frame are shared by CGameRes
        imageCard = new ecImage(s_TextureRes.GetImage(card_in.image));
        imageCardShadow = g_GameRes.imageCardShadow;
        imageCardCommon = g_GameRes.imageCardCommon;
        imageMarkTime = new ecImage(s_TextureRes.GetImage("mark_time.png"));
        imageMarkTechRequest = new ecImage(s_TextureRes.GetImage("mark_techrequest.png"));
        // the five tech requirement icons are created up front; OnRender picks one by technology
        for (int i = 1; i <= 5; i++)
            imageTechnology[i - 1] = new ecImage(s_TextureRes.GetImage(Sprintf("technology_%d.png", i)));
        string tmp_this_char = Sprintf("%d", cardDef.price);
        textPrice.Init(g_Num3);
        textPrice.SetColor(0xFF0F2632u);
        textPrice.SetText(tmp_this_char);
        textPrice.fontSize[0] = -1.0f;
        textPrice.fontSize[1] = 0.0f;
        tmp_this_char = Sprintf("%d", cardDef.industry);
        textIndustry.Init(g_Num3);
        textIndustry.SetColor(0xFF0F2632u);
        textIndustry.SetText(tmp_this_char);
        textIndustry.fontSize[0] = -1.0f;
        textIndustry.fontSize[1] = 0.0f;
        if (cardDef.type <= NAVY_TYPE_CARD)
        {
            // the related unit values depend on the ArmyDef of the player country (units differ per country)
            textAttack.Init(g_Num3);
            textAttack.SetColor(0xFF4BA8E4u);
            textHp.Init(g_Num3);
            textHp.SetColor(0xFF4BA8E4u);
            textMovement.Init(g_Num3);
            textMovement.SetColor(0xFF4BA8E4u);
            if (g_GameManager.GetPlayerCountry() != null && !g_GameManager.GetPlayerCountry().ai)
            {
                ArmyDef tmp_army_def = CObjectDef.Instance().GetArmyDef(cardDef.relatedArmyId, g_GameManager.GetPlayerCountry().name);
                if (tmp_army_def != null)
                {
                    tmp_this_char = Sprintf("%d-%d", tmp_army_def.minAttack, tmp_army_def.maxAttack);
                    textAttack.fontSize[1] = 0.0f;
                    textAttack.fontSize[0] = tmp_this_char.Length > 3 ? -2.0f : -1.0f;
                    textAttack.SetText(tmp_this_char);
                    tmp_this_char = Sprintf("%d", tmp_army_def.hp / 10);

                    textHp.fontSize[1] = 0.0f;
                    textHp.fontSize[0] = tmp_this_char.Length > 3 ? -2.0f : -1.0f;
                    textHp.SetText(tmp_this_char);
                    tmp_this_char = Sprintf("%d", tmp_army_def.movement);
                    textMovement.SetText(tmp_this_char);
                }
            }
        }
        textCoolDown.Init(g_Num5);
        textCoolDown.SetColor(0xFFD1F2FFu);
        cardAlpha = 1.0f;
        technology = 1;
        iTouched = 0;
        touchIndex = 0;
        touched = false;
        showShadow = false;
        coolDown = 0;
        researchLevel = 0;
    }
}
