using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves a stored race/class <i>name</i> back to its <see cref="RaceDefinition"/> /
/// <see cref="ClassDefinition"/> instance (1.3). Character persistence stores only the identifier; this
/// is the seam that reconstructs everything derived from it (stats, HP/mana, known abilities).
///
/// Source of truth is Postgres since M2.10: the server pushes DB-backed snapshots in via
/// <see cref="LoadRaces"/>/<see cref="LoadClasses"/> at startup (<c>ContentLoader</c>) and clients receive
/// the same data over the <c>ContentCatalog</c> sync — races/classes need client sync because
/// <c>CharacterModelFactory</c>/<c>CharacterPreview</c> read this registry on the client. The
/// <see cref="GetRace"/>/<see cref="GetClass"/> API is unchanged from the pre-2.10 Resources-backed
/// version — only where the data comes from moved (push, not lazy Resources.LoadAll).
/// </summary>
public static class RaceClassRegistry
{
    static Dictionary<string, RaceDefinition>  _races   = new();
    static Dictionary<string, ClassDefinition> _classes = new();

    public static RaceDefinition GetRace(string name)
        => string.IsNullOrEmpty(name) ? null : _races.GetValueOrDefault(name);

    public static ClassDefinition GetClass(string name)
        => string.IsNullOrEmpty(name) ? null : _classes.GetValueOrDefault(name);

    /// <summary>All known race names (for the character-creation form, 1.5).</summary>
    public static string[] AllRaceNames() => new List<string>(_races.Keys).ToArray();

    /// <summary>All known class names (for the character-creation form, 1.5).</summary>
    public static string[] AllClassNames() => new List<string>(_classes.Keys).ToArray();

    /// <summary>
    /// Replace the race registry from DB-backed snapshots — building a runtime <see cref="RaceDefinition"/>
    /// instance per row (mirrors <c>ItemRegistry.LoadFrom</c>). Used by the server (from the DB) and by
    /// clients (from the synced catalog).
    /// </summary>
    public static void LoadRaces(IEnumerable<RaceSnapshot> snapshots)
    {
        _races = new Dictionary<string, RaceDefinition>();
        foreach (var s in snapshots)
        {
            var def = BuildRace(s);
            if (def != null && !string.IsNullOrEmpty(def.raceName))
                _races[def.raceName] = def;
        }
    }

    /// <summary>Replace the class registry from DB-backed snapshots — same shape as <see cref="LoadRaces"/>.</summary>
    public static void LoadClasses(IEnumerable<ClassSnapshot> snapshots)
    {
        _classes = new Dictionary<string, ClassDefinition>();
        foreach (var s in snapshots)
        {
            var def = BuildClass(s);
            if (def != null && !string.IsNullOrEmpty(def.className))
                _classes[def.className] = def;
        }
    }

    static RaceDefinition BuildRace(RaceSnapshot s)
    {
        var def = ScriptableObject.CreateInstance<RaceDefinition>();
        def.name       = string.IsNullOrEmpty(s.RaceName) ? s.RaceId : s.RaceName;
        def.raceName   = s.RaceName;
        def.xpModifier = s.XpModifier;
        def.strMod = s.StrMod; def.staMod = s.StaMod; def.agiMod = s.AgiMod;
        def.dexMod = s.DexMod; def.intMod = s.IntMod; def.wisMod = s.WisMod; def.chaMod = s.ChaMod;
        return def;
    }

    static ClassDefinition BuildClass(ClassSnapshot s)
    {
        var def = ScriptableObject.CreateInstance<ClassDefinition>();
        def.name       = string.IsNullOrEmpty(s.ClassName) ? s.ClassId : s.ClassName;
        def.className  = s.ClassName;
        def.xpModifier = s.XpModifier;

        def.baseStr = s.BaseStr; def.baseSta = s.BaseSta; def.baseAgi = s.BaseAgi;
        def.baseDex = s.BaseDex; def.baseInt = s.BaseInt; def.baseWis = s.BaseWis; def.baseCha = s.BaseCha;

        def.classBaseHP   = s.ClassBaseHP;
        def.hpPerLevel    = s.HpPerLevel;
        def.staCap        = s.StaCap;
        def.baseStaRatio  = s.BaseStaRatio;
        def.staGrowthRate = s.StaGrowthRate;

        def.manaStatType   = (ManaStatType)s.ManaStatType;
        def.classBaseMana  = s.ClassBaseMana;
        def.manaPerLevel   = s.ManaPerLevel;
        def.manaCap        = s.ManaCap;
        def.baseManaRatio  = s.BaseManaRatio;
        def.manaGrowthRate = s.ManaGrowthRate;

        def.combatTierTableLevel1 = new CombatTierTable
        {
            Miss = s.TierL1Miss, Glancing = s.TierL1Glancing, Hit = s.TierL1Hit,
            SolidHit = s.TierL1Solid, GoodHit = s.TierL1Good, Critical = s.TierL1Critical, Crippling = s.TierL1Crippling,
        };
        def.combatTierTableLevel20 = new CombatTierTable
        {
            Miss = s.TierL20Miss, Glancing = s.TierL20Glancing, Hit = s.TierL20Hit,
            SolidHit = s.TierL20Solid, GoodHit = s.TierL20Good, Critical = s.TierL20Critical, Crippling = s.TierL20Crippling,
        };

        def.startingAbilities = new List<string>(s.StartingAbilityIds ?? new List<string>());

        return def;
    }
}
