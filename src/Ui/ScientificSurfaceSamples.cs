using System.Collections.Generic;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal static class ScientificSurfaceSamples
    {
        internal struct Sample
        {
            internal Vector3 Position, Normal;
            internal float Area;
            internal int TriangleOffset;
        }

        internal static List<Sample> Read(Mesh mesh)
        {
            List<Sample> selected = new List<Sample>();
            if (!mesh.isReadable) return selected;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;
            List<Sample> candidates = new List<Sample>(triangles.Length / 3);
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                float area = face.magnitude;
                if (area < 1e-8f) continue;
                Vector3 normal = normals.Length == vertices.Length ? (normals[a] + normals[b] + normals[c]) / 3f : face;
                float length = normal.magnitude;
                if (length < 1e-8f) continue;
                candidates.Add(new Sample
                {
                    Position = (vertices[a] + vertices[b] + vertices[c]) / 3f,
                    Normal = normal / length,
                    Area = area,
                    TriangleOffset = i
                });
            }
            candidates.Sort(delegate (Sample a, Sample b)
            {
                int area = b.Area.CompareTo(a.Area);
                return area == 0 ? a.TriangleOffset.CompareTo(b.TriangleOffset) : area;
            });

            // Cover differently facing surfaces before filling the gaps with interior samples.
            foreach (Sample candidate in candidates)
            {
                if (IsSeparated(candidate, selected, 0f)) selected.Add(candidate);
                if (selected.Count == 12) break;
            }
            Vector3 size = mesh.bounds.size;
            float distance = Mathf.Max(Mathf.Max(size.x, Mathf.Max(size.y, size.z)), 1e-5f) * 0.12f;
            foreach (Sample candidate in candidates)
            {
                if (IsSeparated(candidate, selected, distance)) selected.Add(candidate);
                if (selected.Count == 24) break;
            }
            return selected;
        }

        private static bool IsSeparated(Sample candidate, List<Sample> selected, float distance)
        {
            foreach (Sample old in selected)
            {
                if (Vector3.Dot(candidate.Normal, old.Normal) >= 0.94f &&
                    (distance == 0f || (candidate.Position - old.Position).sqrMagnitude <= distance * distance)) return false;
            }
            return true;
        }
    }
}
