using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using fmassman.Shared;
using fmassman.Shared.Services;

namespace fmassman.Web.Models
{
    public class RoleEditorViewModel
    {
        private readonly IRoleService _roleService;

        public event Action? OnChange;

        public List<RoleDefinition>? Roles { get; private set; }
        public RoleDefinition? SelectedRole { get; private set; }
        public string NewAttribute { get; set; } = "";
        public bool ShowToast { get; private set; }
        public string ToastMessage { get; private set; } = "";

        public RoleEditorViewModel(IRoleService roleService)
        {
            _roleService = roleService;
        }

        public async Task LoadDataAsync()
        {
            Roles = await _roleService.LoadLocalRolesAsync();
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
