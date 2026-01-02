using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using fmassman.Api.Functions;
using fmassman.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace fmassman.Tests
{
    public class RosterPatchTests
    {
        private readonly Mock<IRosterRepository> _rosterRepositoryMock;
        private readonly Mock<ILogger<RosterFunctions>> _loggerMock;
        private readonly RosterFunctions _functions;

        public RosterPatchTests()
        {
            _rosterRepositoryMock = new Mock<IRosterRepository>();
            _loggerMock = new Mock<ILogger<RosterFunctions>>();
            _functions = new RosterFunctions(_loggerMock.Object, _rosterRepositoryMock.Object);
        }

        [Fact]
        public async Task UpdatePlayerTags_ValidRequest_CallsRepositoryAndReturnsOk()
        {
            // Arrange
            var playerName = "TestPlayer";
            var tagIds = new List<string> { "Tag1", "Tag2" };
            var json = System.Text.Json.JsonSerializer.Serialize(tagIds);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var context = new DefaultHttpContext();
            var request = context.Request;
            request.Body = memoryStream;
            request.ContentLength = memoryStream.Length;

            // Act
            var result = await _functions.UpdatePlayerTags(request, playerName);

            // Assert
            Assert.IsType<OkResult>(result);
            _rosterRepositoryMock.Verify(x => x.UpdatePlayerTagsAsync(playerName, It.Is<List<string>>(t => t.Count == 2 && t.Contains("Tag1") && t.Contains("Tag2"))), Times.Once);
        }

        [Fact]
        public async Task UpdatePlayerTags_NullPlayerName_ReturnsBadRequest()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var result = await _functions.UpdatePlayerTags(context.Request, null);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Player name is required", badRequestResult.Value);
        }
        
        [Fact]
        public async Task UpdatePlayerTags_EmptyBody_ReturnsBadRequest()
        {
            // Arrange
             var context = new DefaultHttpContext();
             var request = context.Request;
             request.Body = new MemoryStream(); 
             request.ContentLength = 0; 

            // Act
            var result = await _functions.UpdatePlayerTags(request, "TestPlayer");

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
             Assert.Contains("Invalid payload", badRequestResult.Value.ToString());
        }
    }
}
