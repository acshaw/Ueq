[System.Serializable]
public struct FactionThreshold
{
    public string Name;
    public int MinScore;
    // 5.4 (AG1) — the on-demand "consider" message for this standing (e.g. "would attack you on sight").
    // Composed as "{target} {ConsiderText}." by PlayerConsider. Data-driven (Faction Editor) rather than a
    // C# switch on Name, so renaming/adding a threshold never silently breaks the message.
    public string ConsiderText;
}
