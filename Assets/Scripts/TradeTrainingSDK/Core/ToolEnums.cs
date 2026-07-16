// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Core Enumerations
// Lightweight enum definitions shared across the entire SDK.
// No runtime cost — enums compile to plain integers.
// ─────────────────────────────────────────────────────────────────────────────

namespace PTTI.TradeTrainingSDK
{
    /// <summary>
    /// Broad classification of a trade tool.
    /// Used for filtering, validation, and analytics.
    /// </summary>
    public enum ToolCategory
    {
        Cutting,
        Fastening,
        Measuring,
        Electrical,
        Finishing,
        Diagnostic,
        Grinding,
        Drilling,
        Clamping,
        Plumbing,
        Welding,
        Safety
    }

    /// <summary>
    /// Physical material of a workpiece surface.
    /// Tools react differently depending on the surface they touch.
    /// </summary>
    public enum SurfaceMaterialType
    {
        Metal,
        Wood,
        Plastic,
        Wire,
        Concrete,
        Drywall,
        Pipe,
        Glass,
        Rubber,
        Fabric
    }

    /// <summary>
    /// Runtime state machine for a tool.
    /// Idle → Held → Running → InUse (and back).
    /// </summary>
    public enum ToolState
    {
        /// <summary>On the table or floor — not held by anyone.</summary>
        Idle,
        /// <summary>Gripped by a student but not activated.</summary>
        Held,
        /// <summary>Activated (motor on, etc.) but not touching a workpiece.</summary>
        Running,
        /// <summary>Activated AND actively working on a surface.</summary>
        InUse
    }

    /// <summary>
    /// Which hand(s) can grab a particular grip point.
    /// </summary>
    public enum HandPreference
    {
        Both,
        LeftOnly,
        RightOnly
    }

    /// <summary>
    /// Type of VR grip interaction.
    /// </summary>
    public enum GripType
    {
        /// <summary>Full hand grab (squeeze controller).</summary>
        Grab,
        /// <summary>Index finger trigger pull.</summary>
        Trigger,
        /// <summary>Thumb + index pinch (hand-tracking).</summary>
        Pinch
    }
}
