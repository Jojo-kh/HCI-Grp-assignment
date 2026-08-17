using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ARContentManager : MonoBehaviour
{
    public MarkerContentDatabase database;

    /// <summary>
    /// Maps a marker's reference image name to its screen-space UI panel. This
    /// lives here - on a scene MonoBehaviour - rather than as a field on
    /// MarkerContentEntry, because MarkerContentEntry is a ScriptableObject asset,
    /// and Unity cannot persist an asset's reference to a scene object: it holds
    /// in memory during the Editor session but silently serializes back out as
    /// null on save. A scene object referencing another scene object (this array
    /// pointing at each InfoPanel) serializes correctly.
    /// </summary>
    [System.Serializable]
    public struct MarkerUIPanel
    {
        [Tooltip("Must exactly match the image name in your Reference Image Library (same value as the matching MarkerContentEntry.referenceImageName).")]
        public string referenceImageName;
        [Tooltip("Scene UI panel to show while this marker is tracked, hidden otherwise.")]
        public GameObject panel;
    }
    public MarkerUIPanel[] uiPanels;

    ARTrackedImageManager trackedImageManager;
    readonly Dictionary<TrackableId, GameObject> spawnedContent = new();

    void Awake()
    {
        trackedImageManager = GetComponent<ARTrackedImageManager>();
    }

    void OnEnable() => trackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
    void OnDisable() => trackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);

    void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var trackedImage in args.added) UpdateContent(trackedImage);
        foreach (var trackedImage in args.updated) UpdateContent(trackedImage);

        foreach (var removed in args.removed)
        {
            if (spawnedContent.TryGetValue(removed.Key, out var instance))
            {
                Destroy(instance);
                spawnedContent.Remove(removed.Key);
            }

            SetPanelActive(removed.Value.referenceImage.name, false);
        }
    }

    void UpdateContent(ARTrackedImage trackedImage)
    {
        var entry = database.GetByImageName(trackedImage.referenceImage.name);
        if (entry == null) return;

        bool isTracking = trackedImage.trackingState == TrackingState.Tracking;

        SetPanelActive(trackedImage.referenceImage.name, isTracking);
        if (entry.contentPrefab == null) return;

        if (!spawnedContent.TryGetValue(trackedImage.trackableId, out var instance))
        {
            instance = Instantiate(entry.contentPrefab, trackedImage.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = entry.localScaleOverride;
            spawnedContent[trackedImage.trackableId] = instance;
        }

        instance.SetActive(isTracking);
    }

    void SetPanelActive(string imageName, bool active)
    {
        if (uiPanels == null) return;
        foreach (var entry in uiPanels)
        {
            if (entry.panel != null && entry.referenceImageName == imageName)
            {
                entry.panel.SetActive(active);
                return;
            }
        }
    }
}