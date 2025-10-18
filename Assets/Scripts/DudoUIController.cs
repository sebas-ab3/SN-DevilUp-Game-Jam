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

    private DudoGameManager _gameManager;
    private int _bidQty = 1;
    private int _bidFace = 2;
    private bool _waitingForAI = false;
    private bool _gameOver = false;
    private string _errorMessage = "";

    private void Start()
    {
        SetupUI();
        StartNewGame();
    }

    private void Update()
    {
        // Check for Enter key to restart game when game is over
        if (_gameOver && Input.GetKeyDown(KeyCode.Return))
        {
            StartNewGame();
        }
    }

    private void SetupUI()
    {
        // Wire up bid editor buttons
        qtyMinus.onClick.AddListener(() => { 
            _bidQty = Mathf.Max(1, _bidQty - 1); 
            UpdateBidEditor(); 
        });
        qtyPlus.onClick.AddListener(() => { 
            _bidQty = Mathf.Min(10, _bidQty + 1); 
            UpdateBidEditor(); 
        });
        faceMinus.onClick.AddListener(() => { 
            _bidFace = Mathf.Max(1, _bidFace - 1); 
            UpdateBidEditor(); 
        });
        facePlus.onClick.AddListener(() => { 
            _bidFace = Mathf.Min(6, _bidFace + 1); 
            UpdateBidEditor(); 
        });

        // Wire up action buttons
        placeBidBtn.onClick.AddListener(OnPlaceBid);
        callBtn.onClick.AddListener(OnCall);
        spotOnBtn.onClick.AddListener(OnSpotOn);

        // Set button labels
        callBtn.GetComponentInChildren<TMP_Text>().text = "Dudo";
        spotOnBtn.GetComponentInChildren<TMP_Text>().text = "Spot On";
    }

    private void StartNewGame()
    {
        _gameManager = new DudoGameManager(playerName, startingDice);
        _gameManager.StartNewRound();
        _errorMessage = "";
        _gameOver = false;
        
        // Set initial bid editor values
        _bidQty = 1;
        _bidFace = 2;
        UpdateBidEditor();
        
        RefreshUI("New round. " + (_gameManager.currentPlayerIndex == 0 ? "Your turn." : "AI's turn."));

        if (_gameManager.players[_gameManager.currentPlayerIndex].isAI)
        {
            StartCoroutine(AITurn());
        }
    }

    private void RefreshUI(string status)
    {
        // Update top info
        Player player = _gameManager.players[0];
        Player ai = _gameManager.players[1];

        playerDiceLabel.text = $"Your dice: {FormatDice(player.dice, false)}";
        
        // Show AI dice as question marks during active rounds, reveal after
        aiDiceCountLabel.text = _gameManager.roundActive ? 
            $"AI dice: {FormatDice(ai.dice, true)}" : 
            $"AI dice: {FormatDice(ai.dice, false)}";

        if (_gameManager.currentBid != null)
        {
            currentBidLabel.text = $"Current bid: {_gameManager.currentBid}";
        }
        else
        {
            currentBidLabel.text = "Current bid: —";
        }

        // Update status (include error message if present)
        statusLabel.text = _errorMessage != "" ? $"<color=red>{_errorMessage}</color>\n\n{status}" : status;

        // Update button interactability
        bool isPlayerTurn = _gameManager.currentPlayerIndex == 0 && !_waitingForAI && _gameManager.roundActive;
        bool hasBid = _gameManager.currentBid != null;

        placeBidBtn.interactable = isPlayerTurn;
        callBtn.interactable = isPlayerTurn && hasBid;
        spotOnBtn.interactable = isPlayerTurn && hasBid;

        qtyMinus.interactable = isPlayerTurn;
        qtyPlus.interactable = isPlayerTurn;
        faceMinus.interactable = isPlayerTurn;
        facePlus.interactable = isPlayerTurn;

        // Show/hide panels based on game state
        bool shouldShowControls = isPlayerTurn || (!_gameManager.roundActive && _gameManager.currentPlayerIndex == 0);
        bidEditorRow.SetActive(shouldShowControls);
        actionsRow.SetActive(shouldShowControls);

        UpdateBidEditor();
    }

    private void UpdateBidEditor()
    {
        // Clamp to valid ranges
        _bidQty = Mathf.Clamp(_bidQty, 1, 10);
        _bidFace = Mathf.Clamp(_bidFace, 1, 6);
        
        // First bid cannot be aces
        if (_gameManager.currentBid == null)
        {
            if (_bidFace == 1) _bidFace = 2;
        }
        else
        {
            // Check if current selection is valid
            Bid proposed = new Bid(_bidQty, _bidFace);
            var validation = DudoRules.IsValidBid(_gameManager.currentBid, proposed);
            
            // Only auto-correct if the bid is actually illegal
            // This allows free movement as long as it stays legal
            if (!validation.valid)
            {
                // Try to salvage the bid by adjusting minimally
                proposed = AdjustToLegalBid(_gameManager.currentBid, _bidQty, _bidFace);
                _bidQty = proposed.quantity;
                _bidFace = proposed.value;
            }
        }

        qtyValue.text = _bidQty.ToString();
        faceValue.text = _bidFace == 1 ? "A" : _bidFace.ToString();
    }

    private Bid AdjustToLegalBid(Bid current, int attemptedQty, int attemptedFace)
    {
        // If user is trying to go lower than current bid with same/lower face, bump the face up
        if (attemptedQty <= current.quantity && attemptedFace <= current.value && 
            current.value != 1 && attemptedFace != 1)
        {
            // Same quantity - must increase face
            if (attemptedQty == current.quantity)
            {
                return new Bid(current.quantity, Mathf.Min(6, current.value + 1));
            }
            // Lower quantity - must increase quantity to minimum legal
            else
            {
                return new Bid(current.quantity, Mathf.Min(6, current.value + 1));
            }
        }

        // For ace conversions, use the minimal legal bid
        return GetMinimalLegalBid(current);
    }

    private Bid GetMinimalLegalBid(Bid current)
    {
        if (current == null) return new Bid(1, 2);

        // Try increasing quantity first
        Bid test = new Bid(current.quantity + 1, current.value);
        if (DudoRules.IsValidBid(current, test).valid)
            return test;

        // Try increasing face value
        if (current.value < 6)
        {
            test = new Bid(current.quantity, current.value + 1);
            if (DudoRules.IsValidBid(current, test).valid)
                return test;
        }

        // Try converting to aces
        if (current.value != 1)
        {
            int aceQty = Mathf.CeilToInt(current.quantity / 2f);
            test = new Bid(aceQty, 1);
            if (DudoRules.IsValidBid(current, test).valid)
                return test;
        }

        // Try converting from aces
        if (current.value == 1)
        {
            test = new Bid(current.quantity * 2 + 1, 2);
            if (DudoRules.IsValidBid(current, test).valid)
                return test;
        }

        // Fallback
        return new Bid(current.quantity + 1, 2);
    }

    private void OnPlaceBid()
    {
        _errorMessage = "";
        Bid proposed = new Bid(_bidQty, _bidFace);
        var validation = DudoRules.IsValidBid(_gameManager.currentBid, proposed);

        if (!validation.valid)
        {
            _errorMessage = $"INVALID BID: {validation.reason}";
            RefreshUI(statusLabel.text.Split('\n')[statusLabel.text.Split('\n').Length - 1]);
            return;
        }

        _gameManager.MakeBid(_bidQty, _bidFace);
        RefreshUI($"You bid {proposed}. AI's turn.");

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
            _errorMessage = "INVALID: No bid to call.";
            RefreshUI(statusLabel.text);
            return;
        }

        _gameManager.CallBid();
        
        // Build status from game log
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
            _errorMessage = "INVALID: No bid to call spot-on.";
            RefreshUI(statusLabel.text);
            return;
        }

        _gameManager.SpotOn();
        
        // Build status from game log
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

        yield return new WaitForSeconds(1.5f);

        var (action, bid) = _gameManager.GetAIDecision();

        if (action == "raise")
        {
            _gameManager.MakeBid(bid.quantity, bid.value);
            _waitingForAI = false;
            RefreshUI($"AI bids {bid}. Your turn.");
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
        yield return new WaitForSeconds(4f);
        
        _errorMessage = "";
        _gameManager.StartNewRound();
        
        // Reset bid editor to reasonable defaults
        _bidQty = 1;
        _bidFace = 2;
        UpdateBidEditor();
        
        RefreshUI("New round. " + (_gameManager.currentPlayerIndex == 0 ? "Your turn." : "AI's turn."));

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
            Player loser = winner == _gameManager.players[0] ? _gameManager.players[1] : _gameManager.players[0];
            
            string finalStatus = $"{loser.playerName} has lost all dice!\n\n";
            finalStatus += $"🏆 {winner.playerName} WINS! 🏆\n\n";
            finalStatus += "Press ENTER to play again";
            
            RefreshUI(finalStatus);
            
            // Disable all buttons
            placeBidBtn.interactable = false;
            callBtn.interactable = false;
            spotOnBtn.interactable = false;
            qtyMinus.interactable = false;
            qtyPlus.interactable = false;
            faceMinus.interactable = false;
            facePlus.interactable = false;
        }
    }

    private string FormatDice(System.Collections.Generic.List<int> dice, bool hidden)
    {
        if (dice.Count == 0) return "—";
        
        string result = "";
        for (int i = 0; i < dice.Count; i++)
        {
            if (hidden)
            {
                result += "?";
            }
            else
            {
                result += dice[i] == 1 ? "A" : dice[i].ToString();
            }
            if (i < dice.Count - 1) result += " ";
        }
        return result;
    }

    // Public method to restart game (call this from another script or button)
    public void RestartGame()
    {
        StartNewGame();
    }
}