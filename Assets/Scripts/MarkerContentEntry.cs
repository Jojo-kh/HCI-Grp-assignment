using UnityEngine;

[CreateAssetMenu(fileName = "MarkerContentEntry", menuName = "AR/Marker Content Entry")]
public class MarkerContentEntry : ScriptableObject
{
    [Tooltip("Must exactly match the image name in your Reference Image Library")]
    public string referenceImageName;

    public GameObject contentPrefab;
    public string subjectTitle;
    [TextArea] public string explanationText;
    public Vector3 localScaleOverride = Vector3.one;
}