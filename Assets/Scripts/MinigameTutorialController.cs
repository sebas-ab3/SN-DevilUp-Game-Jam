using UnityEngine;
using TMPro;

public class MinigameTutorialController : MonoBehaviour
{
    [SerializeField] private GameFlowController flow;
    [SerializeField] private TMP_Text stepLabel;

    private int step = 0;
    private bool midpointRaised = false;

    private void Start()
    {
        if (!flow) flow = FindObjectOfType<GameFlowController>();
        ShowStep();
    }

    private void Update()
    {
        // Press N to simulate tutorial advancing
        if (Input.GetKeyDown(KeyCode.N))
        {
            step++;
            ShowStep();

            if (step == 1 && !midpointRaised)
            {
                flow.OnTutorialReachedMidpoint(); // Esc now exits 2D
                midpointRaised = true;
            }

            if (step >= 2)
            {
                flow.OnTutorialCompleted(); // free play unlocked
            }
        }
    }

    private void ShowStep()
    {
        if (!stepLabel) return;

        switch (step)
        {
            case 0:
                stepLabel.text = "Tutorial Step 1 (Press N to continue)";
                break;
            case 1:
                stepLabel.text = "Midpoint reached! Press ESC to exit to 3D. (Press N to finish tutorial)";
                break;
            default:
                stepLabel.text = "Tutorial Complete! You now have full control.";
                break;
        }
    }
}