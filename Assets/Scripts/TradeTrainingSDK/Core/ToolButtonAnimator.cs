// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Tool Button Animator
// Reusable component that animates a power-button child transform between
// a rest position and a pressed offset. Driven by TradeToolBase events or
// any external caller via SetPressed().
//
// Quest-Optimised: single Vector3.Lerp per frame, no allocations.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

namespace PTTI.TradeTrainingSDK
{
    /// <summary>
    /// Attach to the button child GameObject on any tool.
    /// Call <see cref="SetPressed"/> to animate the press/release.
    /// </summary>
    public class ToolButtonAnimator : MonoBehaviour
    {
        [Tooltip("Local-space position offset when the button is pressed down.")]
        [SerializeField] private Vector3 pressOffset = new Vector3(0f, -0.003f, 0f);

        [Tooltip("Speed of the press/release animation (higher = snappier).")]
        [SerializeField] private float animSpeed = 15f;

        private Vector3 restLocalPos;
        private bool isPressed;

        private void Awake()
        {
            restLocalPos = transform.localPosition;
        }

        private void Update()
        {
            Vector3 target = isPressed ? restLocalPos + pressOffset : restLocalPos;
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, target, animSpeed * Time.deltaTime);
        }

        /// <summary>Set the button to the pressed or released state.</summary>
        public void SetPressed(bool pressed) => isPressed = pressed;

        /// <summary>Snap the button to rest position immediately.</summary>
        public void ResetButton()
        {
            isPressed = false;
            transform.localPosition = restLocalPos;
        }
    }
}
