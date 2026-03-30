using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Disk;
using Spacearr.Http;

namespace Spacearr.Api.V1.FileSystem
{
    [V3ApiController]
    public class FileSystemController : Controller
    {
        private readonly IFileSystemLookupService _fileSystemLookupService;
        private readonly IDiskProvider _diskProvider;

        public FileSystemController(IFileSystemLookupService fileSystemLookupService,
                                IDiskProvider diskProvider)
        {
            _fileSystemLookupService = fileSystemLookupService;
            _diskProvider = diskProvider;
        }

        [HttpGet]
        public IActionResult GetContents(string path, bool includeFiles = false, bool allowFoldersWithoutTrailingSlashes = false)
        {
            return Ok(_fileSystemLookupService.LookupContents(path, includeFiles, allowFoldersWithoutTrailingSlashes));
        }

        [HttpGet("type")]
        public object GetEntityType(string path)
        {
            if (_diskProvider.FileExists(path))
            {
                return new { type = "file" };
            }

            return new { type = "folder" };
        }
    }
}
