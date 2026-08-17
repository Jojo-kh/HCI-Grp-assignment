using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Owns top-level screen switching: Dashboard <-> Scan <-> per-marker content
/// overlay (the latter already shown/hidden by ARContentManager based on
/// tracking state - this just makes sure DashboardCanvas/ScanCanvas get out of
/// its way, and drives the Scan screen's Searching/Found/NotRecognized/Loading
/// sub-state from ARTrackedImageManager's tracking events).
///
/// "Not recognised" has no direct ARFoundation signal (there's no "wrong page"
/// event for image tracking) - it's approximated as a soft timeout hint while
/// Searching, matching the design's intent without pretending to detect image
/// quality.
/// </summary>
public class AppFlowController : MonoBehaviour
{
    [Header("Screens")]
    public GameObject dashboardCanvas;
    public GameObject scanCanvas;
    public GameObject contentTopBar;
    public GameObject[] contentInfoPanels;

    [Header("Scan sub-states")]
    public GameObject scanStateSearching;
    public GameObject scanStateFound;
    public GameObject scanStateNotRecognized;
    public GameObject scanStateLoading;
    public Image[] scanFrameBars;
    public Color frameNeutralColor = new Color32(0xFF, 0xFF, 0xFF, 0x99);
    public Color frameAccentColor = new Color32(0xEC, 0x30, 0x13, 0xFF);

    [Header("Timing")]
    public float notRecognizedAfterSeconds = 6f;
    public float backToSearchingAfterSeconds = 4f;
    public float foundToLoadingDelay = 0.6f;
    public float loadingToContentDelay = 0.6f;

    enum ScanState { Searching, Found, NotRecognized, Loading }
    ScanState state;
    float stateTimer;
    bool trackingSeenThisSession;

    ARTrackedImageManager trackedImageManager;

    void Awake()
    {
        trackedImageManager = FindFirstObjectByType<ARTrackedImageManager>();
    }

    void OnEnable()
    {
        if (trackedImageManager != null) trackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
    }

    void OnDisable()
    {
        if (trackedImageManager != null) trackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
    }

    void Update()
    {
        bool anyContentActive = false;
        if (contentInfoPanels != null)
        {
            foreach (var panel in contentInfoPanels)
            {
                if (panel != null && panel.activeSelf) { anyContentActive = true; break; }
            }
        }
        if (contentTopBar != null && contentTopBar.activeSelf != anyContentActive)
            contentTopBar.SetActive(anyContentActive);

        if (scanCanvas != null && scanCanvas.activeSelf && !trackingSeenThisSession)
        {
            stateTimer += Time.deltaTime;
            if (state == ScanState.Searching && stateTimer > notRecognizedAfterSeconds)
                SetScanState(ScanState.NotRecognized);
            else if (state == ScanState.NotRecognized && stateTimer > backToSearchingAfterSeconds)
                SetScanState(ScanState.Searching);
        }
    }

    void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var t in args.added) if (t.trackingState == TrackingState.Tracking) HandleMarkerFound();
        foreach (var t in args.updated) if (t.trackingState == TrackingState.Tracking) HandleMarkerFound();
    }

    void HandleMarkerFound()
    {
        trackingSeenThisSession = true;
        if (scanCanvas == null || !scanCanvas.activeSelf) return;
        if (state == ScanState.Found || state == ScanState.Loading) return;

        SetScanState(ScanState.Found);
        CancelInvoke(nameof(ProceedToLoading));
        Invoke(nameof(ProceedToLoading), foundToLoadingDelay);
    }

    void ProceedToLoading()
    {
        SetScanState(ScanState.Loading);
        CancelInvoke(nameof(FinishScan));
        Invoke(nameof(FinishScan), loadingToContentDelay);
    }

    void FinishScan()
    {
        if (scanCanvas != null) scanCanvas.SetActive(false);
    }

    public void ShowDashboard()
    {
        CancelInvoke();
        if (scanCanvas != null) scanCanvas.SetActive(false);
        if (dashboardCanvas != null) dashboardCanvas.SetActive(true);
    }

    public void ShowScan()
    {
        CancelInvoke();
        if (dashboardCanvas != null) dashboardCanvas.SetActive(false);
        if (scanCanvas != null) scanCanvas.SetActive(true);
        trackingSeenThisSession = false;
        SetScanState(ScanState.Searching);
    }

    void SetScanState(ScanState s)
    {
        state = s;
        stateTimer = 0f;
        if (scanStateSearching != null) scanStateSearching.SetActive(s == ScanState.Searching);
        if (scanStateFound != null) scanStateFound.SetActive(s == ScanState.Found);
        if (scanStateNotRecognized != null) scanStateNotRecognized.SetActive(s == ScanState.NotRecognized);
        if (scanStateLoading != null) scanStateLoading.SetActive(s == ScanState.Loading);

        var frameColor = (s == ScanState.Found || s == ScanState.NotRecognized) ? frameAccentColor : frameNeutralColor;
        if (scanFrameBars != null)
            foreach (var bar in scanFrameBars)
                if (bar != null) bar.color = frameColor;
    }
}
