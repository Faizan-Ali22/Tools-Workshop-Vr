using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ButtonToolSpawner : MonoBehaviour
{
    [Tooltip("Drag the Addressable Jigsaw Prefab here")]
    public AssetReferenceGameObject toolPrefab;

    [Tooltip("Leave empty to auto-find")]
    public XRInteractionManager interactionManager;

    private GameObject currentTool;
    private bool isSpawning = false;

    private void Awake()
    {
        if (interactionManager == null)
            interactionManager = FindFirstObjectByType<XRInteractionManager>();
        
    }

    // Accepts the dynamic event data from the button push
    public void SpawnAndEquipTool(SelectEnterEventArgs args)
    {
        if (currentTool != null || isSpawning) return;

        if (toolPrefab == null || !toolPrefab.RuntimeKeyIsValid())
        {
            Debug.LogError("ButtonToolSpawner: Prefab is missing or invalid.");
            return;
        }

        IXRSelectInteractor handInteractor = args.interactorObject as IXRSelectInteractor;
        if (handInteractor == null) return;

        StartCoroutine(SpawnRoutine(handInteractor));
    }

    private IEnumerator SpawnRoutine(IXRSelectInteractor hand)
    {
        isSpawning = true;

        var handle = Addressables.InstantiateAsync(toolPrefab);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            currentTool = handle.Result;
            XRGrabInteractable grabInteractable = currentTool.GetComponent<XRGrabInteractable>();

            if (grabInteractable != null)
            {
                // Wait one physics frame for colliders to register in PhysX
                yield return new WaitForFixedUpdate();

                // Force the hand to grab the spawned tool
                interactionManager.SelectEnter(hand, (IXRSelectInteractable)grabInteractable);
            }
        }
        
        isSpawning = false;
    }
}