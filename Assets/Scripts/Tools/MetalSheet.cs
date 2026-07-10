using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;


// ATTACH TO: the metal sheet GameObject.
// You can leave literally any placeholder mesh on it in the Editor (Quad, Cube, whatever) --
// Awake() replaces it with a generated solid box sized by Sheet Size / Sheet Thickness below,
// so what you had there before doesn't matter. This is also what gives the sheet real
// visible depth instead of being paper-thin.
[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(MeshFilter))]
public class MetalSheet : MonoBehaviour
{
    [Header("Sheet dimensions (Base Sheet)")]
    [SerializeField] private Vector2 sheetSize = new Vector2(0.5f, 0.3f);
    [SerializeField] private float sheetThickness = 0.01f;
 
    [Header("Cut mask")]
    [SerializeField] private int maskResolution = 1024;
    [SerializeField] private int brushRadiusPixels = 2;
    [SerializeField] private float minPointSpacing = 0.001f;
 
    [Header("Splitting into pieces")]
    [SerializeField] private bool enableSplitting = true;
    [SerializeField] private GameObject piecePrefab;
    [SerializeField] private float edgeContactTolerance = 0.015f;
    [SerializeField] private float geometrySimplifySpacing = 0.004f;
 
    [Header("Loop closing / minimum cut length")]
    [SerializeField] private float minPathLengthToClose = 0.05f;
    [SerializeField] private float closeDistanceThreshold = 0.01f;
 
    public System.Action<List<Vector2>> OnCutPathClosed;
 
    // Now tracks any custom shape, allowing infinite recursive cutting
    [HideInInspector] public List<Vector2> currentPolygon;
 
    private Texture2D maskTexture;
    private Renderer sheetRenderer;
    private MeshFilter meshFilter;
    private Bounds localBounds;
    private readonly List<Vector2> cutPathLocal = new List<Vector2>();
    private Vector3 lastCutWorldPos;
    private bool hasLastPoint;
    private Vector2 lastUV;
    private bool hasLastUV;
    private bool textureDirty;
    private bool hasSplit;
 
    private static readonly int MaskTexProperty = Shader.PropertyToID("_CutMask");
 
    private void Awake()
    {
        // If this is the original sheet, generate the base rectangle. 
        // If it's a cut piece, the polygon will already be injected by SpawnPiece.
        if (currentPolygon == null || currentPolygon.Count == 0)
        {
            currentPolygon = new List<Vector2>
            {
                new Vector2(-sheetSize.x * 0.5f, -sheetSize.y * 0.5f),
                new Vector2( sheetSize.x * 0.5f, -sheetSize.y * 0.5f),
                new Vector2( sheetSize.x * 0.5f,  sheetSize.y * 0.5f),
                new Vector2(-sheetSize.x * 0.5f,  sheetSize.y * 0.5f),
            };
        }
        InitializeSheet();
    }
 
    public void InitializeSheet()
    {
        sheetRenderer = GetComponent<Renderer>();
        meshFilter = GetComponent<MeshFilter>();
 
        // Ensure we are using a MeshCollider for arbitrary polygon shapes
        var bc = GetComponent<BoxCollider>();
        if (bc != null) Destroy(bc);
 
        var mc = GetComponent<MeshCollider>();
        if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
 
        // Calculate the accurate bounding box of the custom polygon
        float minX = currentPolygon[0].x, maxX = currentPolygon[0].x;
        float minY = currentPolygon[0].y, maxY = currentPolygon[0].y;
        foreach (var p in currentPolygon)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        localBounds = new Bounds(
            new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0),
            new Vector3(maxX - minX, maxY - minY, sheetThickness));
 
        meshFilter.mesh = ExtrudedMeshBuilder.Build(currentPolygon, sheetThickness);
        if (meshFilter.mesh != null)
        {
            mc.sharedMesh = meshFilter.mesh;
            mc.convex = true; // Required for XR interactions
        }
 
        // Reset the cut mask for the new piece
        maskTexture = new Texture2D(maskResolution, maskResolution, TextureFormat.RGBA32, false);
        var pixels = new Color32[maskResolution * maskResolution];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        maskTexture.SetPixels32(pixels);
        maskTexture.Apply();
        sheetRenderer.material.SetTexture(MaskTexProperty, maskTexture);
    }
 
    private void LateUpdate()
    {
        if (textureDirty)
        {
            maskTexture.Apply();
            textureDirty = false;
        }
    }
 
    public void AddCutPoint(Vector3 worldPos)
    {
        if (hasSplit) return;
 
        if (hasLastPoint && Vector3.Distance(worldPos, lastCutWorldPos) < minPointSpacing)
            return;
 
        hasLastPoint = true;
        lastCutWorldPos = worldPos;
 
        Vector3 local = transform.InverseTransformPoint(worldPos);
        Vector2 uv = new Vector2(
            Mathf.Clamp01((local.x - localBounds.min.x) / localBounds.size.x),
            Mathf.Clamp01((local.y - localBounds.min.y) / localBounds.size.y));
 
        if (hasLastUV)
            PaintLine(lastUV, uv);
        else
            PaintBrush(uv);
 
        lastUV = uv;
        hasLastUV = true;
 
        cutPathLocal.Add(new Vector2(local.x, local.y));
        CheckForSplit();
    }
 
    public void EndCutStroke()
    {
        hasLastPoint = false;
        hasLastUV = false;
        cutPathLocal.Clear();
    }
 
    private void PaintLine(Vector2 fromUV, Vector2 toUV)
    {
        Vector2 fromPx = new Vector2(fromUV.x * maskResolution, fromUV.y * maskResolution);
        Vector2 toPx = new Vector2(toUV.x * maskResolution, toUV.y * maskResolution);
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(fromPx, toPx)));
 
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            PaintBrush(Vector2.Lerp(fromUV, toUV, t));
        }
    }
 
    private void PaintBrush(Vector2 uv)
    {
        int cx = Mathf.RoundToInt(uv.x * maskResolution);
        int cy = Mathf.RoundToInt(uv.y * maskResolution);
        int r = brushRadiusPixels;
        int rSq = r * r;
 
        for (int y = -r; y <= r; y++)
        {
            int py = cy + y;
            if (py < 0 || py >= maskResolution) continue;
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > rSq) continue;
                int px = cx + x;
                if (px < 0 || px >= maskResolution) continue;
                maskTexture.SetPixel(px, py, Color.black);
            }
        }
        textureDirty = true;
    }
 
    private void CheckForSplit()
    {
        if (!enableSplitting || hasSplit || cutPathLocal.Count < 2) return;
 
        float pathLength = 0f;
        for (int i = 1; i < cutPathLocal.Count; i++)
            pathLength += Vector2.Distance(cutPathLocal[i - 1], cutPathLocal[i]);
        if (pathLength < minPathLengthToClose) return;
 
        Vector2 first = cutPathLocal[0];
        Vector2 last = cutPathLocal[cutPathLocal.Count - 1];
 
        if (TryGetBoundaryParam(first, out float startParam) &&
            TryGetBoundaryParam(last, out float endParam) &&
            !Mathf.Approximately(startParam, endParam))
        {
            PerformEdgeToEdgeSplit(startParam, endParam);
            return;
        }
 
        if (Vector2.Distance(first, last) <= closeDistanceThreshold)
            OnCutPathClosed?.Invoke(new List<Vector2>(cutPathLocal));
    }
 
    // Upgraded: Can now trace the boundary of an infinitely complex polygon
    private bool TryGetBoundaryParam(Vector2 point, out float param)
    {
        for (int i = 0; i < currentPolygon.Count; i++)
        {
            Vector2 a = currentPolygon[i];
            Vector2 b = currentPolygon[(i + 1) % currentPolygon.Count];
            
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            float tLine = 0f;
            if (lenSq != 0) tLine = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            
            Vector2 proj = a + tLine * ab;
            float dist = Vector2.Distance(point, proj);
            
            if (dist <= edgeContactTolerance)
            {
                param = i + tLine;
                return true;
            }
        }
        param = 0f;
        return false;
    }
 
    private Vector2 PointOnBoundary(float param)
    {
        int count = currentPolygon.Count;
        param = Mathf.Repeat(param, count);
        int index = Mathf.FloorToInt(param);
        float t = param - index;
        Vector2 a = currentPolygon[index];
        Vector2 b = currentPolygon[(index + 1) % count];
        return Vector2.Lerp(a, b, t);
    }
 
    private void WalkBoundary(float fromParam, float toParam, List<Vector2> output)
    {
        int count = currentPolygon.Count;
        float span = toParam - fromParam;
        if (span <= 0f) span += count;
 
        var included = new List<(float rel, Vector2 pos)>();
        for (int i = 0; i < count; i++)
        {
            float rel = i - fromParam;
            if (rel < 0f) rel += count;
            if (rel > 0.0001f && rel < span - 0.0001f)
                included.Add((rel, currentPolygon[i]));
        }
        included.Sort((a, b) => a.rel.CompareTo(b.rel));
        foreach (var item in included)
            output.Add(item.pos);
    }
 
    private static List<Vector2> SimplifyPath(List<Vector2> path, float minSpacing)
    {
        var result = new List<Vector2>();
        if (path.Count == 0) return result;
 
        result.Add(path[0]);
        for (int i = 1; i < path.Count - 1; i++)
        {
            if (Vector2.Distance(result[result.Count - 1], path[i]) >= minSpacing)
                result.Add(path[i]);
        }
        if (path.Count > 1)
            result.Add(path[path.Count - 1]);
        return result;
    }
 
    private void PerformEdgeToEdgeSplit(float startParam, float endParam)
    {
        List<Vector2> cut = SimplifyPath(cutPathLocal, geometrySimplifySpacing);
        if (cut.Count < 2) return;
 
        cut[0] = PointOnBoundary(startParam);
        cut[cut.Count - 1] = PointOnBoundary(endParam);
 
        var polygonA = new List<Vector2> { cut[0] };
        WalkBoundary(startParam, endParam, polygonA);
        polygonA.Add(cut[cut.Count - 1]);
        for (int i = cut.Count - 2; i >= 1; i--)
            polygonA.Add(cut[i]);
 
        var polygonB = new List<Vector2> { cut[cut.Count - 1] };
        WalkBoundary(endParam, startParam, polygonB);
        polygonB.Add(cut[0]);
        for (int i = 1; i < cut.Count - 1; i++)
            polygonB.Add(cut[i]);
 
        SpawnPieces(polygonA, polygonB);
    }
 
    private void SpawnPieces(List<Vector2> polygonA, List<Vector2> polygonB)
    {
        hasSplit = true;
        SpawnPiece(polygonA, "_PieceA");
        SpawnPiece(polygonB, "_PieceB");
 
        gameObject.SetActive(false);
        Destroy(gameObject, 0.05f);
    }
 
    private void SpawnPiece(List<Vector2> poly, string suffix)
    {
        // 1. Calculate the true center of the new scrap to fix the VR Grabbing pivot
        Vector2 center = Vector2.zero;
        foreach (var p in poly) center += p;
        center /= poly.Count;
 
        // 2. Shift all vertices so the pivot rests perfectly at the center of mass
        for (int i = 0; i < poly.Count; i++) poly[i] -= center;
 
        GameObject piece;
        if (piecePrefab != null)
        {
            piece = Instantiate(piecePrefab, transform.parent);
        }
        else
        {
            piece = new GameObject("SheetPiece");
            piece.transform.SetParent(transform.parent, false);
            piece.AddComponent<MeshFilter>();
            var mr = piece.AddComponent<MeshRenderer>();
            mr.sharedMaterial = sheetRenderer.sharedMaterial;
            piece.AddComponent<Rigidbody>();
        }
 
        piece.name = gameObject.name + suffix;
 
        // 3. Compensate for the vertex shift by moving the physical GameObject
        Vector3 worldOffset = transform.TransformDirection(new Vector3(center.x, center.y, 0));
        piece.transform.position = transform.position + worldOffset;
        piece.transform.rotation = transform.rotation;
        piece.transform.localScale = transform.localScale;
 
        // 4. Attach this exact script to the new piece so it can be cut infinitely
        var newSheet = piece.GetComponent<MetalSheet>();
        if (newSheet == null) newSheet = piece.AddComponent<MetalSheet>();
 
        // Copy parameters over
        newSheet.sheetThickness = this.sheetThickness;
        newSheet.maskResolution = this.maskResolution;
        newSheet.brushRadiusPixels = this.brushRadiusPixels;
        newSheet.minPointSpacing = this.minPointSpacing;
        newSheet.enableSplitting = this.enableSplitting;
        newSheet.piecePrefab = this.piecePrefab;
        newSheet.edgeContactTolerance = this.edgeContactTolerance;
        newSheet.geometrySimplifySpacing = this.geometrySimplifySpacing;
        newSheet.minPathLengthToClose = this.minPathLengthToClose;
        newSheet.closeDistanceThreshold = this.closeDistanceThreshold;
 
        // Pass the updated custom boundary to the piece and force it to build
        newSheet.currentPolygon = new List<Vector2>(poly);
        newSheet.InitializeSheet();
 
        // 5. Force the XR Grab Interactable to realize the MeshCollider has completely changed
        var grab = piece.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            var col = piece.GetComponent<Collider>();
            if (col != null)
            {
                grab.colliders.Clear();
                grab.colliders.Add(col);
            }
        }
    }
}