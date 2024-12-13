using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Contracts.Ai.Arena;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using MerkleTreeWithHistory;
using ZkVerifier;

namespace Ai.Arena
{
    // Contract class must inherit the base class generated from the proto file
    public partial class AiArena : AiArenaImplContainer.AiArenaImplBase
    {
        // ReSharper disable once InconsistentNaming
        private const int TREE_LEVELS = 20;

        // ReSharper disable once InconsistentNaming
        private const int MAX_CAPACITY = 1048576; // 2**20

        /// <summary>
        /// Only race controller can invoke this method.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Empty CreateRace(Race input)
        {
            // TODO: AssertOnlyRaceController();
            Assert(input.StartTime >= Context.CurrentBlockTime, "StartTime is in the past");
            Assert(input.EndTime > input.StartTime, "EndTime has to be after StartTime");
            Assert(input.FeePerVote > 0, "fees required so that votes can be relayed successfully");
            foreach (var symbol in input.SymbolsOfParticipatingAgents)
            {
                // TODO: Should check all symbols are buyable instead
                var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
                {
                    Symbol = symbol
                });
                Assert(tokenInfo != null, "invalid symbol(s) provided");
            }

            var raceHash = HashHelper.ComputeFrom(input);
            State.Races[raceHash] = input;
            State.MerkleTree.CreateTree.Send(new CreateTreeInput
            {
                TreeId = raceHash,
                Owner = Context.Self,
                Levels = TREE_LEVELS
            });
            State.MerkleTree.CreateTree.Send(new CreateTreeInput()
            {
                TreeId = RaceIdToRedemptionTreeId(raceHash),
                Owner = Context.Self,
                Levels = TREE_LEVELS
            });
            Context.Fire(new RaceCreated()
            {
                RaceHash = raceHash,
                Race = input
            });
            return new Empty();
        }

        public override Race GetRace(Hash input)
        {
            return State.Races[input];
        }

        public override UInt32Value Deposit(DepositInput input)
        {
            var race = State.Races[input.RaceId];
            Assert(race != null && !race.Equals(new Race()), "race doesn't exist");
            Assert(State.TotalDepositCount[input.RaceId] < MAX_CAPACITY, "max capacity reached");
            // TODO: Should we stop allowing deposit some time before voting ends so that
            //   1. the depositor has sufficient time to cast vote
            //   2. there's sufficient time to break the link between depositor and voter to achieve real anonymity
            Assert(race!.EndTime >= Context.CurrentBlockTime, "race has ended");
            Assert(!State.VoteCommitments[input.RaceId][input.Commitment], "commitment used");
            State.VoteCommitments[input.RaceId][input.Commitment] = true;
            State.TotalDepositCount[input.RaceId] += 1;

            var raceVirtualAddress = Context.ConvertVirtualAddressToContractAddress(input.RaceId);
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = Context.Sender,
                To = raceVirtualAddress,
                Symbol = "ELF",
                Amount = race.AmountPerVote + race.FeePerVote,
                Memo = input.RaceId.ToHex()
            });

            var leafIndex = State.MerkleTree.GetNextIndex.Call(input.RaceId); // Indirectly confirms the tree exists
            State.MerkleTree.InsertLeaf.Send(new InsertLeafInput()
            {
                TreeId = input.RaceId,
                Leaf = input.Commitment.Value
            });
            Context.Fire(new Deposited
            {
                RaceId = input.RaceId,
                Commitment = input.Commitment,
                LeafIndex = leafIndex.Value
            });
            return leafIndex;
        }

        public override UInt32Value Vote(VoteInput input)
        {
            // TODO: Validate input
            var race = State.Races[input.RaceId];
            Assert(race != null && !race.Equals(new Race()), "race doesn't exist");

            Assert(!State.VoteNullifiers[input.RaceId][input.Nullifier], "nullifier already used");
            State.VoteNullifiers[input.RaceId][input.Nullifier] = true;

            var isKnownRoot = State.MerkleTree.IsKnownRoot.Call(new IsKnownRootInput
            {
                TreeId = input.RaceId,
                Root = input.TreeRoot
            });
            Assert(isKnownRoot.Value, "invalid root");
            var zkInput = new VerifyProofInput()
            {
                Proof = VerifyProofInput.Types.Proof.Parser.ParseFrom(input.Proof.ToByteArray()),
                Input =
                {
                    BigIntValue.FromBigEndianBytes(input.TreeRoot.Value.ToByteArray()).Value,
                    BigIntValue.FromBigEndianBytes(input.Nullifier.Value.ToByteArray()).Value,
                    BigIntValue.FromBigEndianBytes(input.RedemptionCommitment.Value.ToByteArray()).Value,
                    BigIntValue.FromBigEndianBytes(input.HashOfEncryptedVote.Value.ToByteArray()).Value,
                    "0",
                    "0"
                }
            };

            var verified = State.ZkVerifier.VerifyProof.Call(zkInput);
            Assert(verified.Value, "Proof is invalid.");

            State.RedemptionCommitments[input.RaceId][input.RedemptionCommitment] = true;

            var leafIndex =
                State.MerkleTree.GetNextIndex.Call(
                    RaceIdToRedemptionTreeId(input.RaceId)); // Indirectly confirms the tree exists
            State.MerkleTree.InsertLeaf.Send(new InsertLeafInput()
            {
                TreeId = RaceIdToRedemptionTreeId(input.RaceId),
                Leaf = input.RedemptionCommitment.Value
            });
            Context.Fire(new Voted
            {
                RaceId = input.RaceId,
                RedemptionCommitment = input.RedemptionCommitment,
                LeafIndex = leafIndex.Value
            });

            State.TokenContract.Transfer.VirtualSend(input.RaceId, new TransferInput
            {
                To = Context.Sender,
                Symbol = "ELF",
                Amount = race.FeePerVote,
                Memo = "relayer fee"
            });
            return leafIndex;
        }

        public override Empty Redeem(RedeemInput input)
        {
            // TODO: Validate input
            var race = State.Races[input.RaceId];
            Assert(race != null && !race.Equals(new Race()), "race doesn't exist");
            Assert(!State.RedemptionNullifiers[input.RaceId][input.Nullifier], "nullifier already used");
            State.RedemptionNullifiers[input.RaceId][input.Nullifier] = true;

            var isKnownRoot = State.MerkleTree.IsKnownRoot.Call(new IsKnownRootInput
            {
                TreeId = RaceIdToRedemptionTreeId(input.RaceId),
                Root = input.TreeRoot
            });
            Assert(isKnownRoot.Value, "invalid root");

            var zkInput = new VerifyProofInput()
            {
                Proof = VerifyProofInput.Types.Proof.Parser.ParseFrom(input.Proof.ToByteArray()),
                Input =
                {
                    BigIntValue.FromBigEndianBytes(input.TreeRoot.Value.ToByteArray()).Value,
                    BigIntValue.FromBigEndianBytes(input.Nullifier.Value.ToByteArray()).Value,
                    BigIntValue.FromBigEndianBytes(input.Recipient.Value.ToByteArray()).Value,
                    "0",
                    "0",
                    "0"
                }
            };
            var verified = State.ZkVerifier.VerifyProof.Call(zkInput);
            Assert(verified.Value, "Proof is invalid.");

            var winner = State.RaceWinner[input.RaceId];
            var sharePerVote = winner.Amount.Div(State.TotalDepositCount[input.RaceId]);

            State.TokenContract.Transfer.VirtualSend(input.RaceId, new TransferInput
            {
                To = input.Recipient,
                Symbol = winner.Symbol,
                Amount = sharePerVote,
                Memo = input.Nullifier.ToHex()
            });

            return new Empty();
        }
    }
}