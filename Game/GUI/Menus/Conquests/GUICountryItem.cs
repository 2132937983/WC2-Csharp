namespace WC2;

/// <summary>One country entry of the conquest country list (gui/menus/conquests/GUICountryItem.cpp).</summary>
public sealed class GUICountryItem : GUIElement
{
    public string name = "";
    public string id = "";
    public int rankStar;
    public ecImage imageButton;
    public ecImage imageSmallRankstar;
    public bool touched;
    public bool selected;

    /// <summary>The original only clears the picture pointers; name, id, stars and state are set by Init.</summary>
    public GUICountryItem() { }

    /// <summary>Releases the button and star pictures of the entry.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageButton = null;
        imageSmallRankstar = null;
        base.Dispose();
    }

    // empty in the original; kept as a virtual slot
    public override void OnUpdate(float deltaTimeS) { }

    /// <summary>
    /// Initializes the name, the id and the pictures of the entry.
    /// </summary>
    /// <param name="nameIn">country name, also used for the button picture name</param>
    /// <param name="idIn">internal country id</param>
    /// <param name="rectIn">initial area inside the list parent</param>
    /// <param name="flagIn">the scenario's flag key (empty: the button of the name)</param>
    public void Init(string nameIn, string idIn, in GUIRect rectIn, string flagIn = "")
    {
        rect = rectIn;
        name = nameIn ?? "";
        id = idIn ?? "";

        // button_<name>.png of the menu atlas; a mod's new country or a scenario's flag brings its own (CountryArt)
        imageButton = new ecImage(CountryArt.Button(s_TextureRes, string.IsNullOrEmpty(flagIn) ? name : flagIn));

        var smallRankStar = new ecImage(s_TextureRes.GetImage("small_rankstar.png"));
        touched = false;
        selected = false;
        rankStar = 0;
        imageSmallRankstar = smallRankStar;
    }

    /// <summary>Draws the country button and its conquest stars (no text, no locked mark; normal/HD star layouts).</summary>
    public override void OnRender()
    {
        GetAbsRect(out GUIRect absRect);

        int color = touched ? -2960686 : -1;
        imageButton.SetColor(color, -1);
        if (selected)
        {
            imageButton.RenderEx(absRect.x, absRect.y - absRect.h * 0.075f, 0f, 1.15f, 0f);
        }
        else
        {
            imageButton.Render(absRect.x, absRect.y);
        }

        float x = absRect.x;
        float y = absRect.y;
        if (!Layout.Tablet)
        {
            switch (rankStar)
            {
                case 1:
                    imageSmallRankstar.Render(x + 22f, y + 33f);
                    break;
                case 2:
                    imageSmallRankstar.Render(x + 16f, y + 33f);
                    imageSmallRankstar.Render(x + 28f, y + 33f);
                    break;
                case 3:
                    imageSmallRankstar.Render(x + 10f, y + 33f);
                    imageSmallRankstar.Render(x + 22f, y + 33f);
                    imageSmallRankstar.Render(x + 34f, y + 33f);
                    break;
                case 4:
                    imageSmallRankstar.Render(x + 16f, y + 23f);
                    imageSmallRankstar.Render(x + 16f, y + 34f);
                    imageSmallRankstar.Render(x + 28f, y + 23f);
                    imageSmallRankstar.Render(x + 28f, y + 34f);
                    break;
                case 5:
                    imageSmallRankstar.Render(x + 10f, y + 23f);
                    imageSmallRankstar.Render(x + 22f, y + 23f);
                    imageSmallRankstar.Render(x + 34f, y + 23f);
                    imageSmallRankstar.Render(x + 16f, y + 34f);
                    imageSmallRankstar.Render(x + 28f, y + 34f);
                    break;
                default:
                    break;
            }
            return;
        }

        switch (rankStar)
        {
            case 1:
                imageSmallRankstar.Render(x + 44f, y + 66f);
                break;
            case 2:
                imageSmallRankstar.Render(x + 32f, y + 66f);
                imageSmallRankstar.Render(x + 56f, y + 66f);
                break;
            case 3:
                imageSmallRankstar.Render(x + 20f, y + 66f);
                imageSmallRankstar.Render(x + 44f, y + 66f);
                imageSmallRankstar.Render(x + 68f, y + 66f);
                break;
            case 4:
                imageSmallRankstar.Render(x + 32f, y + 46f);
                imageSmallRankstar.Render(x + 32f, y + 68f);
                imageSmallRankstar.Render(x + 56f, y + 46f);
                imageSmallRankstar.Render(x + 56f, y + 68f);
                break;
            case 5:
                imageSmallRankstar.Render(x + 20f, y + 46f);
                imageSmallRankstar.Render(x + 44f, y + 46f);
                imageSmallRankstar.Render(x + 68f, y + 46f);
                imageSmallRankstar.Render(x + 32f, y + 68f);
                imageSmallRankstar.Render(x + 56f, y + 68f);
                break;
            default:
                break;
        }
    }

    /// <summary>Turns a tap on the entry into a GUI BUTTON event for the parent.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH)
        {
            return base.OnEvent(evt);
        }

        uint currentFlags = flags;
        if ((currentFlags & (ACTIVE | SHOWN)) != (ACTIVE | SHOWN))
        {
            return false;
        }
        if ((currentFlags & ENABLED) == 0)
        {
            return false;
        }

        switch (evt.info.touch.type)
        {
            case Event.Info.TouchInfo.BEGIN:
                if (CheckInRect(evt.info.touch.pos[0], evt.info.touch.pos[1]))
                {
                    touched = true;
                }
                break;

            case Event.Info.TouchInfo.MOVE:
                if (touched &&
                    !CheckInRect(evt.info.touch.pos[0], evt.info.touch.pos[1]))
                {
                    touched = false;
                }
                break;

            case Event.Info.TouchInfo.END:
                if (touched)
                {
                    touched = false;
                    CCSoundBox.GetInstance().PlaySE(AudioDefs.Ui("click"));

                    base.OnEvent(Event.Button(this));
                }
                break;
        }

        return base.OnEvent(evt);
    }

    public void SetSelected(bool newSelected)
    {
        selected = newSelected;
    }
}
