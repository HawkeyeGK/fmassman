using System.Collections.Generic;
using System.Threading.Tasks;

namespace fmassman.Shared
{
    public interface ITagRepository
    {
        Task<List<TagDefinition>> GetAllAsync();
        Task SaveAsync(TagDefinition tag);
        Task DeleteAsync(string id);
    }
}
