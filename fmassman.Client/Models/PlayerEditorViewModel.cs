using fmassman.Shared;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace fmassman.Client.Models;

public class PlayerEditorViewModel
{
    private readonly IRosterRepository _rosterRepository;
    private readonly NavigationManager _navigationManager;
    private readonly HttpClient _httpClient;

    public PlayerImportData? Player { get; private set; }
    public bool IsLoading { get; private set; }

    // TagIds will be bound directly to Player.TagIds in the UI, 
    // but we might want exposing AvailableTags here if we move logic to VM.
    // For now, simple pass-through property isn't strictly needed if we bind to Player.TagIds directly.

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
        try
        {
            var players = await _rosterRepository.LoadAsync();
            Player = players.FirstOrDefault(p => p.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase));

            // Ensure attribute objects exist for editing
            if (Player != null)
            {
                if (Player.Snapshot == null)
                    Player.Snapshot = new PlayerSnapshot();

                if (Player.Snapshot.Technical == null)
                    Player.Snapshot.Technical = new TechnicalAttributes();

                if (Player.Snapshot.Mental == null)
                    Player.Snapshot.Mental = new MentalAttributes();

                if (Player.Snapshot.Physical == null)
                    Player.Snapshot.Physical = new PhysicalAttributes();
                
                if (Player.Snapshot.SetPieces == null)
                    Player.Snapshot.SetPieces = new SetPieceAttributes();

                // Note: Goalkeeping is intentionally NOT auto-initialized here.
                // The Goalkeeping object should only exist if the player was imported as a goalkeeper.
                // Auto-initializing it would incorrectly flag outfield players as goalkeepers.
                
                // Ensure TagIds list exists
                if (Player.TagIds == null)
                    Player.TagIds = new List<string>();


            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SaveAsync()
    {
        if (Player == null) return;

        var currentPlayers = await _rosterRepository.LoadAsync();
        var index = currentPlayers.FindIndex(p => p.PlayerName.Equals(Player.PlayerName, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            currentPlayers[index] = Player;
            await _rosterRepository.SaveAsync(currentPlayers);
            _navigationManager.NavigateTo($"/player/{Player.PlayerName}");
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
