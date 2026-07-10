using UnityEngine;

public enum ToolCategory
{
    Cutting,
    Fastening,
    Measuring,
    Electrical,
    Finishing,
    Diagnostic,
}

public interface IToolBehaviour
{
    // Fires once, immediately after Addressables finishes instantiating the prefab.
    void OnToolSpawned(ToolDefinition definition);

    // Fires once the tool is the actively-held tool in the player's hand.
    void OnToolEquipped();

    // Fires right before ToolSpawner releases this instance back to Addressables.
    void OnToolStored();
}