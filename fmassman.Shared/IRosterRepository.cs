using System.Collections.Generic;

namespace fmassman.Shared
{
    public interface IRosterRepository
    {
        List<PlayerImportData> Load();
        void Save(List<PlayerImportData> players);
        void Delete(string playerName);
    }
}
