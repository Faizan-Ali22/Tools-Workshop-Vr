using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class BladeContact : MonoBehaviour
{
    [Tooltip("Drag the 'jigsaww' root object here (the one with JigsawMachine.cs on it).")]
    [SerializeField] private JigsawMachine jigsawMachine;
 
    [Tooltip("Optional spark particle system, parented to this same tip object.")]
    [SerializeField] private ParticleSystem sparkEffect;
 
    private int contactCount;
 
    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }
 
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<MetalSheet>() == null) return;
 
        contactCount++;
        if (sparkEffect != null) sparkEffect.Play();
    }
 
    private void OnTriggerStay(Collider other)
    {
        var sheet = other.GetComponentInParent<MetalSheet>();
        if (sheet == null) return;
 
        if (jigsawMachine == null || !jigsawMachine.MotorOn)
        {
            // Trigger released while still resting on the sheet -> break the stroke so
            // resuming later doesn't draw a bridging line across the gap.
            sheet.EndCutStroke();
            return;
        }
 
        jigsawMachine.IsCutting = true;
        sheet.AddCutPoint(transform.position);
    }
 
    private void OnTriggerExit(Collider other)
    {
        var sheet = other.GetComponentInParent<MetalSheet>();
        if (sheet == null) return;
 
        sheet.EndCutStroke();
 
        contactCount = Mathf.Max(0, contactCount - 1);
        if (contactCount == 0)
        {
            if (jigsawMachine != null) jigsawMachine.IsCutting = false;
            if (sparkEffect != null) sparkEffect.Stop();
        }
    }
}