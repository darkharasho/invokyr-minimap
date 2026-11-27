using System;
using System.Collections.Generic;

namespace InvokyrMinimap
{
    /// <summary>
    /// Notices rooms that went away or changed shape. Invokyr swaps levels inside the same scene, so a new level
    /// shows up only as rooms disappearing (or a reused room address with other boxes). Rooms streaming in aren't a change.
    /// Free of UnityEngine so it can be tested.
    /// </summary>
    internal sealed class RoomTracker
    {
        private Dictionary<long, int> _known = new Dictionary<long, int>();

        public void Clear() => _known.Clear();

        /// <summary>Takes the current rooms (key → <see cref="Shape"/>) and returns the keys of known rooms that are gone or reshaped.</summary>
        public List<long> Update(Dictionary<long, int> rooms)
        {
            var changed = new List<long>();
            foreach (var pair in _known)
            {
                if (!rooms.TryGetValue(pair.Key, out var shape) || shape != pair.Value)
                    changed.Add(pair.Key);
            }
            _known = new Dictionary<long, int>(rooms);
            return changed;
        }

        /// <summary>A hash of a room's box corners, rounded to 5 cm so float noise doesn't count as a change.</summary>
        public static int Shape(IReadOnlyList<float> values)
        {
            var hash = 17;
            for (var i = 0; i < values.Count; i++)
                hash = unchecked(hash * 31 + (int)Math.Round(values[i] * 20f));
            return hash;
        }
    }
}
