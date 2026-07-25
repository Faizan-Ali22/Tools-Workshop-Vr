using UnityEngine;
using PTTI.TradeTrainingSDK;

namespace PTTI.TradeTrainingSDK.Tools
{
    public class JigsawTool : TradeToolBase
    {
        [Header("Blade Configuration")]
        [Tooltip("Drag the 'Blade' child object here.")]
        [SerializeField] private Transform blade;
        [SerializeField] private Vector3 strokeAxis = Vector3.up;
        [SerializeField] private float strokeAmplitude = 0.006f;
        [SerializeField] private float strokeFrequency = 20f;

        private Vector3 bladeRestLocalPos;

        protected override void Awake()
        {
            base.Awake();
            if (blade != null) bladeRestLocalPos = blade.localPosition;
            
            if (string.IsNullOrEmpty(toolName) || toolName == "Unnamed Tool")
            {
                toolName = "Jigsaw";
            }
        }

        protected override void OnToolRunningUpdate()
        {
            if (blade != null)
            {
                // Animate blade
                float offset = Mathf.Sin(Time.time * strokeFrequency * Mathf.PI * 2f) * strokeAmplitude;
                blade.localPosition = bladeRestLocalPos + strokeAxis.normalized * offset;
            }
        }

        public override void ResetTool()
        {
            base.ResetTool();
            if (blade != null) blade.localPosition = bladeRestLocalPos;
        }

        protected override void OnToolDeactivated(UnityEngine.XR.Interaction.Toolkit.DeactivateEventArgs args)
        {
            base.OnToolDeactivated(args);
            if (blade != null) blade.localPosition = bladeRestLocalPos;
        }
    }
}
