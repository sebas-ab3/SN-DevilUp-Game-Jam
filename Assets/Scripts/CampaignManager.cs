// CampaignManager.cs
using UnityEngine;
using System.Collections.Generic;
using DudoGame;
using TMPro;                    // NEW (for optional HUD label)
using UnityEngine.UI;          // NEW (for optional start button)

public class CampaignManager : MonoBehaviour
{
    [Header("Sequence (assign 3 profiles)")]
    public EnemyProfile[] enemies;

    [Header("Scene References")]
    public DudoUIController uiController;
    public IntermissionController intermission; // optional

    [Header("UI (optional)")]
    public TMP_Text enemyBanner;               // drag a TMP_Text to show “Enemy 1/3: NAME”
    public Button startCampaignButton;         // drag your Start Campaign button (so we can disable it)

    [Header("Boot")]
    [SerializeField] private bool autoRunOnStart = false;

    private int _enemyIndex = -1;
    private bool _campaignActive = false;

    private void Start()
    {
        if (autoRunOnStart) StartCampaign();
    }

    public void StartCampaign()
    {
        if (!ValidateSetup()) return;

        // ⛔ Ignore button presses if we’re mid-campaign
        if (_campaignActive)
        {
            Debug.Log("[Campaign] StartCampaign() ignored: already running.");
            return;
        }

        Debug.Log("[Campaign] StartCampaign()");
        _enemyIndex = -1;
        _campaignActive = true;

        // Disable the start button while the campaign runs
        if (startCampaignButton) startCampaignButton.interactable = false;

        NextEnemy();
    }

    public void RestartCampaign()
    {
        if (!ValidateSetup()) return;
        Debug.Log("[Campaign] RestartCampaign()");
        _enemyIndex = -1;
        _campaignActive = true;
        if (startCampaignButton) startCampaignButton.interactable = false;
        NextEnemy();
    }

    private bool ValidateSetup()
    {
        if (uiController == null)
        {
            Debug.LogError("CampaignManager: uiController not assigned.");
            return false;
        }
        if (enemies == null || enemies.Length == 0)
        {
            uiController.ShowCampaignMessage("No enemies configured.");
            return false;
        }
        return true;
    }

    private void NextEnemy()
    {
        if (!_campaignActive) return;

        _enemyIndex++;
        Debug.Log("[Campaign] NextEnemy() -> index = " + _enemyIndex);

        if (_enemyIndex >= enemies.Length)
        {
            _campaignActive = false;
            uiController.ShowCampaignMessage("You defeated all opponents! 🎉");
            if (enemyBanner) enemyBanner.text = "Campaign Complete!";
            // Re-enable start button so you can play again
            if (startCampaignButton) startCampaignButton.interactable = true;
            return;
        }

        var profile = enemies[_enemyIndex];
        if (profile == null)
        {
            Debug.LogWarning($"CampaignManager: Enemy profile at index {_enemyIndex} is null, skipping.");
            NextEnemy();
            return;
        }

        // 🟢 Show clear signifier for which enemy we’re on
        int stageNum = _enemyIndex + 1;
        int total = enemies.Length;
        string name = string.IsNullOrEmpty(profile.enemyName) ? "AI" : profile.enemyName;

        // Option A: dedicated HUD label (cleanest)
        if (enemyBanner)
            enemyBanner.text = $"Enemy {stageNum}/{total}: {name}";
        // Option B: also echo in the game’s status area
        uiController.ShowCampaignMessage($"Enemy {stageNum}/{total}: {name}");

        // Kick off the match
        uiController.StartCampaignMatch(profile, OnPlayerWonMatch, OnPlayerLostMatch);
    }

    private void OnPlayerWonMatch()
    {
        if (!_campaignActive) return;

        if (intermission == null)
        {
            NextEnemy();
            return;
        }

        var steps = BuildIntermissionForIndex(_enemyIndex);
        if (steps == null || steps.Count == 0)
        {
            NextEnemy();
        }
        else
        {
            intermission.RunIntermission(steps, NextEnemy);
        }
    }

    private void OnPlayerLostMatch()
    {
        _campaignActive = false;
        uiController.ShowCampaignMessage("You lost the match. Campaign failed.");
        if (enemyBanner) enemyBanner.text = "Defeat";
        // Re-enable button so player can try again
        if (startCampaignButton) startCampaignButton.interactable = true;
    }

    private List<StepSpec> BuildIntermissionForIndex(int justDefeatedEnemyIndex)
    {
        var list = new List<StepSpec>();
        if (justDefeatedEnemyIndex == 0)
        {
            list.Add(new StepSpec {
                type = StepType.Dialogue,
                dialogueLines = new [] {
                    "Nice work! Your first opponent is down.",
                    "Take a breath. The next rival won’t be as forgiving."
                }
            });
        }
        else if (justDefeatedEnemyIndex == 1)
        {
            list.Add(new StepSpec {
                type = StepType.Dialogue,
                dialogueLines = new [] {
                    "You’re getting the hang of this.",
                    "Rumor says the final rival counts dice like a machine..."
                }
            });
        }
        return list;
    }
}
