using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Spacearr.Scanner
{
    public interface IFileDiscoveryService
    {
        List<string> GetMediaFiles(List<string> libraryPaths);
        List<string> GetMediaFiles(string libraryPath);
    }

    public class FileDiscoveryService : IFileDiscoveryService
    {
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        private static readonly HashSet<string> MediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv",
            ".mp4",
            ".avi",
            ".ts",
            ".m4v",
            ".wmv",
            ".flv",
            ".mov",
            ".webm"
        };

        public FileDiscoveryService(IDiskProvider diskProvider, Logger logger)
        {
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public List<string> GetMediaFiles(List<string> libraryPaths)
        {
            var allFiles = new List<string>();

            foreach (var path in libraryPaths)
            {
                allFiles.AddRange(GetMediaFiles(path));
            }

            return allFiles;
        }

        public List<string> GetMediaFiles(string libraryPath)
        {
            if (!_diskProvider.FolderExists(libraryPath))
            {
                _logger.Warn("Library path does not exist: {0}", libraryPath);
                return new List<string>();
            }

            _logger.Debug("Scanning library path for media files: {0}", libraryPath);

            try
            {
                var files = _diskProvider.GetFiles(libraryPath, true)
                    .Where(f => MediaExtensions.Contains(Path.GetExtension(f)))
                    .ToList();

                _logger.Debug("Found {0} media files in {1}", files.Count, libraryPath);

                return files;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error scanning library path: {0}", libraryPath);
                return new List<string>();
            }
        }
    }
}
