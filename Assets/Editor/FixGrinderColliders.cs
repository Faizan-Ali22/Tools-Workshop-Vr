using UnityEngine;
using UnityEditor;

using System.Collections.Generic;

public class FixGrinderCollidersWindow : EditorWindow
{
    [MenuItem("Tools/Fix Grinder Colliders")]
    public static void FixGrinder()
    {
        // Find the Angle Grinder in the scene
        GameObject grinder = GameObject.Find("Angle Grinder");
        if (grinder == null)
        {
            Debug.LogError("Could not find 'Angle Grinder' in the scene.");
            return;
        }

        // 1. Remove invalid MeshColliders from GrabPoints
        MeshCollider[] meshColliders = grinder.GetComponentsInChildren<MeshCollider>(true);
        foreach (var mc in meshColliders)
        {
            if (mc.sharedMesh == null)
            {
                Debug.Log($"Removing empty MeshCollider from {mc.gameObject.name}");
                DestroyImmediate(mc);
            }
        }

        // 2. Clear XRGrabInteractable colliders list
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable = grinder.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabInteractable != null)
        {
            grabInteractable.colliders.Clear();
            Debug.Log("Cleared invalid colliders from XRGrabInteractable.");
        }

        // 3. Add valid BoxCollider to the body
        Transform body = grinder.transform.Find("body");
        if (body != null)
        {
            BoxCollider bodyCollider = body.gameObject.GetComponent<BoxCollider>();
            if (bodyCollider == null)
            {
                bodyCollider = body.gameObject.AddComponent<BoxCollider>();
                // Approximate bounds for the grinder body
                bodyCollider.center = new Vector3(0f, 0f, 0.05f);
                bodyCollider.size = new Vector3(0.08f, 0.08f, 0.3f);
                Debug.Log("Added BoxCollider to grinder body.");
            }
            
            if (grabInteractable != null)
                grabInteractable.colliders.Add(bodyCollider);
        }

        // 4. Add valid CapsuleCollider to the Handle
        Transform handle = body != null ? body.Find("Handle") : grinder.transform.Find("body/Handle");
        if (handle != null)
        {
            CapsuleCollider handleCollider = handle.gameObject.GetComponent<CapsuleCollider>();
            if (handleCollider == null)
            {
                handleCollider = handle.gameObject.AddComponent<CapsuleCollider>();
                handleCollider.radius = 0.025f;
                handleCollider.height = 0.15f;
                // Depending on orientation, usually X or Z
                handleCollider.direction = 0; // X axis
                Debug.Log("Added CapsuleCollider to grinder handle.");
            }
            
            if (grabInteractable != null)
                grabInteractable.colliders.Add(handleCollider);
        }

        Debug.Log("Grinder Colliders Fixed! It should now be grabbable.");
    }
}
