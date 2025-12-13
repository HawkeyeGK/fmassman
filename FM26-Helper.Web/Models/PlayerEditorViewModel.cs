using FM26_Helper.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace FM26_Helper.Web.Models;

public class PlayerEditorViewModel
{
    private readonly RosterRepository _rosterRepository;
    private readonly NavigationManager _navigationManager;
    private readonly IConfiguration _configuration;

    public PlayerImportData? Player { get; private set; }
    public bool IsLoading { get; private set; }

    public PlayerEditorViewModel(
        RosterRepository rosterRepository,
        NavigationManager navigationManager,
        IConfiguration configuration)
    {
        _rosterRepository = rosterRepository;
        _navigationManager = navigationManager;
        _configuration = configuration;
    }

    public void Load(string name)
    {
        IsLoading = true;
        try
        {
            var path = _configuration["RosterFilePath"];
            if (string.IsNullOrEmpty(path))
            {
                // Handle missing config gracefully, though minimal logic required here.
                return; 
            }

            var players = _rosterRepository.Load(path);
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

    public void Save()
    {
        if (Player == null) return;

        var path = _configuration["RosterFilePath"];
        if (string.IsNullOrEmpty(path)) return;

        var currentPlayers = _rosterRepository.Load(path);
        var index = currentPlayers.FindIndex(p => p.PlayerName.Equals(Player.PlayerName, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            currentPlayers[index] = Player; // Update the list with our edited object
            _rosterRepository.Save(path, currentPlayers);
            _navigationManager.NavigateTo($"/player/{Player.PlayerName}");
        }
    }
}
