using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;


// Builds a solid extruded mesh (top + bottom + side walls) from a simple 2D polygon.
// Used both by MetalSheet.cs to give the sheet itself real thickness (replacing whatever
// mesh was on the Quad/Cube in the Editor) AND to build the two pieces when a cut splits
// the sheet -- so both cases share identical, tested geometry logic.
//
// Every face is added in BOTH triangle winding orders (double-sided) so it renders
// correctly regardless of which winding direction the active render pipeline treats as
// "front" -- there are only ever a handful of these meshes alive at once (the sheet, plus
// two pieces per cut), so the doubled triangle count costs nothing in practice. Normals
// are assigned explicitly per face rather than via RecalculateNormals(), since averaging
// normals across opposite-winding duplicate triangles would cancel out to near-zero and
// break lighting.
public static class ExtrudedMeshBuilder
{
    /// <summary>
    /// polygon2D: the flat shape, in the sheet's local X/Y space, wound either direction.
    /// thickness: total Z depth: the mesh spans Z = -thickness/2 .. +thickness/2.
    /// Returns null if the polygon couldn't be triangulated (e.g. self-intersecting).
    /// </summary>
    public static Mesh Build(List<Vector2> polygon2D, float thickness)
    {
        List<int> capIndices = PolygonTriangulator.Triangulate(polygon2D);
        if (capIndices.Count < 3) return null;
 
        int n = polygon2D.Count;
        float halfT = thickness * 0.5f;
 
        Vector2 min = polygon2D[0], max = polygon2D[0];
        for (int i = 1; i < n; i++)
        {
            min = Vector2.Min(min, polygon2D[i]);
            max = Vector2.Max(max, polygon2D[i]);
        }
        Vector2 size = Vector2.Max(max - min, new Vector2(0.0001f, 0.0001f));
 
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
 
        AddCap(vertices, normals, uvs, triangles, polygon2D, capIndices, halfT, Vector3.forward, min, size);
        AddCap(vertices, normals, uvs, triangles, polygon2D, capIndices, -halfT, Vector3.back, min, size);
        AddWalls(vertices, normals, uvs, triangles, polygon2D, halfT);
 
        var mesh = new Mesh { name = "SheetMesh" };
        if (vertices.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
 
    private static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
        List<Vector2> polygon2D, List<int> capIndices, float z, Vector3 normal, Vector2 uvMin, Vector2 uvSize)
    {
        int start = vertices.Count;
        for (int i = 0; i < polygon2D.Count; i++)
        {
            Vector2 p = polygon2D[i];
            vertices.Add(new Vector3(p.x, p.y, z));
            normals.Add(normal);
            uvs.Add(new Vector2((p.x - uvMin.x) / uvSize.x, (p.y - uvMin.y) / uvSize.y));
        }
        for (int i = 0; i < capIndices.Count; i += 3)
        {
            int a = start + capIndices[i];
            int b = start + capIndices[i + 1];
            int c = start + capIndices[i + 2];
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(b); // reverse winding, see class comment
        }
    }
 
    private static void AddWalls(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
        List<Vector2> polygon2D, float halfT)
    {
        int n = polygon2D.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = polygon2D[i];
            Vector2 b = polygon2D[(i + 1) % n];
            Vector2 edgeDir = (b - a).normalized;
            Vector3 outward = new Vector3(edgeDir.y, -edgeDir.x, 0f);
 
            int vStart = vertices.Count;
            vertices.Add(new Vector3(a.x, a.y, halfT));
            vertices.Add(new Vector3(b.x, b.y, halfT));
            vertices.Add(new Vector3(b.x, b.y, -halfT));
            vertices.Add(new Vector3(a.x, a.y, -halfT));
            normals.Add(outward); normals.Add(outward); normals.Add(outward); normals.Add(outward);
            uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));
            uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 0));
 
            triangles.Add(vStart + 0); triangles.Add(vStart + 1); triangles.Add(vStart + 2);
            triangles.Add(vStart + 0); triangles.Add(vStart + 2); triangles.Add(vStart + 3);
            triangles.Add(vStart + 0); triangles.Add(vStart + 2); triangles.Add(vStart + 1); // reverse winding
            triangles.Add(vStart + 0); triangles.Add(vStart + 3); triangles.Add(vStart + 2); // reverse winding
        }
    }
}
