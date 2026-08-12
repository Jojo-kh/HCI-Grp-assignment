using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ARContentManager : MonoBehaviour
{
    public MarkerContentDatabase database;
    ARTrackedImageManager trackedImageManager;

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
    }

    void UpdateContent(ARTrackedImage trackedImage)
    {
        var entry = database.GetByImageName(trackedImage.referenceImage.name);
        if (entry == null) return;

        bool isTracking = trackedImage.trackingState == TrackingState.Tracking;
        Debug.Log($"{entry.subjectTitle}: tracking = {isTracking}");
    }
}