using UnityEngine;

/// <summary>Continuous rotation for the loading-spinner icon.</summary>
public class UISpin : MonoBehaviour
{
    public float degreesPerSecond = -180f;

    void Update()
    {
        transform.Rotate(0, 0, degreesPerSecond * Time.deltaTime);
    }
}
