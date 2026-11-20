using System;
using System.Collections.Generic;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class PicturePointingArea
    {
        internal float Left, Top, Right, Bottom, X, Y;
        internal bool Dynamic;
        internal bool Contains(float x, float y) { return x >= Left && x < Right && y >= Top && y < Bottom; }
    }
    internal static class PicturePointingGeometry
    {
        internal static int Hit(IList<PicturePointingArea> areas, float x, float y)
        {
            int result = -1; float size = float.MaxValue;
            for (int i = 0; i < areas.Count; i++)
            {
                PicturePointingArea candidate = areas[i];
                if (!candidate.Contains(x, y)) continue;
                float candidateSize = (candidate.Right - candidate.Left) * (candidate.Bottom - candidate.Top);
                if (result < 0 || (candidate.Dynamic && !areas[result].Dynamic) || (candidate.Dynamic == areas[result].Dynamic && candidateSize < size))
                { result = i; size = candidateSize; }
            }
            return result;
        }
        internal static bool Within(float x, float y, float left, float top, float right, float bottom, float excludeX, float excludeY)
        { return x >= left && x <= right && y >= top && y <= bottom && ((excludeX == 0f && excludeY == 0f) || x <= excludeX || y <= excludeY); }
        internal static bool FindAnchor(IList<PicturePointingArea> areas, int index, Func<float, float, bool> allowed, out float x, out float y)
        {
            PicturePointingArea area = areas[index]; x = area.X; y = area.Y;
            if (allowed(x, y) && Hit(areas, x, y) == index) return true;
            for (int i = 0; i < 25; i++)
            {
                x = area.Left + (area.Right - area.Left) * ((i % 5) + 0.5f) / 5f;
                y = area.Top + (area.Bottom - area.Top) * ((i / 5) + 0.5f) / 5f;
                if (allowed(x, y) && Hit(areas, x, y) == index) return true;
            }
            return false;
        }
        internal static int Next(int current, int count, int direction)
        {
            if (count <= 0 || direction == 0) return -1;
            if (current < 0) current = direction > 0 ? -1 : 0;
            return (current + (direction > 0 ? 1 : -1) + count) % count;
        }
    }
}
