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

    // Only one marker's content is shown at a time, even though
    // ARTrackedImageManager can track several images simultaneously (up to
    // m_MaxNumberOfMovingImages) - e.g. two lesson pages both happen to be in
    // frame at once. Without this, every simultaneously-tracked marker's
    // content rendered at the same time, overlapping each other.
    TrackableId activeTrackableId;

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
            if (activeTrackableId == removed.Key) activeTrackableId = default;
        }

        RefreshVisibility();
    }

    void UpdateContent(ARTrackedImage trackedImage)
    {
        var entry = database.GetByImageName(trackedImage.referenceImage.name);
        if (entry == null) return;

        if (entry.contentPrefab != null && !spawnedContent.ContainsKey(trackedImage.trackableId))
        {
            var instance = Instantiate(entry.contentPrefab, trackedImage.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = entry.localScaleOverride;
            spawnedContent[trackedImage.trackableId] = instance;
        }

        // Only a confidently-Tracking read claims focus - a marker merely
        // glimpsed at Limited confidence (e.g. an old marker still faintly in
        // frame) never steals focus back from whatever the user is actually
        // pointed at now.
        if (trackedImage.trackingState == TrackingState.Tracking)
            activeTrackableId = trackedImage.trackableId;
    }

    void RefreshVisibility()
    {
        foreach (var trackedImage in trackedImageManager.trackables)
        {
            // Limited still counts as visible for whichever marker is active - ARKit
            // reports Limited while still holding a recent/extrapolated pose (the
            // marker drifts to the edge of frame, gets briefly occluded, the device
            // moves quickly) - only None (tracking actually lost) hides it.
            bool isVisible = trackedImage.trackableId == activeTrackableId
                && trackedImage.trackingState != TrackingState.None;

            SetPanelActive(trackedImage.referenceImage.name, isVisible);
            if (spawnedContent.TryGetValue(trackedImage.trackableId, out var instance) && instance != null)
                instance.SetActive(isVisible);
        }
    }

    /// <summary>
    /// Destroys every spawned AR content instance so the next time any marker is
    /// (re)tracked it gets a completely fresh instance instead of the same one -
    /// still carrying whatever pinch-zoom/drag/position-button state was left on it
    /// last time - being silently reactivated. Call this when leaving content view
    /// (e.g. AppFlowController.ShowDashboard) so re-scanning a marker always starts
    /// from a clean scene. Tracked images that are still in frame will simply get a
    /// new instance spawned again on their next OnTrackedImagesChanged update.
    /// </summary>
    public void ClearAllContent()
    {
        foreach (var instance in spawnedContent.Values)
            if (instance != null) Destroy(instance);
        spawnedContent.Clear();
        activeTrackableId = default;
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