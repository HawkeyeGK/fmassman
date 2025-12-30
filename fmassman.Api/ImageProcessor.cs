using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
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
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ImageProcessor(ILogger<ImageProcessor> logger, IHttpClientFactory httpClientFactory, CosmosClient cosmosClient)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("OpenAI");
            _playersContainer = cosmosClient.GetContainer("FMAMDB", "Players");
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
                
                string name = "uploaded_image"; // Default name for direct uploads

                // 1. Slice Image using ImageSharp
                var base64Slices = SliceImage(memoryStream);

                // 2. Extract Data using OpenAI
                PlayerImportData playerData = await ExtractDataFromSlicesAsync(base64Slices, name);

                if (playerData != null)
                {
                    // 3. Save to Cosmos DB
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

        private async Task<PlayerImportData> ExtractDataFromSlicesAsync(List<string> base64Slices, string fileName)
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
                PlayerName = flatData["PlayerName"]?.GetValue<string>(),
                DateOfBirth = flatData["DateOfBirth"]?.GetValue<string>(),
                HeightFeet = flatData["HeightFeet"]?.GetValue<int>() ?? 0,
                HeightInches = flatData["HeightInches"]?.GetValue<int>() ?? 0,
                Snapshot = new PlayerSnapshot
                {
                    SourceFilename = fileName,
                    FileCreationDate = fileCreationDate,
                    GameDate = flatData["GameDate"]?.GetValue<string>(),
                    PlayingTime = flatData["PlayingTime"]?.GetValue<string>(),
                    Personality = flatData["Personality"]?.GetValue<string>(),
                    Age = flatData["Age"]?.GetValue<int>() ?? 0,
                    TransferValueLow = flatData["TransferValueLow"]?.GetValue<int>() ?? 0,
                    TransferValueHigh = flatData["TransferValueHigh"]?.GetValue<int>() ?? 0,
                    Wage = flatData["Wage"]?.GetValue<string>(),
                    ContractExpiry = flatData["ContractExpiry"]?.GetValue<string>(),
                    
                    Technical = new TechnicalAttributes
                    {
                        Crossing = flatData["Crossing"]?.GetValue<int>() ?? 0,
                        Dribbling = flatData["Dribbling"]?.GetValue<int>() ?? 0,
                        Finishing = flatData["Finishing"]?.GetValue<int>() ?? 0,
                        FirstTouch = flatData["FirstTouch"]?.GetValue<int>() ?? 0,
                        Heading = flatData["Heading"]?.GetValue<int>() ?? 0,
                        LongShots = flatData["LongShots"]?.GetValue<int>() ?? 0,
                        Marking = flatData["Marking"]?.GetValue<int>() ?? 0,
                        Passing = flatData["Passing"]?.GetValue<int>() ?? 0,
                        Tackling = flatData["Tackling"]?.GetValue<int>() ?? 0,
                        Technique = flatData["Technique"]?.GetValue<int>() ?? 0
                    },
                    SetPieces = new SetPieceAttributes
                    {
                        Corners = flatData["Corners"]?.GetValue<int>() ?? 0,
                        FreeKickTaking = flatData["FreeKickTaking"]?.GetValue<int>() ?? 0,
                        LongThrows = flatData["LongThrows"]?.GetValue<int>() ?? 0,
                        PenaltyTaking = flatData["PenaltyTaking"]?.GetValue<int>() ?? 0
                    },
                    Mental = new MentalAttributes
                    {
                        Aggression = flatData["Aggression"]?.GetValue<int>() ?? 0,
                        Anticipation = flatData["Anticipation"]?.GetValue<int>() ?? 0,
                        Bravery = flatData["Bravery"]?.GetValue<int>() ?? 0,
                        Composure = flatData["Composure"]?.GetValue<int>() ?? 0,
                        Concentration = flatData["Concentration"]?.GetValue<int>() ?? 0,
                        Decisions = flatData["Decisions"]?.GetValue<int>() ?? 0,
                        Determination = flatData["Determination"]?.GetValue<int>() ?? 0,
                        Flair = flatData["Flair"]?.GetValue<int>() ?? 0,
                        Leadership = flatData["Leadership"]?.GetValue<int>() ?? 0,
                        OffTheBall = flatData["OffTheBall"]?.GetValue<int>() ?? 0,
                        Positioning = flatData["Positioning"]?.GetValue<int>() ?? 0,
                        Teamwork = flatData["Teamwork"]?.GetValue<int>() ?? 0,
                        Vision = flatData["Vision"]?.GetValue<int>() ?? 0,
                        WorkRate = flatData["WorkRate"]?.GetValue<int>() ?? 0
                    },
                    Physical = new PhysicalAttributes
                    {
                        Acceleration = flatData["Acceleration"]?.GetValue<int>() ?? 0,
                        Agility = flatData["Agility"]?.GetValue<int>() ?? 0,
                        Balance = flatData["Balance"]?.GetValue<int>() ?? 0,
                        JumpingReach = flatData["JumpingReach"]?.GetValue<int>() ?? 0,
                        NaturalFitness = flatData["NaturalFitness"]?.GetValue<int>() ?? 0,
                        Pace = flatData["Pace"]?.GetValue<int>() ?? 0,
                        Stamina = flatData["Stamina"]?.GetValue<int>() ?? 0,
                        Strength = flatData["Strength"]?.GetValue<int>() ?? 0
                    }
                }
            };

            return playerData;
        }
    }
}
