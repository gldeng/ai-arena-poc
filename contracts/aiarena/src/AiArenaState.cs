using System;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp.State;
using AElf.Types;
using Contracts.Ai.Arena;
using MerkleTreeWithHistory;
using ZkVerifier;

namespace Ai.Arena
{
    public class AiArenaState : ContractState
    {
        public BoolState Initialized { get; set; }
        public MappedState<Hash, Race> Races { get; set; }
        public MappedState<Hash, Hash, bool> VoteCommitments { get; set; }
        public MappedState<Hash, Hash, bool> VoteNullifiers { get; set; }
        public MappedState<Hash, Hash, bool> RedemptionCommitments { get; set; }
        public MappedState<Hash, Hash, bool> RedemptionNullifiers { get; set; }
        public MappedState<Hash, Int64> TotalDepositCount { get; set; }

        public MappedState<Hash, TokenAmount> RaceWinner { get; set; }

        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
        internal ZkVerifierContainer.ZkVerifierReferenceState ZkVerifier { get; set; }
        internal MerkleTreeWithHistoryContainer.MerkleTreeWithHistoryReferenceState MerkleTree { get; set; }
    }
}