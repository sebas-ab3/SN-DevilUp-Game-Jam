using UnityEngine;
using TMPro;
using System;  // <-- add

public class DiceGameController : MonoBehaviour
{
    [SerializeField] private TMP_Text labelResult;
    [SerializeField] private KeyCode rollKey = KeyCode.Space;
    private System.Random rng = new System.Random();

    public static event Action<int,int,int> OnRolled;              // <-- add
    public static (int d1,int d2,int total)? LastRoll;             // <-- add

    private void OnEnable()
    {
        if (labelResult) labelResult.text = "Press SPACE to roll";
    }

    private void Update()
    {
        if (Input.GetKeyDown(rollKey))
        {
            int d1 = rng.Next(1, 7);
            int d2 = rng.Next(1, 7);
            int total = d1 + d2;
            if (labelResult) labelResult.text = $"Rolled: {d1} + {d2} = {total}";

            LastRoll = (d1, d2, total);         // <-- add
            OnRolled?.Invoke(d1, d2, total);    // <-- add
        }
    }
}