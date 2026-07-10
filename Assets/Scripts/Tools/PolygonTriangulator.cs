using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;


// Standard ear-clipping triangulation for a SIMPLE polygon (no self-intersections,
// no holes). Used by SheetPieceBuilder to turn the 2D shape of a cut piece into triangles.
//
// If the input is self-intersecting (a genuinely tangled/crossing cut path), this bails
// out gracefully rather than throwing -- the caller (SheetPieceBuilder) checks for a
// too-short result and skips building that piece.
public static class PolygonTriangulator
{
    /// <summary>Returns a flat list of indices into `polygon`, 3 per triangle.</summary>
    public static List<int> Triangulate(List<Vector2> polygon)
    {
        var indices = new List<int>();
        int n = polygon.Count;
        if (n < 3) return indices;
 
        var remaining = new List<int>(n);
        for (int i = 0; i < n; i++) remaining.Add(i);
 
        // Ear clipping needs a consistent (CCW) traversal order to pick ears correctly.
        if (SignedArea(polygon) < 0f)
            remaining.Reverse();
 
        int guard = 0;
        int maxGuard = n * n + 8; // safety valve so a degenerate polygon can't infinite-loop
 
        while (remaining.Count > 3 && guard < maxGuard)
        {
            guard++;
            bool clipped = false;
 
            for (int i = 0; i < remaining.Count; i++)
            {
                int iPrev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int iCurr = remaining[i];
                int iNext = remaining[(i + 1) % remaining.Count];
 
                Vector2 a = polygon[iPrev];
                Vector2 b = polygon[iCurr];
                Vector2 c = polygon[iNext];
 
                if (!IsConvex(a, b, c)) continue;
                if (AnyOtherPointInside(polygon, remaining, iPrev, iCurr, iNext, a, b, c)) continue;
 
                indices.Add(iPrev);
                indices.Add(iCurr);
                indices.Add(iNext);
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }
 
            if (!clipped) break; // couldn't find a valid ear -> degenerate/self-intersecting input
        }
 
        if (remaining.Count == 3)
        {
            indices.Add(remaining[0]);
            indices.Add(remaining[1]);
            indices.Add(remaining[2]);
        }
 
        return indices;
    }
 
    private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c) => Cross(b - a, c - b) > 0f;
 
    private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
 
    private static float SignedArea(List<Vector2> poly)
    {
        float area = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 p0 = poly[i];
            Vector2 p1 = poly[(i + 1) % poly.Count];
            area += p0.x * p1.y - p1.x * p0.y;
        }
        return area * 0.5f;
    }
 
    private static bool AnyOtherPointInside(List<Vector2> polygon, List<int> remaining, int iPrev, int iCurr, int iNext, Vector2 a, Vector2 b, Vector2 c)
    {
        foreach (int idx in remaining)
        {
            if (idx == iPrev || idx == iCurr || idx == iNext) continue;
            if (PointInTriangle(polygon[idx], a, b, c)) return true;
        }
        return false;
    }
 
    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);
 
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
 
        return !(hasNeg && hasPos);
    }
}
 
