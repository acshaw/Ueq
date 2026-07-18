using System.Collections.Generic;
using Npgsql;

/// <summary>
/// Read-only repository over <c>abilities</c> (M2.9), following the 1.2 DAL convention. Header rows
/// first (into an id-keyed map), then the three ordered children (tags, cooldown links, effects) —
/// so an ability with no children still loads. Tag id/display-name pairs are resolved from
/// <c>ability_tags</c> once and reused for both children that reference a tag.
/// </summary>
public sealed class AbilityRepository : IRepository
{
    public List<AbilitySnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var tagNames = new Dictionary<string, string>();
        using (var cmd = new NpgsqlCommand("SELECT tag_id, display_name FROM ability_tags", conn, tx))
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
                tagNames[reader.GetString(0)] = reader.GetString(1);

        var byId  = new Dictionary<string, AbilitySnapshot>();
        var order = new List<string>();

        using (var cmd = new NpgsqlCommand(
            "SELECT ability_id, display_name, description, targeting_type, range, cast_time, " +
            "mana_cost, anim_trigger FROM abilities ORDER BY ability_id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetString(0);
                byId[id] = new AbilitySnapshot
                {
                    AbilityId      = id,
                    DisplayName    = reader.GetString(1),
                    Description    = reader.GetString(2),
                    TargetingType  = reader.GetInt32(3),
                    Range          = reader.GetFloat(4),
                    CastTime       = reader.GetFloat(5),
                    ManaCost       = reader.GetInt32(6),
                    AnimTrigger    = reader.GetString(7),
                    Tags           = new List<AbilityTagRefSnapshot>(),
                    CooldownLinks  = new List<AbilityCooldownLinkSnapshot>(),
                    Effects        = new List<AbilityEffectSnapshot>(),
                };
                order.Add(id);
            }
        }

        using (var cmd = new NpgsqlCommand(
            "SELECT ability_id, tag_id FROM ability_definition_tags ORDER BY ability_id, sort_order, id",
            conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                if (byId.TryGetValue(reader.GetString(0), out var s))
                {
                    var tagId = reader.GetString(1);
                    s.Tags.Add(new AbilityTagRefSnapshot
                    {
                        TagId       = tagId,
                        DisplayName = tagNames.GetValueOrDefault(tagId, tagId),
                    });
                }
        }

        using (var cmd = new NpgsqlCommand(
            "SELECT ability_id, tag_id, duration FROM ability_cooldown_links ORDER BY ability_id, sort_order, id",
            conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                if (byId.TryGetValue(reader.GetString(0), out var s))
                {
                    var tagId = reader.GetString(1);
                    s.CooldownLinks.Add(new AbilityCooldownLinkSnapshot
                    {
                        TagId          = tagId,
                        TagDisplayName = tagNames.GetValueOrDefault(tagId, tagId),
                        Duration       = reader.GetFloat(2),
                    });
                }
        }

        using (var cmd = new NpgsqlCommand(
            "SELECT ability_id, effect_type, base_amount, scaling_stat, scaling_factor " +
            "FROM ability_effects ORDER BY ability_id, sort_order, id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                if (byId.TryGetValue(reader.GetString(0), out var s))
                    s.Effects.Add(new AbilityEffectSnapshot
                    {
                        EffectType    = reader.GetString(1),
                        BaseAmount    = reader.GetInt32(2),
                        ScalingStat   = reader.GetInt32(3),
                        ScalingFactor = reader.GetFloat(4),
                    });
        }

        var rows = new List<AbilitySnapshot>(order.Count);
        foreach (var id in order) rows.Add(byId[id]);
        return rows;
    }
}

/// <summary>
/// Read-only repository over the standalone <c>ability_tags</c> table (M2.9) — for the Ability Tag
/// content type's own grid, independent of any ability referencing it.
/// </summary>
public sealed class AbilityTagRepository : IRepository
{
    public List<AbilityTagSnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var rows = new List<AbilityTagSnapshot>();
        using var cmd = new NpgsqlCommand("SELECT tag_id, display_name FROM ability_tags ORDER BY tag_id", conn, tx);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(new AbilityTagSnapshot { TagId = reader.GetString(0), DisplayName = reader.GetString(1) });
        return rows;
    }
}
