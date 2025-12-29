using fmassman.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace fmassman.Client.Models;

public class PlayerEditorViewModel
{
    private readonly IRosterRepository _rosterRepository;
    private readonly NavigationManager _navigationManager;

    public PlayerImportData? Player { get; private set; }
    public bool IsLoading { get; private set; }

    public PlayerEditorViewModel(
        IRosterRepository rosterRepository,
        NavigationManager navigationManager)
    {
        _rosterRepository = rosterRepository;
        _navigationManager = navigationManager;
    }

    public async Task LoadAsync(string name)
    {
        IsLoading = true;
        try
        {
            var players = await _rosterRepository.LoadAsync();
            Player = players.FirstOrDefault(p => p.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase));

            // CRITICAL Data Integrity Logic
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
            currentPlayers[index] = Player; // Update the list with our edited object
            await _rosterRepository.SaveAsync(currentPlayers);
            _navigationManager.NavigateTo($"/player/{Player.PlayerName}");
        }
    }
}
