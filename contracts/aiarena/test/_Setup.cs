using AElf.Cryptography.ECDSA;
using AElf.Testing.TestBase;

namespace Ai.Arena
{
    // The Module class load the context required for unit testing
    public class Module : ContractTestModule<AiArena>
    {
        
    }
    
    // The TestBase class inherit ContractTestBase class, it defines Stub classes and gets instances required for unit testing
    public class TestBase : ContractTestBase<Module>
    {
        // The Stub class for unit testing
        internal readonly AiArenaContainer.AiArenaStub AiArenaStub;
        // A key pair that can be used to interact with the contract instance
        private ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;

        public TestBase()
        {
            AiArenaStub = GetAiArenaContractStub(DefaultKeyPair);
        }

        private AiArenaContainer.AiArenaStub GetAiArenaContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<AiArenaContainer.AiArenaStub>(ContractAddress, senderKeyPair);
        }
    }
    
}