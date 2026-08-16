using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ARContentManager : MonoBehaviour
{
    public MarkerContentDatabase database;
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
        }
    }

    void UpdateContent(ARTrackedImage trackedImage)
    {
        var entry = database.GetByImageName(trackedImage.referenceImage.name);
        if (entry == null || entry.contentPrefab == null) return;

        bool isTracking = trackedImage.trackingState == TrackingState.Tracking;

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
}