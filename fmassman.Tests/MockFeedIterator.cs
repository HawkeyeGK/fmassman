using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace fmassman.Tests
{
    public class MockFeedIterator<T> : FeedIterator<T>
    {
        private readonly FeedResponse<T> _response;
        private bool _hasMoreResults = true;

        public MockFeedIterator(FeedResponse<T> response)
        {
            _response = response;
        }

        public override bool HasMoreResults => _hasMoreResults;

        public override Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            _hasMoreResults = false;
            return Task.FromResult(_response);
        }
    }
}
