using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Ai.Arena
{
    // This class is unit test class, and it inherit TestBase. Write your unit test code inside it
    public class AiArenaTests : TestBase
    {
        [Fact]
        public async Task Update_ShouldUpdateMessageAndFireEvent()
        {
            // Arrange
            var inputValue = "Hello, World!";
            var input = new StringValue { Value = inputValue };

            // Act
            await AiArenaStub.Update.SendAsync(input);

            // Assert
            var updatedMessage = await AiArenaStub.Read.CallAsync(new Empty());
            updatedMessage.Value.ShouldBe(inputValue);
        }
    }
    
}