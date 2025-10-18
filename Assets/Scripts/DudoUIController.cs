using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DudoExact;
using System.Linq;

public class DudoExactUIController : MonoBehaviour
{
    [Header("Top Info")]
    [SerializeField] TMP_Text playerDiceLabel;
    [SerializeField] TMP_Text aiDiceCountLabel;
    [SerializeField] TMP_Text currentBidLabel;
    [SerializeField] TMP_Text statusLabel;

    [Header("Bid Editor")]
    [SerializeField] TMP_Text qtyValue;
    [SerializeField] TMP_Text faceValue;
    [SerializeField] Button qtyMinus;
    [SerializeField] Button qtyPlus;
    [SerializeField] Button faceMinus;
    [SerializeField] Button facePlus;

    [Header("Actions")]
    [SerializeField] Button btnPlaceBid;
    [SerializeField] Button btnCall;    // "Dudo"
    [SerializeField] Button btnSpotOn;  // "Spot On"

    private Match match;
    private int bidQty = 1;
    private int bidFace = 2;

    private void Awake()
    {
        match = new Match(startingDice: 5);
        WireButtons();
        RefreshAll("New round. " + (match.turnIndex == 0 ? "You start." : "AI starts."));
        if (match.turnIndex == 1) AITurnIfNeeded();
        PrepareDefaultBidEditor();
    }

    void WireButtons()
    {
        qtyMinus.onClick.AddListener(() => { bidQty = Mathf.Max(1, bidQty - 1); UpdateBidEditorClamped(); });
        qtyPlus.onClick.AddListener(() => { bidQty = Mathf.Min(30, bidQty + 1); UpdateBidEditorClamped(); });
        faceMinus.onClick.AddListener(() => { bidFace = Mathf.Max(1, bidFace - 1); UpdateBidEditorClamped(); });
        facePlus.onClick.AddListener(() => { bidFace = Mathf.Min(6, bidFace + 1); UpdateBidEditorClamped(); });

        btnPlaceBid.onClick.AddListener(OnPlaceBid);
        btnCall.onClick.AddListener(OnCall);
        btnSpotOn.onClick.AddListener(OnSpotOn);
    }

    void RefreshAll(string status)
    {
        playerDiceLabel.text  = $"Your dice: {string.Join(" ", match.playerDice)}";
        aiDiceCountLabel.text = $"AI dice: {match.aiDice.Count}";
        currentBidLabel.text  = match.currentBid.IsValid ? $"Current bid: {match.currentBid.quantity} × {match.currentBid.face}" : "Current bid: —";
        statusLabel.text      = status;

        // Interactions
        bool myTurn = match.turnIndex == 0;
        btnPlaceBid.interactable = myTurn;
        btnCall.interactable     = myTurn && match.currentBid.IsValid; // can only call on an existing bid
        btnSpotOn.interactable   = myTurn && match.currentBid.IsValid; // allowed on an existing bid

        // You cannot open with aces
        if (!match.currentBid.IsValid && bidFace == Rules.WILD)
            bidFace = 2;

        UpdateBidEditorClamped();
    }

    void PrepareDefaultBidEditor()
    {
        int pool = match.playerDice.Count + match.aiDice.Count;
        bidQty  = Mathf.Max(1, Mathf.RoundToInt(pool * (2f / 6f)));
        bidFace = 2;
        UpdateBidEditorClamped();
    }

    void UpdateBidEditorClamped()
    {
        // Enforce legality relative to current bid:
        var proposed = new Bid(bidQty, bidFace);

        if (!match.currentBid.IsValid)
        {
            // first bid cannot be aces
            if (proposed.face == Rules.WILD) proposed.face = 2;
        }
        else
        {
            // If illegal, bump toward minimal legal raise.
            if (!Rules.IsLegalRaise(match.currentBid, proposed))
                proposed = BumpToMinimalLegal(match.currentBid, proposed);
        }

        bidQty = proposed.quantity;
        bidFace = proposed.face;

        qtyValue.text = bidQty.ToString();
        faceValue.text = bidFace.ToString();
    }

    Bid BumpToMinimalLegal(Bid curr, Bid proposed)
    {
        // Try normal minimal steps respecting the exact mapping rules.
        // 1) If curr non-ace: try same qty higher face; else +1 qty face=2; else special jump to aces exactly.
        bool currA = curr.face == Rules.WILD;
        if (!curr.IsValid)
        {
            if (proposed.face == Rules.WILD) return new Bid(Mathf.Max(1, proposed.quantity), 2);
            return proposed;
        }

        if (!currA)
        {
            // If the user picked aces, force EXACT ceil(q/2)
            if (proposed.face == Rules.WILD)
                return new Bid((curr.quantity + 1) / 2, Rules.WILD);

            // else non-ace: ensure strictly higher
            if (proposed.quantity > curr.quantity) return new Bid(proposed.quantity, Mathf.Clamp(proposed.face, 2, 6));
            if (proposed.quantity == curr.quantity && proposed.face > curr.face) return new Bid(proposed.quantity, proposed.face);

            // Minimal legal non-ace raise:
            if (curr.face < 6) return new Bid(curr.quantity, curr.face + 1);
            return new Bid(curr.quantity + 1, 2);
        }
        else
        {
            // curr is aces
            if (proposed.face == Rules.WILD)
            {
                if (proposed.quantity > curr.quantity) return new Bid(proposed.quantity, Rules.WILD);
                return new Bid(curr.quantity + 1, Rules.WILD);
            }
            else
            {
                // non-ace jump must be EXACT 2*q+1
                int need = 2 * curr.quantity + 1;
                return new Bid(need, Mathf.Clamp(proposed.face, 2, 6));
            }
        }
    }

    void OnPlaceBid()
    {
        var proposed = new Bid(bidQty, bidFace);
        if ((!match.currentBid.IsValid && !Rules.IsFirstBidValid(proposed)) ||
            (match.currentBid.IsValid && !Rules.IsLegalRaise(match.currentBid, proposed)))
        {
            RefreshAll("Illegal bid per Dudo rules.");
            return;
        }

        match.currentBid = proposed;
        match.lastBidderIndex = 0; // me
        match.turnIndex = 1;       // AI
        RefreshAll($"You bid {proposed.quantity} × {proposed.face}.");
        AITurnIfNeeded();
    }

    void OnCall()
    {
        if (!match.currentBid.IsValid) { RefreshAll("No bid to call."); return; }

        // Player calls (challenges AI if AI was last bidder; or challenges last bidder in general)
        int challenger = 0;
        int bidder = match.lastBidderIndex;

        var (loserIdxScratch, msg) = Rules.ResolveCall(match.currentBid, match.playerDice, match.aiDice);
        // Decide loser: if bid was true → challenger loses; else bidder loses.
        var pool = match.playerDice.Concat(match.aiDice).ToList();
        int matchCnt = Rules.CountMatching(pool, match.currentBid);
        bool bidTrue = matchCnt >= match.currentBid.quantity;
        int loser = bidTrue ? challenger : bidder;

        match.RemoveDie(loser);
        string post = msg + $"\n{(loser==0?"You":"AI")} loses a die.";

        // Next round: loser starts
        if (!match.IsOver)
        {
            match.RollAll();
            match.turnIndex = loser;
            RefreshAll(post + $"\nNew round. {(match.turnIndex==0?"You start.":"AI starts.")}");
            if (match.turnIndex == 1) AITurnIfNeeded();
        }
        else
        {
            RefreshAll(post + "\n" + (match.playerDice.Count==0?"You lost the game.":"You won the game!"));
            DisableAll();
        }
    }

    void OnSpotOn()
    {
        if (!match.currentBid.IsValid) { RefreshAll("No bid to call spot-on."); return; }

        // Player says spot-on on last bidder's declaration
        var (exact, msg) = Rules.ResolveSpotOn(match.currentBid, match.playerDice, match.aiDice);
        string post = msg;

        if (exact)
        {
            // Round canceled; declarer starts next
            int declarer = match.lastBidderIndex;
            match.RollAll();
            match.turnIndex = declarer;
            RefreshAll(post + $"\nNew round. {(match.turnIndex==0?"You start.":"AI starts.")}");
            if (match.turnIndex == 1) AITurnIfNeeded();
        }
        else
        {
            // Spot-on caller (me) loses a die; caller starts next
            match.RemoveDie(0);
            if (!match.IsOver)
            {
                match.RollAll();
                match.turnIndex = 0;
                RefreshAll(post + "\nYou lose a die.\nNew round. You start.");
            }
            else
            {
                RefreshAll(post + "\nYou lose a die.\nYou lost the game.");
                DisableAll();
            }
        }
    }

    void AITurnIfNeeded()
    {
        if (match.IsOver || match.turnIndex != 1) return;

        var (action, next) = SimpleAI.Decide(match);
        if (action == "raise")
        {
            // Ensure legality
            if (!Rules.IsLegalRaise(match.currentBid, next))
            {
                // fall back to minimal legal
                next = ForceMinimalLegal(match.currentBid);
            }
            match.currentBid = next;
            match.lastBidderIndex = 1;
            match.turnIndex = 0;
            RefreshAll($"AI bids {next.quantity} × {next.face}. Your turn.");
            // Suggest editor near minimal legal above AI’s bid
            SuggestFromCurrent();
        }
        else if (action == "call")
        {
            // AI calls on the player's last bid
            int challenger = 1;
            int bidder = match.lastBidderIndex;

            var (loserIdxScratch, msg) = Rules.ResolveCall(match.currentBid, match.playerDice, match.aiDice);
            var pool = match.playerDice.Concat(match.aiDice).ToList();
            int matchCnt = Rules.CountMatching(pool, match.currentBid);
            bool bidTrue = matchCnt >= match.currentBid.quantity;
            int loser = bidTrue ? challenger : bidder;

            match.RemoveDie(loser);
            string post = msg + $"\n{(loser==0?"You":"AI")} loses a die.";

            if (!match.IsOver)
            {
                match.RollAll();
                match.turnIndex = loser; // loser starts
                RefreshAll(post + $"\nNew round. {(match.turnIndex==0?"You start.":"AI starts.")}");
                if (match.turnIndex == 1) AITurnIfNeeded();
            }
            else
            {
                RefreshAll(post + "\n" + (match.playerDice.Count==0?"You lost the game.":"You won the game!"));
                DisableAll();
            }
        }
        else // spot-on by AI
        {
            var (exact, msg) = Rules.ResolveSpotOn(match.currentBid, match.playerDice, match.aiDice);
            string post = msg;

            if (exact)
            {
                int declarer = match.lastBidderIndex;
                match.RollAll();
                match.turnIndex = declarer; // declarer starts
                RefreshAll(post + $"\nNew round. {(match.turnIndex==0?"You start.":"AI starts.")}");
                if (match.turnIndex == 1) AITurnIfNeeded();
            }
            else
            {
                // AI (caller) loses a die; AI starts next
                match.RemoveDie(1);
                if (!match.IsOver)
                {
                    match.RollAll();
                    match.turnIndex = 1;
                    RefreshAll(post + "\nAI loses a die.\nNew round. AI starts.");
                    AITurnIfNeeded();
                }
                else
                {
                    RefreshAll(post + "\nAI loses a die.\nYou won the game!");
                    DisableAll();
                }
            }
        }
    }

    Bid ForceMinimalLegal(Bid curr)
    {
        if (!curr.IsValid) return new Bid(1, 2);
        bool currA = curr.face == Rules.WILD;
        if (!currA)
        {
            if (curr.face < 6) return new Bid(curr.quantity, curr.face + 1);
            return new Bid(curr.quantity + 1, 2);
        }
        else
        {
            return new Bid(curr.quantity + 1, Rules.WILD); // prefer aces+1
        }
    }

    void SuggestFromCurrent()
    {
        if (!match.currentBid.IsValid)
        {
            PrepareDefaultBidEditor();
            return;
        }

        var c = match.currentBid;
        // Suggest the minimal legal above current
        if (c.face != Rules.WILD)
        {
            if (c.face < 6) { bidQty = c.quantity; bidFace = c.face + 1; }
            else { bidQty = c.quantity + 1; bidFace = 2; }
        }
        else
        {
            bidQty = c.quantity + 1; bidFace = Rules.WILD;
        }
        UpdateBidEditorClamped();
    }

    void DisableAll()
    {
        btnPlaceBid.interactable = false;
        btnCall.interactable = false;
        btnSpotOn.interactable = false;
        qtyMinus.interactable = qtyPlus.interactable = faceMinus.interactable = facePlus.interactable = false;
    }
}
