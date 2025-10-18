using System;
using System.Collections.Generic;
using System.Linq;

namespace DudoExact
{
    [Serializable]
    public struct Bid : IEquatable<Bid>
    {
        public int quantity; // >= 1
        public int face;     // 1..6 (1 = aces)

        public Bid(int q, int f) { quantity = q; face = f; }
        public bool IsValid => quantity > 0 && face >= 1 && face <= 6;

        public override string ToString() => $"{quantity} × {face}";
        public bool Equals(Bid other) => quantity == other.quantity && face == other.face;
    }

    public static class Rules
    {
        public const int WILD = 1;

        /// Count matches for a bid across all dice (aces are wild for non-ace bids only).
        public static int CountMatching(IReadOnlyList<int> pool, Bid bid)
        {
            if (!bid.IsValid) return 0;
            int cnt = 0;
            foreach (var d in pool)
            {
                if (bid.face == WILD) { if (d == WILD) cnt++; }
                else { if (d == bid.face || d == WILD) cnt++; }
            }
            return cnt;
        }

        /// First bid must not be aces.
        public static bool IsFirstBidValid(Bid first) => first.IsValid && first.face != WILD;

        /// LEGALITY of a raise per Dudo rules you gave:
        /// - From non-ace (2..6) → non-ace: normal ordering (higher qty OR same qty & higher face).
        /// - From non-ace → aces(1): ONLY allowed if next.qty == ceil(curr.qty/2) and next.face == 1.
        /// - From aces(1) → aces(1): ONLY allowed if next.qty > curr.qty.
        /// - From aces(1) → non-ace(2..6): ONLY allowed if next.qty == 2*curr.qty + 1 and next.face in 2..6.
        public static bool IsLegalRaise(Bid current, Bid next)
        {
            if (!next.IsValid) return false;

            // No current bid → next must be a valid opener (non-aces).
            if (!current.IsValid) return IsFirstBidValid(next);

            bool currA = current.face == WILD;
            bool nextA = next.face == WILD;

            if (!currA && !nextA)
            {
                // Normal ordering: strictly higher in (qty,face) lexicographic sense.
                if (next.quantity > current.quantity) return true;
                if (next.quantity == current.quantity && next.face > current.face) return true;
                return false;
            }
            else if (!currA && nextA)
            {
                // Non-ace → aces jump: EXACT ceil(q/2)
                int need = (current.quantity + 1) / 2; // ceil
                return next.quantity == need && next.face == WILD;
            }
            else if (currA && nextA)
            {
                // Aces → more aces: strictly increase quantity
                return next.quantity > current.quantity && next.face == WILD;
            }
            else // currA && !nextA
            {
                // Aces → non-ace jump: EXACT 2*q + 1
                int need = 2 * current.quantity + 1;
                return next.quantity == need && next.face >= 2 && next.face <= 6;
            }
        }

        /// Resolve a CALL (Dudo). Returns loserIndex (0=Player,1=AI) and a message.
        public static (int loserIndex, string msg) ResolveCall(Bid lastBid, IReadOnlyList<int> pDice, IReadOnlyList<int> aiDice)
        {
            var pool = pDice.Concat(aiDice).ToList();
            int match = CountMatching(pool, lastBid);
            bool bidTrue = match >= lastBid.quantity;

            // Challenger is the one who said "Call/Dudo!" on previous bidder.
            // If bid was true → challenger loses; else bidder loses.
            // We'll let the caller decide who challenged; here we just produce info.
            string reveal = $"Reveal: Player [{FormatDice(pDice)}], AI [{FormatDice(aiDice)}] → {match} matching {lastBid}.";
            string verdict = bidTrue ? "Bid holds – challenger loses a die." : "Bid was false – bidder loses a die.";
            return (-1, $"{reveal}\n{verdict}"); // caller of this decides loser index
        }

        /// Resolve a SPOT ON. If exact -> cancel round (no one loses), restart with the DECLARER.
        /// If not exact -> spot-on caller loses a die and starts next round (loser starts).
        public static (bool exact, string msg) ResolveSpotOn(Bid lastBid, IReadOnlyList<int> pDice, IReadOnlyList<int> aiDice)
        {
            var pool = pDice.Concat(aiDice).ToList();
            int match = CountMatching(pool, lastBid);
            bool exact = match == lastBid.quantity;
            string reveal = $"Reveal: Player [{FormatDice(pDice)}], AI [{FormatDice(aiDice)}] → {match} matching {lastBid}.";
            string verdict = exact ? "Spot On! Round canceled; declarer starts a new round." :
                                     "Not exact. Spot-on caller loses a die and starts next round.";
            return (exact, $"{reveal}\n{verdict}");
        }

        public static string FormatDice(IReadOnlyList<int> dice) => string.Join(" ", dice.Select(d => d.ToString()));
    }

    public class CupRng
    {
        readonly Random _rng;
        public CupRng(int? seed = null) { _rng = seed.HasValue ? new Random(seed.Value) : new Random(); }

        public void Roll(List<int> dice)
        {
            for (int i = 0; i < dice.Count; i++) dice[i] = _rng.Next(1, 7);
        }
    }

    public class Match
    {
        public List<int> playerDice = new();
        public List<int> aiDice = new();
        public Bid currentBid;       // invalid => no bid yet
        public int lastBidderIndex;  // 0 player, 1 AI (valid only if currentBid.IsValid)
        public int turnIndex;        // whose turn to act now: 0 player, 1 AI
        public readonly CupRng rng;

        public Match(int startingDice = 5, int? seed = null)
        {
            rng = new CupRng(seed);
            for (int i = 0; i < startingDice; i++) { playerDice.Add(1); aiDice.Add(1); }
            RollAll();
            currentBid = default;
            // Decide starter by a single die roll (highest starts)
            var pr = rngRollDie(); var ar = rngRollDie();
            turnIndex = (pr >= ar) ? 0 : 1;
        }

        int rngRollDie() => new Random().Next(1, 7);

        public void RollAll()
        {
            rng.Roll(playerDice);
            rng.Roll(aiDice);
            currentBid = default;
        }

        public bool IsOver => playerDice.Count == 0 || aiDice.Count == 0;

        public void RemoveDie(int idx)
        {
            var bag = (idx == 0 ? playerDice : aiDice);
            if (bag.Count > 0) bag.RemoveAt(bag.Count - 1);
        }
    }

    public static class SimpleAI
    {
        /// Decide AI action: "raise", "call", or "spoton". If "raise", returns a legal next bid.
        public static (string action, Bid next) Decide(Match m)
        {
            var pool = m.playerDice.Count + m.aiDice.Count;

            if (!m.currentBid.IsValid)
            {
                // Must open with NON-ACES; use a mild opener based on pool.
                int baseQty = Math.Max(1, (int)Math.Round(pool * (2.0 / 6.0))); // expect for non-aces (face or wild)
                return ("raise", new Bid(Math.Max(1, baseQty), 2)); // e.g., "3 × 2"
            }

            // Heuristic based on *actual* plausibility without cheating too hard:
            // Estimate expected matches for a face (non-ace ≈ 2/6 per die, aces-only ≈ 1/6).
            double p = (m.currentBid.face == Rules.WILD) ? (1.0 / 6.0) : (2.0 / 6.0);
            int expected = (int)Math.Round(pool * p);

            // Mildly aggressive call/spot-on logic
            if (m.currentBid.quantity > expected + 1)
                return ("call", default);

            // Small chance to go for spot-on when expected ~= quantity
            if (Math.Abs(m.currentBid.quantity - expected) <= 0 && UnityEngine.Random.value < 0.15f)
                return ("spoton", default);

            // Otherwise raise minimally (respecting ace jump rules)
            var raise = NextLegalRaise(m.currentBid);
            return ("raise", raise);
        }

        /// Compute the minimal legal raise per the exact mapping rules.
        public static Bid NextLegalRaise(Bid curr)
        {
            if (!curr.IsValid) return new Bid(1, 2);

            bool currA = curr.face == Rules.WILD;

            if (!currA)
            {
                // Try same qty, next face (non-ace)
                if (curr.face < 6) return new Bid(curr.quantity, curr.face + 1);
                // else +1 qty, face = 2
                return new Bid(curr.quantity + 1, 2);
            }
            else
            {
                // From aces: either more aces, or jump to 2*q+1(anything)
                // We'll prefer aces+1 first (gentle), else jump.
                return new Bid(curr.quantity + 1, Rules.WILD);
            }
        }
    }
}
