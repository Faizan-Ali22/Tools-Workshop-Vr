// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Grinder Blade Contact
// Trigger collider on the blade disc edge. Detects overlap with
// ToolContactSurface components and notifies GrinderTool.
//
// Quest-Optimised:
//  • No Update loop — purely OnTrigger event driven.
//  • GetComponentInParent cached per collision pair (called once per Enter/Exit).
//  • Contact counting prevents spurious end events from multi-collider surfaces.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using PTTI.TradeTrainingSDK;

namespace PTTI.TradeTrainingSDK.Tools
{
    /// <summary>
    /// Attach to a thin trigger collider covering the grinder blade disc edge.
    /// Assign the parent GrinderTool reference in the Inspector.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GrinderBladeContact : MonoBehaviour
    {
        [Tooltip("The GrinderTool on the parent/root grinder object.")]
        [SerializeField] private GrinderTool grinderTool;

        // Tracks how many surface colliders we're currently overlapping.
        // This prevents a false "contact end" when the blade exits one
        // sub-collider of a multi-collider workpiece but is still inside another.
        private int contactCount;

        // ══════════════════ Editor Helpers ══════════════════

        private void Reset()
        {
            // Auto-find GrinderTool on any parent when first added
            grinderTool = GetComponentInParent<GrinderTool>();

            // Ensure the collider is set to trigger
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        // ══════════════════ Trigger Events ══════════════════

        private void OnTriggerEnter(Collider other)
        {
            var surface = other.GetComponentInParent<ToolContactSurface>();
            if (surface == null || !surface.IsActive) return;

            contactCount++;
            if (contactCount == 1 && grinderTool != null)
            {
                grinderTool.OnBladeContactStart();
                surface.NotifyContact(grinderTool);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var surface = other.GetComponentInParent<ToolContactSurface>();
            if (surface == null) return;

            contactCount = Mathf.Max(0, contactCount - 1);
            if (contactCount == 0 && grinderTool != null)
            {
                grinderTool.OnBladeContactEnd();
                surface.NotifyContactEnd(grinderTool);
            }
        }
    }
}
