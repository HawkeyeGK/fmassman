using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using fmassman.Shared;
using fmassman.Shared.Services;

namespace fmassman.Client.Models
{
    public class RoleEditorViewModel
    {
        private readonly IRoleService _roleService;

        public event Action? OnChange;

        public List<RoleDefinition>? Roles { get; private set; }
        private List<RoleDefinition>? _allRoles; // Cache all roles

        private bool _isGoalkeeperMode;
        public bool IsGoalkeeperMode
        {
            get => _isGoalkeeperMode;
            set
            {
                if (_isGoalkeeperMode != value)
                {
                    _isGoalkeeperMode = value;
                    FilterRoles();
                    SelectedRole = null;
                    NotifyStateChanged();
                }
            }
        }

        public RoleDefinition? SelectedRole { get; private set; }
        public string NewAttribute { get; set; } = "";
        public bool ShowToast { get; private set; }
        public string ToastMessage { get; private set; } = "";

        // Hardcoded lists for filtering attributes based on mode
        private static readonly HashSet<string> _gkExclusives = new()
        {
            "AerialReach", "CommandOfArea", "Communication", "Eccentricity", "Handling", 
            "Kicking", "OneOnOnes", "Punching", "Reflexes", "RushingOut", "Throwing" 
            // Note: FirstTouch and Passing are shared, so not "exclusive" to remove from field players? 
            // Actually, requirements say "Standard Field Player attribute list". 
            // Usually GK attributes are hidden for field players. 
            // I will exclude these 11 from Field Mode.
        };

        private static readonly HashSet<string> _gkSpecifics = new()
        {
            // 13 Goalkeeping Attributes
            "AerialReach", "CommandOfArea", "Communication", "Eccentricity", "FirstTouch",
            "Handling", "Kicking", "OneOnOnes", "Passing", "Punching", "Reflexes", 
            "RushingOut", "Throwing",
            // + 3 GK Technicals
            "FreeKickTaking", "PenaltyTaking", "Technique"
            // Mental and Physical will be added dynamically
        };
        
        // Helper to get all Mental and Physical attributes
        private IEnumerable<string> GetStandardAttributes()
        {
             // We can derive this by taking All Valid Attributes and removing Technical, SetPieces (except the specific ones), and GK
             // Or better, just list them if we want to be precise?
             // Actually, the requirements said: "13 Goalkeeping attributes + standard Mental + standard Physical + 3 GK Technicals"
             // I'll filter the ValidAttributeNames.
             
             // Simplest approach for "Standard Field": All Valid Attributes EXCEPT the GK Exclusives.
             return RoleFitCalculator.ValidAttributeNames.Where(a => !_gkExclusives.Contains(a));
        }

        public IEnumerable<string> AvailableAttributes
        {
            get
            {
                if (IsGoalkeeperMode)
                {
                    // GK Mode: GK Specifics + All Mental + All Physical
                    // Since we don't know which are Mental/Physical easily without a map, 
                    // I will filter ALL valid attributes:
                    // Include if it IS in _gkSpecifics OR it is NOT a Technical/SetPiece/GK attribute?
                    // This is getting tricky without metadata.
                    // Let's assume "Standard Mental + Standard Physical" implies all attributes that are NOT Technical or SetPiece or GK.
                    // But wait, "Aggression" is Mental. 
                    // Let's rely on the exclusion list logic.
                    
                    // Strategy: Start with ALL attributes.
                    // Filter based on needs.
                    
                    // Requirement: 13 GK + Mental + Physical + 3 Technicals.
                    // So we EXCLUDE: Most Technicals (Crossing, Dribbling, finishing, heading, longshots, marking, tackling)
                    // And EXCLUDE: Most SetPieces (Corners, LongThrows).
                    
                    var excludedForGk = new HashSet<string> { "Crossing", "Dribbling", "Finishing", "Heading", "LongShots", "Marking", "Tackling", "Corners", "LongThrows" };
                    
                    return RoleFitCalculator.ValidAttributeNames.Where(a => !excludedForGk.Contains(a));
                }
                else
                {
                    // Field Mode: Everything EXCEPT GK Exclusives
                    return RoleFitCalculator.ValidAttributeNames.Where(a => !_gkExclusives.Contains(a));
                }
            }
        }

        public RoleEditorViewModel(IRoleService roleService)
        {
            _roleService = roleService;
        }

        public async Task LoadDataAsync()
        {
            _allRoles = await _roleService.LoadLocalRolesAsync();
            FilterRoles();
        }

        private void FilterRoles()
        {
            if (_allRoles == null) return;
            
            if (IsGoalkeeperMode)
            {
                Roles = _allRoles.Where(r => r.Category == "Goalkeeper").ToList();
            }
            else
            {
                Roles = _allRoles.Where(r => r.Category != "Goalkeeper").ToList();
            }
            NotifyStateChanged();
        }

        public void SelectRole(RoleDefinition role)
        {
            SelectedRole = role;
            NewAttribute = "";
            NotifyStateChanged();
        }

        public void UpdateWeight(string key, object? value)
        {
            if (SelectedRole != null && double.TryParse(value?.ToString(), out double val))
            {
                SelectedRole.Weights[key] = val;
                NotifyStateChanged();
            }
        }

        public void RemoveWeight(string key)
        {
            if (SelectedRole != null)
            {
                SelectedRole.Weights.Remove(key);
                NotifyStateChanged();
            }
        }

        public void AddAttribute()
        {
            if (SelectedRole != null && !string.IsNullOrEmpty(NewAttribute))
            {
                SelectedRole.Weights[NewAttribute] = 3; // Default weight
                NewAttribute = "";
                NotifyStateChanged();
            }
        }

        public void CloseToast()
        {
            ShowToast = false;
            NotifyStateChanged();
        }

        public async Task SaveChanges()
        {
            if (Roles != null)
            {
                await _roleService.SaveRolesAsync(Roles);

                // Show Toast
                ToastMessage = "Roles saved successfully!";
                ShowToast = true;
                NotifyStateChanged();

                // Auto-hide after 3 seconds
                await Task.Delay(3000);
                ShowToast = false;
                NotifyStateChanged();
            }
        }

        public async Task ResetDefaults()
        {
            await _roleService.ResetToBaselineAsync();
            await LoadDataAsync(); // Reload UI
            SelectedRole = null;

            ToastMessage = "All roles reset to factory settings!";
            ShowToast = true;
            NotifyStateChanged();

            // Auto-hide after 3 seconds
            await Task.Delay(3000);
            ShowToast = false;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
