using UnityEngine;

namespace PTTI.TradeTrainingSDK.Tools
{
    [RequireComponent(typeof(Collider))]
    public class JigsawBladeContact : MonoBehaviour
    {
        [Tooltip("The JigsawTool on the parent/root jigsaw object.")]
        [SerializeField] private JigsawTool jigsawTool;

        [Tooltip("Optional spark particle system, parented to this same tip object.")]
        [SerializeField] private ParticleSystem sparkEffect;

        private int contactCount;

        private void Reset()
        {
            jigsawTool = GetComponentInParent<JigsawTool>();
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var surface = other.GetComponentInParent<ToolContactSurface>();
            if (surface == null || !surface.IsActive) return;

            contactCount++;
            if (contactCount == 1 && jigsawTool != null)
            {
                jigsawTool.SetInUse(true);
                surface.NotifyContact(jigsawTool);
                if (sparkEffect != null) sparkEffect.Play();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            var surface = other.GetComponentInParent<ToolContactSurface>();
            if (surface == null || !surface.IsActive) return;

            // Only perform cutting logic if the tool is actively running
            if (jigsawTool == null || !jigsawTool.IsActivated)
            {
                // If it's a metal sheet, break the stroke so resuming later doesn't draw a bridging line
                var sheet = other.GetComponentInParent<MetalSheet>();
                if (sheet != null) sheet.EndCutStroke();
                return;
            }

            // Tell the tool it is actively being used (for haptics/audio/state tracking)
            jigsawTool.SetInUse(true);

            // If the surface is a MetalSheet, send it cut points
            var metalSheet = other.GetComponentInParent<MetalSheet>();
            if (metalSheet != null)
            {
                metalSheet.AddCutPoint(transform.position);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var surface = other.GetComponentInParent<ToolContactSurface>();
            if (surface == null) return;

            var metalSheet = other.GetComponentInParent<MetalSheet>();
            if (metalSheet != null) metalSheet.EndCutStroke();

            contactCount = Mathf.Max(0, contactCount - 1);
            if (contactCount == 0)
            {
                if (jigsawTool != null) jigsawTool.SetInUse(false);
                if (sparkEffect != null) sparkEffect.Stop();
                
                if (surface != null) surface.NotifyContactEnd(jigsawTool);
            }
        }
    }
}
