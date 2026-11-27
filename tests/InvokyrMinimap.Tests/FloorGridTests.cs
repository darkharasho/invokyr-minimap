using InvokyrMinimap;
using Xunit;

public class FloorGridTests
{
    private static Cell Floor(float y, float depth = 1f, byte flags = 0) =>
        new Cell { Kind = CellKind.Floor, Y = y, Depth = depth, Flags = flags };

    [Fact]
    public void EmptyGridHasNothing()
    {
        Assert.False(new FloorGrid().TryGet(3f, 4f, 0f, out _));
    }

    [Fact]
    public void CellIsFoundAnywhereInsideIt()
    {
        var grid = new FloorGrid();
        grid.Add(FloorGrid.Index(-3.1f), FloorGrid.Index(7.9f), Floor(0f));
        var cx = FloorGrid.Center(FloorGrid.Index(-3.1f));
        var cz = FloorGrid.Center(FloorGrid.Index(7.9f));
        Assert.True(grid.TryGet(cx, cz, 0f, out var cell));
        Assert.Equal(CellKind.Floor, cell.Kind);
        Assert.False(grid.TryGet(cx + FloorGrid.CellSize, cz, 0f, out _));
    }

    [Fact]
    public void DeeperRoomOwnsAnOverlap()
    {
        var grid = new FloorGrid();
        grid.Add(5, 5, Floor(0f, depth: 0.2f, flags: MapPainter.Exit));
        grid.Add(5, 5, Floor(0.1f, depth: 3f));
        grid.Add(5, 5, Floor(0f, depth: 1f, flags: MapPainter.Shop));
        Assert.True(grid.TryGet(FloorGrid.Center(5), FloorGrid.Center(5), 0f, out var cell));
        Assert.Equal(0, cell.Flags);
    }

    [Fact]
    public void StackedFloorsAreKeptApart()
    {
        var grid = new FloorGrid();
        grid.Add(1, 1, Floor(0f));
        grid.Add(1, 1, Floor(4f, flags: MapPainter.Shop));
        var c = FloorGrid.Center(1);
        Assert.True(grid.TryGet(c, c, 0.5f, out var low));
        Assert.Equal(0f, low.Y);
        Assert.True(grid.TryGet(c, c, 3.8f, out var high));
        Assert.Equal(4f, high.Y);
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        var grid = new FloorGrid();
        grid.Add(1, 1, Floor(0f));
        grid.Clear();
        Assert.False(grid.TryGet(FloorGrid.Center(1), FloorGrid.Center(1), 0f, out _));
    }
}

public class FloorClassifierTests
{
    private static CellKind Classify(float level, out float y, params float[] hits) =>
        FloorClassifier.Classify(new System.Collections.Generic.List<float>(hits), 0, hits.Length, level, out y);

    [Fact]
    public void LevelIsTheMedianSoPitsDoNotMoveIt()
    {
        Assert.Equal(1f, FloorClassifier.Level(new System.Collections.Generic.List<float> { 1f, 1f, -4f, 1.02f, 0.98f }), 2);
    }

    [Fact]
    public void FloorUnderATableIsFloorAtFloorHeight()
    {
        Assert.Equal(CellKind.Floor, Classify(0f, out var y, 0.8f, 0.01f));
        Assert.Equal(0.01f, y, 3);
    }

    [Fact]
    public void OnlyFarBelowIsAPit()
    {
        Assert.Equal(CellKind.Pit, Classify(0f, out _, -3f));
    }

    [Fact]
    public void OnlyFarAboveIsSolid()
    {
        Assert.Equal(CellKind.Wall, Classify(0f, out _, 2.5f));
    }

    [Fact]
    public void NothingIsNothing()
    {
        Assert.Equal(CellKind.None, Classify(0f, out _));
    }
}

public class PitPaintTests
{
    [Fact]
    public void PitsAreDark()
    {
        var grid = new FloorGrid();
        grid.Add(0, 0, new Cell { Kind = CellKind.Pit, Y = 0f, Depth = 1f });
        var raster = new Raster(10, 10);
        var view = new MapView(new Box(0, 0, 10, 10), FloorGrid.Center(0), FloorGrid.Center(0), 10f);
        MapPainter.Paint(raster, grid, view, 0f);
        Assert.Equal(MapPainter.Pit, raster.Get(5, 5));
    }
}
