using UnityEngine;
using TMPro;

public class DiceMonitorMirror : MonoBehaviour
{
    [SerializeField] private TMP_Text resultText;

    private void OnEnable()
    {
        DiceGameController.OnRolled += UpdateText;

        if (DiceGameController.LastRoll.HasValue && resultText)
        {
            var r = DiceGameController.LastRoll.Value;
            resultText.text = $"Rolled: {r.total}";
        }
    }

    private void OnDisable()
    {
        DiceGameController.OnRolled -= UpdateText;
    }

    private void UpdateText(int d1, int d2, int total)
    {
        if (resultText) resultText.text = $"Rolled: {total}";
    }
}