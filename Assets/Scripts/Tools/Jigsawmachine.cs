using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;
// ATTACH TO: "jigsaww" (the root object — already has Rigidbody + XR Grab Interactable)
//
// This turns the trigger/grip "Activate" input into a motor on/off state,
// oscillates the Blade child while the motor runs, and exposes MotorOn / IsCutting
// so BladeContact.cs and MetalSheet.cs can react to it.
//
// NOTE: class names (ActivateEventArgs, SelectExitEventArgs, etc.) match
// XR Interaction Toolkit 3.x (the version bundled with Unity 6). If your project
// uses an older 2.x package, drop ".Interactables" / ".Interactors" from the two
[RequireComponent(typeof(XRGrabInteractable))]
public class JigsawMachine : MonoBehaviour
{
    [Header("Blade")]
    [Tooltip("Drag the 'Blade' child object here.")]
    [SerializeField] private Transform blade;
    [SerializeField] private Vector3 strokeAxis = Vector3.up;
    [SerializeField] private float strokeAmplitude = 0.006f;
    [SerializeField] private float strokeFrequency = 20f;

    [Header("Audio (Assign in Inspector)")]
    [SerializeField] private AudioSource motorAudio;    
    [SerializeField] private AudioSource cuttingAudio;  
    [SerializeField] private float audioFadeSpeed = 4f;

    [Header("Haptics (Vibration)")]
    [Tooltip("Vibration intensity when the motor is just running.")]
    [SerializeField] private float idleVibration = 0.1f;
    [Tooltip("Vibration intensity when the blade is actively cutting metal.")]
    [SerializeField] private float cuttingVibration = 0.5f;

    private XRGrabInteractable grabInteractable;
    private Vector3 bladeRestLocalPos;

    public bool MotorOn { get; private set; }
    public bool IsCutting { get; set; }

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (blade != null) bladeRestLocalPos = blade.localPosition;
    }

    private void OnEnable()
    {
        grabInteractable.activated.AddListener(OnActivated);
        grabInteractable.deactivated.AddListener(OnDeactivated);
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        grabInteractable.activated.RemoveListener(OnActivated);
        grabInteractable.deactivated.RemoveListener(OnDeactivated);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnActivated(ActivateEventArgs args) => MotorOn = true;
    private void OnDeactivated(DeactivateEventArgs args) => StopMotor();
    private void OnSelectExited(SelectExitEventArgs args) => StopMotor();

    private void StopMotor()
    {
        MotorOn = false;
        IsCutting = false;
        if (blade != null) blade.localPosition = bladeRestLocalPos;
    }

    private void Update()
    {
        if (MotorOn && blade != null)
        {
            // Animate blade
            float offset = Mathf.Sin(Time.time * strokeFrequency * Mathf.PI * 2f) * strokeAmplitude;
            blade.localPosition = bladeRestLocalPos + strokeAxis.normalized * offset;

            // Trigger Vibration
            if (grabInteractable.isSelected)
            {
                foreach (var interactor in grabInteractable.interactorsSelecting)
                {
                    if (interactor is XRBaseInputInteractor controllerInteractor)
                    {
                        float intensity = IsCutting ? cuttingVibration : idleVibration;
                        controllerInteractor.xrController.SendHapticImpulse(intensity, 0.1f);
                    }
                }
            }
        }

        UpdateAudioSource(motorAudio, MotorOn);
        UpdateAudioSource(cuttingAudio, MotorOn && IsCutting);
    }

    private void UpdateAudioSource(AudioSource source, bool shouldPlay)
    {
        if (source == null) return;
        float target = shouldPlay ? 1f : 0f;
        source.volume = Mathf.MoveTowards(source.volume, target, audioFadeSpeed * Time.deltaTime);
        if (shouldPlay && !source.isPlaying) source.Play();
        if (!shouldPlay && source.volume <= 0.001f && source.isPlaying) source.Stop();
    }
}