using UnityEngine;

/// <summary>
/// A single point charge. Sign is toggled by the user (see ChargeTapToggle) -
/// there's no fixed magnitude, this is visualization only.
/// </summary>
public class ElectricalCharge : MonoBehaviour
{
    public bool isPositive = true;
    public Color positiveColor = Color.red;
    public Color negativeColor = Color.blue;
    [Tooltip("Optional child TMP label showing + or -.")]
    public TMPro.TMP_Text signLabel;

    Renderer rend;
    MaterialPropertyBlock block;

    void Awake()
    {
        Refresh();
    }

    public void ToggleSign()
    {
        isPositive = !isPositive;
        Refresh();
    }

    void Refresh()
    {
        if (rend == null) rend = GetComponent<Renderer>();
        if (rend != null)
        {
            block ??= new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            block.SetColor("_BaseColor", isPositive ? positiveColor : negativeColor);
            rend.SetPropertyBlock(block);
        }
        if (signLabel != null) signLabel.text = isPositive ? "+" : "-";
    }

#if UNITY_EDITOR
    void OnValidate() => Refresh();
#endif
}
