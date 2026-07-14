using Mirror;
using UnityEngine;

/// <summary>
/// 4.2 — one client movement input, sent every frame via <c>NetworkedPlayer.CmdSendInput</c>. Bundled as a
/// struct (rather than more loose scalar Command params) because the input model is about to grow — swimming
/// and levitation are near-term additions that will need a vertical-control input and likely a movement-mode
/// flag, and reshaping this once now beats reshaping it twice.
/// </summary>
public struct PlayerInputCmd
{
    public Vector2 move;
    public float   yaw;
    public bool    sprint;
    public bool    jump;
    public uint    seq; // client-assigned, monotonically increasing
    public float   dt;  // elapsed time this input represents (used for deterministic replay)
}

/// <summary>
/// Manual wire serializer, written proactively rather than trusting Weaver's struct auto-generation — this
/// project already needed a hand-written extension for a plain-field struct once (see
/// <c>InventorySlotSerializer</c>), so this mirrors that proven pattern instead of risking a mid-implementation
/// surprise.
/// </summary>
public static class PlayerInputCmdSerializer
{
    public static void WritePlayerInputCmd(this NetworkWriter writer, PlayerInputCmd cmd)
    {
        writer.WriteVector2(cmd.move);
        writer.WriteFloat(cmd.yaw);
        writer.WriteBool(cmd.sprint);
        writer.WriteBool(cmd.jump);
        writer.WriteUInt(cmd.seq);
        writer.WriteFloat(cmd.dt);
    }

    public static PlayerInputCmd ReadPlayerInputCmd(this NetworkReader reader)
    {
        return new PlayerInputCmd
        {
            move   = reader.ReadVector2(),
            yaw    = reader.ReadFloat(),
            sprint = reader.ReadBool(),
            jump   = reader.ReadBool(),
            seq    = reader.ReadUInt(),
            dt     = reader.ReadFloat(),
        };
    }
}
