using UnityEngine;

public class GameFlowController : MonoBehaviour
{
    public enum Phase { TutorialLocked2D, TutorialMidExitOK, FreePlay }

    [Header("References")]
    [SerializeField] private PlayerViewController view;
    [SerializeField] private GameObject minigameUIRoot; // assign MinigameUI (Canvas root)

    [Header("Debug State (read-only)")]
    [SerializeField] private Phase phase = Phase.TutorialLocked2D;

    // Back-compat wrappers so older scripts still compile
    public void OnTutorialStartConfirmed()  => Enter_TutorialLocked2D();
    public void OnTutorialReachedMidpoint() => AdvanceTo_TutorialMidExitOK();
    public void OnTutorialCompleted()       => AdvanceTo_FreePlay();

    
    private void OnValidate()
    {
        // Auto-wire in editor when possible
        if (!view) view = FindObjectOfType<PlayerViewController>();
        if (!minigameUIRoot)
        {
            // Try to grab whatever view is already pointing at
            if (view) minigameUIRoot = GetMinigameRootFromView();
        }
    }

    private void Awake()
    {
        // Final auto-wire at runtime
        if (!view) view = FindObjectOfType<PlayerViewController>();
        if (!minigameUIRoot) minigameUIRoot = GetMinigameRootFromView();

        // FORCE the game to start inside the 2D tutorial
        Enter_TutorialLocked2D();
    }

    private GameObject GetMinigameRootFromView()
    {
        // Reflect the private serialized field via public method if you add one,
        // or simply drag it in the Inspector. For safety, try to find by name:
        var go = GameObject.Find("MinigameUI");
        return go;
    }

    // ===== Phase handlers =====
    private void Enter_TutorialLocked2D()
    {
        phase = Phase.TutorialLocked2D;

        // Turn on UI no matter the editor checkbox
        if (minigameUIRoot && !minigameUIRoot.activeSelf)
            minigameUIRoot.SetActive(true);

        // Lock everything and force Zoom view with UI visible
        if (view)
        {
            view.lockLookInputs = true;  // block A/W/D
            view.lockZoomEnter  = true;  // block E
            view.lockZoomExit   = true;  // block Esc
            view.ForceView(PlayerViewController.View.Zoom, showMinigame: true);
        }
        else
        {
            Debug.LogError("GameFlowController: 'view' not assigned/found.");
        }
    }

    public void AdvanceTo_TutorialMidExitOK()
    {
        phase = Phase.TutorialMidExitOK;

        if (view)
        {
            view.lockLookInputs = true;   // still in 2D until user presses Esc
            view.lockZoomEnter  = true;   // they must exit to Center first
            view.lockZoomExit   = false;  // Esc now allowed
        }
    }

    public void AdvanceTo_FreePlay()
    {
        phase = Phase.FreePlay;

        if (view)
        {
            view.lockLookInputs = false;
            view.lockZoomEnter  = false;
            view.lockZoomExit   = false;
            view.ForceView(PlayerViewController.View.Center, showMinigame: false);
        }

        // Optional: ensure UI is off
        if (minigameUIRoot && minigameUIRoot.activeSelf)
            minigameUIRoot.SetActive(false);
    }

    // ===== Debug/Dev hotkeys work regardless of UI state =====
    private void Update()
    {
        // Simulate your tutorial steps:
        // Press N once -> midpoint (Esc allowed); N again -> free play
        if (Input.GetKeyDown(KeyCode.N))
        {
            if (phase == Phase.TutorialLocked2D)
            {
                AdvanceTo_TutorialMidExitOK();
                Debug.Log("[Flow] Midpoint reached: Esc will now exit to 3D.");
            }
            else if (phase == Phase.TutorialMidExitOK)
            {
                AdvanceTo_FreePlay();
                Debug.Log("[Flow] Tutorial complete: full control enabled.");
            }
        }

        // Emergency toggles:
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Enter_TutorialLocked2D();
            Debug.Log("[Flow] Forced TutorialLocked2D.");
        }
        if (Input.GetKeyDown(KeyCode.F1))
        {
            AdvanceTo_FreePlay();
            Debug.Log("[Flow] Forced FreePlay.");
        }
    }
}
