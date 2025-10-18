using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class IntermissionController : MonoBehaviour
{
    [Header("Overlay UI")]
    [SerializeField] private GameObject overlayPanel;   // a Panel that covers the screen
    [SerializeField] private TMP_Text promptLabel;      // text at the center/top
    [SerializeField] private Button clickCatcher;       // big invisible button on overlay

    private List<StepSpec> _steps;
    private int _stepIndex;
    private Action _onDone;

    // Runtime state per-step
    private int _clicksRemaining;
    private int _dialogueIndex;

    public bool IsRunning { get; private set; }

    private void Awake()
    {
        if (clickCatcher != null)
            clickCatcher.onClick.AddListener(OnOverlayClick);
        HideOverlay();
    }

    public void RunIntermission(List<StepSpec> steps, Action onDone)
    {
        if (steps == null || steps.Count == 0)
        {
            onDone?.Invoke();
            return;
        }
        _steps = steps;
        _stepIndex = 0;
        _onDone = onDone;
        IsRunning = true;

        ShowOverlay();
        StartStep(_steps[_stepIndex]);
    }

    private void StartStep(StepSpec spec)
    {
        switch (spec.type)
        {
            case StepType.ClickAnywhere:
                _clicksRemaining = Mathf.Max(1, spec.clicksRequired);
                SetPrompt(string.IsNullOrEmpty(spec.prompt)
                    ? $"Click anywhere {_clicksRemaining} time(s) to continue"
                    : spec.prompt);
                break;

            case StepType.Dialogue:
                _dialogueIndex = 0;
                if (spec.dialogueLines == null || spec.dialogueLines.Length == 0)
                {
                    // nothing to show, step completes immediately
                    NextStep();
                    return;
                }
                SetPrompt(spec.dialogueLines[_dialogueIndex] + "\n\n(Click to continue)");
                break;

            default:
                NextStep();
                break;
        }
    }

    private void OnOverlayClick()
    {
        if (!IsRunning || _steps == null || _stepIndex >= _steps.Count) return;

        var spec = _steps[_stepIndex];
        switch (spec.type)
        {
            case StepType.ClickAnywhere:
                _clicksRemaining--;
                if (_clicksRemaining <= 0)
                {
                    NextStep();
                }
                else
                {
                    SetPrompt(string.IsNullOrEmpty(spec.prompt)
                        ? $"Click anywhere {_clicksRemaining} time(s) to continue"
                        : spec.prompt);
                }
                break;

            case StepType.Dialogue:
                _dialogueIndex++;
                if (spec.dialogueLines != null && _dialogueIndex < spec.dialogueLines.Length)
                {
                    SetPrompt(spec.dialogueLines[_dialogueIndex] + "\n\n(Click to continue)");
                }
                else
                {
                    NextStep();
                }
                break;
        }
    }

    private void NextStep()
    {
        _stepIndex++;
        if (_stepIndex >= _steps.Count)
        {
            FinishIntermission();
        }
        else
        {
            StartStep(_steps[_stepIndex]);
        }
    }

    private void FinishIntermission()
    {
        HideOverlay();
        IsRunning = false;
        var cb = _onDone; // protect from reentrancy
        _onDone = null;
        _steps = null;
        cb?.Invoke();
    }

    private void SetPrompt(string text)
    {
        if (promptLabel != null) promptLabel.text = text ?? "";
    }

    private void ShowOverlay()
    {
        if (overlayPanel != null) overlayPanel.SetActive(true);
    }

    private void HideOverlay()
    {
        if (overlayPanel != null) overlayPanel.SetActive(false);
        if (promptLabel != null) promptLabel.text = "";
    }
}

// ============================
// Data types for steps
// ============================
[Serializable]
public class StepSpec
{
    public StepType type = StepType.ClickAnywhere;

    [TextArea]
    public string prompt;             // optional custom prompt text

    [Min(1)]
    public int clicksRequired = 3;    // for ClickAnywhere

    public string[] dialogueLines;    // for Dialogue
}

public enum StepType
{
    ClickAnywhere,
    Dialogue
}
