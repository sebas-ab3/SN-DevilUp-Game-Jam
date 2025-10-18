using System;
using System.Collections.Generic;
using UnityEngine;

namespace DudoGame
{
    [Serializable]
    public class Bid
    {
        public int quantity;
        public int value; // 1=Aces, 2-6=normal faces

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
            string valueName = value == 1 ? "Aces" : value.ToString();
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
            {
                dice.Add(UnityEngine.Random.Range(1, 7));
            }
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
                    // If checking for aces, only count aces
                    if (value == 1)
                    {
                        if (die == 1) count++;
                    }
                    // If checking for non-aces, count that value + aces (wild)
                    else
                    {
                        if (die == value || die == 1) count++;
                    }
                }
            }
            return count;
        }

        // Validate if a bid is legal based on previous bid
        public static (bool valid, string reason) IsValidBid(Bid currentBid, Bid newBid)
        {
            if (!newBid.IsValid())
            {
                return (false, "Invalid bid values");
            }

            // First bid cannot be aces
            if (currentBid == null)
            {
                if (newBid.value == 1)
                {
                    return (false, "First bid cannot be Aces");
                }
                return (true, "");
            }

            int prevQty = currentBid.quantity;
            int prevVal = currentBid.value;
            int newQty = newBid.quantity;
            int newVal = newBid.value;

            // Converting TO aces from non-aces
            if (newVal == 1 && prevVal != 1)
            {
                int minAces = Mathf.CeilToInt(prevQty / 2f);
                if (newQty < minAces)
                {
                    return (false, $"Must bid at least {minAces} Aces (half of {prevQty} rounded up)");
                }
                return (true, "");
            }

            // Converting FROM aces to non-aces
            if (newVal != 1 && prevVal == 1)
            {
                int minQty = prevQty * 2 + 1;
                if (newQty < minQty)
                {
                    return (false, $"Must bid at least {minQty} (double {prevQty} plus 1)");
                }
                return (true, "");
            }

            // Both same type (both aces or both non-aces)
            if (newVal == prevVal)
            {
                if (newQty <= prevQty)
                {
                    return (false, $"Must increase quantity above {prevQty}");
                }
                return (true, "");
            }

            // Both non-aces, different values
            if (newVal != 1 && prevVal != 1)
            {
                // Higher quantity is always valid
                if (newQty > prevQty)
                {
                    return (true, "");
                }
                // Same quantity but higher value
                if (newQty == prevQty && newVal > prevVal)
                {
                    return (true, "");
                }
                return (false, "Must increase quantity or value");
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
            players = new List<Player>();
            players.Add(new Player(playerName, startingDice, false)); // Human player
            players.Add(new Player("AI", startingDice, true));         // AI opponent
            
            gameLog = new List<string>();
            currentPlayerIndex = 0;
            roundActive = false;
        }

        public void StartNewRound()
        {
            currentBid = null;
            lastBidderIndex = -1;
            roundActive = true;

            // Roll dice for all non-eliminated players
            foreach (Player player in players)
            {
                if (!player.eliminated)
                {
                    player.RollDice();
                }
            }

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

            string valueName = currentBid.value == 1 ? "Aces" : currentBid.value.ToString();
            AddLog($"{players[caller].playerName} calls! Checking...");
            AddLog($"Actual count: {actual} {valueName}");

            int loser;
            if (actual >= currentBid.quantity)
            {
                // Bid was correct, caller loses
                loser = caller;
                AddLog($"Bid was correct! {players[caller].playerName} loses a die!");
            }
            else
            {
                // Bid was wrong, bidder loses
                loser = bidder;
                AddLog($"Bid was wrong! {players[bidder].playerName} loses a die!");
            }

            players[loser].LoseDie();
            currentPlayerIndex = loser; // Loser starts next round
        }

        public void SpotOn()
        {
            if (currentBid == null) return;

            roundActive = false;
            int actual = DudoRules.CountDice(players, currentBid.value);
            int caller = currentPlayerIndex;

            string valueName = currentBid.value == 1 ? "Aces" : currentBid.value.ToString();
            AddLog($"{players[caller].playerName} calls Spot On! Checking...");
            AddLog($"Actual count: {actual} {valueName}");

            if (actual == currentBid.quantity)
            {
                // Spot on was correct - no one loses, declarer starts
                AddLog("Spot On! No one loses dice.");
                currentPlayerIndex = lastBidderIndex;
            }
            else
            {
                // Spot on was wrong - caller loses
                AddLog($"Wrong! {players[caller].playerName} loses a die!");
                players[caller].LoseDie();
                currentPlayerIndex = caller; // Caller starts next round
            }
        }

        public bool IsGameOver()
        {
            int activePlayers = 0;
            foreach (Player p in players)
            {
                if (!p.eliminated) activePlayers++;
            }
            return activePlayers <= 1;
        }

        public Player GetWinner()
        {
            foreach (Player p in players)
            {
                if (!p.eliminated) return p;
            }
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
            float decision = UnityEngine.Random.value;

            if (currentBid == null)
            {
                // Make initial bid (cannot be aces)
                int quantity = UnityEngine.Random.Range(1, 4);
                int value = UnityEngine.Random.Range(2, 7);
                return ("raise", new Bid(quantity, value));
            }

            // Small chance to call or spot on
            if (decision < 0.15f)
            {
                return ("call", null);
            }
            else if (decision < 0.25f)
            {
                return ("spoton", null);
            }
            else
            {
                // Try to raise
                Bid newBid = GenerateAIBid();
                if (newBid != null)
                {
                    return ("raise", newBid);
                }
                else
                {
                    // If can't raise, call instead
                    return ("call", null);
                }
            }
        }

        private Bid GenerateAIBid()
        {
            if (currentBid == null)
            {
                return new Bid(UnityEngine.Random.Range(1, 4), UnityEngine.Random.Range(2, 7));
            }

            // Try different bid strategies
            List<Bid> possibleBids = new List<Bid>();

            // Try increasing quantity
            possibleBids.Add(new Bid(currentBid.quantity + 1, currentBid.value));

            // Try increasing value (same quantity)
            if (currentBid.value < 6)
            {
                possibleBids.Add(new Bid(currentBid.quantity, currentBid.value + 1));
            }

            // Try converting to aces
            if (currentBid.value != 1)
            {
                int aceQty = Mathf.CeilToInt(currentBid.quantity / 2f);
                possibleBids.Add(new Bid(aceQty, 1));
            }

            // Try converting from aces
            if (currentBid.value == 1)
            {
                int nonAceQty = currentBid.quantity * 2 + 1;
                possibleBids.Add(new Bid(nonAceQty, UnityEngine.Random.Range(2, 7)));
            }

            // Find first valid bid
            foreach (Bid bid in possibleBids)
            {
                var validation = DudoRules.IsValidBid(currentBid, bid);
                if (validation.valid)
                {
                    return bid;
                }
            }

            return null;
        }
    }
}