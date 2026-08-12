using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MarkerContentDatabase", menuName = "AR/Marker Content Database")]
public class MarkerContentDatabase : ScriptableObject
{
    public List<MarkerContentEntry> entries;

    public MarkerContentEntry GetByImageName(string imageName)
    {
        return entries.Find(e => e.referenceImageName == imageName);
    }
}