namespace WC2;

/// <summary>Card category tab bar of the buy card page (GUICardTab.cpp).</summary>
public sealed class GUICardTab : GUIElement
{
    /// <summary>
    /// Fixed display order of the five tabs: land, navy, air force, development, strategy.
    /// GUICardList creates its cards in this order and GUIBuyCard finds them back by serial.
    /// </summary>
    public static readonly List<CARD_ID>[] AllCardTab =
    {
        new()
        {
            INFANTRY_CARD,
            ARMOUR_CARD,
            ARTILLERY_CARD,
            ROCKET_CARD,
            TANK_CARD,
            HEAVY_TANK_CARD,
            ELITE_INFANTRY_CARD,
        },
        new()
        {
            DESTROYER_CARD,
            CRUISER_CARD,
            BATTLE_SHIP_CARD,
            AIRCRAFT_CARRIER_CARD,
        },
        new()
        {
            AIR_STRIKE_CARD,
            BOMBER_CARD,
            AIRBORNE_FORCE_CARD,
            NUCLEAR_BOMB_CARD,
        },
        new()
        {
            CITY_CARD,
            INDUSTRY_CARD,
            AIRPORT_CARD,
            LAND_FORT_CARD,
            ENTRENCHMENT_CARD,
            ANTIAIRCRAFT_CARD,
            RADAR_CARD,
        },
        new()
        {
            RESEARCH_CARD,
            CARRIER_CARD,
            ASSAULT_ART_CARD,
            DEFEND_ART_CARD,
            COMMANDER_CARD,
            SUPPLY_LINE_CARD,
            ACE_FORCES_CARD,
        },
    };

    public GUIRadioButton[] buttonLabel = new GUIRadioButton[5];
    public GUIImage imageBoardPaper;
    public int selectedTab;

    public GUICardTab() { }

    /// <summary>Initializes the five category labels and the paper background of the bar.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        string[] labelImages =
        {
            "label_groundforces.png",
            "label_navy.png",
            "label_airforces.png",
            "label_development.png",
            "label_strategy.png",
        };

        bool wideLayout = ecGraphics.Instance().widthMode == 3;
        float firstLabelX = wideLayout ? 75.0f : 27.0f;
        float labelY = wideLayout ? 32.0f : -8.0f;

        for (int index = 0; index < 5; ++index)
        {
            buttonLabel[index] = new GUIRadioButton();
            var labelRect = new GUIRect(firstLabelX + index * 66.0f, labelY, 60.0f, 233.0f);
            buttonLabel[index].Init(labelImages[index], labelImages[index], labelRect, null);
            AddChild(buttonLabel[index], true);
        }

        string boardImageName;
        ecTextureRect boardTextureRect;
        var boardRect = new GUIRect(0.0f, 0.0f, 1.0f, 1.0f);

        if (wideLayout)
        {
            boardImageName = "board_paper_iPad.webp";
            boardTextureRect = new ecTextureRect(0.0f, 0.0f, 1013.0f, 206.0f, 0.0f, 0.0f);
            boardRect.x = 5.0f;
            boardRect.y = 17.0f;
        }
        else if (ecGraphics.Instance().screenW <= 480)
        {
            boardImageName = "board_paper.webp";
            boardTextureRect = new ecTextureRect(0.0f, 0.0f, 461.0f, 191.0f, 0.0f, 0.0f);
            boardRect.x = 12.0f;
            boardRect.y = 0.0f;
        }
        else if (ecGraphics.Instance().screenW <= 568)
        {
            boardImageName = "board_paper-568h.webp";
            boardTextureRect = new ecTextureRect(0.0f, 0.0f, 568.0f, 201.0f, 0.0f, 0.0f);
            boardRect.x = 0.0f;
            boardRect.y = 0.0f;
        }
        else
        {
            boardImageName = "board_paper-640h.webp";
            boardTextureRect = new ecTextureRect(0.0f, 0.0f, 640.0f, 201.0f, 0.0f, 0.0f);
            boardRect.x = 0.0f;
            boardRect.y = 0.0f;
        }

        imageBoardPaper = GUIManager.Instance().AddImage(boardImageName, boardTextureRect, boardRect, this, false);
        MoveToFront(imageBoardPaper);

        selectedTab = -1;
        SelectTab(0);
    }

    /// <summary>Label button events switch the current tab.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.GUI && evt.info.gui.type == Event.Info.GUIInfo.BUTTON)
        {
            for (int index = 0; index < 5; ++index)
            {
                if (ReferenceEquals(evt.info.gui.ptr, buttonLabel[index]))
                {
                    SelectTab(index);
                    break;
                }
            }
        }

        return base.OnEvent(evt);
    }

    /// <summary>Index of the label button among the five categories, or -1.</summary>
    public int GetCardTabButtonIdx(GUIElement button)
    {
        for (int index = 0; index < 5; ++index)
        {
            if (ReferenceEquals(button, buttonLabel[index]))
                return index;
        }
        return -1;
    }

    /// <summary>Switches the selected state and the layer order of the tab.</summary>
    public void SelectTab(int index)
    {
        if (selectedTab == index) return;

        float x;
        float y;
        if (selectedTab >= 0)
        {
            GUIRadioButton previousButton = buttonLabel[selectedTab];
            previousButton.selected = false;
            previousButton.GetPos(out x, out y);
            previousButton.SetPos(x, y + 12.0f);
        }

        selectedTab = index;
        GUIRadioButton selectedButton = buttonLabel[index];
        selectedButton.selected = true;
        selectedButton.GetPos(out x, out y);
        selectedButton.SetPos(x, y - 12.0f);

        MoveToFront(imageBoardPaper);
        MoveToFront(selectedButton);
    }
}
