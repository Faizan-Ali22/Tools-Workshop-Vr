// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Tool Contact Surface
// Attach to any workpiece (metal sheet, pipe, wire, panel) that tools
// can interact with. Defines the surface material and fires events when
// a tool's active element makes or breaks contact.
//
// Quest-Optimised: no Update loop, pure event-driven.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.Events;

namespace PTTI.TradeTrainingSDK
{
    /// <summary>
    /// Marks a GameObject as a surface that trade tools can work on.
    /// Tool contact scripts (e.g. GrinderBladeContact) look for this
    /// component on collider parents to determine if they hit a workpiece.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ToolContactSurface : MonoBehaviour
    {
        [Tooltip("Physical material of this surface. Tools react differently per type.")]
        [SerializeField] private SurfaceMaterialType materialType = SurfaceMaterialType.Metal;

        [Tooltip("When false, tools pass through without triggering work events.")]
        [SerializeField] private bool isActive = true;

        [Header("Events")]
        [Tooltip("Fired when a running tool first touches this surface.")]
        public UnityEvent<TradeToolBase> onToolContact;
        [Tooltip("Fired when a running tool leaves this surface.")]
        public UnityEvent<TradeToolBase> onToolContactEnd;

        // ── Public Properties ──
        public SurfaceMaterialType MaterialType => materialType;
        public bool IsActive => isActive;

        /// <summary>Enable or disable this surface at runtime.</summary>
        public void SetActive(bool active) => isActive = active;

        /// <summary>
        /// Called by tool contact scripts when their active element
        /// touches this surface while the tool is running.
        /// </summary>
        public void NotifyContact(TradeToolBase tool)
        {
            if (!isActive) return;
            onToolContact?.Invoke(tool);
        }

        /// <summary>
        /// Called by tool contact scripts when their active element
        /// leaves this surface.
        /// </summary>
        public void NotifyContactEnd(TradeToolBase tool)
        {
            onToolContactEnd?.Invoke(tool);
        }
    }
}
