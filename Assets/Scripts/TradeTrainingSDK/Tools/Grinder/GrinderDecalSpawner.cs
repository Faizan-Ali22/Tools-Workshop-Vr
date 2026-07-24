// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Grinder Decal Spawner (Quest-Safe & Orientation-Independent)
//
// Spawns grind-mark scratch quads when the blade contacts a surface.
// Uses simple unlit transparent quads instead of URP Decal Projectors
// so it works on Mobile_Renderer (Quest 2/3) without needing
// DecalRendererFeature.
//
// Quest-Optimised & Robust:
//  • Spawns lightweight Quad primitives (4 verts) instead of Decal Projectors
//  • Uses ClosestPoint to guarantee decal placement on contact surface regardless of blade rotation
//  • Pools and caps total decals to prevent unbounded memory growth
//  • No Update raycast when not grinding (early return)
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using UnityEngine;

namespace PTTI.TradeTrainingSDK.Tools
{
    public class GrinderDecalSpawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The grinder tool to monitor.")]
        [SerializeField] private GrinderTool grinderTool;

        [Tooltip("Material with the scratch/grind texture. Should be Unlit or Simple Lit with Transparent rendering.")]
        [SerializeField] private Material scratchMaterial;

        [Tooltip("The origin point for contact detection (usually the blade or trigger). If unassigned, uses this object's Transform.")]
        [SerializeField] private Transform bladeBottomEdge;

        [Header("Decal Size")]
        [Tooltip("Width of each scratch mark in meters.")]
        [SerializeField] private float decalWidth = 0.04f;
        [Tooltip("Height of each scratch mark in meters.")]
        [SerializeField] private float decalHeight = 0.005f;

        [Header("Spawn Settings")]
        [Tooltip("Minimum distance the blade must move before spawning another decal.")]
        [SerializeField] private float minDistanceInterval = 0.015f;

        [Tooltip("Minimum time (in seconds) between spawns to prevent flooding.")]
        [SerializeField] private float minTimeInterval = 0.04f;

        [Tooltip("Search radius to find the workpiece surface when grinding.")]
        [SerializeField] private float searchRadius = 0.15f;

        [Tooltip("LayerMask for the workpieces (so search ignores the grinder itself).")]
        [SerializeField] private LayerMask surfaceLayerMask = ~0;

        [Header("Performance")]
        [Tooltip("Maximum decals alive at once. Oldest are recycled when exceeded.")]
        [SerializeField] private int maxDecals = 40;

        [Tooltip("Offset above surface to prevent z-fighting (meters).")]
        [SerializeField] private float surfaceOffset = 0.002f;

        // ── Internal State ──
        private float nextSpawnTime;
        private Vector3 lastSpawnPos = Vector3.positiveInfinity;
        private readonly List<GameObject> decalPool = new List<GameObject>();
        private int poolIndex;

        private void Reset()
        {
            if (grinderTool == null)
                grinderTool = GetComponentInParent<GrinderTool>();
        }

        private void Update()
        {
            if (grinderTool == null) return;

            if (!grinderTool.IsInUse)
            {
                // Reset spawn position so we get an immediate mark on next contact
                lastSpawnPos = Vector3.positiveInfinity;
                return;
            }

            Transform origin = bladeBottomEdge != null ? bladeBottomEdge : transform;

            if (Time.time < nextSpawnTime) return;
            if (Vector3.Distance(origin.position, lastSpawnPos) < minDistanceInterval) return;

            SpawnDecal(origin);
            nextSpawnTime = Time.time + minTimeInterval;
            lastSpawnPos = origin.position;
        }

        private void SpawnDecal(Transform origin)
        {
            Vector3 originPos = origin.position;

            // Find overlapping or nearby colliders with ToolContactSurface
            Collider[] hits = Physics.OverlapSphere(originPos, searchRadius, surfaceLayerMask);
            Collider bestCollider = null;
            float closestDist = float.MaxValue;
            Vector3 bestSurfacePoint = Vector3.zero;

            foreach (var col in hits)
            {
                // Skip colliders attached to the grinder itself
                if (col.transform.IsChildOf(grinderTool.transform) || col.gameObject == gameObject)
                    continue;

                // Check if it's a ToolContactSurface or solid workpiece
                var surface = col.GetComponentInParent<ToolContactSurface>();
                if (surface != null || col.attachedRigidbody == null)
                {
                    Vector3 pt = col.ClosestPoint(originPos);
                    float d = Vector3.SqrMagnitude(pt - originPos);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        bestCollider = col;
                        bestSurfacePoint = pt;
                    }
                }
            }

            if (bestCollider == null)
            {
                // Fallback: simple raycast down
                if (Physics.Raycast(originPos + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, searchRadius, surfaceLayerMask))
                {
                    bestCollider = hit.collider;
                    bestSurfacePoint = hit.point;
                }
                else
                {
                    return; // No workpiece found nearby
                }
            }

            // Determine normal: raycast from slightly outside toward bestSurfacePoint
            Vector3 normal = Vector3.up;
            Vector3 dirToSurface = (bestSurfacePoint - originPos).normalized;
            if (dirToSurface.sqrMagnitude < 0.0001f) dirToSurface = Vector3.down;

            Vector3 rayStart = bestSurfacePoint - dirToSurface * 0.03f;
            if (Physics.Raycast(rayStart, dirToSurface, out RaycastHit normalHit, 0.06f))
            {
                normal = normalHit.normal;
                bestSurfacePoint = normalHit.point;
            }
            else
            {
                normal = -dirToSurface;
            }

            // Get or recycle a decal quad
            GameObject decal = GetOrCreateDecal();
            decal.SetActive(true);

            // Position: on the surface, offset slightly above to prevent z-fighting
            decal.transform.position = bestSurfacePoint + normal * surfaceOffset;

            // Orientation: face away from the surface normal
            // Align the scratch mark with the blade's disc plane
            Vector3 bladePlaneDir = origin.forward; // Assuming blade's forward is in the disc plane
            if (Vector3.Dot(bladePlaneDir, normal) > 0.9f) bladePlaneDir = origin.right; 
            
            // Project blade direction onto the surface plane
            Vector3 scratchUp = Vector3.ProjectOnPlane(bladePlaneDir, normal).normalized;
            if (scratchUp.sqrMagnitude < 0.001f) scratchUp = Vector3.up;

            decal.transform.rotation = Quaternion.LookRotation(-normal, scratchUp);
            // Slight random wiggle (±15 degrees) for organic feel rather than complete randomness
            decal.transform.Rotate(0, 0, Random.Range(-15f, 15f), Space.Self);

            // Random size variation
            float sizeVar = Random.Range(0.8f, 1.2f);
            // Make them thin and long
            decal.transform.localScale = new Vector3(decalWidth * sizeVar, decalHeight * sizeVar, 1f);

            // Parent to the workpiece transform so it moves if the workpiece is moved
            decal.transform.SetParent(bestCollider.transform, true);
        }

        private GameObject GetOrCreateDecal()
        {
            // Recycle from pool if at max capacity
            if (decalPool.Count >= maxDecals)
            {
                GameObject recycled = decalPool[poolIndex % maxDecals];
                poolIndex = (poolIndex + 1) % maxDecals;
                if (recycled != null)
                {
                    recycled.transform.SetParent(null, false);
                    return recycled;
                }
            }

            // Create a new Quad primitive
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "GrindMark";

            // Remove collider — scratch marks must not interfere with physics
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Apply the scratch material
            if (scratchMaterial != null)
            {
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = scratchMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            // Track in pool
            if (decalPool.Count < maxDecals)
                decalPool.Add(quad);
            else
                decalPool[poolIndex] = quad;

            return quad;
        }

        /// <summary>Destroy all spawned decal marks (e.g., on tool reset).</summary>
        public void ClearAllDecals()
        {
            for (int i = decalPool.Count - 1; i >= 0; i--)
            {
                if (decalPool[i] != null)
                    Destroy(decalPool[i]);
            }
            decalPool.Clear();
            poolIndex = 0;
        }

        private void OnDestroy()
        {
            ClearAllDecals();
        }
    }
}
