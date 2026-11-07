namespace WC2.Tests;

/// <summary>
/// The map camera: the zoom stops where the map still fills the screen (no black space around it),
/// and a wrapping map (conquests.json leftRightWrap) scrolls past its left and right edge.
/// </summary>
public class CameraTests
{
    // the phone's logical screen is 568 x 320
    private static CCamera Camera(float mapW, float mapH, bool wrap)
    {
        var c = new CCamera();
        c.mapSize[0] = mapW; c.mapSize[1] = mapH;
        c.screenHalfSize[0] = 284; c.screenHalfSize[1] = 160;
        c.scale = 1; c.wrapX = wrap;
        c.centerPos[0] = mapW / 2; c.centerPos[1] = mapH / 2;
        return c;
    }

    [Fact]
    public void ZoomingOutStopsWhenTheMapFillsTheScreen()
    {
        // a wide map (the world map: 9000 x 2700 units): the height decides; the original stopped
        // when the whole width fitted, leaving black bars above and below
        var wide = Camera(9000, 2700, false);
        Assert.Equal(320f / 2700f, wide.MinScale(), 4);
        wide.SetPosAndScale(4500, 1350, 0.01f);
        Assert.Equal(320f / 2700f, wide.scale, 4);
        // a narrow map: the width decides
        Assert.Equal(568f / 1500f, Camera(1500, 3000, false).MinScale(), 4);
        // a wrapping map only has to fill the height
        Assert.Equal(320f / 2700f, Camera(1500, 2700, true).MinScale(), 4);
        // never above 1 (a map smaller than the screen)
        Assert.Equal(1f, Camera(300, 200, false).MinScale());
    }

    [Fact]
    public void AWrappingMapScrollsPastItsEdges()
    {
        var c = Camera(9000, 2700, true);
        c.SetPos(-100, 1000, true);
        Assert.Equal(8900f, c.centerPos[0], 1);            // over the left edge: the right end of the map
        c.SetPos(9100, 1000, true);
        Assert.Equal(100f, c.centerPos[0], 1);
        Assert.Equal(200f, c.WrapDelta(100 - 8900), 1);    // the short way over the seam
        Assert.Equal(-200f, c.WrapDelta(8900 - 100), 1);
        c.SetPos(500, -50, true);
        Assert.Equal(160f, c.centerPos[1], 1);             // the height is still clamped

        var plain = Camera(9000, 2700, false);
        plain.SetPos(-100, 1000, true);
        Assert.Equal(284f, plain.centerPos[0], 1);         // a map without wrapping stops at its edge
        Assert.Equal(-8800f, plain.WrapDelta(100 - 8900), 1);
    }

    [Fact]
    public void TheCameraFliesToTheNearestCopy()
    {
        var c = Camera(9000, 2700, true);
        c.SetPos(8900, 1000, true);
        c.MoveTo(100, 1000, true);                          // 200 units to the right over the seam, not 8800 to the left
        Assert.True(c.speed[0] > 0);
        Assert.Equal(9100f, c.targetPos[0], 1);
    }
}
