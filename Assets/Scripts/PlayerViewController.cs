using UnityEngine;

public class PlayerViewController : MonoBehaviour
{
    public enum View { Left, Center, Right, Zoom }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform anchorLeft;
    [SerializeField] private Transform anchorCenter;
    [SerializeField] private Transform anchorRight;
    [SerializeField] private Transform anchorZoom;
    [SerializeField] private GameObject minigameRoot;

    [Header("Dudo Monitor Display")]
    [SerializeField] private Camera dudoUICamera;     // Camera that renders UI to monitor
    [SerializeField] private Canvas dudoCanvas;       // Your main Dudo overlay canvas

    [Header("FOV")]
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float zoomFOV = 40f;

    [Header("Smooth Snap Speeds")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float rotateSpeed = 240f;
    [SerializeField] private float fovSpeed = 120f;

    [Header("Input (classic)")]
    [SerializeField] private KeyCode keyLeft = KeyCode.A;
    [SerializeField] private KeyCode keyCenter = KeyCode.W;
    [SerializeField] private KeyCode keyRight = KeyCode.D;
    [SerializeField] private KeyCode keyZoom = KeyCode.E;
    [SerializeField] private KeyCode keyExitZoom = KeyCode.Escape;

    // ===== Locks controlled by tutorial / game flow =====
    [Header("Runtime Locks")]
    public bool lockLookInputs = false;   // blocks A/W/D
    public bool lockZoomEnter = false;    // blocks E (enter zoom)
    public bool lockZoomExit  = false;    // blocks Esc (exit zoom)

    private View currentView = View.Center;
    private Transform targetAnchor;
    private float targetFOV;

    public View CurrentView => currentView;
    
    private void Reset() => mainCamera = Camera.main;

    private void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;

        // Detect if something (GameFlow) already put us in Zoom/tutorial
        bool uiAlreadyOn = (minigameRoot && minigameRoot.activeSelf);
        bool cameraIsNearZoom =
            (anchorZoom != null && mainCamera != null) &&
            Vector3.Distance(mainCamera.transform.position, anchorZoom.position) < 0.05f;

        if (uiAlreadyOn || cameraIsNearZoom)
        {
            // Respect the pre-set tutorial state
            currentView = View.Zoom;
            targetAnchor = anchorZoom ? anchorZoom : mainCamera.transform;
            targetFOV = zoomFOV;
            if (mainCamera && targetAnchor)
            {
                mainCamera.transform.SetPositionAndRotation(targetAnchor.position, targetAnchor.rotation);
                mainCamera.fieldOfView = targetFOV;
            }
            // In zoom mode - show fullscreen overlay, disable monitor camera
            if (dudoCanvas) dudoCanvas.enabled = true;
            if (dudoUICamera) dudoUICamera.enabled = false;
        }
        else
        {
            // Default startup (no tutorial override)
            currentView = View.Center;
            targetAnchor = anchorCenter ? anchorCenter : mainCamera.transform;
            targetFOV = normalFOV;
            if (mainCamera && targetAnchor)
            {
                mainCamera.transform.SetPositionAndRotation(targetAnchor.position, targetAnchor.rotation);
                mainCamera.fieldOfView = targetFOV;
            }
            if (minigameRoot) minigameRoot.SetActive(false);
            // In 3D world - hide fullscreen overlay, enable monitor camera
            if (dudoCanvas) dudoCanvas.enabled = false;
            if (dudoUICamera) dudoUICamera.enabled = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    private void Update()
    {
        HandleInput();
        SmoothSnapTowardsTarget();
    }

    private void HandleInput()
    {
        if (currentView != View.Zoom)
        {
            if (!lockLookInputs)
            {
                if (Input.GetKeyDown(keyLeft))   SetView(View.Left);
                if (Input.GetKeyDown(keyRight))  SetView(View.Right);
                if (Input.GetKeyDown(keyCenter)) SetView(View.Center);
            }

            // Zoom only allowed from Center, and only if not locked
            if (!lockZoomEnter && Input.GetKeyDown(keyZoom) && currentView == View.Center)
                SetView(View.Zoom);
        }
        else
        {
            if (!lockZoomExit && Input.GetKeyDown(keyExitZoom))
                SetView(View.Center);
        }
    }

    private void SmoothSnapTowardsTarget()
    {
        if (!mainCamera || !targetAnchor) return;

        mainCamera.transform.position = Vector3.MoveTowards(
            mainCamera.transform.position,
            targetAnchor.position,
            moveSpeed * Time.deltaTime
        );

        mainCamera.transform.rotation = Quaternion.RotateTowards(
            mainCamera.transform.rotation,
            targetAnchor.rotation,
            rotateSpeed * Time.deltaTime
        );

        mainCamera.fieldOfView = Mathf.MoveTowards(
            mainCamera.fieldOfView,
            targetFOV,
            fovSpeed * Time.deltaTime
        );
    }

    private void SetView(View v)
    {
        currentView = v;

        if (minigameRoot) minigameRoot.SetActive(v == View.Zoom);

        // Toggle between fullscreen UI and monitor display
        if (v == View.Zoom)
        {
            // Entering minigame - show fullscreen overlay
            if (dudoCanvas) dudoCanvas.enabled = true;
            if (dudoUICamera) dudoUICamera.enabled = false; // Stop rendering to monitor
        }
        else
        {
            // Exiting to 3D world - show on monitor
            if (dudoCanvas) dudoCanvas.enabled = false;
            if (dudoUICamera) dudoUICamera.enabled = true; // Render to monitor
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        targetAnchor = GetAnchor(v);
        targetFOV = (v == View.Zoom) ? zoomFOV : normalFOV;
    }

    private Transform GetAnchor(View v)
    {
        switch (v)
        {
            case View.Left:   return anchorLeft;
            case View.Center: return anchorCenter;
            case View.Right:  return anchorRight;
            case View.Zoom:   return anchorZoom;
            default:          return anchorCenter;
        }
    }

    // Convenience setters for the flow controller:
    public void ForceView(View v, bool showMinigame = false)
    {
        currentView = v;
        targetAnchor = GetAnchor(v);
        targetFOV = (v == View.Zoom) ? zoomFOV : normalFOV;

        if (mainCamera && targetAnchor)
        {
            mainCamera.transform.SetPositionAndRotation(targetAnchor.position, targetAnchor.rotation);
            mainCamera.fieldOfView = targetFOV;
        }
        if (minigameRoot) minigameRoot.SetActive(showMinigame);

        // Handle UI display based on view
        if (v == View.Zoom)
        {
            if (dudoCanvas) dudoCanvas.enabled = true;
            if (dudoUICamera) dudoUICamera.enabled = false;
        }
        else
        {
            if (dudoCanvas) dudoCanvas.enabled = false;
            if (dudoUICamera) dudoUICamera.enabled = true;
        }
    }
}