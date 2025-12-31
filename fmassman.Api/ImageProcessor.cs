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
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using fmassman.Shared.Helpers;

namespace fmassman.Api
{
    public class ImageProcessor
    {
        private readonly ILogger<ImageProcessor> _logger;
        private readonly HttpClient _httpClient;
        private readonly IRosterRepository _repository;
        private readonly BlobServiceClient _blobServiceClient;
        private const string RawUploadsContainer = "raw-uploads";
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private record ScreenLayout(string Name, int MinWidth, Rectangle[] Slices);

        private static readonly List<ScreenLayout> _layouts = new()
        {
            new ScreenLayout("Desktop (QHD)", 2000, new[]
            {
                new Rectangle(0, 0, -1, 390),       // 1. Header (-1 = Use Image Width)
                new Rectangle(660, 420, 440, 715),  // 2. Technical
                new Rectangle(1120, 420, 440, 665), // 3. Mental
                new Rectangle(1570, 420, 440, 410), // 4. Physical
                new Rectangle(2035, 420, 475, 305)  // 5. Bio
            }),
            new ScreenLayout("Laptop (FHD)", 0, new[] // Fallback
            {
                new Rectangle(0, 0, -1, 295),       // 1. Header
                new Rectangle(500, 315, 330, 535),  // 2. Technical
                new Rectangle(840, 315, 330, 535),  // 3. Mental
                new Rectangle(1175, 315, 335, 310), // 4. Physical
                new Rectangle(1525, 330, 355, 295)  // 5. Bio
            })
        };

        private static readonly List<ScreenLayout> _gkLayouts = new()
        {
            // 1. Desktop (QHD)
            new ScreenLayout("Desktop (QHD) - GK", 2000, new[]
            {
                new Rectangle(0, 0, -1, 390),       // 1. Header
                new Rectangle(660, 420, 440, 625),  // 2. Goalkeeping (x:660->1100, y:420->1045)
                new Rectangle(1120, 420, 440, 665), // 3. Mental (Standard)
                new Rectangle(1570, 420, 440, 580), // 4. Phys + Tech (x:1570->2010, y:420->1000)
                new Rectangle(2035, 420, 475, 305)  // 5. Bio (Standard)
            }),
            // 2. Laptop (FHD)
            new ScreenLayout("Laptop (FHD) - GK", 0, new[]
            {
                new Rectangle(0, 0, -1, 295),       // 1. Header
                new Rectangle(500, 315, 330, 465),  // 2. Goalkeeping (x:500->830, y:315->780)
                new Rectangle(840, 315, 330, 535),  // 3. Mental (Standard)
                new Rectangle(1175, 315, 335, 435), // 4. Phys + Tech (x:1175->1510, y:315->750 est)
                new Rectangle(1525, 330, 355, 295)  // 5. Bio (Standard)
            })
        };

        // Helper methods moved to fmassman.Shared.Helpers.SafeJsonParser

        public ImageProcessor(ILogger<ImageProcessor> logger, IHttpClientFactory httpClientFactory, IRosterRepository repository, BlobServiceClient blobServiceClient)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("OpenAI");
            _repository = repository;
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
                bool isGoalkeeper = bool.TryParse(req.Query["gk"], out var gk) && gk;
                var base64Slices = SliceImage(memoryStream, isGoalkeeper);

                // 3. Extract Data using OpenAI
                PlayerImportData playerData = await ExtractDataFromSlicesAsync(base64Slices, blobName, rawImageBlobUrl);

                if (playerData != null)
                {
                    // 4. Save to Cosmos DB via repository
                    await _repository.UpsertAsync(playerData);
                    _logger.LogInformation($"Successfully upserted player: {playerData.PlayerName}");
                    
                    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(playerData);
                    return response;
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

        private List<string> SliceImage(Stream imageStream, bool isGoalkeeper)
        {
            var base64Slices = new List<string>();

            // ImageSharp Load
            using (Image img = Image.Load(imageStream))
            {
                // Select Layout based on Width
                var layouts = isGoalkeeper ? _gkLayouts : _layouts;
                var layout = layouts.OrderByDescending(x => x.MinWidth)
                                     .FirstOrDefault(x => img.Width >= x.MinWidth) 
                                     ?? _layouts.Last(); // Should theoretically always match the last one (0 min width), but fallback just in case

                _logger.LogInformation("Processing Image: {Width}x{Height}. Selected Layout: {LayoutName}", 
                                       img.Width, img.Height, layout.Name);

                foreach (var rectDef in layout.Slices)
                {
                    // Handle dynamic width for Header (Width = -1)
                    int width = rectDef.Width == -1 ? img.Width : rectDef.Width;
                    var cropRect = new Rectangle(rectDef.X, rectDef.Y, width, rectDef.Height);

                    try
                    {
                        // Clone and Crop
                        using (var slice = img.Clone(ctx => ctx.Crop(cropRect)))
                        {
                            using (var ms = new MemoryStream())
                            {
                                slice.Save(ms, new JpegEncoder());
                                base64Slices.Add(Convert.ToBase64String(ms.ToArray()));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Crop failed for layout '{layout.Name}'. Image Size: {img.Width}x{img.Height}. Attempted Crop: {cropRect}. Error: {ex.Message}");
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
                PlayerName = SafeJsonParser.GetSafeString(flatData["PlayerName"]) ?? string.Empty,
                DateOfBirth = SafeJsonParser.GetSafeString(flatData["DateOfBirth"]) ?? string.Empty,
                HeightFeet = SafeJsonParser.GetSafeInt(flatData["HeightFeet"]),
                HeightInches = SafeJsonParser.GetSafeInt(flatData["HeightInches"]),
                Snapshot = new PlayerSnapshot
                {
                    SourceFilename = fileName,
                    RawImageBlobUrl = rawImageBlobUrl,
                    FileCreationDate = fileCreationDate,
                    GameDate = SafeJsonParser.GetSafeString(flatData["GameDate"]) ?? string.Empty,
                    PlayingTime = SafeJsonParser.GetSafeString(flatData["PlayingTime"]) ?? string.Empty,
                    Personality = SafeJsonParser.GetSafeString(flatData["Personality"]) ?? string.Empty,
                    Age = SafeJsonParser.GetSafeInt(flatData["Age"]),
                    TransferValueLow = SafeJsonParser.GetSafeInt(flatData["TransferValueLow"]),
                    TransferValueHigh = SafeJsonParser.GetSafeInt(flatData["TransferValueHigh"]),
                    Wage = SafeJsonParser.GetSafeString(flatData["Wage"]) ?? string.Empty,
                    ContractExpiry = SafeJsonParser.GetSafeString(flatData["ContractExpiry"]) ?? string.Empty,
                    
                    Technical = new TechnicalAttributes
                    {
                        Crossing = SafeJsonParser.GetSafeInt(flatData["Crossing"]),
                        Dribbling = SafeJsonParser.GetSafeInt(flatData["Dribbling"]),
                        Finishing = SafeJsonParser.GetSafeInt(flatData["Finishing"]),
                        FirstTouch = SafeJsonParser.GetSafeInt(flatData["FirstTouch"]),
                        Heading = SafeJsonParser.GetSafeInt(flatData["Heading"]),
                        LongShots = SafeJsonParser.GetSafeInt(flatData["LongShots"]),
                        Marking = SafeJsonParser.GetSafeInt(flatData["Marking"]),
                        Passing = SafeJsonParser.GetSafeInt(flatData["Passing"]),
                        Tackling = SafeJsonParser.GetSafeInt(flatData["Tackling"]),
                        Technique = SafeJsonParser.GetSafeInt(flatData["Technique"])
                    },
                    SetPieces = new SetPieceAttributes
                    {
                        Corners = SafeJsonParser.GetSafeInt(flatData["Corners"]),
                        FreeKickTaking = SafeJsonParser.GetSafeInt(flatData["FreeKickTaking"]),
                        LongThrows = SafeJsonParser.GetSafeInt(flatData["LongThrows"]),
                        PenaltyTaking = SafeJsonParser.GetSafeInt(flatData["PenaltyTaking"])
                    },
                    Mental = new MentalAttributes
                    {
                        Aggression = SafeJsonParser.GetSafeInt(flatData["Aggression"]),
                        Anticipation = SafeJsonParser.GetSafeInt(flatData["Anticipation"]),
                        Bravery = SafeJsonParser.GetSafeInt(flatData["Bravery"]),
                        Composure = SafeJsonParser.GetSafeInt(flatData["Composure"]),
                        Concentration = SafeJsonParser.GetSafeInt(flatData["Concentration"]),
                        Decisions = SafeJsonParser.GetSafeInt(flatData["Decisions"]),
                        Determination = SafeJsonParser.GetSafeInt(flatData["Determination"]),
                        Flair = SafeJsonParser.GetSafeInt(flatData["Flair"]),
                        Leadership = SafeJsonParser.GetSafeInt(flatData["Leadership"]),
                        OffTheBall = SafeJsonParser.GetSafeInt(flatData["OffTheBall"]),
                        Positioning = SafeJsonParser.GetSafeInt(flatData["Positioning"]),
                        Teamwork = SafeJsonParser.GetSafeInt(flatData["Teamwork"]),
                        Vision = SafeJsonParser.GetSafeInt(flatData["Vision"]),
                        WorkRate = SafeJsonParser.GetSafeInt(flatData["WorkRate"])
                    },
                    Physical = new PhysicalAttributes
                    {
                        Acceleration = SafeJsonParser.GetSafeInt(flatData["Acceleration"]),
                        Agility = SafeJsonParser.GetSafeInt(flatData["Agility"]),
                        Balance = SafeJsonParser.GetSafeInt(flatData["Balance"]),
                        JumpingReach = SafeJsonParser.GetSafeInt(flatData["JumpingReach"]),
                        NaturalFitness = SafeJsonParser.GetSafeInt(flatData["NaturalFitness"]),
                        Pace = SafeJsonParser.GetSafeInt(flatData["Pace"]),
                        Stamina = SafeJsonParser.GetSafeInt(flatData["Stamina"]),
                        Strength = SafeJsonParser.GetSafeInt(flatData["Strength"])
                    }
                }
            };

            return playerData;
        }
    }
}
