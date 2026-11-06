namespace WC2.Tests;

/// <summary>
/// Controls the original hides by moving them off the screen must not react there (letterbox bars,
/// wider phones). The scripted hit tests (hit_test*.txt) probe the real screens; these check the rule.
/// </summary>
[Collection("assets")]
public class HitTestTests
{
    private static void Screen(int w, int h)
    {
        var g = ecGraphics.Instance();
        g.screenW = w;
        g.screenH = h;
    }

    [Fact]
    public void PointsOffTheScreenNeverHit()
    {
        Screen(568, 320);
        var button = new GUIButton { rect = new GUIRect(568, 98, 107, 33) };   // a series button waiting right of the screen
        Assert.False(button.CheckInRect(600, 110));
        Assert.False(button.AcceptsInputAt(600, 110));
        Screen(640, 320);                                                        // the same button on a wider layout is on screen
        Assert.True(button.CheckInRect(600, 110));
    }

    [Fact]
    public void OnlyTheVisiblePartOfAnEdgeControlHits()
    {
        Screen(568, 320);
        var medal = new GUIButton { rect = new GUIRect(107, 295, 32, 27) };      // 2 px below the screen
        Assert.True(medal.CheckInRect(120, 300));
        Assert.False(medal.CheckInRect(120, 321));
    }

    [Fact]
    public void HiddenControlsRefuseInput()
    {
        Screen(568, 320);
        var panel = new GUIElement { rect = new GUIRect(0, 0, 568, 320) };
        var button = new GUIButton { rect = new GUIRect(10, 10, 50, 30) };
        panel.AddChild(button);
        Assert.True(button.AcceptsInputAt(20, 20));
        panel.Hide();
        Assert.False(button.AcceptsInputAt(20, 20));
        panel.Show();
        button.SetEnable(false);
        Assert.False(button.AcceptsInputAt(20, 20));
    }
}
