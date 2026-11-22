using InvokyrMinimap;
using Xunit;

public class MapViewTests
{
    private static readonly MapView View = new MapView(new Box(100, 100, 200, 200), 10f, 20f, 2f);

    [Fact]
    public void CenterMapsToMiddle()
    {
        Assert.Equal(200f, View.ScreenX(10f));
        Assert.Equal(200f, View.ScreenY(20f));
    }

    [Fact]
    public void NorthIsUp()
    {
        Assert.True(View.ScreenY(30f) < View.ScreenY(20f));
        Assert.True(View.ScreenX(15f) > View.ScreenX(10f));
    }

    [Fact]
    public void ProjectClipsToTheView()
    {
        var inside = View.Project(10f, 20f, 15f, 25f);
        Assert.Equal(200f, inside.X);
        Assert.Equal(190f, inside.Y);
        Assert.Equal(10f, inside.W);
        Assert.Equal(10f, inside.H);

        var partly = View.Project(-100f, 20f, 15f, 25f);
        Assert.Equal(100f, partly.X);
        Assert.Equal(110f, partly.W);

        Assert.True(View.Project(500f, 500f, 510f, 510f).Empty);
    }

    [Fact]
    public void FitShowsEverything()
    {
        var view = MapView.Fit(new Box(0, 0, 400, 200), -50f, -10f, 50f, 10f);
        var box = view.Project(-50f, -10f, 50f, 10f);
        Assert.True(box.X > 0f && box.XMax < 400f);
        Assert.Equal(box.W / 100f, box.H / 20f, 3);
    }
}
