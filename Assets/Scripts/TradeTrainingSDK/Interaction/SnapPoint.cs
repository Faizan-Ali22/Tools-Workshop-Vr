// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Snap Point
// Defines a position where a part can snap into place during assembly.
// Correct parts snap; incorrect parts are rejected with an event.
//
// Quest-Optimised:
//  • No Update loop — purely called by assembly logic or OnTrigger events.
//  • Highlight uses MaterialPropertyBlock (no material instance allocations).
//  • Editor gizmos wrapped in UNITY_EDITOR.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.Events;

namespace PTTI.TradeTrainingSDK
{
    /// <summary>
    /// Assembly snap point. Place on empty GameObjects at the exact
    /// position/rotation where a part should be installed.
    /// </summary>
    public class SnapPoint : MonoBehaviour
    {
        // ══════════════════ Inspector ══════════════════

        [Header("Snap Configuration")]
        [Tooltip("Distance at which the part begins to attract (magnetic pull).")]
        [SerializeField] private float magneticRange = 0.1f;
        [Tooltip("Distance at which the part locks into place.")]
        [SerializeField] private float snapThreshold = 0.03f;
        [Tooltip("If set, only objects with this tag can snap here.")]
        [SerializeField] private string acceptedTag = "";
        [Tooltip("If set, only objects whose name contains this string can snap here.")]
        [SerializeField] private string acceptedPartName = "";

        [Header("Snap Behaviour")]
        [Tooltip("Also match the part's rotation to this snap point.")]
        [SerializeField] private bool snapRotation = true;

        [Header("Events")]
        public UnityEvent<GameObject> onPartSnapped;
        public UnityEvent<GameObject> onPartRejected;
        public UnityEvent             onPartRemoved;

        [Header("Visual Feedback")]
        [Tooltip("Optional renderer to highlight when a valid part is nearby.")]
        [SerializeField] private Renderer highlightRenderer;
        [SerializeField] private Color highlightColor = new Color(0f, 1f, 0.5f, 0.5f);

        // ══════════════════ Internal State ══════════════════

        private GameObject snappedPart;
        private bool isOccupied;
        private Color originalColor;
        private MaterialPropertyBlock propBlock;
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

        // ══════════════════ Public Properties ══════════════════

        public bool       IsOccupied  => isOccupied;
        public GameObject SnappedPart => snappedPart;

        // ══════════════════ Lifecycle ══════════════════

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            if (highlightRenderer != null && highlightRenderer.sharedMaterial != null)
            {
                var mat = highlightRenderer.sharedMaterial;
                originalColor = mat.HasProperty(BaseColorID)
                    ? mat.GetColor(BaseColorID)
                    : Color.white;
            }
        }

        // ══════════════════ Public API ══════════════════

        /// <summary>
        /// Attempt to snap a part to this point.
        /// Returns true if accepted, false if rejected or already occupied.
        /// </summary>
        public bool TrySnap(GameObject part)
        {
            if (isOccupied) return false;

            if (!IsAccepted(part))
            {
                onPartRejected?.Invoke(part);
                return false;
            }

            float distance = Vector3.Distance(part.transform.position, transform.position);
            if (distance > magneticRange) return false;

            // Snap the part into place
            isOccupied = true;
            snappedPart = part;
            part.transform.position = transform.position;
            if (snapRotation)
                part.transform.rotation = transform.rotation;

            // Make part kinematic so it stays put
            var rb = part.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            SetHighlight(false);
            onPartSnapped?.Invoke(part);
            return true;
        }

        /// <summary>Remove and release the currently snapped part.</summary>
        public void RemovePart()
        {
            if (!isOccupied || snappedPart == null) return;

            var rb = snappedPart.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;

            snappedPart = null;
            isOccupied = false;
            onPartRemoved?.Invoke();
        }

        /// <summary>Check if a part is within magnetic attraction range.</summary>
        public bool IsInRange(GameObject part)
        {
            return Vector3.Distance(part.transform.position, transform.position) <= magneticRange;
        }

        /// <summary>Toggle the highlight on the optional renderer.</summary>
        public void SetHighlight(bool on)
        {
            if (highlightRenderer == null) return;
            highlightRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorID, on ? highlightColor : originalColor);
            highlightRenderer.SetPropertyBlock(propBlock);
        }

        // ══════════════════ Internals ══════════════════

        private bool IsAccepted(GameObject part)
        {
            if (!string.IsNullOrEmpty(acceptedTag) && !part.CompareTag(acceptedTag))
                return false;
            if (!string.IsNullOrEmpty(acceptedPartName) && !part.name.Contains(acceptedPartName))
                return false;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Inner sphere = snap threshold
            Gizmos.color = isOccupied ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, snapThreshold);
            // Outer sphere = magnetic range
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, magneticRange);
        }
#endif
    }
}
