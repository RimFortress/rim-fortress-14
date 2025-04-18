// RimFortress Start
using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Preferences;

/// <summary>
/// The client sends this to update a characters profile.
/// </summary>
public sealed class MsgUpdateCharacters : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public Dictionary<int, ICharacterProfile> Profiles = default!;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var length = buffer.ReadVariableInt32();
        using var stream = new MemoryStream(length);
        buffer.ReadAlignedMemory(stream, length);
        Profiles = serializer.Deserialize<Dictionary<int, ICharacterProfile>>(stream);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        using (var stream = new MemoryStream())
        {
            serializer.Serialize(stream, Profiles);
            buffer.WriteVariableInt32((int) stream.Length);
            stream.TryGetBuffer(out var segment);
            buffer.Write(segment);
        }
    }
}
// RimFortress End
