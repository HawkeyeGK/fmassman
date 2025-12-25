using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace fmassman.Shared.Services
{
    public class RoleService : IRoleService
    {
        private readonly string _baselinePath; // Factory Settings
        private readonly string _localPath;    // User Edits (Database)

        public RoleService(string baselinePath, string localPath)
        {
            _baselinePath = baselinePath;
            _localPath = localPath;
        }

        public async Task InitializeAsync()
        {
            // 1. Ensure Local Exists
            if (!File.Exists(_localPath))
            {
                // If Baseline is missing, we can't do anything (or use empty)
                if (File.Exists(_baselinePath))
                {
                    File.Copy(_baselinePath, _localPath);
                }
            }

            // 2. Load Local into Engine
            var roles = await LoadLocalRolesAsync();
            RoleFitCalculator.SetCache(roles);
        }

        public Task<List<RoleDefinition>> LoadLocalRolesAsync()
        {
            if (!File.Exists(_localPath)) return Task.FromResult(new List<RoleDefinition>());
            try
            {
                var json = File.ReadAllText(_localPath);
                return Task.FromResult(JsonSerializer.Deserialize<List<RoleDefinition>>(json) ?? new List<RoleDefinition>());
            }
            catch
            {
                return Task.FromResult(new List<RoleDefinition>());
            }
        }

        public Task SaveRolesAsync(List<RoleDefinition> roles)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(roles, options);
            File.WriteAllText(_localPath, json);
            
            // Hot Reload the Engine
            RoleFitCalculator.SetCache(roles);
            return Task.CompletedTask;
        }

        public async Task ResetToBaselineAsync()
        {
            if (File.Exists(_baselinePath))
            {
                File.Copy(_baselinePath, _localPath, overwrite: true);
                var roles = await LoadLocalRolesAsync();
                RoleFitCalculator.SetCache(roles);
            }
        }
    }
}
