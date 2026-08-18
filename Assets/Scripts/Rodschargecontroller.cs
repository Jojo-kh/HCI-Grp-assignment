using UnityEngine;
using TMPro;

/// <summary>
/// Drives the "Rods & Charge" AR content. Deliberately simplified rather than a
/// literal circulating magnetic field: each rod pushes/pulls straight along the
/// perpendicular from itself to a point, falling off as 1/r. Push vs pull isn't
/// simply "Forward pushes, Reverse pulls" - it follows the rod's actual current
/// direction relative to the content's own right side (current running toward
/// content-right pushes/repels, toward content-left pulls/attracts - see
/// FieldFromRod), matching whichever way its little yellow surface arrows
/// (RodDirectionArrows) are actually drawn, not just the Forward/Reverse label.
/// That's what both the resultant arrow at the test charge and the "radial
/// lines" visual are built from (FieldFromRod/CombinedField) - real
/// superposition of both rods' contributions, so two rods can genuinely cancel
/// or add depending on their actual current directions and the charge's
/// position, exactly as "Opposite Currents" intends.
/// (A real current-carrying wire's B-field actually circulates around it per the
/// right-hand rule rather than pushing straight out - TangentialFromRod/
/// CombinedTangential still model that if this ever needs to switch back - but
/// this content intentionally trades that rigor for a simpler, more readable
/// straight-line effect.)
///
/// Radial Lines: straight arrows shooting in/out at 4 fixed cardinal positions
/// around each rod (not every angle) - visualizes this same field, flipping with
/// each rod's direction.
/// </summary>
public class RodsChargeController : MonoBehaviour
{
    /// <summary>
    /// "Adds" / "Cancels" / "Partial Cancellation" / "No Field" for whichever position
    /// the test charge is currently at - kept up to date every frame regardless of
    /// whether relationshipLabel is wired, so screen-space UI (which can't hold a
    /// persisting reference into this runtime-instantiated content - see
    /// RodsChargeUIBridge) can read it through RodsChargeController.Current instead.
    /// </summary>
    public string CurrentRelationship { get; private set; } = "";

    /// <summary>
    /// The currently active instance, if a Rods & Charge marker is being tracked.
    /// AR content is instantiated at runtime per marker, so it can't be dragged
    /// into the Inspector at edit time � persistent screen-space UI (which exists
    /// whether or not a marker is tracked) reaches this content through here instead.
    /// See RodsChargeUIBridge, which the UI buttons actually call.
    /// </summary>
    public static RodsChargeController Current { get; private set; }

    public enum RodDirection { Forward = 1, Reverse = -1 }

    [Header("Rods")]
    [Tooltip("Transform of Rod A. Its local +Y axis is treated as the rod's long axis.")]
    public Transform rodA;
    public RodDirection rodADirection = RodDirection.Forward;
    [Tooltip("Renderer on Rod A � its material is colored to show current direction.")]
    public Renderer rodARenderer;
    [Tooltip("Local radius of the rod (unscaled units � 0.5 for a default Cylinder primitive).")]
    public float rodLocalRadius = 0.5f;
    [Tooltip("Local length of the rod (unscaled units � 2 for a default Cylinder primitive).")]
    public float rodLocalLength = 2f;

    [Tooltip("Transform of Rod B. Its local +Y axis is treated as the rod's long axis.")]
    public Transform rodB;
    public RodDirection rodBDirection = RodDirection.Forward;
    [Tooltip("Renderer on Rod B � its material is colored to show current direction.")]
    public Renderer rodBRenderer;

    [Header("Test Charge")]
    public Transform testCharge;
    [Tooltip("The three positions the charge can snap to, set these as empty GameObjects in the scene.")]
    public Transform posAbove;
    public Transform posBetween;
    public Transform posBelow;

    [Header("Resultant Field Visual")]
    [Tooltip("The arrow object (with ArrowMeshBuilder on it). Its local +Y points 'forward' at rest.")]
    public Transform resultantArrow;
    [Tooltip("Renderer on the resultant arrow � colored by which way it's currently pointing.")]
    public Renderer resultantArrowRenderer;
    [Tooltip("World-space or screen-space text showing Adds / Cancels / Partial.")]
    public TMP_Text relationshipLabel;

    [Header("Direction Colors")]
    [Tooltip("Color used for Forward-direction rods, and for field visuals pointing along +Y (world up).")]
    public Color forwardColor = Color.blue;
    [Tooltip("Color used for Reverse-direction rods, and for field visuals pointing against +Y (world up).")]
    public Color reverseColor = Color.red;

    [Header("Tuning")]
    [Tooltip("Multiplies field magnitude before it's used to scale the arrow, purely visual.")]
    public float arrowScaleMultiplier = 1f;
    [Tooltip("Minimum arrow scale so it never fully disappears even near-cancellation.")]
    public float minArrowScale = 0.05f;
    [Tooltip("Maximum arrow scale so it can't grow without bound if the charge ends up very close to a rod.")]
    public float maxArrowScale = 1f;
    [Tooltip("Below this fraction of the larger individual contribution, we call it 'Cancels'.")]
    [Range(0f, 1f)] public float cancelThreshold = 0.15f;
    [Tooltip("Above this fraction of (contribution A + contribution B), we call it 'Adds'.")]
    [Range(0f, 1f)] public float addThreshold = 0.85f;

    [Header("Circulation Curtain (loops around each rod)")]
    public bool buildCirculationCurtain = true;
    [Tooltip("How many starting points spaced along each rod's length.")]
    [Range(1, 12)] public int circLinesAlongLength = 5;
    [Tooltip("How many starting points spaced around each rod's circumference.")]
    [Range(1, 16)] public int circLinesAroundCircumference = 8;
    [Tooltip("How many arrow-steps each traced loop takes.")]
    [Range(1, 20)] public int circStepsPerLine = 6;
    public float circStepDistance = 0.1f;
    public float circArrowSize = 0.04f;
    public float circArrowThicknessRatio = 0.02f;
    public Material circArrowMaterial;

    [Header("Radial Lines (straight, 4 cardinal directions only)")]
    public bool buildRadialLines = true;
    [Tooltip("How many starting points spaced along each rod's length.")]
    [Range(1, 12)] public int radialLinesAlongLength = 5;
    [Tooltip("How many arrow-steps each straight line takes as it moves away from the rod.")]
    [Range(1, 20)] public int radialStepsPerLine = 6;
    public float radialStepDistance = 0.15f;
    [Tooltip("Extra multiplier on how far each step travels outward, on top of proportional rod-size scaling - raise this to push the 4 cardinal arrows further from the rod's surface.")]
    public float radialReachMultiplier = 2f;
    public float radialArrowSize = 0.05f;
    public float radialArrowThicknessRatio = 0.02f;
    public Material radialArrowMaterial;

    MaterialPropertyBlock propBlock;
    [SerializeField, HideInInspector] Transform circContainer;
    [SerializeField, HideInInspector] Transform radialContainer;
    Vector3 testChargeBaseScale = Vector3.one;

    static readonly float[] cardinalAngles = { 0f, 90f, 180f, 270f };

    [ContextMenu("Force Reset Curtain Containers")]
    void ForceResetCurtainContainers()
    {
        // Use this if containers ever get corrupted (e.g. after editing the
        // prefab asset in isolation, which can leave stale references
        // pointing at the wrong context). Right-click this component's
        // header in the Inspector and choose this from the menu.
        circContainer = null;
        radialContainer = null;
        if (buildCirculationCurtain) BuildCirculationCurtain();
        if (buildRadialLines) BuildRadialLines();
    }

    void Awake()
    {
        // Whatever scale the test charge was authored at (in the prefab/scene) is
        // treated as its "1x" size, matching a default rod radius scale of 1 - see
        // ScaleTestChargeToRods.
        if (testCharge != null) testChargeBaseScale = testCharge.localScale;
    }

    void OnEnable()
    {
        Current = this;
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        propBlock = new MaterialPropertyBlock();

        ScaleTestChargeToRods();
        SyncRodDirectionArrows();

        if (buildCirculationCurtain) BuildCirculationCurtain();
        if (buildRadialLines) BuildRadialLines();

        MoveChargeTo(posBetween);
    }

    // Hook these up to a tap on each rod (see RodTapToggle), same tap-to-toggle
    // concept as ChargeTapToggle on the electrical charges.
    public void ToggleRodADirection()
    {
        rodADirection = rodADirection == RodDirection.Forward ? RodDirection.Reverse : RodDirection.Forward;
        RefreshAfterDirectionChange();
    }

    public void ToggleRodBDirection()
    {
        rodBDirection = rodBDirection == RodDirection.Forward ? RodDirection.Reverse : RodDirection.Forward;
        RefreshAfterDirectionChange();
    }

    /// <summary>
    /// Re-derives everything direction depends on - the surface arrows, both field
    /// visualizations, and the resultant at the test charge - so a tap-toggled rod
    /// takes effect immediately rather than waiting for the next OnValidate/Update.
    /// </summary>
    void RefreshAfterDirectionChange()
    {
        SyncRodDirectionArrows();
        if (buildCirculationCurtain) BuildCirculationCurtain();
        if (buildRadialLines) BuildRadialLines();
        UpdateResultant();
    }

    /// <summary>
    /// Pushes rodADirection/rodBDirection onto each rod's RodDirectionArrows so the
    /// little surface arrows always point the same way as the circulation curtain
    /// and radial lines - this is the single place direction is set; RodDirectionArrows
    /// no longer needs its own direction kept in sync by hand.
    /// </summary>
    void SyncRodDirectionArrows()
    {
        SyncOneRodDirectionArrows(rodA, rodADirection);
        SyncOneRodDirectionArrows(rodB, rodBDirection);
    }

    void SyncOneRodDirectionArrows(Transform rod, RodDirection dir)
    {
        if (rod == null) return;
        var arrows = rod.GetComponent<RodDirectionArrows>();
        if (arrows == null) return;

        arrows.direction = dir == RodDirection.Forward
            ? RodDirectionArrows.CurrentDirection.Forward
            : RodDirectionArrows.CurrentDirection.Reverse;
        arrows.Build();
    }

    /// <summary>
    /// Sizes the test charge relative to the rods' actual radius (average of both,
    /// in case they differ), so it stays proportionate to however big the rods
    /// themselves have been scaled instead of a fixed absolute size.
    /// </summary>
    void ScaleTestChargeToRods()
    {
        if (testCharge == null) return;
        float rodScale = (RodRadiusScale(rodA) + RodRadiusScale(rodB)) / 2f;
        testCharge.localScale = testChargeBaseScale * rodScale;
    }

    // Hook these up to the 3 UI buttons in the Inspector (OnClick).
    public void OnAboveButton() => MoveChargeTo(posAbove);
    public void OnBetweenButton() => MoveChargeTo(posBetween);
    public void OnBelowButton() => MoveChargeTo(posBelow);

    void MoveChargeTo(Transform target)
    {
        if (testCharge == null || target == null) return;
        testCharge.position = target.position;
        UpdateResultant();
    }

    /// <summary>
    /// Radial field contribution from a single rod, falling off as 1/r. Push vs
    /// pull isn't tied to the Forward/Reverse label directly - it follows the
    /// rod's actual current direction (axis * dir, the same direction
    /// RodDirectionArrows draws) relative to the content's own right side: current
    /// running toward content-right pushes (repels), current running toward
    /// content-left pulls (attracts). Two rods with the same current direction can
    /// therefore end up on opposite sides of push/pull if one is wired Forward and
    /// the other Reverse to run the opposite way, which is the point of "Opposite
    /// Currents."
    /// </summary>
    Vector3 FieldFromRod(Transform rod, RodDirection dir, Vector3 worldPoint)
    {
        if (rod == null) return Vector3.zero;

        Vector3 axis = rod.up.normalized;
        Vector3 toPoint = worldPoint - rod.position;
        Vector3 alongAxis = Vector3.Project(toPoint, axis);
        Vector3 perp = toPoint - alongAxis;
        float r = perp.magnitude;

        if (r < 0.001f) return Vector3.zero;

        Vector3 radialDir = perp.normalized;
        Vector3 currentDirectionWorld = axis * (float)dir;
        float pushSign = Vector3.Dot(currentDirectionWorld, transform.right) >= 0f ? 1f : -1f;

        return radialDir * (1f / r) * pushSign;
    }

    Vector3 CombinedField(Vector3 worldPoint)
    {
        return FieldFromRod(rodA, rodADirection, worldPoint) + FieldFromRod(rodB, rodBDirection, worldPoint);
    }

    /// <summary>
    /// The real field: circulates around the rod per the right-hand rule (Forward =
    /// curls one way, Reverse = curls the other), falling off as 1/r - drives both
    /// the circulation curtain and the resultant arrow at the test charge.
    /// </summary>
    Vector3 TangentialFromRod(Transform rod, RodDirection dir, Vector3 worldPoint)
    {
        if (rod == null) return Vector3.zero;

        Vector3 axis = rod.up.normalized;
        Vector3 toPoint = worldPoint - rod.position;
        Vector3 alongAxis = Vector3.Project(toPoint, axis);
        Vector3 perp = toPoint - alongAxis;
        float r = perp.magnitude;

        if (r < 0.001f) return Vector3.zero;

        Vector3 radialDir = perp.normalized;
        Vector3 circulation = Vector3.Cross(axis, radialDir).normalized;
        return circulation * (1f / r) * (float)dir;
    }

    Vector3 CombinedTangential(Vector3 worldPoint)
    {
        return TangentialFromRod(rodA, rodADirection, worldPoint) + TangentialFromRod(rodB, rodBDirection, worldPoint);
    }

    void UpdateResultant()
    {
        if (testCharge == null) return;

        Vector3 pointWorld = testCharge.position;
        Vector3 fA = FieldFromRod(rodA, rodADirection, pointWorld);
        Vector3 fB = FieldFromRod(rodB, rodBDirection, pointWorld);
        Vector3 resultant = fA + fB;

        if (resultantArrow != null)
        {
            resultantArrow.position = pointWorld;

            float resultMag = resultant.magnitude;
            if (resultMag > 0.0001f)
                resultantArrow.rotation = BillboardRotation(resultant.normalized, pointWorld);

            float scale = Mathf.Clamp(resultMag * arrowScaleMultiplier, minArrowScale, maxArrowScale);
            resultantArrow.localScale = new Vector3(resultantArrow.localScale.x, scale, resultantArrow.localScale.z);
        }

        if (resultantArrowRenderer != null && resultant.sqrMagnitude > 0.0001f)
        {
            float alignment = Vector3.Dot(resultant.normalized, Vector3.up);
            SetRendererColor(resultantArrowRenderer, alignment >= 0f ? forwardColor : reverseColor);
        }

        float magA = fA.magnitude;
        float magB = fB.magnitude;
        float sumOfMags = magA + magB;
        float netMag = resultant.magnitude;

        if (sumOfMags < 0.0001f)
        {
            CurrentRelationship = "No Field";
        }
        else
        {
            float ratio = netMag / sumOfMags;
            if (ratio <= cancelThreshold) CurrentRelationship = "Fields Cancel";
            else if (ratio >= addThreshold) CurrentRelationship = "Fields Add";
            else CurrentRelationship = "Partial Cancellation";
        }

        if (relationshipLabel != null) relationshipLabel.text = CurrentRelationship;
    }

    // ---------------- Circulation Curtain ----------------

    public void BuildCirculationCurtain()
    {
        ClearContainer(ref circContainer, "CirculationCurtainContainer");
        TraceCirculationFromRod(rodA, rodADirection);
        TraceCirculationFromRod(rodB, rodBDirection);
    }

    void TraceCirculationFromRod(Transform rod, RodDirection sourceRodDirection)
    {
        if (rod == null) return;
        Color lineColor = sourceRodDirection == RodDirection.Forward ? forwardColor : reverseColor;
        float rodScale = RodRadiusScale(rod);

        for (int i = 0; i < circLinesAlongLength; i++)
        {
            float t = (i + 0.5f) / circLinesAlongLength;
            float alongAxisLocal = Mathf.Lerp(-rodLocalLength / 2f, rodLocalLength / 2f, t);

            for (int a = 0; a < circLinesAroundCircumference; a++)
            {
                float angle = a * (360f / circLinesAroundCircumference);
                Vector3 outwardLocal = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 startLocal = new Vector3(0f, alongAxisLocal, 0f) + outwardLocal * rodLocalRadius;
                Vector3 currentPos = rod.TransformPoint(startLocal);

                for (int s = 0; s < circStepsPerLine; s++)
                {
                    Vector3 tangential = CombinedTangential(currentPos);
                    if (tangential.sqrMagnitude < 0.0001f) break;

                    Vector3 dir = tangential.normalized;
                    SpawnArrow(circContainer, currentPos, dir, lineColor, circArrowSize * rodScale, circArrowThicknessRatio, circArrowMaterial, "CircArrow");
                    currentPos += dir * circStepDistance * rodScale;
                }
            }
        }
    }

    // ---------------- Radial Lines (4 cardinal directions) ----------------

    public void BuildRadialLines()
    {
        ClearContainer(ref radialContainer, "RadialLinesContainer");
        TraceRadialFromRod(rodA, rodADirection);
        TraceRadialFromRod(rodB, rodBDirection);
    }

    void TraceRadialFromRod(Transform rod, RodDirection sourceRodDirection)
    {
        if (rod == null) return;
        Color lineColor = sourceRodDirection == RodDirection.Forward ? forwardColor : reverseColor;
        float rodScale = RodRadiusScale(rod);

        for (int i = 0; i < radialLinesAlongLength; i++)
        {
            float t = (i + 0.5f) / radialLinesAlongLength;
            float alongAxisLocal = Mathf.Lerp(-rodLocalLength / 2f, rodLocalLength / 2f, t);

            foreach (float angle in cardinalAngles)
            {
                Vector3 outwardLocal = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 startLocal = new Vector3(0f, alongAxisLocal, 0f) + outwardLocal * rodLocalRadius;
                Vector3 currentPos = rod.TransformPoint(startLocal);

                for (int s = 0; s < radialStepsPerLine; s++)
                {
                    Vector3 radial = CombinedField(currentPos);
                    if (radial.sqrMagnitude < 0.0001f) break;

                    Vector3 dir = radial.normalized;
                    SpawnArrow(radialContainer, currentPos, dir, lineColor, radialArrowSize * rodScale, radialArrowThicknessRatio, radialArrowMaterial, "RadialArrow");
                    currentPos += dir * radialStepDistance * rodScale * radialReachMultiplier;
                }
            }
        }
    }

    // ---------------- Shared helpers ----------------

    /// <summary>
    /// Average of the rod's own LOCAL X/Z scale (its radius dimensions relative to
    /// its parent - the content root - not lossyScale/world scale). The spawned
    /// arrows end up parented under this same content root, so they already inherit
    /// the content's own external scale (e.g. MarkerContentEntry.localScaleOverride)
    /// through normal Unity transform inheritance; baking lossyScale into their mesh
    /// size on top of that would apply the content's external scale twice, shrinking
    /// them by its square instead of by itself (this used to be exactly that bug -
    /// circulation/radial arrows and the auto-scaled test charge were coming out
    /// roughly (content scale) times smaller than intended).
    /// </summary>
    float RodRadiusScale(Transform rod)
    {
        if (rod == null) return 1f;
        Vector3 s = rod.localScale;
        return (Mathf.Abs(s.x) + Mathf.Abs(s.z)) / 2f;
    }

    void SpawnArrow(Transform parent, Vector3 worldPos, Vector3 worldDir, Color color, float size, float thicknessRatio, Material material, string name)
    {
        var arrowObj = new GameObject(name);
        // Editor-time preview only: DontSave keeps these out of the saved scene/prefab
        // (they're cheap to regenerate) instead of baking thousands of objects into the asset.
        if (!Application.isPlaying) arrowObj.hideFlags = HideFlags.DontSave;
        arrowObj.transform.SetParent(parent, true);
        arrowObj.transform.position = worldPos;
        arrowObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, worldDir);
        arrowObj.transform.localScale = Vector3.one;

        arrowObj.AddComponent<MeshFilter>();
        var meshRenderer = arrowObj.AddComponent<MeshRenderer>();
        var builder = arrowObj.AddComponent<ArrowMeshBuilder>();
        builder.size = size;
        builder.thicknessRatio = thicknessRatio;

        if (material != null)
            meshRenderer.sharedMaterial = material;

        var block = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        meshRenderer.SetPropertyBlock(block);

        builder.Build();
    }

    void ClearContainer(ref Transform container, string containerName)
    {
        // Self-healing: if the cached reference was lost (e.g. after a script
        // recompile), look for an existing container by name instead of
        // blindly creating a new one � and clean up any accidental duplicates
        // left over from before this fix.
        if (container == null)
        {
            Transform found = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == containerName)
                {
                    if (found == null)
                    {
                        found = child;
                    }
                    else
                    {
                        // Duplicate container from before � remove it.
                        if (Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }
            }
            container = found;
        }

        if (container == null)
        {
            var containerObj = new GameObject(containerName);
            if (!Application.isPlaying) containerObj.hideFlags = HideFlags.DontSave;
            containerObj.transform.SetParent(transform, false);
            container = containerObj.transform;
            return;
        }

        if (!Application.isPlaying) container.gameObject.hideFlags = HideFlags.DontSave;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    void SetRendererColor(Renderer r, Color c)
    {
        if (r == null) return;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        r.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", c);
        r.SetPropertyBlock(propBlock);
    }

    /// <summary>
    /// Rotation with local +Y locked exactly to lengthDir (the real resultant
    /// direction - the actual pull from both rods' current directions) while local
    /// +Z (the arrow's flat face normal) is rotated around that axis to point as
    /// closely as possible at the camera, so the resultant arrow always reads as a
    /// flat shape facing the viewer rather than edge-on, however the AR device
    /// turns - same technique GaussianSurfaceController/ElectricalForceController
    /// use for their own field arrows.
    /// </summary>
    Quaternion BillboardRotation(Vector3 lengthDir, Vector3 origin)
    {
        var cam = Camera.main;
        Vector3 toCamera = cam != null ? (cam.transform.position - origin).normalized : Vector3.back;

        Vector3 xAxis = Vector3.Cross(toCamera, lengthDir);
        if (xAxis.sqrMagnitude < 0.0001f) xAxis = Vector3.Cross(Vector3.up, lengthDir);
        if (xAxis.sqrMagnitude < 0.0001f) xAxis = Vector3.right;
        xAxis.Normalize();

        Vector3 zAxis = Vector3.Cross(xAxis, lengthDir).normalized;
        return Quaternion.LookRotation(zAxis, lengthDir);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            // Safe to rebuild immediately here - this is a live scene instance, not
            // an asset being edited, so there's no risk of the delayCall-related
            // prefab-corruption issue the edit-mode branch below works around.
            if (testCharge != null)
                UpdateResultant();
            SyncRodDirectionArrows();
            if (buildCirculationCurtain) BuildCirculationCurtain();
            if (buildRadialLines) BuildRadialLines();
        }
        else
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                SyncRodDirectionArrows();
                if (buildCirculationCurtain) BuildCirculationCurtain();
                if (buildRadialLines) BuildRadialLines();
            };
        }
    }
#endif
}