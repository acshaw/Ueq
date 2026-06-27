using System.Collections.Generic;
using Npgsql;
using UnityEngine;

/// <summary>
/// Read-only repository over <c>xp_levels</c> (M2.7) — the single shared XP curve. Server-only (XP is
/// computed on the server). Builds a runtime <see cref="XpTableDefinition"/> whose <c>xpPerLevel</c> is
/// ordered by level; <c>PlayerExperience.SetTable</c> installs it (falling back to Resources / defaults).
/// </summary>
public sealed class XpRepository : IRepository
{
    public XpTableDefinition LoadTable(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var perLevel = new List<int>();
        using (var cmd = new NpgsqlCommand(
            "SELECT xp_to_next FROM xp_levels ORDER BY level", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                perLevel.Add(reader.GetInt32(0));
        }

        if (perLevel.Count == 0) return null;   // no rows → caller keeps the Resources/default fallback

        var table = ScriptableObject.CreateInstance<XpTableDefinition>();
        table.name = "DbXpTable";
        table.xpPerLevel = perLevel.ToArray();
        return table;
    }
}
