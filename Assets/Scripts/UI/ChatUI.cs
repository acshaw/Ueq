using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ChatUI : MonoBehaviour
{
    [SerializeField] TMP_Text       log;
    [SerializeField] ScrollRect     scrollRect;
    [SerializeField] GameObject     inputRow;
    [SerializeField] TMP_InputField inputField;

    [Header("Display")]
    [SerializeField] bool showTimestamps = true;

    [Header("Channel Filters")]
    [SerializeField] bool filterSay         = true;
    [SerializeField] bool filterShout       = true;
    [SerializeField] bool filterWhisper     = true;
    [SerializeField] bool filterSystem      = true;
    [SerializeField] bool filterCombat      = true;
    [SerializeField] bool filterReward      = true;
    [SerializeField] bool filterAbility     = true;
    [SerializeField] bool filterEnvironment = true;
    [SerializeField] bool filterNPC         = true;

    static readonly Color[] ChannelColors =
    {
        Color.white,                       // Say
        new Color(1f,   0.80f, 0.40f),    // Shout   — warm yellow
        new Color(1f,   0.50f, 1f),       // Whisper — pink/violet
        new Color(0.5f, 0.80f, 1f),       // System  — light blue
        new Color(1f,   0.40f, 0.40f),    // Combat  — red
        new Color(0.4f, 1f,    0.40f),    // Reward  — green
        new Color(0.6f, 0.40f, 1f),       // Ability — purple
        new Color(0.7f, 0.90f, 0.70f),   // Environment — muted sage
        new Color(1f,   0.90f, 0.60f),   // NPC     — warm cream
    };

    const int MaxStoredLines = 500;

    public static ChatUI Instance { get; private set; }
    public static bool   IsOpen   => Instance != null && Instance._open;

    bool _open;
    bool _suppressEnterOpen;
    bool _scrollLocked;
    bool _programmaticScroll;

    readonly List<string> _lines = new List<string>(MaxStoredLines);

    static readonly int[] FontSizes = { 11, 13, 16, 20, 25 };

    // Single source of truth for /help — keep this in sync as commands are added (1.6.1).
    static readonly (string cmd, string desc)[] Commands =
    {
        ("/say <msg>",            "Speak to players nearby"),
        ("/shout <msg>",          "Shout to a wider area"),
        ("/whisper <name> <msg>", "Private message to a player"),
        ("/camp",                 "Return to character select (10s, must be out of combat)"),
        ("/sit",                  "Sit / stand (also hotbar key 0); rest faster while seated"),
        ("/unstuck",              "Warp to a safe spot if stuck or falling (out of combat)"),
        ("/travel <name>",        "Fast travel: creslins, thornwood, grukmar, village, mobs, crossroads"),
        ("/help",                 "List chat commands"),
        ("/font-size <1-5>",      "Set chat text size"),
    };

    void Awake()     => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    // Clear the log when the local player goes away (camp / switch) so the next character starts
    // fresh — the welcome/MOTD then populates a clean log (1.6.1).
    void OnEnable()  => LocalPlayer.Despawned += ClearLog;
    void OnDisable() => LocalPlayer.Despawned -= ClearLog;

    void ClearLog()
    {
        _lines.Clear();
        if (log != null) log.text = "";
    }

    void Start()
    {
        if (scrollRect != null)
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
    }

    void OnScrollValueChanged(Vector2 val)
    {
        if (_programmaticScroll) return;
        _scrollLocked = val.y > 0.01f;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (_suppressEnterOpen && !kb.enterKey.isPressed)
            _suppressEnterOpen = false;

        if (!_open)
        {
            if (!_suppressEnterOpen && kb.enterKey.wasPressedThisFrame)
                Open();
            else if (kb.slashKey.wasPressedThisFrame)
                Open("/");
        }
        else if (kb.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    void Open(string prefix = "")
    {
        _open = true;
        inputRow.SetActive(true);
        // Don't select-all on focus, or the "/" prefix gets highlighted and replaced by the first keystroke
        // (reads as "the / disappeared"). With it off, the prefix stays and the caret sits after it, so
        // pressing "/" then typing lands "/command" — feels like typing straight away.
        inputField.onFocusSelectAll = false;
        inputField.text = prefix;
        inputField.ActivateInputField();
        inputField.caretPosition = prefix.Length;
        inputField.stringPosition = prefix.Length; // pin the caret after the prefix (no lingering selection)
        inputField.onSubmit.RemoveAllListeners();
        inputField.onSubmit.AddListener(_ => Submit());
    }

    void Submit()
    {
        _suppressEnterOpen = true;
        string raw = inputField.text.Trim();
        if (!string.IsNullOrEmpty(raw))
        {
            if (!TryHandleLocalCommand(raw))
            {
                var (channel, target, text) = ParseInput(raw);

                var local = LocalPlayer.Current; // 1.7 — single binding seam

                if (local != null)
                    local.CmdSendChat(channel, target, text);
                else
                    AppendLine("<i>[Not connected — start Host first]</i>");
            }
        }
        Close();
    }

    // Returns true if the input was a local command and should not be sent to the server.
    bool TryHandleLocalCommand(string raw)
    {
        if (raw.Equals("/camp", StringComparison.OrdinalIgnoreCase))
        {
            CampController.Instance?.RequestCamp();
            return true;
        }

        if (raw.Equals("/sit", StringComparison.OrdinalIgnoreCase))
        {
            LocalPlayer.Current?.GetComponent<PlayerSitting>()?.CmdToggleSit();
            return true;
        }

        if (raw.Equals("/stand", StringComparison.OrdinalIgnoreCase))
        {
            LocalPlayer.Current?.GetComponent<PlayerSitting>()?.CmdStand();
            return true;
        }

        if (raw.Equals("/unstuck", StringComparison.OrdinalIgnoreCase))
        {
            var local = LocalPlayer.Current;
            if (local != null) local.CmdUnstuck();
            else AppendLine("<i>[Not connected — start Host first]</i>");
            return true;
        }

        if (raw.Equals("/travel", StringComparison.OrdinalIgnoreCase))
        {
            var local = LocalPlayer.Current;
            if (local != null) local.CmdTravel(""); // server replies with the option list
            else AppendLine("<i>[Not connected — start Host first]</i>");
            return true;
        }

        if (raw.StartsWith("/travel ", StringComparison.OrdinalIgnoreCase))
        {
            string arg = raw.Substring(8).Trim();
            var local = LocalPlayer.Current;
            if (local != null) local.CmdTravel(arg);
            else AppendLine("<i>[Not connected — start Host first]</i>");
            return true;
        }

        if (raw.Equals("/help", StringComparison.OrdinalIgnoreCase))
        {
            AppendLine("<i>[Commands]</i>");
            foreach (var c in Commands)
                AppendLine($"<i>  {c.cmd} — {c.desc}</i>");
            return true;
        }

        if (raw.StartsWith("/font-size ", StringComparison.OrdinalIgnoreCase))
        {
            string arg = raw.Substring(11).Trim();
            if (int.TryParse(arg, out int level))
            {
                int idx = Mathf.Clamp(level - 1, 0, FontSizes.Length - 1);
                log.enableAutoSizing = false;
                log.fontSize = FontSizes[idx];
                AppendLine($"<i>[Font size set to {idx + 1}]</i>");
            }
            else
            {
                AppendLine("<i>[Usage: /font-size 1|2|3|4|5]</i>");
            }
            return true;
        }
        return false;
    }

    void Close()
    {
        _open = false;
        inputField.DeactivateInputField();
        inputRow.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public static void Receive(ChatMessage msg)
    {
        if (Instance == null) { Debug.Log($"[{msg.Channel}] {msg.SenderName}: {msg.Text}"); return; }
        Instance.ReceiveInternal(msg);
    }

    // Compat shim — routes raw strings as System messages.
    public static void AddMessage(string line)
        => Receive(new ChatMessage(ChatChannel.System, string.Empty, line));

    // ── Internals ─────────────────────────────────────────────────────────────

    void ReceiveInternal(ChatMessage msg)
    {
        if (!IsChannelEnabled(msg.Channel)) return;

        int    idx = (int)msg.Channel;
        Color  col = idx < ChannelColors.Length ? ChannelColors[idx] : Color.white;
        string hex = ColorUtility.ToHtmlStringRGB(col);

        string line = msg.Channel switch
        {
            ChatChannel.Say         => $"<color=#{hex}>[{msg.SenderName}] {msg.Text}</color>",
            ChatChannel.Shout       => $"<color=#{hex}>[SHOUT] {msg.SenderName}: {msg.Text}</color>",
            ChatChannel.Whisper     => $"<color=#{hex}>{msg.SenderName} whispers: {msg.Text}</color>",
            ChatChannel.System      => $"<color=#{hex}>[System] {msg.Text}</color>",
            ChatChannel.Combat      => $"<color=#{hex}>{msg.Text}</color>",
            ChatChannel.Reward      => $"<color=#{hex}>{msg.Text}</color>",
            ChatChannel.Ability     => $"<color=#{hex}>{msg.Text}</color>",
            ChatChannel.Environment => $"<color=#{hex}>{msg.Text}</color>",
            ChatChannel.NPC         => $"<color=#{hex}>[{msg.SenderName}] {msg.Text}</color>",
            _                       => msg.Text,
        };

        if (showTimestamps)
            line = $"[{msg.Timestamp:HH:mm:ss}] {line}";

        AppendLine(line);
    }

    void AppendLine(string line)
    {
        _lines.Add(line);
        if (_lines.Count > MaxStoredLines)
            _lines.RemoveAt(0);
        log.text = string.Join("\n", _lines);

        if (scrollRect != null && !_scrollLocked)
            ScrollToBottom();
    }

    void ScrollToBottom()
    {
        _programmaticScroll = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        scrollRect.verticalNormalizedPosition = 0f;
        _programmaticScroll = false;
    }

    bool IsChannelEnabled(ChatChannel channel) => channel switch
    {
        ChatChannel.Say         => filterSay,
        ChatChannel.Shout       => filterShout,
        ChatChannel.Whisper     => filterWhisper,
        ChatChannel.System      => filterSystem,
        ChatChannel.Combat      => filterCombat,
        ChatChannel.Reward      => filterReward,
        ChatChannel.Ability     => filterAbility,
        ChatChannel.Environment => filterEnvironment,
        ChatChannel.NPC         => filterNPC,
        _                       => true,
    };

    static (ChatChannel channel, string target, string text) ParseInput(string raw)
    {
        if (raw.StartsWith("/shout ", StringComparison.OrdinalIgnoreCase))
            return (ChatChannel.Shout, string.Empty, raw.Substring(7).Trim());

        if (raw.StartsWith("/say ", StringComparison.OrdinalIgnoreCase))
            return (ChatChannel.Say, string.Empty, raw.Substring(5).Trim());

        if (raw.StartsWith("/whisper ", StringComparison.OrdinalIgnoreCase))
        {
            string rest  = raw.Substring(9).Trim();
            int    space = rest.IndexOf(' ');
            if (space > 0)
                return (ChatChannel.Whisper, rest.Substring(0, space), rest.Substring(space + 1).Trim());
            return (ChatChannel.Whisper, rest, string.Empty);
        }

        // No prefix → /say
        return (ChatChannel.Say, string.Empty, raw);
    }
}
