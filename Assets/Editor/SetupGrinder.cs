using UnityEngine;
using UnityEditor;
using PTTI.TradeTrainingSDK;
using PTTI.TradeTrainingSDK.Tools;

using System.Reflection;

public class SetupGrinderWindow : EditorWindow
{
    [MenuItem("Tools/Setup Grinder SDK")]
    public static void SetupGrinder()
    {
        // 1. Load Grinder FBX
        string grinderPath = "Assets/Env/Tools/Grinder fbx.fbx";
        GameObject grinderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(grinderPath);
        if (grinderPrefab == null)
        {
            Debug.LogError("Could not find Grinder FBX at " + grinderPath);
            return;
        }

        // Instantiate
        GameObject grinderInstance = (GameObject)PrefabUtility.InstantiatePrefab(grinderPrefab);
        PrefabUtility.UnpackPrefabInstance(grinderInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        grinderInstance.name = "Angle Grinder";

        // Add GrinderTool
        GrinderTool grinderTool = grinderInstance.AddComponent<GrinderTool>();

        // We might need reflection to set private serialized fields if there are no public properties.
        // Let's use SerializedObject for robust assignment.
        SerializedObject soTool = new SerializedObject(grinderTool);
        
        // Find Blade
        Transform body = grinderInstance.transform.Find("body");
        if (body != null)
        {
            Transform blade = body.Find("Blade");
            if (blade != null)
            {
                soTool.FindProperty("bladeTransform").objectReferenceValue = blade;
                
                // Blade Contact
                GameObject bladeContactObj = new GameObject("BladeContactTrigger");
                bladeContactObj.transform.SetParent(blade, false);
                CapsuleCollider bladeCol = bladeContactObj.AddComponent<CapsuleCollider>();
                bladeCol.isTrigger = true;
                bladeCol.radius = 0.05f;
                bladeCol.height = 0.15f;
                bladeCol.direction = 2; // Z-axis usually for disc
                
                GrinderBladeContact bladeContact = bladeContactObj.AddComponent<GrinderBladeContact>();
                SerializedObject soBladeContact = new SerializedObject(bladeContact);
                soBladeContact.FindProperty("grinderTool").objectReferenceValue = grinderTool;
                soBladeContact.ApplyModifiedProperties();
            }

            // Find Button
            Transform button = body.Find("Button");
            if (button != null)
            {
                ToolButtonAnimator btnAnimator = button.gameObject.AddComponent<ToolButtonAnimator>();
                SerializedObject soBtn = new SerializedObject(btnAnimator);
                soBtn.FindProperty("pressOffset").vector3Value = new Vector3(0, -0.003f, 0);
                soBtn.ApplyModifiedProperties();
                
                soTool.FindProperty("buttonAnimator").objectReferenceValue = btnAnimator;
            }

            // Handle
            Transform handle = body.Find("Handle");
            if (handle != null)
            {
                GameObject handleGrip = new GameObject("HandleGrabPoint");
                handleGrip.transform.SetParent(handle, false);
                GrabPoint gp = handleGrip.AddComponent<GrabPoint>();
                SerializedObject soGp = new SerializedObject(gp);
                soGp.FindProperty("isPrimaryGrip").boolValue = false;
                soGp.ApplyModifiedProperties();
            }

            // Grab points on body
            GameObject grip1 = new GameObject("PrimaryGrabPoint");
            grip1.transform.SetParent(body, false);
            grip1.transform.localPosition = new Vector3(0, 0, -0.1f);
            GrabPoint gp1 = grip1.AddComponent<GrabPoint>();
            SerializedObject soGp1 = new SerializedObject(gp1);
            soGp1.FindProperty("isPrimaryGrip").boolValue = true;
            soGp1.ApplyModifiedProperties();

            // Assign attach transform
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable = grinderInstance.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grabInteractable != null)
            {
                // Assign multiple attach points if possible, else just single
                grabInteractable.attachTransform = grip1.transform;
                // Since SDK 3.x, multiple interactable attach transforms are available but let's just use the main one if we can't easily serialize the list
            }
        }

        // 6. Audio Sources
        AudioSource audio1 = grinderInstance.AddComponent<AudioSource>();
        audio1.loop = true;
        audio1.playOnAwake = false;
        
        AudioSource audio2 = grinderInstance.AddComponent<AudioSource>();
        audio2.loop = true;
        audio2.playOnAwake = false;

        soTool.FindProperty("motorAudioSource").objectReferenceValue = audio1;
        soTool.FindProperty("useAudioSource").objectReferenceValue = audio2;

        // 7. Sparks (FlareMobile)
        string particlePath = "Assets/Standard Assets/ParticleSystems/Prefabs/FlareMobile.prefab";
        GameObject particlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(particlePath);
        if (particlePrefab != null)
        {
            GameObject particleInstance = (GameObject)PrefabUtility.InstantiatePrefab(particlePrefab);
            particleInstance.transform.SetParent(grinderInstance.transform, false);
            
            // Try to place near blade
            if (body != null)
            {
                Transform blade = body.Find("Blade");
                if (blade != null) particleInstance.transform.position = blade.position + new Vector3(0, -0.05f, 0.05f);
            }
            
            ParticleSystem ps = particleInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.maxParticles = 20;
                main.startLifetime = 0.3f;
                main.startSpeed = 3f;
                main.playOnAwake = false;
                
                soTool.FindProperty("sparkEffect").objectReferenceValue = ps;
            }
        }

        soTool.ApplyModifiedProperties();

        // 8. Test Workpiece
        GameObject workpiece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        workpiece.name = "Test Workpiece";
        workpiece.transform.position = grinderInstance.transform.position + new Vector3(0, -0.5f, 0);
        workpiece.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
        
        ToolContactSurface contactSurface = workpiece.AddComponent<ToolContactSurface>();
        // Reflection for Material Type if possible, else default
        
        Debug.Log("Grinder SDK Setup Complete!");
    }
}
