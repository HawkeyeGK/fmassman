using fmassman.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

namespace fmassman.Client.Models;

public class PlayerEditorViewModel
{
    private readonly IRosterRepository _rosterRepository;
    private readonly NavigationManager _navigationManager;
    private readonly HttpClient _httpClient;

    public PlayerImportData? Player { get; private set; }
    public bool IsLoading { get; private set; }
    
    // DEBUG: Trace log for troubleshooting save issues
    public List<string> DebugLog { get; } = new();

    public PlayerEditorViewModel(
        IRosterRepository rosterRepository,
        NavigationManager navigationManager,
        HttpClient httpClient)
    {
        _rosterRepository = rosterRepository;
        _navigationManager = navigationManager;
        _httpClient = httpClient;
    }

    public async Task LoadAsync(string name)
    {
        IsLoading = true;
        DebugLog.Clear();
        DebugLog.Add($"[LOAD] Starting load for: {name}");
        
        try
        {
            var players = await _rosterRepository.LoadAsync();
            DebugLog.Add($"[LOAD] Loaded {players.Count} players from repository");
            
            Player = players.FirstOrDefault(p => p.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (Player != null)
            {
                // Log what we got from the API
                DebugLog.Add($"[LOAD] Found player: {Player.PlayerName}");
                DebugLog.Add($"[LOAD] Snapshot null? {Player.Snapshot == null}");
                
                if (Player.Snapshot != null)
                {
                    DebugLog.Add($"[LOAD] Goalkeeping null? {Player.Snapshot.Goalkeeping == null}");
                    
                    if (Player.Snapshot.Goalkeeping != null)
                    {
                        var gk = Player.Snapshot.Goalkeeping;
                        DebugLog.Add($"[LOAD] GK Handling={gk.Handling}, Reflexes={gk.Reflexes}, AerialReach={gk.AerialReach}");
                    }
                }
                
                // CRITICAL Data Integrity Logic
                if (Player.Snapshot == null)
                {
                    Player.Snapshot = new PlayerSnapshot();
                    DebugLog.Add("[LOAD] Created new Snapshot");
                }

                if (Player.Snapshot.Technical == null)
                    Player.Snapshot.Technical = new TechnicalAttributes();

                if (Player.Snapshot.Mental == null)
                    Player.Snapshot.Mental = new MentalAttributes();

                if (Player.Snapshot.Physical == null)
                    Player.Snapshot.Physical = new PhysicalAttributes();
                
                if (Player.Snapshot.SetPieces == null)
                    Player.Snapshot.SetPieces = new SetPieceAttributes();

                if (Player.Snapshot.Goalkeeping == null)
                {
                    Player.Snapshot.Goalkeeping = new GoalkeepingAttributes();
                    DebugLog.Add("[LOAD] Created new GoalkeepingAttributes (was null!)");
                }
            }
            else
            {
                DebugLog.Add($"[LOAD] Player not found: {name}");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SaveAsync()
    {
        if (Player == null) 
        {
            DebugLog.Add("[SAVE] Player is null, aborting save");
            return;
        }

        DebugLog.Add($"[SAVE] Starting save for: {Player.PlayerName}");
        
        // Log current state before save
        if (Player.Snapshot?.Goalkeeping != null)
        {
            var gk = Player.Snapshot.Goalkeeping;
            DebugLog.Add($"[SAVE] Before save - GK Handling={gk.Handling}, Reflexes={gk.Reflexes}, AerialReach={gk.AerialReach}");
        }
        else
        {
            DebugLog.Add("[SAVE] Before save - Goalkeeping is NULL!");
        }

        var currentPlayers = await _rosterRepository.LoadAsync();
        DebugLog.Add($"[SAVE] Re-loaded {currentPlayers.Count} players for merge");
        
        var index = currentPlayers.FindIndex(p => p.PlayerName.Equals(Player.PlayerName, StringComparison.OrdinalIgnoreCase));
        DebugLog.Add($"[SAVE] Found player at index: {index}");

        if (index >= 0)
        {
            // Log what's in the re-loaded player before we replace it
            var reloadedPlayer = currentPlayers[index];
            if (reloadedPlayer.Snapshot?.Goalkeeping != null)
            {
                var gk = reloadedPlayer.Snapshot.Goalkeeping;
                DebugLog.Add($"[SAVE] Re-loaded player GK Handling={gk.Handling}, Reflexes={gk.Reflexes}");
            }
            else
            {
                DebugLog.Add("[SAVE] Re-loaded player has NULL Goalkeeping!");
            }
            
            currentPlayers[index] = Player; // Update the list with our edited object
            DebugLog.Add("[SAVE] Replaced player in list, calling SaveAsync...");
            
            await _rosterRepository.SaveAsync(currentPlayers);
            DebugLog.Add("[SAVE] SaveAsync completed, navigating...");
            
            _navigationManager.NavigateTo($"/player/{Player.PlayerName}");
        }
        else
        {
            DebugLog.Add("[SAVE] Player not found in list, save aborted!");
        }
    }

    public async Task<string?> GetImageSasAsync(string fileName)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<System.Text.Json.Nodes.JsonNode>($"api/images/sas/{fileName}");
            return response?["sasUrl"]?.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching SAS token: {ex.Message}");
            return null;
        }
    }
}

