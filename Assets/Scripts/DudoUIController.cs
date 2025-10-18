using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DudoGame;
using System;
using System.Collections;

public class DudoUIController : MonoBehaviour
{
    [Header("Top Info")]
    [SerializeField] private TMP_Text playerDiceLabel;
    [SerializeField] private TMP_Text aiDiceCountLabel;
    [SerializeField] private TMP_Text currentBidLabel;

    [Header("Status")]
    [SerializeField] private TMP_Text statusLabel;

    [Header("Bid Editor")]
    [SerializeField] private TMP_Text qtyValue;
    [SerializeField] private TMP_Text faceValue;
    [SerializeField] private Button qtyMinus;
    [SerializeField] private Button qtyPlus;
    [SerializeField] private Button faceMinus;
    [SerializeField] private Button facePlus;
    [SerializeField] private GameObject bidEditorRow;

    [Header("Actions")]
    [SerializeField] private Button placeBidBtn;
    [SerializeField] private Button callBtn;
    [SerializeField] private Button spotOnBtn;
    [SerializeField] private GameObject actionsRow;

    [Header("Game Setup")]
    [SerializeField] private string playerName = "You";
    [SerializeField] private int startingDice = 5;

    [Header("Timing (seconds)")]
    [SerializeField] private float aiThinkSeconds = 1.5f;
    [SerializeField] private float interRoundSeconds = 4f;

    [Header("UX")]
    [Tooltip("How long error messages are shown before auto-clearing.")]
    [SerializeField] private float errorDisplaySeconds = 2.0f;

    [Header("Freeplay")]
    [Tooltip("If true, a freeplay game starts on scene load. Turn off if using CampaignManager to start matches.")]
    [SerializeField] private bool autoStartFreeplay = true;

    private DudoGameManager _gameManager;
    private int _bidQty = 1;
    private int _bidFace = 2;
    private bool _waitingForAI = false;
    private bool _gameOver = false;

    private string _errorMessage = "";
    private float _errorUntilTime = -1f; // when to auto-clear error
    private string _lastStatus = "";     // last non-error status line

    // Optional campaign callbacks
    private Action _onPlayerWon;
    private Action _onPlayerLost;

    private void Start()
    {
        SetupUI();
        if (autoStartFreeplay)
        {
            StartNewGame();
        }
        else
        {
            // Clear UI if we’re waiting for campaign to start
            RefreshUI("Campaign: press Start.");
        }
    }

    private void Update()
    {
        // Restart shortcut in freeplay
        if (autoStartFreeplay && _gameOver && Input.GetKeyDown(KeyCode.Return))
        {
            StartNewGame();
        }

        // Auto-clear error message
        if (!string.IsNullOrEmpty(_errorMessage) && Time.time >= _errorUntilTime)
        {
            _errorMessage = "";
            // Repaint status without the error, keep last status line
            RefreshUI(_lastStatus);
        }
    }

    private void SetupUI()
    {
        // Wire up bid editor buttons
        qtyMinus.onClick.AddListener(OnQtyMinus);
        qtyPlus.onClick.AddListener(OnQtyPlus);
        faceMinus.onClick.AddListener(OnFaceMinus);
        facePlus.onClick.AddListener(OnFacePlus);

        // Wire up action buttons
        placeBidBtn.onClick.AddListener(OnPlaceBid);
        callBtn.onClick.AddListener(OnCall);
        spotOnBtn.onClick.AddListener(OnSpotOn);

        // Set labels
        var callTxt = callBtn.GetComponentInChildren<TMP_Text>();
        if (callTxt) callTxt.text = "Dudo";
        var spotTxt = spotOnBtn.GetComponentInChildren<TMP_Text>();
        if (spotTxt) spotTxt.text = "Spot On";
    }

    #region Public (Campaign) Hooks

    // Optional: used by CampaignManager
    public void StartCampaignMatch(EnemyProfile enemy, Action onPlayerWon, Action onPlayerLost)
    {
        _onPlayerWon = onPlayerWon;
        _onPlayerLost = onPlayerLost;

        // Build a fresh manager for each match
        _gameManager = new DudoGameManager(playerName, startingDice);

        // Configure opponent identity / dice. (If your DudoGameManager has SetEnemyProfile, call it there.)
        _gameManager.players[1].playerName = string.IsNullOrEmpty(enemy.enemyName) ? "AI" : enemy.enemyName;
        _gameManager.players[1].diceCount = Mathf.Max(1, enemy.startingDice);
        _gameManager.players[0].diceCount = Mathf.Max(1, startingDice);

        _errorMessage = "";
        _gameOver = false;

        _bidQty = 1;
        _bidFace = 2;

        _gameManager.StartNewRound();
        UpdateBidEditor();

        string startStatus = "New round. " + (_gameManager.currentPlayerIndex == 0 ? "Your turn." : _gameManager.players[1].playerName + "'s turn.");
        RefreshUI(startStatus);
        
        // inside DudoUIController.StartCampaignMatch(...)
        var enemyName = string.IsNullOrEmpty(enemy.enemyName) ? "AI" : enemy.enemyName;
        ShowCampaignMessage($"Facing {enemyName} ({_gameManager.players[1].diceCount} dice)");


        if (_gameManager.players[_gameManager.currentPlayerIndex].isAI)
        {
            StartCoroutine(AITurn());
        }
    }

    // Optional: campaign message banner
    public void ShowCampaignMessage(string msg)
    {
        _errorMessage = "";
        _lastStatus = msg ?? "";

        if (statusLabel) statusLabel.text = _lastStatus;

        // Also update placeholders if no game yet
        if (_gameManager == null)
        {
            if (playerDiceLabel)   playerDiceLabel.text   = "Your dice: —";
            if (aiDiceCountLabel)  aiDiceCountLabel.text  = "AI dice: —";
            if (currentBidLabel)   currentBidLabel.text   = "Current bid: —";
            SetControlsInteractable(false);
            return;
        }

        // If a match is running, repaint normally
        RefreshUI(_lastStatus);
    }


    #endregion

    #region Game Flow

    private void StartNewGame()
    {
        _gameManager = new DudoGameManager(playerName, startingDice);
        _gameManager.StartNewRound();

        _errorMessage = "";
        _gameOver = false;

        _bidQty = 1;
        _bidFace = 2;
        UpdateBidEditor();

        string startStatus = "New round. " + (_gameManager.currentPlayerIndex == 0 ? "Your turn." : "AI's turn.");
        RefreshUI(startStatus);

        if (_gameManager.players[_gameManager.currentPlayerIndex].isAI)
        {
            StartCoroutine(AITurn());
        }
    }

    private void RefreshUI(string status)
{
    _lastStatus = status ?? "";

    // If the game manager doesn't exist yet (e.g., before StartNewGame / StartCampaignMatch),
    // just paint the status text and placeholders, and bail out gracefully.
    if (_gameManager == null)
    {
        if (playerDiceLabel)   playerDiceLabel.text   = "Your dice: —";
        if (aiDiceCountLabel)  aiDiceCountLabel.text  = "AI dice: —";
        if (currentBidLabel)   currentBidLabel.text   = "Current bid: —";

        if (statusLabel)
        {
            statusLabel.text = string.IsNullOrEmpty(_errorMessage)
                ? _lastStatus
                : "<color=red>" + _errorMessage + "</color>\n\n" + _lastStatus;
        }

        // Disable inputs until a match is actually started
        SetControlsInteractable(false);
        return;
    }

    // ---------- Existing logic (safe because _gameManager != null) ----------
    var player = _gameManager.players[0];
    var ai     = _gameManager.players[1];

    if (playerDiceLabel)
        playerDiceLabel.text = "Your dice: " + FormatDice(player.dice, false);

    if (aiDiceCountLabel)
    {
        bool hideAI = _gameManager.roundActive;
        aiDiceCountLabel.text = hideAI
            ? "AI dice: " + FormatDice(ai.dice, true)
            : "AI dice: " + FormatDice(ai.dice, false);
    }

    if (currentBidLabel)
        currentBidLabel.text = _gameManager.currentBid != null ? ("Current bid: " + _gameManager.currentBid) : "Current bid: —";

    if (statusLabel)
    {
        statusLabel.text = string.IsNullOrEmpty(_errorMessage)
            ? _lastStatus
            : "<color=red>" + _errorMessage + "</color>\n\n" + _lastStatus;
    }

    bool isPlayerTurn = _gameManager.currentPlayerIndex == 0 && !_waitingForAI && _gameManager.roundActive;
    bool hasBid       = _gameManager.currentBid != null;

    if (placeBidBtn) placeBidBtn.interactable = isPlayerTurn;
    if (callBtn)     callBtn.interactable     = isPlayerTurn && hasBid;
    if (spotOnBtn)   spotOnBtn.interactable   = isPlayerTurn && hasBid;

    if (qtyMinus)    qtyMinus.interactable    = isPlayerTurn;
    if (qtyPlus)     qtyPlus.interactable     = isPlayerTurn;
    if (faceMinus)   faceMinus.interactable   = isPlayerTurn;
    if (facePlus)    facePlus.interactable    = isPlayerTurn;

    bool shouldShowControls = isPlayerTurn || (!_gameManager.roundActive && _gameManager.currentPlayerIndex == 0);
    if (bidEditorRow) bidEditorRow.SetActive(shouldShowControls);
    if (actionsRow)   actionsRow.SetActive(shouldShowControls);

    UpdateBidEditor();
}

    private void CheckGameOver()
    {
        if (_gameManager.IsGameOver())
        {
            _gameOver = true;
            DudoGame.Player winner = _gameManager.GetWinner();
            DudoGame.Player loser = (winner == _gameManager.players[0]) ? _gameManager.players[1] : _gameManager.players[0];

            string finalStatus = loser.playerName + " has lost all dice!\n\n";
            finalStatus += "🏆 " + winner.playerName + " WINS! 🏆\n\n";
            finalStatus += (autoStartFreeplay ? "Press ENTER to play again" : "Campaign: press Start to play again");

            RefreshUI(finalStatus);
            DisableAllControls();

            // Campaign callbacks
            if (!autoStartFreeplay)
            {
                if (winner == _gameManager.players[0]) _onPlayerWon?.Invoke();
                else _onPlayerLost?.Invoke();
            }
        }
    }

    #endregion

    #region Bid Editor (clamps & validation)

    private void OnQtyMinus()
    {
        int minQty = GetMinBidQty();
        _bidQty = Mathf.Max(minQty, _bidQty - 1);
        UpdateBidEditor();
    }

    private void OnQtyPlus()
    {
        int maxDice = GetTotalDiceInPlay();
        _bidQty = Mathf.Min(Mathf.Max(1, maxDice), _bidQty + 1);
        UpdateBidEditor();
    }

    private void OnFaceMinus()
    {
        _bidFace = Mathf.Max(1, _bidFace - 1);
        UpdateBidEditor();
    }

    private void OnFacePlus()
    {
        _bidFace = Mathf.Min(6, _bidFace + 1);
        UpdateBidEditor();
    }

    private int GetMinBidQty()
    {
        // Lock: cannot go below the CURRENT BID quantity
        if (_gameManager != null && _gameManager.currentBid != null)
            return Mathf.Max(1, _gameManager.currentBid.quantity);
        return 1;
    }

    private void UpdateBidEditor()
    {
        if (_gameManager == null) return;

        int maxDice = GetTotalDiceInPlay();
        int minQty = GetMinBidQty();

        // Clamp to valid ranges
        _bidQty = Mathf.Clamp(_bidQty, minQty, Mathf.Max(minQty, maxDice));
        _bidFace = Mathf.Clamp(_bidFace, 1, 6);

        // First bid cannot be aces (no warnings until user errs)
        if (_gameManager.currentBid == null && _bidFace == 1)
            _bidFace = 2;

        // Validate the *current selection* (only show error if invalid)
        if (_gameManager.currentBid != null)
        {
            var proposed = new Bid(_bidQty, _bidFace);
            var validation = DudoRules.IsValidBid(_gameManager.currentBid, proposed, maxDice);
            if (!validation.valid)
            {
                ShowError(validation.reason);
            }
            else
            {
                // Clear only if we were showing an error from prior invalid selection
                if (!string.IsNullOrEmpty(_errorMessage))
                {
                    _errorMessage = "";
                    RefreshUI(_lastStatus);
                }
            }
        }

        if (qtyValue) qtyValue.text = _bidQty.ToString();
        if (faceValue) faceValue.text = (_bidFace == 1) ? "A" : _bidFace.ToString();
    }

    private int GetTotalDiceInPlay()
    {
        int total = 0;
        foreach (DudoGame.Player p in _gameManager.players)
        {
            if (!p.eliminated) total += p.diceCount;
        }
        return total;
    }

    #endregion

    #region Actions

    private void OnPlaceBid()
    {
        _errorMessage = "";

        var proposed = new Bid(_bidQty, _bidFace);
        int maxDice = GetTotalDiceInPlay();
        var validation = DudoRules.IsValidBid(_gameManager.currentBid, proposed, maxDice);

        if (!validation.valid)
        {
            ShowError("INVALID BID: " + validation.reason);
            // Keep status unchanged apart from the error
            RefreshUI(_lastStatus);
            return;
        }

        _gameManager.MakeBid(_bidQty, _bidFace);
        RefreshUI("You bid " + proposed + ". AI's turn.");

        if (_gameManager.players[_gameManager.currentPlayerIndex].isAI)
        {
            StartCoroutine(AITurn());
        }
    }

    private void OnCall()
    {
        _errorMessage = "";
        if (_gameManager.currentBid == null)
        {
            ShowError("INVALID: No bid to call.");
            RefreshUI(_lastStatus);
            return;
        }

        _gameManager.CallBid();

        string status = string.Join("\n", _gameManager.gameLog.GetRange(0, Mathf.Min(3, _gameManager.gameLog.Count)));
        RefreshUI(status);

        CheckGameOver();

        if (!_gameManager.IsGameOver() && !_gameManager.roundActive)
        {
            StartCoroutine(StartNextRound());
        }
    }

    private void OnSpotOn()
    {
        _errorMessage = "";
        if (_gameManager.currentBid == null)
        {
            ShowError("INVALID: No bid to call spot-on.");
            RefreshUI(_lastStatus);
            return;
        }

        _gameManager.SpotOn();

        string status = string.Join("\n", _gameManager.gameLog.GetRange(0, Mathf.Min(3, _gameManager.gameLog.Count)));
        RefreshUI(status);

        CheckGameOver();

        if (!_gameManager.IsGameOver() && !_gameManager.roundActive)
        {
            StartCoroutine(StartNextRound());
        }
    }

    #endregion

    #region Turns & Rounds

    private IEnumerator AITurn()
    {
        _waitingForAI = true;
        RefreshUI("AI is thinking...");

        yield return new WaitForSeconds(Mathf.Max(0f, aiThinkSeconds));

        // Decision
        var decision = _gameManager.GetAIDecision();
        string action = decision.action;
        Bid bid = decision.bid;

        if (action == "raise")
        {
            _gameManager.MakeBid(bid.quantity, bid.value);
            _waitingForAI = false;
            RefreshUI("AI bids " + bid + ". Your turn.");
        }
        else if (action == "call")
        {
            _gameManager.CallBid();
            _waitingForAI = false;

            string status = string.Join("\n", _gameManager.gameLog.GetRange(0, Mathf.Min(3, _gameManager.gameLog.Count)));
            RefreshUI(status);

            CheckGameOver();
            if (!_gameManager.IsGameOver() && !_gameManager.roundActive)
            {
                StartCoroutine(StartNextRound());
            }
        }
        else if (action == "spoton")
        {
            _gameManager.SpotOn();
            _waitingForAI = false;

            string status = string.Join("\n", _gameManager.gameLog.GetRange(0, Mathf.Min(3, _gameManager.gameLog.Count)));
            RefreshUI(status);

            CheckGameOver();
            if (!_gameManager.IsGameOver() && !_gameManager.roundActive)
            {
                StartCoroutine(StartNextRound());
            }
        }
    }

    private IEnumerator StartNextRound()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, interRoundSeconds));

        _errorMessage = "";
        _errorUntilTime = -1f;

        _gameManager.StartNewRound();

        _bidQty = 1;
        _bidFace = 2;
        UpdateBidEditor();

        string nextStatus = "New round. " + (_gameManager.currentPlayerIndex == 0 ? "Your turn." : "AI's turn.");
        RefreshUI(nextStatus);

        if (_gameManager.players[_gameManager.currentPlayerIndex].isAI)
        {
            StartCoroutine(AITurn());
        }
    }

    #endregion

    #region Helpers

    private void DisableAllControls()
    {
        SetControlsInteractable(false);
    }

    private void SetControlsInteractable(bool isEnabled)
    {
        if (placeBidBtn) placeBidBtn.interactable = isEnabled;
        if (callBtn) callBtn.interactable = isEnabled;
        if (spotOnBtn) spotOnBtn.interactable = isEnabled;
        if (qtyMinus) qtyMinus.interactable = isEnabled;
        if (qtyPlus) qtyPlus.interactable = isEnabled;
        if (faceMinus) faceMinus.interactable = isEnabled;
        if (facePlus) facePlus.interactable = isEnabled;
    }

    private string FormatDice(System.Collections.Generic.List<int> dice, bool hidden)
    {
        if (dice == null || dice.Count == 0) return "—";
        string result = "";
        for (int i = 0; i < dice.Count; i++)
        {
            result += hidden ? "?" : (dice[i] == 1 ? "A" : dice[i].ToString());
            if (i < dice.Count - 1) result += " ";
        }
        return result;
    }

    private void ShowError(string msg)
    {
        _errorMessage = msg ?? "";
        _errorUntilTime = Time.time + Mathf.Max(0.01f, errorDisplaySeconds);
        // No immediate RefreshUI here if we're already refreshing—callers handle that.
    }

    // Public method to restart game (for freeplay/testing)
    public void RestartGame()
    {
        StartNewGame();
    }

    #endregion
}
