using Mirror;
using UnityEngine;

public class CharacterStats : NetworkBehaviour
{
    int _baseStr, _baseSta, _baseAgi, _baseDex, _baseInt, _baseWis, _baseCha;
    int _bonusStr, _bonusSta, _bonusAgi, _bonusDex, _bonusInt, _bonusWis, _bonusCha;

    // 2026-08-21 (Mitigation) — AC has no class/race base, unlike the seven core stats above; it's
    // purely a sum of equipped gear's armorClass, same AddEquipmentBonus/RemoveEquipmentBonus pipeline.
    int _bonusAc;

    [SyncVar] int _totalStr;
    [SyncVar] int _totalSta;
    [SyncVar] int _totalAgi;
    [SyncVar] int _totalDex;
    [SyncVar] int _totalInt;
    [SyncVar] int _totalWis;
    [SyncVar] int _totalCha;
    [SyncVar] int _totalAc;

    public int Str => isServer ? _baseStr + _bonusStr : _totalStr;
    public int Sta => isServer ? _baseSta + _bonusSta : _totalSta;
    public int Agi => isServer ? _baseAgi + _bonusAgi : _totalAgi;
    public int Dex => isServer ? _baseDex + _bonusDex : _totalDex;
    public int Int => isServer ? _baseInt + _bonusInt : _totalInt;
    public int Wis => isServer ? _baseWis + _bonusWis : _totalWis;
    public int Cha => isServer ? _baseCha + _bonusCha : _totalCha;
    public int Ac  => isServer ? _bonusAc : _totalAc;

    [Server]
    public void SetRaceClass(RaceDefinition race, ClassDefinition cls)
    {
        _baseStr = (cls?.baseStr ?? 0) + (race?.strMod ?? 0);
        _baseSta = (cls?.baseSta ?? 0) + (race?.staMod ?? 0);
        _baseAgi = (cls?.baseAgi ?? 0) + (race?.agiMod ?? 0);
        _baseDex = (cls?.baseDex ?? 0) + (race?.dexMod ?? 0);
        _baseInt = (cls?.baseInt ?? 0) + (race?.intMod ?? 0);
        _baseWis = (cls?.baseWis ?? 0) + (race?.wisMod ?? 0);
        _baseCha = (cls?.baseCha ?? 0) + (race?.chaMod ?? 0);
        SyncTotals();
    }

    [Server]
    public void AddEquipmentBonus(int str, int sta, int agi, int dex, int int_, int wis, int cha, int ac)
    {
        _bonusStr += str; _bonusSta += sta; _bonusAgi += agi; _bonusDex += dex;
        _bonusInt += int_; _bonusWis += wis; _bonusCha += cha; _bonusAc += ac;
        SyncTotals();
    }

    [Server]
    public void RemoveEquipmentBonus(int str, int sta, int agi, int dex, int int_, int wis, int cha, int ac)
    {
        _bonusStr -= str; _bonusSta -= sta; _bonusAgi -= agi; _bonusDex -= dex;
        _bonusInt -= int_; _bonusWis -= wis; _bonusCha -= cha; _bonusAc -= ac;
        SyncTotals();
    }

    void SyncTotals()
    {
        _totalStr = _baseStr + _bonusStr;
        _totalSta = _baseSta + _bonusSta;
        _totalAgi = _baseAgi + _bonusAgi;
        _totalDex = _baseDex + _bonusDex;
        _totalInt = _baseInt + _bonusInt;
        _totalWis = _baseWis + _bonusWis;
        _totalCha = _baseCha + _bonusCha;
        _totalAc  = _bonusAc;
    }
}
