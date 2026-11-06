namespace WC2;

/// <summary>Rectangle in logical units (x, y relative to the parent element).</summary>
public struct GUIRect
{
    public float x;
    public float y;
    public float w;
    public float h;

    public GUIRect(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }

    public bool Contains(float px, float py) => px >= x && px < x + w && py >= y && py < y + h;

    public override string ToString() => $"({x},{y},{w},{h})";
}

/// <summary>Receiver of GUI events (the current state).</summary>
public interface IEventReceiver
{
    bool OnEvent(Event evt);
}

/// <summary>
/// GUI / touch / back-key event. Mirrors the original union; both the gui and touch members
/// always exist so ported code can read either.
/// </summary>
public sealed class Event
{
    public enum EVENT_TYPE { GUI, TOUCH, BACK_PRESSED }

    public const EVENT_TYPE GUI = EVENT_TYPE.GUI;
    public const EVENT_TYPE TOUCH = EVENT_TYPE.TOUCH;
    public const EVENT_TYPE BACK_PRESSED = EVENT_TYPE.BACK_PRESSED;

    public EVENT_TYPE type;
    public readonly Info info = new Info();

    public sealed class Info
    {
        public readonly GUIInfo gui = new GUIInfo();
        public readonly TouchInfo touch = new TouchInfo();

        public sealed class GUIInfo
        {
            public enum GUI_TYPE { BUTTON, SCROLL_BAR, WARNING, FADE_IN, FADE_OUT, OTHER }

            public const GUI_TYPE BUTTON = GUI_TYPE.BUTTON;
            public const GUI_TYPE SCROLL_BAR = GUI_TYPE.SCROLL_BAR;
            public const GUI_TYPE WARNING = GUI_TYPE.WARNING;
            public const GUI_TYPE FADE_IN = GUI_TYPE.FADE_IN;
            public const GUI_TYPE FADE_OUT = GUI_TYPE.FADE_OUT;
            public const GUI_TYPE OTHER = GUI_TYPE.OTHER;

            public GUI_TYPE type;
            public GUIElement ptr;
            public int info;
        }

        public sealed class TouchInfo
        {
            public enum TOUCH_TYPE { BEGIN, MOVE, END }

            public const TOUCH_TYPE BEGIN = TOUCH_TYPE.BEGIN;
            public const TOUCH_TYPE MOVE = TOUCH_TYPE.MOVE;
            public const TOUCH_TYPE END = TOUCH_TYPE.END;

            public TOUCH_TYPE type;
            public readonly float[] pos = new float[2];
            public int index;
        }
    }

    public Event() { }

    public static Event Gui(Info.GUIInfo.GUI_TYPE guiType, GUIElement ptr, int info = 0)
    {
        var e = new Event { type = GUI };
        e.info.gui.type = guiType;
        e.info.gui.ptr = ptr;
        e.info.gui.info = info;
        return e;
    }

    public static Event Button(GUIElement button) => Gui(Info.GUIInfo.BUTTON, button, 0);

    public static Event Touch(Info.TouchInfo.TOUCH_TYPE touchType, float x, float y, int index)
    {
        var e = new Event { type = TOUCH };
        e.info.touch.type = touchType;
        e.info.touch.pos[0] = x;
        e.info.touch.pos[1] = y;
        e.info.touch.index = index;
        return e;
    }

    public static Event Back() => new Event { type = BACK_PRESSED };

    public override string ToString()
        => type == GUI ? $"GUI {info.gui.type} info={info.gui.info} ptr={info.gui.ptr?.GetType().Name}"
         : type == TOUCH ? $"TOUCH {info.touch.type} ({info.touch.pos[0]},{info.touch.pos[1]}) #{info.touch.index}"
         : "BACK_PRESSED";
}
