using System;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class VaseShowState
    {
        internal readonly Vector3 Rotation;
        internal readonly int[] Axes;
        internal bool AllAligned { get { return Axes[0] == 2 && Axes[1] == 2 && Axes[2] == 2; } }
        internal VaseShowState(Vector3 rotation, Vector3 first, Vector3 second, Vector3 tolerance)
        {
            Rotation = new Vector3(Normalize(rotation.x), Normalize(rotation.y), Normalize(rotation.z));
            Vector3 target = Distance(Rotation, first) <= Distance(Rotation, second) ? first : second;
            Axes = new[] { Status(Rotation.x, target.x, tolerance.x), Status(Rotation.y, target.y, tolerance.y), Status(Rotation.z, target.z, tolerance.z) };
        }
        internal static float Normalize(float angle) { angle %= 360f; if (angle > 180f) angle -= 360f; if (angle < -180f) angle += 360f; return angle; }
        private static float Difference(float a, float b) { return Math.Abs(Normalize(a - b)); }
        private static float Distance(Vector3 a, Vector3 b) { return Difference(a.x, b.x) + Difference(a.y, b.y) + Difference(a.z, b.z); }
        private static int Status(float current, float target, float tolerance)
        { float distance = Difference(current, target); return distance <= tolerance ? 2 : distance < 10f ? 1 : 0; }
    }
}
