using System.Collections.Generic;

namespace FM26_Helper.Shared
{
    public interface IRosterRepository
    {
        List<PlayerImportData> Load();
        void Save(List<PlayerImportData> players);
        void Delete(string playerName);
    }
}
