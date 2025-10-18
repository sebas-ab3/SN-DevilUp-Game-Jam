using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DudoGame;
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
    [SerializeField] private float errorHideSeconds = 2.0f;

    private DudoGameManager _gameManager;
    private int _bidQty = 1;
    private int _bidFace = 2;
    private bool _waitingForAI = false;
    private bool _gameOver = false;
    private string _errorMessage = "";
    private string _lastStatus = "";

    private Coroutine _errorClearRoutine;

    private void Start()
    {
        SetupUI();
        StartNewGame();
    }

    private void Update()
    {
        if (_gameOver && Input.GetKeyDown(KeyCode.Return))
        {
            StartNewGame();
        }
    }

    private void SetupUI()
    {
        qtyMinus.onClick.AddListener(OnQtyMinus);
        qtyPlus.onClick.AddListener(OnQtyPlus);
        faceMinus.onClick.AddListener(OnFaceMinus);
        facePlus.onClick.AddListener(OnFacePlus);

        placeBidBtn.onClick.AddListener(OnPlaceBid);
        callBtn.onClick.AddListener(OnCall);
        spotOnBtn.onClick.AddListener(OnSpotOn);

        TMP_Text callTxt = callBtn.GetComponentInChildren<TMP_Text>();
        if (callTxt != null) callTxt.text = "Dudo";
        TMP_Text spotTxt = spotOnBtn.GetComponentInChildren<TMP_Text>();
        if (spotTxt != null) spotTxt.text = "Spot On";
    }

    private void OnQtyMinus()
    {
        _bidQty = Mathf.Max(1, _bidQty - 1);
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
        _lastStatus = status;

        Player player = _gameManager.players[0];
        Player ai = _gameManager.players[1];

        if (playerDiceLabel != null)
            playerDiceLabel.text = "Your dice: " + FormatDice(player.dice, false);

        if (aiDiceCountLabel != null)
        {
            bool hideAI = _gameManager.roundActive;
            aiDiceCountLabel.text = hideAI
                ? "AI dice: " + FormatDice(ai.dice, true)
                : "AI dice: " + FormatDice(ai.dice, false);
        }

        if (currentBidLabel != null)
            currentBidLabel.text = _gameManager.currentBid != null ? ("Current bid: " + _gameManager.currentBid.ToString()) : "Current bid: —";

        if (statusLabel != null)
        {
            statusLabel.text = string.IsNullOrEmpty(_errorMessage)
                ? status
                : "<color=red>" + _errorMessage + "</color>\n\n" + status;
        }

        bool isPlayerTurn = IsPlayerTurn();
        bool hasBid = _gameManager.currentBid != null;

        placeBidBtn.interactable = isPlayerTurn;
        callBtn.interactable = isPlayerTurn && hasBid;
        spotOnBtn.interactable = isPlayerTurn && hasBid;

        qtyMinus.interactable = isPlayerTurn;
        qtyPlus.interactable = isPlayerTurn;
        faceMinus.interactable = isPlayerTurn;
        facePlus.interactable = isPlayerTurn;

        bool shouldShowControls = isPlayerTurn || (!_gameManager.roundActive && _gameManager.currentPlayerIndex == 0);
        if (bidEditorRow != null) bidEditorRow.SetActive(shouldShowControls);
        if (actionsRow != null) actionsRow.SetActive(shouldShowControls);

        UpdateBidEditor();
    }

    private bool IsPlayerTurn()
    {
        return _gameManager.currentPlayerIndex == 0 && !_waitingForAI && _gameManager.roundActive;
    }

    private void UpdateBidEditor()
    {
        int maxDice = GetTotalDiceInPlay();

        if (maxDice < 1)
        {
            SetControlsInteractable(false);
            if (qtyValue != null) qtyValue.text = "-";
            if (faceValue != null) faceValue.text = "-";
            return;
        }

        // Start with general clamp
        _bidQty = Mathf.Clamp(_bidQty, 1, maxDice);
        _bidFace = Mathf.Clamp(_bidFace, 1, 6);

        // Compute dynamic minimum quantity based on current bid/rules
        int minQty = 1;
        if (_gameManager.currentBid == null)
        {
            // First bid cannot be aces
            if (_bidFace == 1) _bidFace = 2;
            _errorMessage = ""; // do not show errors while editing
        }
        else
        {
            Bid current = _gameManager.currentBid;
            int prevQty = current.quantity;
            int prevVal = current.value;

            if (_bidFace == 1 && prevVal != 1)
            {
                // to-aces: one above half
                minQty = Mathf.CeilToInt(prevQty / 2f) + 1;
            }
            else if (_bidFace != 1 && prevVal == 1)
            {
                // from-aces: 2x + 1, but user can always choose maxDice (exception handled by rules on Place)
                int req = prevQty * 2 + 1;
                minQty = Mathf.Min(req, maxDice); // let them pick maxDice if req > maxDice
            }
            else if (_bidFace == prevVal)
            {
                // same face: must increase quantity
                minQty = prevQty + 1;
            }
            else if (_bidFace != 1 && prevVal != 1 && _bidFace > prevVal)
            {
                // both non-aces, higher face: quantity must be >= prevQty (lock from going below)
                minQty = prevQty;
            }
            else
            {
                // any other odd combination: keep at least 1, validation on Place will handle
                minQty = 1;
            }
        }

        // Apply dynamic min clamp and update minus button affordance
        _bidQty = Mathf.Clamp(_bidQty, Mathf.Max(1, minQty), maxDice);
        if (qtyMinus != null) qtyMinus.interactable = IsPlayerTurn() && _bidQty > Mathf.Max(1, minQty);

        // Update labels
        if (qtyValue != null) qtyValue.text = _bidQty.ToString();
        if (faceValue != null) faceValue.text = (_bidFace == 1) ? "A" : _bidFace.ToString();

        // Suppress error messages while editing; only show on explicit actions
        if (!IsPlayerTurn())
        {
            _errorMessage = "";
        }
    }

    private int GetTotalDiceInPlay()
    {
        int total = 0;
        foreach (Player p in _gameManager.players)
        {
            if (!p.eliminated)
            {
                total += p.diceCount;
            }
        }
        return total;
    }

    private void OnPlaceBid()
    {
        // Validate at action time only
        _errorMessage = "";
        Bid proposed = new Bid(_bidQty, _bidFace);
        int totalDice = GetTotalDiceInPlay();
        var validation = DudoRules.IsValidBid(_gameManager.currentBid, proposed, totalDice);

        if (!validation.valid)
        {
            ShowError("INVALID BID: " + validation.reason);
            RefreshUI(_lastStatus);
            return;
        }

        _gameManager.MakeBid(_bidQty, _bidFace);
        RefreshUI("You bid " + proposed.ToString() + ". AI's turn.");

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

    private IEnumerator AITurn()
    {
        _waitingForAI = true;
        RefreshUI("AI is thinking...");

        yield return new WaitForSeconds(Mathf.Max(0f, aiThinkSeconds));

        var decision = _gameManager.GetAIDecision();
        string action = decision.action;
        Bid bid = decision.bid;

        if (action == "raise")
        {
            _gameManager.MakeBid(bid.quantity, bid.value);
            _waitingForAI = false;
            RefreshUI("AI bids " + bid.ToString() + ". Your turn.");
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

    private void CheckGameOver()
    {
        if (_gameManager.IsGameOver())
        {
            _gameOver = true;
            Player winner = _gameManager.GetWinner();
            Player loser = (winner == _gameManager.players[0]) ? _gameManager.players[1] : _gameManager.players[0];

            string finalStatus = loser.playerName + " has lost all dice!\n\n";
            finalStatus += winner.playerName + " WINS!\n\n";
            finalStatus += "Press ENTER to play again";

            RefreshUI(finalStatus);
            DisableAllControls();
        }
    }

    private void DisableAllControls()
    {
        SetControlsInteractable(false);
    }

    private void SetControlsInteractable(bool enabled)
    {
        if (placeBidBtn != null) placeBidBtn.interactable = enabled;
        if (callBtn != null) callBtn.interactable = enabled;
        if (spotOnBtn != null) spotOnBtn.interactable = enabled;
        if (qtyMinus != null) qtyMinus.interactable = enabled;
        if (qtyPlus != null) qtyPlus.interactable = enabled;
        if (faceMinus != null) faceMinus.interactable = enabled;
        if (facePlus != null) facePlus.interactable = enabled;
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

    // Error helpers (auto-hide)
    private void ShowError(string msg)
    {
        _errorMessage = msg;
        if (_errorClearRoutine != null) StopCoroutine(_errorClearRoutine);
        if (!string.IsNullOrEmpty(_errorMessage) && errorHideSeconds > 0f)
            _errorClearRoutine = StartCoroutine(ClearErrorAfterDelay());
    }

    private IEnumerator ClearErrorAfterDelay()
    {
        yield return new WaitForSeconds(errorHideSeconds);
        _errorMessage = "";
        RefreshUI(_lastStatus);
        _errorClearRoutine = null;
    }

    public void RestartGame()
    {
        StartNewGame();
    }
}
