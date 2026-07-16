// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Multi-Grip Interactable
// Companion component for XRGrabInteractable that enables two-hand grip.
// Configures the XRGrabInteractable for multi-select on Awake and tracks
// the number of active grips.
//
// XRI 3.x already handles two-hand manipulation natively when selectMode
// is set to Multiple — this component automates that setup and exposes
// useful state for tool scripts (IsTwoHandGrip, GripCount).
//
// Quest-Optimised: event-driven grip counting, no Update overhead.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PTTI.TradeTrainingSDK
{
    /// <summary>
    /// Add alongside XRGrabInteractable on any tool that supports
    /// two-hand operation (e.g. grinder with side handle, large saw).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class MultiGripInteractable : MonoBehaviour
    {
        [Header("Secondary Grip")]
        [Tooltip("Transform for the secondary hand grip (e.g. side handle). " +
                 "Add this to the XRGrabInteractable's 'Attach Transform' list.")]
        [SerializeField] private Transform secondaryGripPoint;

        private XRGrabInteractable grabInteractable;
        private int currentGripCount;

        /// <summary>True when two or more hands are gripping the tool.</summary>
        public bool IsTwoHandGrip => currentGripCount >= 2;
        /// <summary>Number of hands currently gripping the tool.</summary>
        public int GripCount => currentGripCount;
        /// <summary>The secondary grip point transform.</summary>
        public Transform SecondaryGripPoint => secondaryGripPoint;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            // Enable multi-select so two hands can grab simultaneously.
            grabInteractable.selectMode = InteractableSelectMode.Multiple;
        }

        private void OnEnable()
        {
            grabInteractable.selectEntered.AddListener(OnGripAdded);
            grabInteractable.selectExited.AddListener(OnGripRemoved);
        }

        private void OnDisable()
        {
            grabInteractable.selectEntered.RemoveListener(OnGripAdded);
            grabInteractable.selectExited.RemoveListener(OnGripRemoved);
        }

        private void OnGripAdded(SelectEnterEventArgs args)
        {
            currentGripCount++;
        }

        private void OnGripRemoved(SelectExitEventArgs args)
        {
            currentGripCount = Mathf.Max(0, currentGripCount - 1);
        }
    }
}
