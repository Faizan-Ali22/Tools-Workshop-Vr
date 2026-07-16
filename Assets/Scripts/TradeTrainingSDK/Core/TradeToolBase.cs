// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Trade Tool Base
// Abstract base class for EVERY trade training tool in the SDK.
//
// Responsibilities:
//  • XRI 3.x grab / activate / deactivate lifecycle
//  • Tool state machine  (Idle → Held → Running → InUse)
//  • Audio management    (motor idle + active-use, with smooth fade)
//  • Haptic feedback     (throttled to save Quest CPU)
//  • Usage tracking      (correct / incorrect events → ToolUsageTracker)
//  • Instructor controls (force-reset, disable)
//
// Quest-Optimised:
//  • Zero allocations in Update (no LINQ, no foreach on non-struct enumerators)
//  • Haptic pulses throttled via configurable interval
//  • All component references cached in Awake
//  • Audio fade uses Mathf.MoveTowards (no coroutines / AnimationCurves)
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PTTI.TradeTrainingSDK
{
    /// <summary>
    /// Inherit from this class to build any trade tool (grinder, drill, wrench, etc.).
    /// Override <see cref="OnToolRunningUpdate"/> for per-frame tool behaviour and
    /// the various On* virtual hooks for lifecycle events.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public abstract class TradeToolBase : MonoBehaviour
    {
        // ═══════════════════════ Inspector Fields ═══════════════════════

        [Header("Tool Identity")]
        [Tooltip("Display name shown to students and in tracking logs.")]
        [SerializeField] protected string toolName = "Unnamed Tool";
        [SerializeField] protected ToolCategory toolCategory;

        [Header("Audio")]
        [Tooltip("Looping sound when the tool motor is running (idle).")]
        [SerializeField] protected AudioSource motorAudioSource;
        [Tooltip("Looping sound when the tool is actively working on a surface.")]
        [SerializeField] protected AudioSource useAudioSource;
        [Tooltip("Volume fade speed (units per second). Higher = snappier.")]
        [SerializeField] protected float audioFadeSpeed = 4f;

        [Header("Haptics")]
        [Tooltip("Controller vibration intensity when the motor is running.")]
        [SerializeField, Range(0f, 1f)] protected float idleHapticIntensity = 0.1f;
        [Tooltip("Controller vibration intensity when actively grinding/cutting/etc.")]
        [SerializeField, Range(0f, 1f)] protected float activeHapticIntensity = 0.5f;
        [Tooltip("Seconds between haptic pulses. Higher = less Quest CPU load.")]
        [SerializeField] protected float hapticInterval = 0.1f;

        [Header("Events (Inspector Hookups)")]
        public UnityEvent onToolGrabbed;
        public UnityEvent onToolReleased;
        public UnityEvent onToolActivated;
        public UnityEvent onToolDeactivated;
        public UnityEvent onCorrectUse;
        public UnityEvent onIncorrectUse;
        public UnityEvent onToolReset;

        // ═══════════════════════ Cached References ═══════════════════════

        protected XRGrabInteractable grabInteractable;
        protected Rigidbody          toolRigidbody;
        protected ToolUsageTracker   usageTracker;

        // ═══════════════════════ Runtime State ═══════════════════════

        /// <summary>Current tool state. Read-only outside the class hierarchy.</summary>
        public ToolState CurrentState { get; protected set; } = ToolState.Idle;

        /// <summary>True when the tool is gripped by a student.</summary>
        public bool IsHeld      => CurrentState != ToolState.Idle;
        /// <summary>True when the trigger is pressed (motor running).</summary>
        public bool IsActivated => CurrentState == ToolState.Running || CurrentState == ToolState.InUse;
        /// <summary>True when the tool is actively working on a surface.</summary>
        public bool IsInUse     => CurrentState == ToolState.InUse;
        /// <summary>Display name of this tool.</summary>
        public string       ToolName     => toolName;
        /// <summary>Tool category classification.</summary>
        public ToolCategory ToolCategory => toolCategory;

        // Haptic throttle timer
        private float lastHapticTime;
        // Grab duration tracking
        private float grabStartTime;

        // ═══════════════════════ Unity Lifecycle ═══════════════════════

        protected virtual void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            toolRigidbody    = GetComponent<Rigidbody>();
            usageTracker     = GetComponent<ToolUsageTracker>();
            if (usageTracker == null)
                usageTracker = gameObject.AddComponent<ToolUsageTracker>();
        }

        protected virtual void OnEnable()
        {
            grabInteractable.selectEntered.AddListener(HandleSelectEntered);
            grabInteractable.selectExited.AddListener(HandleSelectExited);
            grabInteractable.activated.AddListener(HandleActivated);
            grabInteractable.deactivated.AddListener(HandleDeactivated);
        }

        protected virtual void OnDisable()
        {
            grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
            grabInteractable.selectExited.RemoveListener(HandleSelectExited);
            grabInteractable.activated.RemoveListener(HandleActivated);
            grabInteractable.deactivated.RemoveListener(HandleDeactivated);
        }

        /// <summary>
        /// Main update loop. Override in subclasses but always call base.Update().
        /// </summary>
        protected virtual void Update()
        {
            if (IsActivated)
                OnToolRunningUpdate();

            UpdateToolHaptics();
            UpdateToolAudio();
        }

        // ═══════════════════════ XRI Event Handlers ═══════════════════════

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            SetState(ToolState.Held);
            grabStartTime = Time.time;
            usageTracker.RecordEvent(toolName, "Grab", true);
            onToolGrabbed?.Invoke();
            OnToolGrabbed(args);
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            // Deactivate first if still running
            if (IsActivated)
                DeactivateInternal();

            float gripDuration = Time.time - grabStartTime;
            usageTracker.RecordEvent(toolName, "Release", true, gripDuration);

            SetState(ToolState.Idle);
            onToolReleased?.Invoke();
            OnToolReleased(args);
        }

        private void HandleActivated(ActivateEventArgs args)
        {
            SetState(ToolState.Running);
            usageTracker.RecordEvent(toolName, "Activate", true);
            onToolActivated?.Invoke();
            OnToolActivated(args);
        }

        private void HandleDeactivated(DeactivateEventArgs args)
        {
            DeactivateInternal();
            onToolDeactivated?.Invoke();
            OnToolDeactivated(args);
        }

        private void DeactivateInternal()
        {
            SetState(IsHeld ? ToolState.Held : ToolState.Idle);
        }

        // ═══════════════════════ Abstract / Virtual Hooks ═══════════════════════

        /// <summary>
        /// Called every frame while the tool is activated (Running or InUse).
        /// Implement tool-specific behaviour here (blade spin, oscillation, etc.).
        /// </summary>
        protected abstract void OnToolRunningUpdate();

        /// <summary>Called once when the student grabs the tool.</summary>
        protected virtual void OnToolGrabbed(SelectEnterEventArgs args) { }

        /// <summary>Called once when the student releases / drops the tool.</summary>
        protected virtual void OnToolReleased(SelectExitEventArgs args) { }

        /// <summary>Called once when the trigger is pressed (tool activated).</summary>
        protected virtual void OnToolActivated(ActivateEventArgs args) { }

        /// <summary>Called once when the trigger is released (tool deactivated).</summary>
        protected virtual void OnToolDeactivated(DeactivateEventArgs args) { }

        /// <summary>
        /// Override to define what constitutes correct use for this specific tool.
        /// Called when the tool first contacts a surface while activated.
        /// Return false to flag an incorrect-use event.
        /// </summary>
        protected virtual bool IsUsedCorrectly() => true;

        // ═══════════════════════ Public API ═══════════════════════

        /// <summary>
        /// Called by contact scripts (e.g. GrinderBladeContact) when the tool's
        /// active element touches or leaves a workpiece surface.
        /// </summary>
        public void SetInUse(bool inUse)
        {
            if (inUse && CurrentState == ToolState.Running)
            {
                SetState(ToolState.InUse);

                if (IsUsedCorrectly())
                {
                    usageTracker.RecordEvent(toolName, "CorrectUse", true);
                    onCorrectUse?.Invoke();
                }
                else
                {
                    usageTracker.RecordEvent(toolName, "IncorrectUse", false);
                    onIncorrectUse?.Invoke();
                }
            }
            else if (!inUse && CurrentState == ToolState.InUse)
            {
                SetState(ToolState.Running);
            }
        }

        /// <summary>
        /// Report an incorrect-use attempt (e.g. blade touching surface with motor off).
        /// </summary>
        public void ReportIncorrectUse(string reason = "")
        {
            string actionType = string.IsNullOrEmpty(reason)
                ? "IncorrectUse"
                : "IncorrectUse";
            usageTracker.RecordEvent(toolName, actionType, false);
            onIncorrectUse?.Invoke();
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(reason))
                Debug.LogWarning($"[{toolName}] Incorrect use: {reason}", this);
#endif
        }

        /// <summary>Reset tool to factory-fresh state.</summary>
        public virtual void ResetTool()
        {
            SetState(ToolState.Idle);
            onToolReset?.Invoke();
        }

        /// <summary>
        /// Instructor-only: force-drop and reset the tool.
        /// Safe to call even if no student is holding it.
        /// </summary>
        public void InstructorReset()
        {
            if (grabInteractable.isSelected)
            {
                var manager = grabInteractable.interactionManager;
                if (manager != null)
                {
                    // Copy to a temp list to avoid modification during iteration.
                    // This allocation is acceptable — instructor resets are rare.
                    var interactors = new List<IXRSelectInteractor>(
                        grabInteractable.interactorsSelecting);
                    for (int i = 0; i < interactors.Count; i++)
                        manager.SelectExit(interactors[i], (IXRSelectInteractable)grabInteractable);
                }
            }
            ResetTool();
            usageTracker.RecordEvent(toolName, "InstructorReset", true);
        }

        // ═══════════════════════ Audio & Haptics ═══════════════════════

        /// <summary>
        /// Override to customise which audio sources play during which states.
        /// Default: motor audio when activated, use audio when InUse.
        /// </summary>
        protected virtual void UpdateToolAudio()
        {
            FadeAudio(motorAudioSource, IsActivated);
            FadeAudio(useAudioSource, IsInUse);
        }

        /// <summary>
        /// Override to customise haptic feedback.
        /// Default: idle intensity when Running, active intensity when InUse.
        /// </summary>
        protected virtual void UpdateToolHaptics()
        {
            if (IsActivated)
            {
                float intensity = IsInUse ? activeHapticIntensity : idleHapticIntensity;
                SendHapticPulse(intensity);
            }
        }

        /// <summary>
        /// Smoothly fade an AudioSource volume toward target (0 or 1).
        /// Auto-starts/stops the source to save Quest audio channels.
        /// </summary>
        protected void FadeAudio(AudioSource source, bool shouldPlay)
        {
            if (source == null) return;
            float target = shouldPlay ? 1f : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target, audioFadeSpeed * Time.deltaTime);
            if (shouldPlay && !source.isPlaying) source.Play();
            if (!shouldPlay && source.volume <= 0.01f && source.isPlaying) source.Stop();
        }

        /// <summary>
        /// Send a haptic pulse to all controllers currently gripping this tool.
        /// Throttled by <see cref="hapticInterval"/> to limit Quest CPU overhead.
        /// </summary>
        protected void SendHapticPulse(float intensity)
        {
            if (Time.time - lastHapticTime < hapticInterval) return;
            lastHapticTime = Time.time;

            if (!grabInteractable.isSelected) return;

            var interactors = grabInteractable.interactorsSelecting;
            for (int i = 0, count = interactors.Count; i < count; i++)
            {
                if (interactors[i] is XRBaseInputInteractor controllerInteractor)
                {
                    controllerInteractor.xrController.SendHapticImpulse(intensity, hapticInterval);
                }
            }
        }

        // ═══════════════════════ Helpers ═══════════════════════

        protected void SetState(ToolState newState)
        {
            CurrentState = newState;
        }
    }
}
