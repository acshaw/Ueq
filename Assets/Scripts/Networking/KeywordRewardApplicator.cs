using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 3.2 — turns a conversation keyword into a quest turn-in. Add to any NPC (it's on the shared Enemy patch list,
/// inert unless a keyword in its conversation set carries a transaction bundle). Mirrors <see cref="VendorApplicator"/>:
/// an <see cref="IOnConversationKeyword"/> that resolves the NPC's DB conversation set, finds the matched keyword,
/// and — if it has a bundle — runs the transaction on the player: validate → consume required items/coin → grant
/// XP / coin / items / faction, all-or-nothing, with Reward-channel chat.
///
/// The keyword's faction gate is already enforced by <see cref="NpcConversation"/> before this dispatches, so no
/// gating happens here. Transactions are repeatable (no completion tracking — 3.2 Q2); anti-farm for item rewards
/// is the LORE flag (3.2.1), inherited via <c>PlayerInventory.AddItem(enforceLore: true)</c>.
/// </summary>
public class KeywordRewardApplicator : NetworkBehaviour, IOnConversationKeyword
{
    string _setId = "";

    public override void OnStartServer()
    {
        var mob = GetComponent<MobApplicator>();
        _setId = mob?.Definition?.conversationSetId ?? "";
    }

    void IOnConversationKeyword.OnConversationKeyword(NetworkIdentity player, string keyword)
    {
        if (!isServer || player == null) return;

        var set = ConversationRegistry.Get(_setId);
        if (set == null) return;

        ConversationKeyword kw = null;
        foreach (var k in set.Keywords)
            if (string.Equals(k.Keyword, keyword, StringComparison.OrdinalIgnoreCase)) { kw = k; break; }
        if (kw == null || !kw.HasTransaction) return;

        RunTransaction(player, kw);
    }

    [Server]
    void RunTransaction(NetworkIdentity player, ConversationKeyword kw)
    {
        var conn = player.connectionToClient;
        var inv  = player.GetComponent<PlayerInventory>();
        if (inv == null) return;

        // ── Validate the WHOLE transaction before consuming anything (all-or-nothing) ──
        foreach (var req in kw.RequiredItems)
            if (req.quantity > 0 && !inv.HasItem(req.itemId, req.quantity))
            { Refuse(conn, "You don't have what I asked for."); return; }

        if (kw.RequiredCopper > 0 && inv.TotalCopperValue < kw.RequiredCopper)
        { Refuse(conn, "You don't have the coin I asked for."); return; }

        // Rewards must fit after the hand-off (simulated; also catches LORE dupes).
        if (!inv.CanExchange(kw.RequiredItems, kw.RewardItems))
        { Refuse(conn, "You'll need to make room for the reward first."); return; }

        // ── Consume ──
        foreach (var req in kw.RequiredItems)
            if (req.quantity > 0) inv.RemoveItem(req.itemId, req.quantity);
        if (kw.RequiredCopper > 0) inv.SpendCurrency(kw.RequiredCopper);

        // ── Grant ──
        if (kw.RewardXp > 0)
        {
            player.GetComponent<PlayerExperience>()?.AddXp(kw.RewardXp);
            Reward(conn, $"You gain {kw.RewardXp} experience.");
        }

        if (kw.RewardCopper > 0)
        {
            inv.AddCurrency(kw.RewardCopper);
            Reward(conn, $"You receive {CurrencyUtil.Format(kw.RewardCopper)}.");
        }

        foreach (var rw in kw.RewardItems)
        {
            if (rw.quantity <= 0) continue;
            inv.AddItem(rw.itemId, rw.quantity, enforceLore: true);
            var def = ItemRegistry.Instance?.Get(rw.itemId);
            string label = def != null ? def.displayName : rw.itemId;
            Reward(conn, rw.quantity > 1 ? $"You receive {label} x{rw.quantity}." : $"You receive {label}.");
        }

        if (kw.RewardFactionHits.Count > 0)
        {
            var scores = player.GetComponent<PlayerFactionScores>();
            if (scores != null)
                foreach (var fh in kw.RewardFactionHits)
                {
                    if (fh.delta == 0) continue;
                    var faction = FactionRegistry.Get(fh.factionId);
                    if (faction == null) continue;
                    scores.ModifyScore(faction, fh.delta);
                    Reward(conn, $"Your standing with {faction.FactionName} has {(fh.delta > 0 ? "improved" : "worsened")}.");
                }
        }
    }

    void Refuse(NetworkConnectionToClient conn, string line)
    {
        if (conn != null)
            ChatManager.Instance?.SendDirect(new ChatMessage(ChatChannel.System, "System", line), conn);
    }

    void Reward(NetworkConnectionToClient conn, string line)
    {
        if (conn != null)
            ChatManager.Instance?.SendDirect(new ChatMessage(ChatChannel.Reward, "System", line), conn);
    }
}
