using fmassman.Shared;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DotNetEnv;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace fmassman.Extractor
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            // 1. Setup
            Env.TraversePath().Load();
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Error: OPENAI_API_KEY not found in .env file.");
                return;
            }

            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // 2. Discovery
            // Navigate up from bin/Debug/net8.0 to Solution Root, then into players
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string playersDir = Path.GetFullPath(Path.Combine(baseDir, "../../../../../players"));

            if (!Directory.Exists(playersDir))
            {
                Console.WriteLine($"Directory not found: {playersDir}");
                return;
            }

            var imageFiles = new List<string>();
            imageFiles.AddRange(Directory.GetFiles(playersDir, "*.png", SearchOption.TopDirectoryOnly));
            imageFiles.AddRange(Directory.GetFiles(playersDir, "*.jpg", SearchOption.TopDirectoryOnly));
            
            Console.WriteLine($"Found {imageFiles.Count} images to process.");

            var processedPlayers = new List<PlayerImportData>();

            // 3. Processing Loop
            foreach (var filePath in imageFiles)
            {
                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"Processing {fileName}...");

                try
                {
                    // Local Data
                    DateTime fileCreationDate = File.GetCreationTime(filePath);
                    
                    // Image Slicing
                    List<string> base64Slices = new List<string>();
                    
                    // On Windows, System.Drawing.Common requires this switch or a runtime config, 
                    // but usually works for simple bitmap operations if GDI+ is available.
                    // If running on Linux/Mac, this would require libgdiplus.
                    using (Image img = Image.FromFile(filePath))
                    {
                        // Define Slices
                        // Header: { X=0, Y=0, Width=img.Width, Height=390 }
                        // Technical: { X=660, Y=420, Width=440, Height=715 }
                        // Mental: { X=1120, Y=420, Width=440, Height=665 }
                        // Physical: { X=1570, Y=420, Width=440, Height=410 }
                        // Bio: { X=2035, Y=420, Width=475, Height=305 }

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
                            base64Slices.Add(CropToBase64(img, rect));
                        }
                    }

                    // Define the Prompt with 5-Slice Strategy
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

                    // API Call with Retry Logic
                    int maxRetries = 3;
                    int currentRetry = 0;
                    bool success = false;
                    string responseString = "";

                    while (currentRetry < maxRetries && !success)
                    {
                        try
                        {
                            var requestBody = new
                            {
                                model = "gpt-4o",
                                temperature = 0.0, // Critical for deterministic data extraction
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

                            string jsonBody = JsonConvert.SerializeObject(requestBody);
                            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                            HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                            responseString = await response.Content.ReadAsStringAsync();

                            if (response.IsSuccessStatusCode)
                            {
                                success = true;
                            }
                            else if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                Console.WriteLine($"API Error (Attempt {currentRetry + 1}/{maxRetries}): {response.StatusCode}. Retrying...");
                                currentRetry++;
                                await Task.Delay(2000);
                            }
                            else
                            {
                                Console.WriteLine($"API Error for {fileName}: {response.StatusCode} - {responseString}");
                                break; // Non-retriable error
                            }
                        }
                        catch (HttpRequestException ex)
                        {
                            Console.WriteLine($"Network Error (Attempt {currentRetry + 1}/{maxRetries}): {ex.Message}. Retrying...");
                            currentRetry++;
                            await Task.Delay(2000);
                        }
                    }

                    if (!success)
                    {
                        Console.WriteLine($"Failed to process {fileName} after {maxRetries} attempts.");
                        continue;
                    }

                    // Console.WriteLine("Raw API Response: " + contentString); // Uncomment for debugging

                    // Deserialization & Mapping
                    var jsonResponse = JObject.Parse(responseString);
                    string contentString = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();

                    if (string.IsNullOrEmpty(contentString))
                    {
                        Console.WriteLine($"Empty response content for {fileName}.");
                        continue;
                    }

                    JObject flatData = JObject.Parse(contentString);

                    // Manual Mapping: Flat JSON -> Nested Class Structure
                    PlayerImportData playerData = new PlayerImportData
                    {
                        PlayerName = (string)flatData["PlayerName"],
                        DateOfBirth = (string)flatData["DateOfBirth"],
                        HeightFeet = (int?)flatData["HeightFeet"] ?? 0,
                        HeightInches = (int?)flatData["HeightInches"] ?? 0,
                        Snapshot = new PlayerSnapshot
                        {
                            SourceFilename = fileName,
                            FileCreationDate = fileCreationDate,
                            GameDate = (string)flatData["GameDate"],
                            PlayingTime = (string)flatData["PlayingTime"],
                            Personality = (string)flatData["Personality"],
                            Age = (int?)flatData["Age"] ?? 0,
                            TransferValueLow = (int?)flatData["TransferValueLow"] ?? 0,
                            TransferValueHigh = (int?)flatData["TransferValueHigh"] ?? 0,
                            Wage = (string)flatData["Wage"],
                            ContractExpiry = (string)flatData["ContractExpiry"],
                            
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

                    processedPlayers.Add(playerData);
                    Console.WriteLine($"Successfully processed {fileName}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {fileName}: {ex.Message}");
                }
            }

            // 4. Output
            string outputFilePath = Path.Combine(playersDir, "roster_data.json");
            string outputJson = JsonConvert.SerializeObject(processedPlayers, Formatting.Indented);
            await File.WriteAllTextAsync(outputFilePath, outputJson);

            Console.WriteLine($"Processing complete. Data saved to {outputFilePath}");
        }

        private static string CropToBase64(Image img, Rectangle rect)
        {
            using (Bitmap bmp = new Bitmap(rect.Width, rect.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(img, new Rectangle(0, 0, bmp.Width, bmp.Height), rect, GraphicsUnit.Pixel);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Jpeg);
                    byte[] byteImage = ms.ToArray();
                    return Convert.ToBase64String(byteImage);
                }
            }
        }
    }
}
