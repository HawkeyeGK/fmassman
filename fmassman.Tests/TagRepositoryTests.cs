using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using fmassman.Api.Repositories;
using fmassman.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace fmassman.Tests
{
    public class TagRepositoryTests
    {
        private readonly Mock<CosmosClient> _cosmosClientMock;
        private readonly Mock<Container> _tagContainerMock;
        private readonly Mock<Container> _rosterContainerMock;
        private readonly Mock<IOptions<CosmosSettings>> _settingsMock;
        private readonly CosmosTagRepository _repository;

        public TagRepositoryTests()
        {
            _cosmosClientMock = new Mock<CosmosClient>();
            _tagContainerMock = new Mock<Container>();
            _rosterContainerMock = new Mock<Container>();
            _settingsMock = new Mock<IOptions<CosmosSettings>>();

            var settings = new CosmosSettings
            {
                DatabaseName = "TestDB",
                TagContainer = "Tags",
                PlayerContainer = "Roster"
            };
            _settingsMock.Setup(s => s.Value).Returns(settings);

            _cosmosClientMock.Setup(c => c.GetContainer("TestDB", "Tags"))
                .Returns(_tagContainerMock.Object);
            _cosmosClientMock.Setup(c => c.GetContainer("TestDB", "Roster"))
                .Returns(_rosterContainerMock.Object);

            _repository = new CosmosTagRepository(_cosmosClientMock.Object, _settingsMock.Object);
        }

        [Fact]
        public async Task DeleteAsync_ThrowsInvalidOperation_WhenTagIsUsedByPlayers()
        {
            // Arrange
            var tagId = "test-tag-id";

            // Mock the count query response
            var feedResponseMock = new Mock<FeedResponse<int>>();
            feedResponseMock.Setup(x => x.GetEnumerator())
                .Returns(new List<int> { 5 }.GetEnumerator()); // Count > 0

            var helper = new MockFeedIterator<int>(feedResponseMock.Object);

            _rosterContainerMock.Setup(x => x.GetItemQueryIterator<int>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
                .Returns(helper);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.DeleteAsync(tagId));
        }

        [Fact]
        public async Task DeleteAsync_DeletesTag_WhenTagIsNotUsed()
        {
            // Arrange
            var tagId = "test-tag-id";

             // Mock the count query response
            var feedResponseMock = new Mock<FeedResponse<int>>();
            feedResponseMock.Setup(x => x.GetEnumerator())
                .Returns(new List<int> { 0 }.GetEnumerator()); // Count == 0

            var helper = new MockFeedIterator<int>(feedResponseMock.Object);

            _rosterContainerMock.Setup(x => x.GetItemQueryIterator<int>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
                .Returns(helper);

            // Act
            await _repository.DeleteAsync(tagId);

            // Assert
            _tagContainerMock.Verify(x => x.DeleteItemAsync<TagDefinition>(
                tagId,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
