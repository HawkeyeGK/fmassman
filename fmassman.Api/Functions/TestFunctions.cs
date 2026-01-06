using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace fmassman.Api.Functions
{
    public class TestFunctions
    {
        private readonly ILogger _logger;

        public TestFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TestFunctions>();
        }

        [Function("TestEndpoint")]
        public HttpResponseData Test([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test")] HttpRequestData req)
        {
            _logger.LogInformation("TEST ENDPOINT HIT!");
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString("Test successful!");
            return response;
        }
    }
}
