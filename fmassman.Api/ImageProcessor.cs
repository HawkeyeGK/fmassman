using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using fmassman.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace fmassman.Api
{
    public class ImageProcessor
    {
        private readonly ILogger<ImageProcessor> _logger;
        private readonly HttpClient _httpClient;
        private readonly Container _playersContainer;
        private readonly BlobServiceClient _blobServiceClient;
        private const string RawUploadsContainer = "raw-uploads";
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Helper to safely get a string value from JsonNode (handles both string and number types)
        private static string? GetSafeString(JsonNode? node)
        {
            if (node == null) return null;
            
            // Try to get the underlying JsonElement to check its type
            if (node is JsonValue jsonValue)
            {
                try
                {
                    // First try to get as string directly
                    return jsonValue.GetValue<string>();
                }
                catch (InvalidOperationException)
                {
                    // If it's not a string, convert to string representation
                    return node.ToJsonString().Trim('"');
                }
            }
            
            return node.ToString();
        }

        // Helper to safely get an int value from JsonNode (handles both int and string types)
        private static int GetSafeInt(JsonNode? node, int defaultValue = 0)
        {
            if (node == null) return defaultValue;
            
            if (node is JsonValue jsonValue)
            {
                try
                {
                    return jsonValue.GetValue<int>();
                }
                catch (InvalidOperationException)
                {
                    // Try parsing as string
                    var str = node.ToString();
                    return int.TryParse(str, out var result) ? result : defaultValue;
                }
            }
            
            return defaultValue;
        }

        public ImageProcessor(ILogger<ImageProcessor> logger, IHttpClientFactory httpClientFactory, CosmosClient cosmosClient, BlobServiceClient blobServiceClient)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("OpenAI");
            _playersContainer = cosmosClient.GetContainer("FMAMDB", "Players");
            _blobServiceClient = blobServiceClient;
        }

        [Function("UploadPlayerImage")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Processing image upload request.");

            try
            {
                // Copy to MemoryStream to ensure seekability for ImageSharp
                using var memoryStream = new MemoryStream();
                await req.Body.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // 1. Upload raw image to Blob Storage for data lineage
                var containerClient = _blobServiceClient.GetBlobContainerClient(RawUploadsContainer);
                await containerClient.CreateIfNotExistsAsync();
                
                string blobName = $"{Guid.NewGuid()}.jpg";
                var blobClient = containerClient.GetBlobClient(blobName);
                await blobClient.UploadAsync(memoryStream, overwrite: true);
                string rawImageBlobUrl = blobClient.Uri.ToString();
                _logger.LogInformation("Uploaded raw image to blob: {BlobUrl}", rawImageBlobUrl);
                
                // Critical: Reset stream position for ImageSharp
                memoryStream.Position = 0;

                // 2. Slice Image using ImageSharp
                var base64Slices = SliceImage(memoryStream);

                // 3. Extract Data using OpenAI
                PlayerImportData playerData = await ExtractDataFromSlicesAsync(base64Slices, blobName, rawImageBlobUrl);

                if (playerData != null)
                {
                    // 4. Save to Cosmos DB
                    await _playersContainer.UpsertItemAsync(playerData, new PartitionKey(playerData.PlayerName));
                    _logger.LogInformation($"Successfully upserted player: {playerData.PlayerName}");
                    
                    return req.CreateResponse(System.Net.HttpStatusCode.OK);
                }
                
                return req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image upload: {Message}", ex.Message);
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"{ex.GetType().Name}: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
                return errorResponse;
            }
        }

        private List<string> SliceImage(Stream imageStream)
        {
            var base64Slices = new List<string>();

            // ImageSharp Load
            using (Image img = Image.Load(imageStream))
            {
                // Define Slices (Same coordinates as before)
                var slices = new Rectangle[]
                {
                    new Rectangle(0, 0, img.Width, 390),                 // 1. Header
                    new Rectangle(660, 420, 440, 715),                   // 2. Technical
                    new Rectangle(1120, 420, 440, 665),                  // 3. Mental
                    new Rectangle(1570, 420, 440, 410),                  // 4. Physical
                    new Rectangle(2035, 420, 475, 305)                   // 5. Bio
                };

                foreach (var rect in slices)
                {
                    // Clone and Crop
                    using (var slice = img.Clone(ctx => ctx.Crop(rect)))
                    {
                        using (var ms = new MemoryStream())
                        {
                            slice.Save(ms, new JpegEncoder());
                            base64Slices.Add(Convert.ToBase64String(ms.ToArray()));
                        }
                    }
                }
            }

            return base64Slices;
        }

        private async Task<PlayerImportData> ExtractDataFromSlicesAsync(List<string> base64Slices, string fileName, string rawImageBlobUrl)
        {
            string systemPrompt = @"
You are a data extraction assistant for Football Manager 26.
Your goal is to extract player data from 5 separate image slices into a strict, FLAT JSON format.

### I have sent you 5 images:
1. **Header:** Contains Name, Game Date, Wage, Contract, Transfer Value, Age, DOB.
2. **Technical & Set Pieces:** 
   - Top Block (10 rows): Crossing, Dribbling, Finishing, First Touch, Heading, Long Shots, Marking, Passing, Tackling, Technique.
   - Bottom Block (4 rows): Corners, Free Kick Taking, Long Throws, Penalty Taking.
3. **Mental:** 14 rows. Aggression, Anticipation, Bravery, Composure, Concentration, Decisions, Determination, Flair, Leadership, Off The Ball, Positioning, Teamwork, Vision, Work Rate.
4. **Physical:** 8 rows. Acceleration, Agility, Balance, Jumping Reach, Natural Fitness, Pace, Stamina, Strength.
5. **Bio:** Height and Personality.

### REQUIRED JSON STRUCTURE
Return ONLY a FLAT JSON object with these keys. Values must be Integers (except Name/Dates).
- PlayerName, DateOfBirth, HeightFeet, HeightInches, Personality
- GameDate, PlayingTime, Age, TransferValueLow, TransferValueHigh, Wage, ContractExpiry
- Crossing, Dribbling, Finishing, FirstTouch, Heading, LongShots, Marking, Passing, Tackling, Technique
- Corners, FreeKickTaking, LongThrows, PenaltyTaking
- Aggression, Anticipation, Bravery, Composure, Concentration, Decisions, Determination, Flair, Leadership, OffTheBall, Positioning, Teamwork, Vision, WorkRate
- Acceleration, Agility, Balance, JumpingReach, NaturalFitness, Pace, Stamina, Strength

### CRITICAL RULES
- **Temperature 0:** Do not guess. If a number is obscured, return 0.
- **Alignment:** Ensure the value matches the label. Do not skip rows.
- **Playing Time:** Look for text describing squad status (e.g., 'Star Player', 'Regular Starter', 'Youngster', 'Squad Player') in the Header image. If a Club Name appears instead, use 'On Loan'.
";

            var requestBody = new
            {
                model = "gpt-4o",
                temperature = 0.0,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Extract the player data from these 5 images." },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Slices[0]}" } },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Slices[1]}" } },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Slices[2]}" } },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Slices[3]}" } },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Slices[4]}" } }
                        }
                    }
                },
                response_format = new { type = "json_object" }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody, _jsonSerializerOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"OpenAI API Error: {response.StatusCode} - {errorBody}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonNode.Parse(responseString);
            string? contentString = jsonResponse?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();

            if (string.IsNullOrEmpty(contentString))
            {
                 throw new Exception("Empty response content from OpenAI.");
            }

            JsonNode? flatData = JsonNode.Parse(contentString);

            if (flatData == null)
            {
                throw new Exception("Failed to parse OpenAI response content as JSON.");
            }

            // Manual Mapping: Flat JSON -> Nested Class Structure
            DateTime fileCreationDate = DateTime.UtcNow; 

            PlayerImportData playerData = new PlayerImportData
            {
                PlayerName = GetSafeString(flatData["PlayerName"]),
                DateOfBirth = GetSafeString(flatData["DateOfBirth"]),
                HeightFeet = GetSafeInt(flatData["HeightFeet"]),
                HeightInches = GetSafeInt(flatData["HeightInches"]),
                Snapshot = new PlayerSnapshot
                {
                    SourceFilename = fileName,
                    RawImageBlobUrl = rawImageBlobUrl,
                    FileCreationDate = fileCreationDate,
                    GameDate = GetSafeString(flatData["GameDate"]),
                    PlayingTime = GetSafeString(flatData["PlayingTime"]),
                    Personality = GetSafeString(flatData["Personality"]),
                    Age = GetSafeInt(flatData["Age"]),
                    TransferValueLow = GetSafeInt(flatData["TransferValueLow"]),
                    TransferValueHigh = GetSafeInt(flatData["TransferValueHigh"]),
                    Wage = GetSafeString(flatData["Wage"]),
                    ContractExpiry = GetSafeString(flatData["ContractExpiry"]),
                    
                    Technical = new TechnicalAttributes
                    {
                        Crossing = GetSafeInt(flatData["Crossing"]),
                        Dribbling = GetSafeInt(flatData["Dribbling"]),
                        Finishing = GetSafeInt(flatData["Finishing"]),
                        FirstTouch = GetSafeInt(flatData["FirstTouch"]),
                        Heading = GetSafeInt(flatData["Heading"]),
                        LongShots = GetSafeInt(flatData["LongShots"]),
                        Marking = GetSafeInt(flatData["Marking"]),
                        Passing = GetSafeInt(flatData["Passing"]),
                        Tackling = GetSafeInt(flatData["Tackling"]),
                        Technique = GetSafeInt(flatData["Technique"])
                    },
                    SetPieces = new SetPieceAttributes
                    {
                        Corners = GetSafeInt(flatData["Corners"]),
                        FreeKickTaking = GetSafeInt(flatData["FreeKickTaking"]),
                        LongThrows = GetSafeInt(flatData["LongThrows"]),
                        PenaltyTaking = GetSafeInt(flatData["PenaltyTaking"])
                    },
                    Mental = new MentalAttributes
                    {
                        Aggression = GetSafeInt(flatData["Aggression"]),
                        Anticipation = GetSafeInt(flatData["Anticipation"]),
                        Bravery = GetSafeInt(flatData["Bravery"]),
                        Composure = GetSafeInt(flatData["Composure"]),
                        Concentration = GetSafeInt(flatData["Concentration"]),
                        Decisions = GetSafeInt(flatData["Decisions"]),
                        Determination = GetSafeInt(flatData["Determination"]),
                        Flair = GetSafeInt(flatData["Flair"]),
                        Leadership = GetSafeInt(flatData["Leadership"]),
                        OffTheBall = GetSafeInt(flatData["OffTheBall"]),
                        Positioning = GetSafeInt(flatData["Positioning"]),
                        Teamwork = GetSafeInt(flatData["Teamwork"]),
                        Vision = GetSafeInt(flatData["Vision"]),
                        WorkRate = GetSafeInt(flatData["WorkRate"])
                    },
                    Physical = new PhysicalAttributes
                    {
                        Acceleration = GetSafeInt(flatData["Acceleration"]),
                        Agility = GetSafeInt(flatData["Agility"]),
                        Balance = GetSafeInt(flatData["Balance"]),
                        JumpingReach = GetSafeInt(flatData["JumpingReach"]),
                        NaturalFitness = GetSafeInt(flatData["NaturalFitness"]),
                        Pace = GetSafeInt(flatData["Pace"]),
                        Stamina = GetSafeInt(flatData["Stamina"]),
                        Strength = GetSafeInt(flatData["Strength"])
                    }
                }
            };

            return playerData;
        }
    }
}
