using AElf.Types;
using Contracts.Ai.Arena;
using Google.Protobuf;
using ZkVerifier;

namespace Ai.Arena;

public partial class AiArena
{
    private static Hash RaceIdToRedemptionTreeId(Hash raceId)
    {
        var bytes = raceId.Value.ToByteArray();
        bytes[^1] = (byte)((bytes[^1] + 1) % 127);
        return new Hash()
        {
            Value = ByteString.CopyFrom(bytes)
        };
    }
}