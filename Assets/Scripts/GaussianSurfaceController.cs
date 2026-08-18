using UnityEngine;

/// <summary>
/// Gauss's Law visualization: a point charge inside a cubic Gaussian surface. The
/// surface is subdivided into a tile grid; each tile's local E-field (Coulomb's law)
/// and flux (E . normal * area) get summed into a numerical total flux, which should
/// match the theoretical Q/epsilon0 while the charge is inside the cube and trend
/// toward zero as it's dragged outside.
///
/// All physics runs in this object's own LOCAL space (1 local unit = 1 "meter") so
/// the numbers stay physically meaningful (matching the literal 1.0 m cube / 1.0 nC
/// charge spec) regardless of whatever small visual scale is applied on top for AR
/// display on a real, much smaller, printed marker.
/// </summary>
public class GaussianSurfaceController : MonoBehaviour
{
    /// <summary>
    /// The currently active instance, if a Gaussian Surfaces marker is being tracked.
    /// AR content is instantiated at runtime per marker, so it can't be dragged into
    /// the Inspector at edit time - persistent screen-space UI reaches it through
    /// here instead. See GaussianSurfaceUIBridge, which the UI actually calls.
    /// </summary>
    public static GaussianSurfaceController Current { get; private set; }

    public enum Resolution { Low, Medium, High }

    [Header("Physics")]
    [Tooltip("Charge magnitude in nanocoulombs.")]
    public float chargeNanoCoulombs = 1.0f;
    [Tooltip("Cube side length in local units (1 unit = 1 meter for this content's internal math).")]
    public float cubeSize = 1.0f;

    [Header("Charge")]
    public Transform charge;

    [Header("Tile Grid")]
    public Resolution resolution = Resolution.Medium;
    [Tooltip("The six face quads, each owning its own patch of the grid and handling tap-to-select.")]
    public GaussianFaceTile[] faces = new GaussianFaceTile[6];
    [Tooltip("The six grid-line overlays, one per face, toggled by 'Show surface tiles'.")]
    public TileGridMeshBuilder[] gridOverlays = new TileGridMeshBuilder[6];
    public Transform selectedTileHighlight;

    [Header("Cross-Section View")]
    [Tooltip("Faces flagged hiddenInCrossSection open up to reveal the interior; the remaining faces tint toward this color to mark the cut, matching how physics diagrams shade a cutaway.")]
    public Color crossSectionTintColor = new Color(0.9f, 0.25f, 0.15f, 0.32f);
    public Color normalFaceColor = new Color(0.3f, 0.6f, 1f, 0.18f);
    public bool CrossSectionActive { get; private set; }
    MaterialPropertyBlock facePropBlock;

    [Header("Radiating Field Arrows")]
    [Tooltip("Arrows always shown coming out of the charge in fixed directions (illustrative, not one per tile) so the radial field reads at a glance.")]
    public Transform[] fieldArrows;
    public Renderer[] fieldArrowRenderers;
    [Tooltip("Local-space direction each arrow in fieldArrows points, matched by array index.")]
    public Vector3[] fieldArrowDirections;
    [Tooltip("How far from the charge (in local units) the field is sampled to size each arrow.")]
    public float fieldArrowSampleDistance = 0.3f;
    [Tooltip("Baked into each arrow's own mesh (ArrowMeshBuilder.baseOffset, applied once at Start - see ApplyFieldArrowGap) as invisible length near the pivot, so the pivot itself can sit exactly at the charge's surface (arrows read as shot out of the charge) while the visible shaft still starts a little further out instead of right at the surface.")]
    public float fieldArrowGapFromSurface = 0.04f;
    public Color fieldArrowColor = new Color(0.86f, 0.83f, 0.24f);
    public float fieldArrowScaleMultiplier = 0.03f;
    public float fieldArrowMinScale = 1f;
    public float fieldArrowMaxScale = 5f;

    MaterialPropertyBlock fieldArrowPropBlock;

    public Vector3 SelectedTileFieldVector { get; private set; }
    public float SelectedTileArea { get; private set; }
    public bool HasSelectedTile { get; private set; }
    public float TotalNumericalFlux { get; private set; }
    public float TheoreticalFlux { get; private set; }
    public bool TilesVisible { get; private set; } = true;

    public int TilesPerSide => resolution switch
    {
        Resolution.Low => 2,
        Resolution.Medium => 4,
        _ => 8,
    };

    const float CoulombConstant = 8.99e9f; // N*m^2/C^2
    const float VacuumPermittivity = 8.854e-12f; // C^2/(N*m^2)

    void OnEnable() => Current = this;
    void OnDisable() { if (Current == this) Current = null; }

    void Start()
    {
        TheoreticalFlux = (chargeNanoCoulombs * 1e-9f) / VacuumPermittivity;
        SetResolution(resolution);
        SetTilesVisible(TilesVisible);
        ApplyFieldArrowGap();
    }

    /// <summary>
    /// Bakes fieldArrowGapFromSurface into each arrow's own mesh (ArrowMeshBuilder.
    /// baseOffset) once at startup, rather than as a per-frame position offset -
    /// so the object's transform can sit exactly at the charge's surface (see
    /// UpdateFieldArrows) while the drawn shape still starts a little further out.
    /// One-time only: rebuilding a mesh every frame in Update() would allocate a
    /// new Mesh every frame, the same class of bug that caused the TMP crash this
    /// text sits next to.
    /// </summary>
    void ApplyFieldArrowGap()
    {
        if (fieldArrows == null) return;
        foreach (var arrow in fieldArrows)
        {
            if (arrow == null) continue;
            var builder = arrow.GetComponent<ArrowMeshBuilder>();
            if (builder == null) continue;
            builder.baseOffset = fieldArrowGapFromSurface;
            builder.Build();
        }
    }

    void Update()
    {
        RecalculateFlux();
        UpdateFieldArrows();
    }

    public void SetResolution(Resolution newResolution)
    {
        resolution = newResolution;
        int n = TilesPerSide;
        foreach (var overlay in gridOverlays)
            if (overlay != null) overlay.Build(n, cubeSize);
    }

    public void ToggleTilesVisible() => SetTilesVisible(!TilesVisible);

    public void SetTilesVisible(bool visible)
    {
        TilesVisible = visible;
        for (int i = 0; i < gridOverlays.Length; i++)
        {
            if (gridOverlays[i] == null) continue;
            bool openedUp = i < faces.Length && faces[i] != null && faces[i].hiddenInCrossSection && CrossSectionActive;
            gridOverlays[i].gameObject.SetActive(visible && !openedUp);
        }
    }

    public void ToggleCrossSection() => SetCrossSection(!CrossSectionActive);

    public void SetCrossSection(bool active)
    {
        CrossSectionActive = active;
        foreach (var face in faces)
        {
            if (face == null) continue;

            if (face.hiddenInCrossSection)
            {
                face.gameObject.SetActive(!active);
            }
            else if (face.faceRenderer != null)
            {
                facePropBlock ??= new MaterialPropertyBlock();
                face.faceRenderer.GetPropertyBlock(facePropBlock);
                facePropBlock.SetColor("_BaseColor", active ? crossSectionTintColor : normalFaceColor);
                face.faceRenderer.SetPropertyBlock(facePropBlock);
            }
        }
        SetTilesVisible(TilesVisible); // re-sync overlay visibility now that face visibility changed
    }

    void RecalculateFlux()
    {
        if (charge == null || faces == null) { TotalNumericalFlux = 0f; return; }

        Vector3 chargeLocal = transform.InverseTransformPoint(charge.position);
        int n = TilesPerSide;
        float tileArea = (cubeSize / n) * (cubeSize / n);
        float total = 0f;

        foreach (var face in faces)
        {
            if (face == null) continue;
            Vector3 axisU = face.localAxisU.normalized;
            Vector3 axisV = face.localAxisV.normalized;
            Vector3 normal = face.localNormal.normalized;

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float u = (i + 0.5f) / n - 0.5f;
                    float v = (j + 0.5f) / n - 0.5f;
                    Vector3 tileCenter = face.localOrigin + axisU * (u * cubeSize) + axisV * (v * cubeSize);
                    Vector3 field = FieldAt(tileCenter, chargeLocal);
                    total += Vector3.Dot(field, normal) * tileArea;
                }
            }
        }
        TotalNumericalFlux = total;
    }

    Vector3 FieldAt(Vector3 pointLocal, Vector3 chargeLocal)
    {
        Vector3 r = pointLocal - chargeLocal;
        float rMag = r.magnitude;
        if (rMag < 0.001f) return Vector3.zero;

        float qCoulombs = chargeNanoCoulombs * 1e-9f;
        float eMag = CoulombConstant * qCoulombs / (rMag * rMag);
        return r.normalized * eMag;
    }

    void UpdateFieldArrows()
    {
        if (charge == null || fieldArrows == null) return;

        Vector3 chargeLocal = transform.InverseTransformPoint(charge.position);
        // charge is a direct child of this content root, so its localScale is
        // already in the same local-unit space as cubeSize/fieldArrowSampleDistance
        // etc. (unlike lossyScale, which would double up this content's own
        // external AR scale - see RodsChargeController.RodRadiusScale for exactly
        // that bug). Assumes a uniform-scale default sphere primitive (radius =
        // half its scale).
        float chargeRadius = charge.localScale.x * 0.5f;

        for (int i = 0; i < fieldArrows.Length; i++)
        {
            var arrow = fieldArrows[i];
            if (arrow == null) continue;

            Vector3 dir = (i < fieldArrowDirections.Length ? fieldArrowDirections[i] : Vector3.up).normalized;
            Vector3 sampleLocal = chargeLocal + dir * fieldArrowSampleDistance;
            Vector3 field = FieldAt(sampleLocal, chargeLocal);

            // Pivot sits exactly at the charge's surface, as if the arrow is shot
            // out of the charge itself - the visible gap comes from baseOffset
            // baked into the mesh (see ApplyFieldArrowGap), not a position offset.
            arrow.position = charge.position + transform.TransformDirection(dir) * chargeRadius;
            float mag = field.magnitude;
            if (mag > 0.0001f)
                arrow.rotation = BillboardRotation(field.normalized, charge.position);

            float scale = Mathf.Clamp(mag * fieldArrowScaleMultiplier, fieldArrowMinScale, fieldArrowMaxScale);
            arrow.localScale = new Vector3(arrow.localScale.x, scale, arrow.localScale.z);

            var renderer = i < fieldArrowRenderers.Length ? fieldArrowRenderers[i] : null;
            if (renderer != null)
            {
                fieldArrowPropBlock ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(fieldArrowPropBlock);
                fieldArrowPropBlock.SetColor("_BaseColor", fieldArrowColor);
                renderer.SetPropertyBlock(fieldArrowPropBlock);
            }
        }
    }

    /// <summary>
    /// Rotation with local +Y locked exactly to lengthDir (the real field direction)
    /// while local +Z (the arrow's flat face normal) is rotated around that axis to
    /// point as closely as possible at the camera, so the arrow always reads as a
    /// flat shape rather than edge-on, however the AR device turns.
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

    /// <summary>Called by whichever GaussianFaceTile was tapped.</summary>
    public void SelectTile(Vector3 tileCenterLocal, Vector3 tileNormalLocal, float tileArea)
    {
        if (charge == null) return;

        Vector3 chargeLocal = transform.InverseTransformPoint(charge.position);
        SelectedTileFieldVector = FieldAt(tileCenterLocal, chargeLocal);
        SelectedTileArea = tileArea;
        HasSelectedTile = true;

        if (selectedTileHighlight != null)
        {
            selectedTileHighlight.gameObject.SetActive(true);
            selectedTileHighlight.localPosition = tileCenterLocal;
            Vector3 upHint = Mathf.Abs(Vector3.Dot(tileNormalLocal, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            selectedTileHighlight.localRotation = Quaternion.LookRotation(tileNormalLocal, upHint);
            float tileSide = Mathf.Sqrt(tileArea);
            selectedTileHighlight.localScale = new Vector3(tileSide, tileSide, 1f);
        }
    }
}
