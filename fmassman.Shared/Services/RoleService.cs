using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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

        public void Initialize()
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
            var roles = LoadLocalRoles();
            RoleFitCalculator.SetCache(roles);
        }

        public List<RoleDefinition> LoadLocalRoles()
        {
            if (!File.Exists(_localPath)) return new List<RoleDefinition>();
            try
            {
                var json = File.ReadAllText(_localPath);
                return JsonSerializer.Deserialize<List<RoleDefinition>>(json) ?? new List<RoleDefinition>();
            }
            catch
            {
                return new List<RoleDefinition>();
            }
        }

        public void SaveRoles(List<RoleDefinition> roles)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(roles, options);
            File.WriteAllText(_localPath, json);
            
            // Hot Reload the Engine
            RoleFitCalculator.SetCache(roles);
        }

        public void ResetToBaseline()
        {
            if (File.Exists(_baselinePath))
            {
                File.Copy(_baselinePath, _localPath, overwrite: true);
                var roles = LoadLocalRoles();
                RoleFitCalculator.SetCache(roles);
            }
        }
    }
}
