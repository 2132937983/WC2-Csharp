namespace WC2;

/// <summary>
/// Tutorial overlay. The original GUITutorails.cpp is empty (the tutorial lives only in the
/// binary library and is out of scope), so this is a passive full screen element that shows nothing.
/// </summary>
public sealed class GUITutorails : GUIElement
{
    public GUITutorails() { }

    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;
    }
}
