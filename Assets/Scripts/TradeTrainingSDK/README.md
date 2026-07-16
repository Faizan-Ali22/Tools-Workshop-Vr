# PTTI Trade Training SDK

A reusable Unity SDK for building interactive VR trade training tools. Built on **XR Interaction Toolkit 3.5.1** for **Unity 6** targeting **Meta Quest 2/3**.

---

## Project Structure

```
Assets/Scripts/TradeTrainingSDK/
├── Core/
│   ├── ToolEnums.cs            — Shared enumerations (ToolCategory, ToolState, etc.)
│   ├── GrabPoint.cs            — Marks grab positions on tools (editor gizmos)
│   ├── ToolUsageTracker.cs     — Records correct/incorrect usage events (JSON export)
│   ├── ToolButtonAnimator.cs   — Animates power buttons on tools
│   └── TradeToolBase.cs        — Abstract base class for every tool
├── Interaction/
│   ├── ToolContactSurface.cs   — Marks workpiece surfaces tools can interact with
│   ├── SnapPoint.cs            — Assembly snap-to-position system
│   └── MultiGripInteractable.cs — Two-hand grab support
└── Tools/
    └── Grinder/
        ├── GrinderTool.cs          — Angle grinder implementation
        └── GrinderBladeContact.cs  — Blade collision detection
```

---

## Quest 2/3 Optimisation Notes

This SDK is designed to run well on mobile XR hardware:

| Concern | How we handle it |
|---------|-----------------|
| **GC allocations in Update** | Zero. No LINQ, no `foreach` on boxing enumerators, no string ops in hot paths. |
| **Haptic CPU cost** | Throttled via configurable `hapticInterval` (default 100ms between pulses). |
| **Audio channels** | AudioSources auto-stop when volume fades to zero, freeing Quest audio channels. |
| **Material instances** | SnapPoint highlights use `MaterialPropertyBlock` — no `.material` cloning. |
| **Component lookups** | All `GetComponent` calls cached in `Awake()`. Contact scripts use `GetComponentInParent` only in `OnTriggerEnter/Exit` (not per-frame). |
| **Particle systems** | Set Max Particles to **20** and Simulation Space to **World** on Quest. |
| **Usage tracker memory** | FIFO cap at 200 records (configurable). Oldest records dropped automatically. |
| **No Update loops** | ToolContactSurface, SnapPoint, GrabPoint, GrinderBladeContact — all event-driven. |

### Recommended Quest Build Settings

- **Texture Compression**: ASTC
- **Graphics API**: Vulkan (primary), OpenGLES 3.0 (fallback)
- **Target Frame Rate**: 72 Hz (Quest 2) / 90 Hz (Quest 3)
- **Fixed Timestep**: 1/72 (0.01389)
- **Particle Max**: Keep per-system under 20 particles
- **Audio**: Spatialize sparingly, limit concurrent AudioSources to ~16

---

## How to Set Up the Grinder (Existing Model)

### Grinder Model Hierarchy
```
Grinder fbx              ← Root: add GrinderTool + XRGrabInteractable + Rigidbody
├── body
│   ├── Blade             ← Assign to GrinderTool → Blade Transform
│   ├── Blade hook        ← Safety guard (optional)
│   ├── Button            ← Add ToolButtonAnimator; assign to GrinderTool → Button Animator
│   ├── cyl2
│   ├── joint1
│   │   ├── joint2
│   │   └── joint3
│   └── Handle            ← Side handle (add as secondary GrabPoint)
```

### Step-by-Step Setup

1. **Root Object** (`Grinder fbx`):
   - Add `GrinderTool` component (auto-adds `XRGrabInteractable` + `Rigidbody`)
   - Set `Tool Name` = "Angle Grinder"
   - Optionally add `MultiGripInteractable` for two-hand support

2. **Blade** child:
   - Drag into GrinderTool → `Blade Transform` field
   - Create a child empty GameObject with a thin **CapsuleCollider** (trigger) around the disc edge
   - Add `GrinderBladeContact` to that trigger object
   - Assign the root's `GrinderTool` to `GrinderBladeContact` → `Grinder Tool` field

3. **Button** child:
   - Add `ToolButtonAnimator` component
   - Set `Press Offset` to match the button's travel (e.g. `0, -0.003, 0`)
   - Drag into GrinderTool → `Button Animator` field

4. **Handle** child (side handle):
   - Create a child empty GameObject, position where the hand grips
   - Add `GrabPoint` component, set `Is Primary Grip` = false

5. **Grab Points** (on body):
   - Create 2-3 empty child GameObjects on the body at natural grip positions
   - Add `GrabPoint` component to each, set `Is Primary Grip` = true
   - Add these transforms to the `XRGrabInteractable` → multiple interactable attach transforms

6. **Audio** (on root):
   - Add two `AudioSource` components (both set to **Loop**, **Play On Awake** = false)
   - Assign grinder idle loop → GrinderTool → `Motor Audio Source`
   - Assign grinder cutting loop → GrinderTool → `Use Audio Source`

7. **Sparks** (on blade area):
   - Add a `ParticleSystem` child near the blade edge
   - Set Max Particles = 20, Start Lifetime = 0.3s, Start Speed = 2-4
   - Set **Play On Awake** = false
   - Assign to GrinderTool → `Spark Effect`

8. **Test Workpiece**:
   - Place any mesh in the scene (cube, sheet metal, etc.)
   - Add `ToolContactSurface` component
   - Set `Material Type` = Metal
   - Ensure it has a **non-trigger Collider**

9. **Test in Play Mode** using the XR Device Simulator already in the scene.

---

## How to Create a New Tool

Follow this pattern to add any new trade tool (drill, wrench, multimeter, etc.):

### Step 1: Create the Tool Script

```csharp
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using PTTI.TradeTrainingSDK;

namespace PTTI.TradeTrainingSDK.Tools
{
    public class MyNewTool : TradeToolBase
    {
        // Add tool-specific Inspector fields here
        [Header("My Tool Settings")]
        [SerializeField] private Transform movingPart;
        
        protected override void Awake()
        {
            base.Awake();
            toolCategory = ToolCategory.Fastening; // Set appropriate category
            toolName = "My New Tool";
        }
        
        // Called every frame while the trigger is pressed
        protected override void OnToolRunningUpdate()
        {
            // Animate your moving part, apply forces, etc.
        }
        
        // Optional overrides:
        // protected override void OnToolGrabbed(SelectEnterEventArgs args) { }
        // protected override void OnToolReleased(SelectExitEventArgs args) { }
        // protected override void OnToolActivated(ActivateEventArgs args) { }
        // protected override void OnToolDeactivated(DeactivateEventArgs args) { }
        // protected override bool IsUsedCorrectly() => true;
        // protected override void UpdateToolAudio() { }
        // protected override void UpdateToolHaptics() { }
    }
}
```

### Step 2: Create a Contact Script (if tool has an active tip)

```csharp
using UnityEngine;
using PTTI.TradeTrainingSDK;

namespace PTTI.TradeTrainingSDK.Tools
{
    [RequireComponent(typeof(Collider))]
    public class MyToolContact : MonoBehaviour
    {
        [SerializeField] private MyNewTool myTool;
        private int contactCount;

        private void OnTriggerEnter(Collider other)
        {
            var surface = other.GetComponentInParent<ToolContactSurface>();
            if (surface == null || !surface.IsActive) return;
            contactCount++;
            if (contactCount == 1)
            {
                // Tell the tool it's touching a surface
                myTool.SetInUse(true);
                surface.NotifyContact(myTool);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var surface = other.GetComponentInParent<ToolContactSurface>();
            if (surface == null) return;
            contactCount = Mathf.Max(0, contactCount - 1);
            if (contactCount == 0)
            {
                myTool.SetInUse(false);
                surface.NotifyContactEnd(myTool);
            }
        }
    }
}
```

### Step 3: Set Up the Prefab

1. Import or create the 3D model
2. Add your tool script to the root (auto-adds XRGrabInteractable + Rigidbody)
3. Set up colliders, grab points, audio sources
4. Add a trigger collider on the active part → attach your contact script
5. Create the prefab in `Assets/Prefabs/`

### Step 4: Test

1. Place in scene with a `ToolContactSurface` nearby
2. Enter Play Mode with XR Device Simulator
3. Grab, activate, touch surface, release, drop
4. Check usage tracker in Inspector

---

## Common Tool Types — Quick Reference

| Tool | Category | Key Override | Active Part |
|------|----------|-------------|-------------|
| Angle Grinder | Grinding | Blade spin + coast-down | Disc edge trigger |
| Jigsaw | Cutting | Blade oscillation | Blade tip trigger |
| Drill | Drilling | Bit rotation | Bit tip trigger |
| Wrench | Fastening | Rotation constraint | Jaw trigger |
| Screwdriver | Fastening | Rotation + linear push | Tip trigger |
| Multimeter | Electrical | Reading display | Probe tip triggers |
| Wire Stripper | Electrical | Jaw close animation | Jaw trigger |
| Clamp | Clamping | Jaw open/close | Jaw trigger |
| Pipe Wrench | Plumbing | Jaw + rotation | Jaw trigger |

---

## Assembly System (SnapPoint)

For step-by-step assembly training:

1. Place `SnapPoint` components at each assembly position
2. Set `Accepted Tag` or `Accepted Part Name` to filter correct parts
3. Wire up `OnPartSnapped` / `OnPartRejected` events for feedback
4. Use `SnapPoint.IsOccupied` to check completion
5. Build a sequence controller that enables snap points in order

---

## API Quick Reference

### TradeToolBase (inherit from this)

| Method/Property | Description |
|----------------|-------------|
| `OnToolRunningUpdate()` | **Abstract** — your per-frame tool logic |
| `OnToolGrabbed(args)` | Virtual — called on grab |
| `OnToolReleased(args)` | Virtual — called on release |
| `OnToolActivated(args)` | Virtual — called on trigger press |
| `OnToolDeactivated(args)` | Virtual — called on trigger release |
| `IsUsedCorrectly()` | Virtual — return false for incorrect use |
| `UpdateToolAudio()` | Virtual — override audio behaviour |
| `UpdateToolHaptics()` | Virtual — override haptic behaviour |
| `SetInUse(bool)` | Tell base that tool is working on a surface |
| `ReportIncorrectUse(reason)` | Log an incorrect use event |
| `ResetTool()` | Virtual — reset to initial state |
| `InstructorReset()` | Force-drop and reset (instructor only) |
| `FadeAudio(source, shouldPlay)` | Smooth volume fade helper |
| `SendHapticPulse(intensity)` | Throttled haptic pulse helper |
| `CurrentState` | Current ToolState enum |
| `IsHeld / IsActivated / IsInUse` | State query shortcuts |
| `SpeedNormalized` | (GrinderTool) 0–1 blade speed |

### ToolUsageTracker

| Method | Description |
|--------|-------------|
| `RecordEvent(name, type, correct, duration)` | Log an event |
| `ToJson()` | Export all records as JSON |
| `ClearRecords()` | Reset all data |
| `AccuracyPercent` | Correct/(Correct+Incorrect) × 100 |

---

## Namespace

All SDK code lives under `PTTI.TradeTrainingSDK`. Tool implementations use `PTTI.TradeTrainingSDK.Tools`.

```csharp
using PTTI.TradeTrainingSDK;        // Core types
using PTTI.TradeTrainingSDK.Tools;  // Specific tool implementations
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-07-17 | Initial SDK: Core framework + Angle Grinder tool |
