using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace fmassman.Api.Functions;

public class StorageFunctions
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<StorageFunctions> _logger;

    public StorageFunctions(BlobServiceClient blobServiceClient, ILogger<StorageFunctions> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    [Function("GetImageSas")]
    public async Task<IActionResult> GetImageSas(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "images/sas/{fileName}")] HttpRequest req,
        string fileName)
    {
        _logger.LogInformation("Generating SAS token for image: {FileName}", fileName);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new BadRequestObjectResult("Filename cannot be empty.");
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient("raw-uploads");
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Image not found: {FileName}", fileName);
                return new NotFoundObjectResult("Image not found.");
            }

            if (!blobClient.CanGenerateSasUri)
            {
                _logger.LogError("Blob client cannot generate SAS URI for: {FileName}", fileName);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerClient.Name,
                BlobName = blobClient.Name,
                Resource = "b", // b for blob
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(10)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);

            return new OkObjectResult(new { sasUrl = sasUri.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SAS token for image: {FileName}", fileName);
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}
