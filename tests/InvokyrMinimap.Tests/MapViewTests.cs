using InvokyrMinimap;
using Xunit;

public class MapViewTests
{
    private static readonly MapView View = new MapView(new Box(100, 100, 200, 200), 10f, 20f, 2f);

    [Fact]
    public void CenterMapsToMiddle()
    {
        Assert.Equal(200f, View.ScreenX(10f, 20f));
        Assert.Equal(200f, View.ScreenY(10f, 20f));
    }

    [Fact]
    public void NorthIsUpByDefault()
    {
        Assert.True(View.ScreenY(10f, 30f) < View.ScreenY(10f, 20f));
        Assert.True(View.ScreenX(15f, 20f) > View.ScreenX(10f, 20f));
    }

    [Fact]
    public void TurnedViewPutsTheLookDirectionUp()
    {
        // Looking east (+X): a point east of you is straight up, a point north is to your left.
        var view = new MapView(new Box(0, 0, 200, 200), 0f, 0f, 2f, 1f, 0f);
        Assert.Equal(100f, view.ScreenX(10f, 0f), 3);
        Assert.Equal(80f, view.ScreenY(10f, 0f), 3);
        Assert.Equal(80f, view.ScreenX(0f, 10f), 3);
        Assert.Equal(100f, view.ScreenY(0f, 10f), 3);
    }

    [Fact]
    public void DirectionFollowsTheTurn()
    {
        var view = new MapView(new Box(0, 0, 200, 200), 0f, 0f, 2f, 1f, 0f);
        view.Direction(1f, 0f, out var sx, out var sy);
        Assert.Equal(0f, sx, 3);
        Assert.Equal(-1f, sy, 3);
        View.Direction(1f, 0f, out sx, out sy); // north-up: east is right
        Assert.Equal(1f, sx, 3);
        Assert.Equal(0f, sy, 3);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(0.6f, 0.8f)]
    [InlineData(-1f, 0f)]
    public void FitShowsEveryCorner(float upX, float upZ)
    {
        var screen = new Box(0, 0, 400, 200);
        var view = MapView.Fit(screen, -50f, -10f, 50f, 10f, upX, upZ);
        foreach (var (x, z) in new[] { (-50f, -10f), (-50f, 10f), (50f, -10f), (50f, 10f) })
            Assert.True(screen.Contains(view.ScreenX(x, z), view.ScreenY(x, z)), $"{x},{z}");
    }

    [Fact]
    public void FitNorthUpKeepsMetresSquare()
    {
        var view = MapView.Fit(new Box(0, 0, 400, 200), -50f, -10f, 50f, 10f);
        var w = view.ScreenX(50f, 0f) - view.ScreenX(-50f, 0f);
        var h = view.ScreenY(0f, -10f) - view.ScreenY(0f, 10f);
        Assert.Equal(w / 100f, h / 20f, 3);
    }
}

public class LayoutTests
{
    [Fact]
    public void TopRightIsOffsetFromTheRightAndTop()
    {
        var b = Layout.Corner(MapCorner.TopRight, 260f, 24f, 120f, 1920f, 1080f);
        Assert.Equal(1920f - 260f - 24f, b.X);
        Assert.Equal(120f, b.Y);
    }

    [Fact]
    public void BottomLeftIsOffsetFromTheLeftAndBottom()
    {
        var b = Layout.Corner(MapCorner.BottomLeft, 260f, 24f, 120f, 1920f, 1080f);
        Assert.Equal(24f, b.X);
        Assert.Equal(1080f - 260f - 120f, b.Y);
    }

    [Fact]
    public void HugeOffsetsStayOnScreen()
    {
        var b = Layout.Corner(MapCorner.TopLeft, 260f, 5000f, 5000f, 1920f, 1080f);
        Assert.Equal(1920f - 260f, b.X);
        Assert.Equal(1080f - 260f, b.Y);
    }
}
