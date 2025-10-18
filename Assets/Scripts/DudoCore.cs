using System;
using System.Collections.Generic;
using UnityEngine;

namespace DudoGame
{
    [Serializable]
    public class Bid
    {
        public int quantity;
        public int value; // 1=Aces, 2–6=normal faces

        public Bid(int q, int v)
        {
            quantity = q;
            value = v;
        }

        public bool IsValid()
        {
            return quantity > 0 && value >= 1 && value <= 6;
        }

        public override string ToString()
        {
            string valueName = (value == 1) ? "Aces" : value.ToString();
            return $"{quantity} {valueName}";
        }
    }

    [Serializable]
    public class Player
    {
        public string playerName;
        public List<int> dice;
        public int diceCount;
        public bool isAI;
        public bool eliminated;

        public Player(string name, int startingDice, bool ai)
        {
            playerName = name;
            diceCount = startingDice;
            isAI = ai;
            eliminated = false;
            dice = new List<int>();
        }

        public void RollDice()
        {
            dice.Clear();
            for (int i = 0; i < diceCount; i++)
                dice.Add(UnityEngine.Random.Range(1, 7));
        }

        public void LoseDie()
        {
            diceCount--;
            if (diceCount <= 0)
            {
                eliminated = true;
                diceCount = 0;
            }
        }
    }

    public static class DudoRules
    {
        // Count how many dice match the bid (aces are wild for non-ace bids)
        public static int CountDice(List<Player> players, int value)
        {
            int count = 0;
            foreach (Player player in players)
            {
                if (player.eliminated) continue;
                foreach (int die in player.dice)
                {
                    if (value == 1)
                    {
                        if (die == 1) count++;
                    }
                    else
                    {
                        if (die == value || die == 1) count++;
                    }
                }
            }
            return count;
        }

        // Validation with table maximum (totalDiceInPlay) awareness
        public static (bool valid, string reason) IsValidBid(Bid currentBid, Bid newBid, int totalDiceInPlay = -1)
        {
            if (!newBid.IsValid())
                return (false, "Invalid bid values");

            // Absolute cap: cannot exceed the total dice in play
            if (totalDiceInPlay > 0 && newBid.quantity > totalDiceInPlay)
                return (false, $"Cannot bid more than the table maximum of {totalDiceInPlay}");

            // First bid cannot be aces
            if (currentBid == null)
            {
                if (newBid.value == 1)
                    return (false, "First bid cannot be Aces");
                return (true, "");
            }

            int prevQty = currentBid.quantity;
            int prevVal = currentBid.value;
            int newQty  = newBid.quantity;
            int newVal  = newBid.value;

            // If quantity is already at the table max, the only legal raise is a higher face (same qty)
            if (totalDiceInPlay > 0 && prevQty >= totalDiceInPlay)
            {
                if (newQty != prevQty)
                    return (false, $"Quantity is already at the table maximum ({totalDiceInPlay}); you must increase the face value");
                if (newVal <= prevVal)
                    return (false, $"Must increase face value above {prevVal}");
                return (true, "");
            }

            // Converting TO aces from non-aces
            if (newVal == 1 && prevVal != 1)
            {
                int minAces = Mathf.CeilToInt(prevQty / 2f) + 1; // one above half
                if (newQty < minAces)
                    return (false, $"Must bid at least {minAces} Aces (one above half of {prevQty})");
                return (true, "");
            }

            // Converting FROM aces to non-aces
            if (newVal != 1 && prevVal == 1)
            {
                int minQty = prevQty * 2 + 1;
                if (newQty < minQty)
                {
                    // Exception: allow exactly the table maximum even if below 2x+1
                    if (totalDiceInPlay > 0 && newQty == totalDiceInPlay)
                        return (true, "");
                    return (false, $"Must bid at least {minQty} (double {prevQty} plus 1), or use the table maximum of {totalDiceInPlay}");
                }
                return (true, "");
            }

            // Same face (aces->aces or same non-ace face): quantity must strictly increase
            if (newVal == prevVal)
            {
                if (newQty <= prevQty)
                    return (false, $"Must increase quantity above {prevQty}");
                return (true, "");
            }

            // Both non-aces, different values
            if (newVal != 1 && prevVal != 1)
            {
                if (newQty > prevQty) return (true, "");              // raising quantity OK
                if (newQty == prevQty && newVal > prevVal) return (true, ""); // same qty, higher face OK

                if (newQty == prevQty && newVal <= prevVal)
                    return (false, $"Must increase face value above {prevVal}");

                if (newQty < prevQty && newVal > prevVal)
                    return (false, $"Cannot decrease quantity below {prevQty} when increasing face value");

                return (false, $"Must increase quantity above {prevQty} or (with same quantity) increase face value above {prevVal}");
            }

            return (false, "Invalid bid");
        }
    }

    public class DudoGameManager
    {
        public List<Player> players;
        public int currentPlayerIndex;
        public Bid currentBid;
        public int lastBidderIndex;
        public bool roundActive;
        public List<string> gameLog;

        public DudoGameManager(string playerName, int startingDice = 5)
        {
            players = new List<Player>
            {
                new Player(playerName, startingDice, false),
                new Player("AI", startingDice, true)
            };
            gameLog = new List<string>();
            currentPlayerIndex = 0;
            roundActive = false;
        }

        public void StartNewRound()
        {
            currentBid = null;
            lastBidderIndex = -1;
            roundActive = true;

            foreach (Player player in players)
                if (!player.eliminated) player.RollDice();

            AddLog($"New round started. {players[currentPlayerIndex].playerName} goes first.");
        }

        public void MakeBid(int quantity, int value)
        {
            currentBid = new Bid(quantity, value);
            lastBidderIndex = currentPlayerIndex;
            AddLog($"{players[currentPlayerIndex].playerName} bids {currentBid}");
            NextTurn();
        }

        public void CallBid()
        {
            if (currentBid == null) return;

            roundActive = false;
            int actual = DudoRules.CountDice(players, currentBid.value);
            int bidder = lastBidderIndex;
            int caller = currentPlayerIndex;

            string valueName = (currentBid.value == 1) ? "Aces" : currentBid.value.ToString();
            AddLog($"{players[caller].playerName} calls! Checking...");
            AddLog($"Actual count: {actual} {valueName}");

            int loser = (actual >= currentBid.quantity) ? caller : bidder;
            AddLog((actual >= currentBid.quantity)
                ? $"Bid was correct! {players[caller].playerName} loses a die!"
                : $"Bid was wrong! {players[bidder].playerName} loses a die!");

            players[loser].LoseDie();
            currentPlayerIndex = loser; // Loser starts next round
        }

        public void SpotOn()
        {
            if (currentBid == null) return;

            roundActive = false;
            int actual = DudoRules.CountDice(players, currentBid.value);
            int caller = currentPlayerIndex;

            string valueName = (currentBid.value == 1) ? "Aces" : currentBid.value.ToString();
            AddLog($"{players[caller].playerName} calls Spot On! Checking...");
            AddLog($"Actual count: {actual} {valueName}");

            if (actual == currentBid.quantity)
            {
                AddLog("Spot On! No one loses dice.");
                currentPlayerIndex = lastBidderIndex; // declarer starts
            }
            else
            {
                AddLog($"Wrong! {players[caller].playerName} loses a die!");
                players[caller].LoseDie();
                currentPlayerIndex = caller; // caller starts next
            }
        }

        public bool IsGameOver()
        {
            int active = 0;
            foreach (Player p in players) if (!p.eliminated) active++;
            return active <= 1;
        }

        public Player GetWinner()
        {
            foreach (Player p in players) if (!p.eliminated) return p;
            return null;
        }

        private void NextTurn()
        {
            do
            {
                currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
            } while (players[currentPlayerIndex].eliminated);
        }

        public void AddLog(string message)
        {
            gameLog.Insert(0, message);
            if (gameLog.Count > 20) gameLog.RemoveAt(20);
        }

        // AI Decision Making
        public (string action, Bid bid) GetAIDecision()
        {
            int maxDice = GetTotalDiceInPlay();
            float decision = UnityEngine.Random.value;

            // If we are already at (qty == maxDice, face == 6), AI must call to avoid infinite loop
            if (currentBid != null && currentBid.quantity >= maxDice && currentBid.value >= 6)
                return ("call", null);

            if (currentBid == null)
            {
                // initial bid (non-aces), cap quantity to maxDice
                int quantity = Mathf.Clamp(UnityEngine.Random.Range(1, 4), 1, Mathf.Max(1, maxDice));
                int value = UnityEngine.Random.Range(2, 7);
                return ("raise", new Bid(quantity, value));
            }

            // Small chance to call or spot on
            if (decision < 0.15f)      return ("call", null);
            else if (decision < 0.25f) return ("spoton", null);

            // Try to raise, respecting maxDice and the "must raise face when qty==maxDice" rule
            Bid newBid = GenerateAIBid(maxDice);
            if (newBid != null) return ("raise", newBid);

            // If can't raise, call instead
            return ("call", null);
        }

        private Bid GenerateAIBid(int maxDice)
        {
            if (currentBid == null)
                return new Bid(Mathf.Clamp(UnityEngine.Random.Range(1, 4), 1, Mathf.Max(1, maxDice)),
                               UnityEngine.Random.Range(2, 7));

            var possible = new List<Bid>();

            // If quantity already at maxDice: only face+1 (same qty) is legal
            if (currentBid.quantity >= maxDice)
            {
                if (currentBid.value < 6)
                    possible.Add(new Bid(currentBid.quantity, currentBid.value + 1));
            }
            else
            {
                // Increase quantity (same face) if it won't exceed maxDice
                if (currentBid.quantity + 1 <= maxDice)
                    possible.Add(new Bid(currentBid.quantity + 1, currentBid.value));

                // Increase face (same qty)
                if (currentBid.value < 6)
                    possible.Add(new Bid(currentBid.quantity, currentBid.value + 1));

                // Convert to aces
                if (currentBid.value != 1)
                {
                    int aceQty = Mathf.CeilToInt(currentBid.quantity / 2f) + 1; // align with player rule (one above half)
                    aceQty = Mathf.Min(aceQty, maxDice);
                    possible.Add(new Bid(aceQty, 1));
                }

                // Convert from aces
                if (currentBid.value == 1)
                {
                    int nonAceQty = currentBid.quantity * 2 + 1;
                    nonAceQty = Mathf.Min(nonAceQty, maxDice); // respect table max (exception handled in rules)
                    possible.Add(new Bid(nonAceQty, UnityEngine.Random.Range(2, 7)));
                }
            }

            // Return first valid candidate
            foreach (Bid b in possible)
            {
                var val = DudoRules.IsValidBid(currentBid, b, maxDice);
                if (val.valid) return b;
            }

            return null;
        }

        private int GetTotalDiceInPlay()
        {
            int total = 0;
            foreach (var p in players) if (!p.eliminated) total += p.diceCount;
            return total;
        }
    }
}
