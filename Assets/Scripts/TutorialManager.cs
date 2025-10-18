using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    // What should we wait for before advancing to the next step?
    public enum WaitType
    {
        None,
        DelaySeconds,
        PressKey,
        ExitZoomed,   // wait until player exits the 2D UI (CurrentView != Zoom)
        EnterZoomed   // wait until player enters the 2D UI (CurrentView == Zoom)
    }

    [System.Serializable]
    public class TutorialStep
    {
        [TextArea] public string text;

        [Header("Locks to Apply (optional)")]
        public bool applyLocks = true;
        public bool lockLookInputs = true; // blocks A/W/D
        public bool lockZoomEnter  = true; // blocks E
        public bool lockZoomExit   = true; // blocks Esc

        [Header("Force View (optional)")]
        public bool forceCenter = false;
        public bool forceZoom   = false;

        [Header("Wait Condition")]
        public WaitType waitFor = WaitType.None;
        public float delaySeconds = 0f;           // for DelaySeconds
        public KeyCode waitKey   = KeyCode.None;  // for PressKey

        [Header("Flow Hooks")]
        public bool markMidpoint = false; // lets other systems know we hit midpoint (optional)
        public bool completesTutorial = false; // last step -> go to FreePlay
    }

    [Header("References")]
    [SerializeField] private PlayerViewController view;
    [SerializeField] private GameFlowController flow;
    [SerializeField] private GameObject minigameUIRoot; // Canvas root "MinigameUI"
    [SerializeField] private TMP_Text tutorialLabel;    // a TMP_Text inside MinigameUI

    [Header("Steps")]
    [SerializeField] private List<TutorialStep> steps = new();

    private void OnValidate()
    {
        if (!view) view = FindObjectOfType<PlayerViewController>();
        if (!flow) flow = FindObjectOfType<GameFlowController>();
        if (!minigameUIRoot)
        {
            var go = GameObject.Find("MinigameUI");
            if (go) minigameUIRoot = go;
        }
        if (!tutorialLabel && minigameUIRoot)
        {
            // Try to find a TMP_Text under the UI by name; otherwise assign manually.
            var t = minigameUIRoot.GetComponentInChildren<TMP_Text>(true);
            if (t) tutorialLabel = t;
        }
    }

    private void Start()
    {
        // If you didn’t hand-author steps in the Inspector, build a sensible default set.
        if (steps == null || steps.Count == 0)
            BuildDefaultSteps();

        // Ensure we start in TutorialLocked2D via GameFlow (self-healing).
        if (flow != null)
        {
            // Flow.Start already forces TutorialLocked2D in our latest version.
            // If you want to be extra defensive:
            // flow.OnTutorialStartConfirmed();
        }

        StartCoroutine(RunTutorial());
    }

    private IEnumerator RunTutorial()
    {
        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];

            // 1) Show text (if any)
            if (tutorialLabel)
                tutorialLabel.text = s.text;

            // 2) Apply locks
            if (view && s.applyLocks)
            {
                view.lockLookInputs = s.lockLookInputs;
                view.lockZoomEnter  = s.lockZoomEnter;
                view.lockZoomExit   = s.lockZoomExit;
            }

            // 3) Force camera/view if requested
            if (view)
            {
                if (s.forceCenter)
                    view.ForceView(PlayerViewController.View.Center, showMinigame: false);
                if (s.forceZoom)
                {
                    // make sure UI is on
                    if (minigameUIRoot && !minigameUIRoot.activeSelf) minigameUIRoot.SetActive(true);
                    view.ForceView(PlayerViewController.View.Zoom, showMinigame: true);
                }
            }

            // 4) Midpoint hook (optional, informational)
            if (s.markMidpoint && flow != null)
            {
                // If you kept the wrapper:
                flow.OnTutorialReachedMidpoint();
                // Or directly: flow.AdvanceTo_TutorialMidExitOK();
            }

            // 5) Wait for the required condition
            yield return WaitForCondition(s);

            // 6) Completion hook
            if (s.completesTutorial && flow != null)
            {
                // End tutorial -> FreePlay
                flow.OnTutorialCompleted(); // or flow.AdvanceTo_FreePlay();
            }
        }
    }

    private IEnumerator WaitForCondition(TutorialStep s)
    {
        switch (s.waitFor)
        {
            case WaitType.None:
                yield break;

            case WaitType.DelaySeconds:
                yield return new WaitForSeconds(s.delaySeconds);
                yield break;

            case WaitType.PressKey:
                if (s.waitKey == KeyCode.None) yield break;
                while (!Input.GetKeyDown(s.waitKey)) yield return null;
                yield break;

            case WaitType.ExitZoomed:
                // Wait until CurrentView != Zoom
                while (view != null && view.CurrentView == PlayerViewController.View.Zoom)
                    yield return null;
                yield break;

            case WaitType.EnterZoomed:
                while (view != null && view.CurrentView != PlayerViewController.View.Zoom)
                    yield return null;
                yield break;
        }
    }

    private void BuildDefaultSteps()
    {
        steps = new List<TutorialStep>()
        {
            new TutorialStep{
                text = "Welcome! This tutorial starts in the 2D screen. Press SPACE to roll once.",
                applyLocks = true,
                lockLookInputs = true,
                lockZoomEnter  = true,
                lockZoomExit   = true,     // cannot leave yet
                forceZoom = true,          // make sure we are in MinigameUI
                waitFor = WaitType.PressKey,
                waitKey = KeyCode.Space
            },
            new TutorialStep{
                text = "Nice! Now you can press ESC to exit to the 3D room.",
                applyLocks = true,
                lockLookInputs = true,     // still locked until they exit
                lockZoomEnter  = true,
                lockZoomExit   = false,    // allow Esc
                waitFor = WaitType.ExitZoomed,
                markMidpoint = true        // informs GameFlow we reached midpoint
            },
            new TutorialStep{
                text = "Explore. A/W/D to look. From Center, press E to re-enter the screen.",
                applyLocks = true,
                lockLookInputs = false,
                lockZoomEnter  = false,
                lockZoomExit   = false,
                forceCenter = true,        // place them nicely
                waitFor = WaitType.DelaySeconds,
                delaySeconds = 1.0f
            },
            new TutorialStep{
                text = "Tutorial complete! Have fun.",
                applyLocks = false,
                completesTutorial = true,
                waitFor = WaitType.DelaySeconds,
                delaySeconds = 0.25f
            }
        };
    }
}
