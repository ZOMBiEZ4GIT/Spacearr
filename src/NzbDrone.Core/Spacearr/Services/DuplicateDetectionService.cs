using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Spacearr.Services
{
    public class DuplicateGroup
    {
        public int GroupId { get; set; }
        public string Title { get; set; }
        public List<LibraryItem> Items { get; set; }
    }

    public interface IDuplicateDetectionService
    {
        List<DuplicateGroup> GetDuplicateGroups();
        DuplicateGroup GetGroup(int groupId);
    }

    public class DuplicateDetectionService : IDuplicateDetectionService
    {
        private readonly ILibraryService _libraryService;

        public DuplicateDetectionService(ILibraryService libraryService)
        {
            _libraryService = libraryService;
        }

        public List<DuplicateGroup> GetDuplicateGroups()
        {
            var all = _libraryService.GetAll();

            var groups = all
                .Where(x => x.MediaItem != null)
                .GroupBy(x => new { x.MediaItem.Title, x.MediaItem.Year, x.MediaItem.Source })
                .Where(g => g.Count() > 1)
                .Select((g, index) => new DuplicateGroup
                {
                    GroupId = index + 1,
                    Title = g.Key.Title,
                    Items = g.ToList()
                })
                .ToList();

            return groups;
        }

        public DuplicateGroup GetGroup(int groupId)
        {
            var groups = GetDuplicateGroups();
            return groups.FirstOrDefault(g => g.GroupId == groupId);
        }
    }
}
