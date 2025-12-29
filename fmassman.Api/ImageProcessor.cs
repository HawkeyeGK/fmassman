using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using fmassman.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        public ImageProcessor(ILogger<ImageProcessor> logger, IHttpClientFactory httpClientFactory, CosmosClient cosmosClient)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("OpenAI");
            _playersContainer = cosmosClient.GetContainer("FMAMDB", "Players");
        }

        [Function("ProcessPlayerImage")]
        public async Task Run([BlobTrigger("player-images/{name}", Connection = "AzureWebJobsStorage")] Stream imageStream, string name)
        {
            _logger.LogInformation($"Processing blob\n Name: {name} \n Size: {imageStream.Length} Bytes");

            try
            {
                // 1. Slice Image using ImageSharp
                var base64Slices = SliceImage(imageStream);

                // 2. Extract Data using OpenAI
                PlayerImportData playerData = await ExtractDataFromSlicesAsync(base64Slices, name);

                if (playerData != null)
                {
                    // 3. Save to Cosmos DB
                    await _playersContainer.UpsertItemAsync(playerData, new PartitionKey(playerData.PlayerName));
                    _logger.LogInformation($"Successfully upserted player: {playerData.PlayerName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing image {name}");
                throw; // Rethrow to mark function as failed (and potentially retry)
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

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"OpenAI API Error: {response.StatusCode} - {errorBody}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var jsonResponse = JObject.Parse(responseString);
            string contentString = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();

            if (string.IsNullOrEmpty(contentString))
            {
                 throw new Exception("Empty response content from OpenAI.");
            }

            JObject flatData = JObject.Parse(contentString);

            // Manual Mapping: Flat JSON -> Nested Class Structure
            DateTime fileCreationDate = DateTime.UtcNow; 

            PlayerImportData playerData = new PlayerImportData
            {
                PlayerName = (string?)flatData["PlayerName"],
                DateOfBirth = (string?)flatData["DateOfBirth"],
                HeightFeet = (int?)flatData["HeightFeet"] ?? 0,
                HeightInches = (int?)flatData["HeightInches"] ?? 0,
                Snapshot = new PlayerSnapshot
                {
                    SourceFilename = fileName,
                    FileCreationDate = fileCreationDate,
                    GameDate = (string?)flatData["GameDate"],
                    PlayingTime = (string?)flatData["PlayingTime"],
                    Personality = (string?)flatData["Personality"],
                    Age = (int?)flatData["Age"] ?? 0,
                    TransferValueLow = (int?)flatData["TransferValueLow"] ?? 0,
                    TransferValueHigh = (int?)flatData["TransferValueHigh"] ?? 0,
                    Wage = (string?)flatData["Wage"],
                    ContractExpiry = (string?)flatData["ContractExpiry"],
                    
                    Technical = new TechnicalAttributes
                    {
                        Crossing = (int?)flatData["Crossing"] ?? 0,
                        Dribbling = (int?)flatData["Dribbling"] ?? 0,
                        Finishing = (int?)flatData["Finishing"] ?? 0,
                        FirstTouch = (int?)flatData["FirstTouch"] ?? 0,
                        Heading = (int?)flatData["Heading"] ?? 0,
                        LongShots = (int?)flatData["LongShots"] ?? 0,
                        Marking = (int?)flatData["Marking"] ?? 0,
                        Passing = (int?)flatData["Passing"] ?? 0,
                        Tackling = (int?)flatData["Tackling"] ?? 0,
                        Technique = (int?)flatData["Technique"] ?? 0
                    },
                    SetPieces = new SetPieceAttributes
                    {
                        Corners = (int?)flatData["Corners"] ?? 0,
                        FreeKickTaking = (int?)flatData["FreeKickTaking"] ?? 0,
                        LongThrows = (int?)flatData["LongThrows"] ?? 0,
                        PenaltyTaking = (int?)flatData["PenaltyTaking"] ?? 0
                    },
                    Mental = new MentalAttributes
                    {
                        Aggression = (int?)flatData["Aggression"] ?? 0,
                        Anticipation = (int?)flatData["Anticipation"] ?? 0,
                        Bravery = (int?)flatData["Bravery"] ?? 0,
                        Composure = (int?)flatData["Composure"] ?? 0,
                        Concentration = (int?)flatData["Concentration"] ?? 0,
                        Decisions = (int?)flatData["Decisions"] ?? 0,
                        Determination = (int?)flatData["Determination"] ?? 0,
                        Flair = (int?)flatData["Flair"] ?? 0,
                        Leadership = (int?)flatData["Leadership"] ?? 0,
                        OffTheBall = (int?)flatData["OffTheBall"] ?? 0,
                        Positioning = (int?)flatData["Positioning"] ?? 0,
                        Teamwork = (int?)flatData["Teamwork"] ?? 0,
                        Vision = (int?)flatData["Vision"] ?? 0,
                        WorkRate = (int?)flatData["WorkRate"] ?? 0
                    },
                    Physical = new PhysicalAttributes
                    {
                        Acceleration = (int?)flatData["Acceleration"] ?? 0,
                        Agility = (int?)flatData["Agility"] ?? 0,
                        Balance = (int?)flatData["Balance"] ?? 0,
                        JumpingReach = (int?)flatData["JumpingReach"] ?? 0,
                        NaturalFitness = (int?)flatData["NaturalFitness"] ?? 0,
                        Pace = (int?)flatData["Pace"] ?? 0,
                        Stamina = (int?)flatData["Stamina"] ?? 0,
                        Strength = (int?)flatData["Strength"] ?? 0
                    }
                }
            };

            return playerData;
        }
    }
}
