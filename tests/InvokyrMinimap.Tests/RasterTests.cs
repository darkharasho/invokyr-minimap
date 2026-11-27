using InvokyrMinimap;
using Xunit;

public class RasterTests
{
    private static readonly Rgba Red = new Rgba(255, 0, 0, 255);

    [Fact]
    public void RowsAreStoredBottomUp()
    {
        var r = new Raster(4, 3);
        r.Set(1, 0, Red); // top row in GUI space
        Assert.Equal(255, r.Pixels[((2 * 4) + 1) * 4]);
        Assert.Equal(Red, r.Get(1, 0));
    }

    [Fact]
    public void SquareFillsInsideOnly()
    {
        var r = new Raster(20, 20);
        r.FillSquare(10f, 10f, 3f, Red);
        Assert.Equal(255, r.Get(10, 10).A);
        Assert.Equal(255, r.Get(7, 12).A);
        Assert.Equal(0, r.Get(14, 10).A);
        Assert.Equal(0, r.Get(10, 3).A);
    }

    [Fact]
    public void ArrowPointsWhereItIsTold()
    {
        var r = new Raster(40, 40);
        r.FillArrow(20f, 20f, 0f, -1f, 16f, Red); // up
        Assert.Equal(255, r.Get(20, 14).A);   // near the tip, above centre
        Assert.Equal(0, r.Get(20, 30).A);     // nothing far below
        Assert.Equal(255, r.Get(17, 22).A);   // a back wing
        Assert.Equal(0, r.Get(20, 25).A);     // the notch between the wings
    }

    [Fact]
    public void ShapesClipAtTheEdges()
    {
        var r = new Raster(10, 10);
        r.FillCircle(0f, 0f, 6f, Red);
        r.FillArrow(9f, 9f, 1f, 1f, 30f, Red);
        Assert.Equal(255, r.Get(1, 1).A);
    }

    [Fact]
    public void BlendMixesOverAColour()
    {
        var r = new Raster(1, 1);
        r.Set(0, 0, new Rgba(0, 0, 255, 255));
        r.Blend(0, 0, new Rgba(255, 0, 0, 128));
        var c = r.Get(0, 0);
        Assert.InRange(c.R, 126, 130);
        Assert.InRange(c.B, 125, 129);
        Assert.Equal(255, c.A);
    }
}

public class MapPainterTests
{
    [Fact]
    public void TurnedMapPutsWhatIsAheadAtTheTop()
    {
        // A floor strip 2-6 m east of you; you look east, so it should be painted above the centre.
        var grid = new FloorGrid();
        for (var x = FloorGrid.Index(2f); x <= FloorGrid.Index(6f); x++)
            for (var z = FloorGrid.Index(-1f); z <= FloorGrid.Index(1f); z++)
                grid.Add(x, z, new Cell { Kind = CellKind.Floor, Y = 0f, Depth = 1f });
        var raster = new Raster(40, 40);
        var view = new MapView(new Box(0, 0, 40, 40), 0f, 0f, 2f, 1f, 0f);
        MapPainter.Paint(raster, grid, view, 0f);
        Assert.Equal(MapPainter.Floor, raster.Get(20, 12));  // 4 m ahead
        Assert.Equal(0, raster.Get(32, 20).A);               // 6 m to the right: nothing
        Assert.Equal(0, raster.Get(20, 28).A);               // behind: nothing
    }

    [Fact]
    public void OtherFloorsAreFaint()
    {
        var grid = new FloorGrid();
        grid.Add(0, 0, new Cell { Kind = CellKind.Floor, Y = 5f, Depth = 1f });
        var raster = new Raster(10, 10);
        var view = new MapView(new Box(0, 0, 10, 10), FloorGrid.Center(0), FloorGrid.Center(0), 10f);
        MapPainter.Paint(raster, grid, view, 0f);
        Assert.Equal(MapPainter.OtherFloor, raster.Get(5, 5));
    }
}

public class OutlineTests
{
    // Two 4 m rooms side by side along X (room 1 west of x=0, room 2 east), north-up, 4 px per metre.
    private static Raster PaintTwoRooms()
    {
        var grid = new FloorGrid();
        for (var x = FloorGrid.Index(-4f); x < FloorGrid.Index(4f); x++)
            for (var z = FloorGrid.Index(-2f); z < FloorGrid.Index(2f); z++)
                grid.Add(x, z, new Cell { Kind = CellKind.Floor, Y = 0f, Depth = 1f, Room = (ushort)(x < 0 ? 1 : 2) });
        var raster = new Raster(48, 48);
        MapPainter.Paint(raster, grid, new MapView(new Box(0, 0, 48, 48), 0f, 0f, 4f), 0f, 1f);
        return raster;
    }

    [Fact]
    public void EdgeNextToNothingIsOutlined()
    {
        var r = PaintTwoRooms();
        Assert.Equal(MapPainter.Edge, r.Get(8, 20));   // x = -3.9 m, the west edge
        Assert.Equal(MapPainter.Edge, r.Get(20, 16));  // z = +2 m, the north edge
    }

    [Fact]
    public void BorderBetweenRoomsIsOutlined()
    {
        var r = PaintTwoRooms();
        Assert.Equal(MapPainter.Edge, r.Get(24, 20));  // x = 0, between the rooms
    }

    [Fact]
    public void InsideARoomIsPlainFloor()
    {
        var r = PaintTwoRooms();
        Assert.Equal(MapPainter.Floor, r.Get(16, 22));
        Assert.Equal(MapPainter.Floor, r.Get(32, 22));
    }
}
