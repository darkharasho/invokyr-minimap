using System.Collections.Generic;
using InvokyrMinimap;
using Xunit;

public class RoomTrackerTests
{
    private static Dictionary<long, int> Rooms(params (long key, int shape)[] rooms)
    {
        var d = new Dictionary<long, int>();
        foreach (var (key, shape) in rooms)
            d[key] = shape;
        return d;
    }

    [Fact]
    public void RoomsStreamingInAreNotAChange()
    {
        var t = new RoomTracker();
        Assert.Empty(t.Update(Rooms((1, 10))));
        Assert.Empty(t.Update(Rooms((1, 10), (2, 20))));
    }

    [Fact]
    public void RoomThatIsGoneIsReported()
    {
        var t = new RoomTracker();
        t.Update(Rooms((1, 10), (2, 20)));
        Assert.Equal(new long[] { 2 }, t.Update(Rooms((1, 10))));
        Assert.Empty(t.Update(Rooms((1, 10))));
    }

    [Fact]
    public void RoomWithANewShapeIsReported()
    {
        // A new level can reuse a destroyed room's address; its boxes tell them apart.
        var t = new RoomTracker();
        t.Update(Rooms((1, 10), (2, 20)));
        Assert.Equal(new long[] { 1 }, t.Update(Rooms((1, 11), (2, 20))));
    }

    [Fact]
    public void ShapeHashIgnoresTinyFloatNoise()
    {
        Assert.Equal(RoomTracker.Shape(new[] { 1.001f, 2f, 3f }), RoomTracker.Shape(new[] { 1.004f, 2f, 3f }));
        Assert.NotEqual(RoomTracker.Shape(new[] { 1f, 2f, 3f }), RoomTracker.Shape(new[] { 1.5f, 2f, 3f }));
    }
}
