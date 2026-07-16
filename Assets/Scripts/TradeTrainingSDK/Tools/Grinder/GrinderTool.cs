// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Angle Grinder Tool
//
// Concrete implementation of TradeToolBase for the angle grinder.
//
// Behaviour:
//  1. GRIP  — Pick up from body or side handle (multi-angle via GrabPoints).
//  2. ACTIVATE (trigger press) — Button animates down, blade spins up with
//     realistic acceleration. Haptics begin.
//  3. CONTACT — Spinning blade touches a ToolContactSurface → sparks emit,
//     grinding audio plays, haptics increase. State → InUse.
//  4. DEACTIVATE (trigger release) — Motor off, blade decelerates with inertia.
//  5. DROP — Motor off, blade coasts to stop.
//  6. SAFETY — Blade touching surface with motor off → IncorrectUse event.
//
// Quest-Optimised:
//  • Blade rotation via Transform.Rotate (zero allocation).
//  • Speed uses Mathf.MoveTowards (no AnimationCurves / allocation).
//  • RPM → DPS pre-computed in Awake.
//  • Haptics throttled via base class interval.
//  • Spark particles capped in ParticleSystem settings (configure max 20).
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Import SDK core namespace (GrinderTool is in the .Tools sub-namespace)
using PTTI.TradeTrainingSDK;

namespace PTTI.TradeTrainingSDK.Tools
{
    public class GrinderTool : TradeToolBase
    {
        // ══════════════════ Inspector ══════════════════

        [Header("Grinder — Blade")]
        [Tooltip("The blade/disc child transform that spins.")]
        [SerializeField] private Transform bladeTransform;
        [Tooltip("Local axis the blade spins around (usually forward for a disc).")]
        [SerializeField] private Vector3 spinAxis = Vector3.forward;

        [Header("Grinder — Speed")]
        [Tooltip("Revolutions per minute at idle (no load).")]
        [SerializeField] private float idleRPM = 500f;
        [Tooltip("Revolutions per minute under load (grinding a surface).")]
        [SerializeField] private float maxRPM = 2000f;
        [Tooltip("Seconds from standstill to full idle speed.")]
        [SerializeField] private float spinUpDuration = 0.8f;
        [Tooltip("Seconds to coast from full speed to stop after trigger release.")]
        [SerializeField] private float spinDownDuration = 1.5f;

        [Header("Grinder — Button")]
        [Tooltip("Optional ToolButtonAnimator on the power button child.")]
        [SerializeField] private ToolButtonAnimator buttonAnimator;

        [Header("Grinder — Particles")]
        [Tooltip("Spark particle system positioned near the blade edge.")]
        [SerializeField] private ParticleSystem sparkEffect;

        // ══════════════════ Runtime State ══════════════════

        private float currentSpeedNormalized;   // 0 = stopped, 1 = full speed
        private float targetSpeedNormalized;     // What we're accelerating toward
        private float idleDPS;                   // Idle degrees per second
        private float maxDPS;                    // Load degrees per second

        /// <summary>Current blade speed as a 0–1 value. Useful for audio pitch or UI.</summary>
        public float SpeedNormalized => currentSpeedNormalized;

        // ══════════════════ Lifecycle ══════════════════

        protected override void Awake()
        {
            base.Awake();

            // Pre-compute degrees per second:  RPM × 360° / 60s = RPM × 6
            idleDPS = idleRPM * 6f;
            maxDPS  = maxRPM * 6f;

            // Set identity
            toolCategory = ToolCategory.Grinding;
            if (string.IsNullOrEmpty(toolName))
                toolName = "Angle Grinder";
        }

        // ══════════════════ Tool Hooks ══════════════════

        protected override void OnToolActivated(ActivateEventArgs args)
        {
            targetSpeedNormalized = 1f;
            if (buttonAnimator != null) buttonAnimator.SetPressed(true);
        }

        protected override void OnToolDeactivated(DeactivateEventArgs args)
        {
            targetSpeedNormalized = 0f;
            if (buttonAnimator != null) buttonAnimator.SetPressed(false);
        }

        protected override void OnToolReleased(SelectExitEventArgs args)
        {
            targetSpeedNormalized = 0f;
            if (buttonAnimator != null) buttonAnimator.SetPressed(false);
        }

        /// <summary>Called every frame while activated — accelerate blade.</summary>
        protected override void OnToolRunningUpdate()
        {
            AccelerateBlade();
            RotateBlade();
        }

        // ══════════════════ Update Override ══════════════════

        protected override void Update()
        {
            // Handle coast-down when motor is off but blade is still spinning
            if (!IsActivated && currentSpeedNormalized > 0f)
            {
                DecelerateBlade();
                RotateBlade();
            }

            // Base handles: OnToolRunningUpdate (when activated), haptics, audio
            base.Update();
        }

        // ══════════════════ Audio / Haptic Overrides ══════════════════

        /// <summary>
        /// Motor audio plays while blade is spinning (including coast-down),
        /// not just when the trigger is held.
        /// </summary>
        protected override void UpdateToolAudio()
        {
            bool motorShouldPlay = currentSpeedNormalized > 0.01f;
            FadeAudio(motorAudioSource, motorShouldPlay);
            FadeAudio(useAudioSource, IsInUse);
        }

        /// <summary>
        /// Haptics scale with blade speed, including during coast-down.
        /// </summary>
        protected override void UpdateToolHaptics()
        {
            if (currentSpeedNormalized > 0.01f && IsHeld)
            {
                float intensity = IsInUse
                    ? activeHapticIntensity
                    : idleHapticIntensity * currentSpeedNormalized;
                SendHapticPulse(intensity);
            }
        }

        // ══════════════════ Blade Speed ══════════════════

        private void AccelerateBlade()
        {
            float rate = spinUpDuration > 0f ? 1f / spinUpDuration : 100f;
            currentSpeedNormalized = Mathf.MoveTowards(
                currentSpeedNormalized, targetSpeedNormalized, rate * Time.deltaTime);
        }

        private void DecelerateBlade()
        {
            float rate = spinDownDuration > 0f ? 1f / spinDownDuration : 100f;
            currentSpeedNormalized = Mathf.MoveTowards(
                currentSpeedNormalized, 0f, rate * Time.deltaTime);
        }

        private void RotateBlade()
        {
            if (bladeTransform == null) return;
            float targetDPS = IsInUse ? maxDPS : idleDPS;
            float dps = targetDPS * currentSpeedNormalized;
            bladeTransform.Rotate(spinAxis, dps * Time.deltaTime, Space.Self);
        }

        // ══════════════════ Contact API (called by GrinderBladeContact) ══════════════════

        /// <summary>
        /// Called by GrinderBladeContact when the spinning disc first touches a surface.
        /// </summary>
        public void OnBladeContactStart()
        {
            if (IsActivated)
            {
                SetInUse(true);
                if (sparkEffect != null && !sparkEffect.isPlaying)
                    sparkEffect.Play();
            }
            else
            {
                // Touching a surface with the motor off is unsafe / incorrect
                ReportIncorrectUse("Blade contact without motor running");
            }
        }

        /// <summary>
        /// Called by GrinderBladeContact when the disc leaves the surface.
        /// </summary>
        public void OnBladeContactEnd()
        {
            SetInUse(false);
            if (sparkEffect != null && sparkEffect.isPlaying)
                sparkEffect.Stop();
        }

        // ══════════════════ Reset ══════════════════

        public override void ResetTool()
        {
            base.ResetTool();
            currentSpeedNormalized = 0f;
            targetSpeedNormalized  = 0f;
            if (buttonAnimator != null) buttonAnimator.ResetButton();
            if (sparkEffect != null)    sparkEffect.Stop();
        }
    }
}
