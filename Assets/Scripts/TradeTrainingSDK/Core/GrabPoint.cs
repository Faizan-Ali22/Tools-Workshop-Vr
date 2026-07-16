// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Grab Point Marker
// Attach to empty child GameObjects positioned where the student's hand
// should grip the tool. The XRGrabInteractable on the root picks the
// nearest grab point automatically.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

namespace PTTI.TradeTrainingSDK
{
    /// <summary>
    /// Marks a Transform as a valid grab position on a trade tool.
    /// Place multiple GrabPoints on a tool to allow gripping from
    /// different directions and angles.
    /// </summary>
    public class GrabPoint : MonoBehaviour
    {
        [Tooltip("Which hand(s) can use this grip point.")]
        [SerializeField] private HandPreference handPreference = HandPreference.Both;

        [Tooltip("The VR grip type expected at this point.")]
        [SerializeField] private GripType gripType = GripType.Grab;

        [Tooltip("True = main body grip. False = secondary (e.g., side handle).")]
        [SerializeField] private bool isPrimaryGrip = true;

        // ── Public Read-Only Properties ──
        public HandPreference HandPreference => handPreference;
        public GripType       GripType       => gripType;
        public bool           IsPrimaryGrip  => isPrimaryGrip;

#if UNITY_EDITOR
        // Draw a small sphere in the Scene view so designers can see grab points.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isPrimaryGrip ? Color.green : Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, 0.02f);
            // Forward line shows the direction the palm faces
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.05f);
        }
#endif
    }
}
