using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Spacearr.Scanner
{
    public interface IFileScannerService
    {
        void Scan();
    }

    public class FileScannerService : IFileScannerService
    {
        private readonly IRootFolderService _rootFolderService;
        private readonly IFileDiscoveryService _fileDiscoveryService;
        private readonly IMediaInfoExtractor _mediaInfoExtractor;
        private readonly IMediaFileRepository _mediaFileRepository;
        private readonly IScanJobRepository _scanJobRepository;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public FileScannerService(IRootFolderService rootFolderService,
            IFileDiscoveryService fileDiscoveryService,
            IMediaInfoExtractor mediaInfoExtractor,
            IMediaFileRepository mediaFileRepository,
            IScanJobRepository scanJobRepository,
            IDiskProvider diskProvider,
            Logger logger)
        {
            _rootFolderService = rootFolderService;
            _fileDiscoveryService = fileDiscoveryService;
            _mediaInfoExtractor = mediaInfoExtractor;
            _mediaFileRepository = mediaFileRepository;
            _scanJobRepository = scanJobRepository;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public void Scan()
        {
            var scanJob = new ScanJob
            {
                StartedAt = DateTime.UtcNow,
                Status = ScanStatus.Running,
                FilesScanned = 0,
                FilesAdded = 0,
                FilesRemoved = 0
            };

            scanJob = _scanJobRepository.Insert(scanJob);

            try
            {
                var rootFolders = _rootFolderService.All();
                var libraryPaths = rootFolders.Select(r => r.Path).ToList();

                _logger.Info("Starting library scan for {0} root folder(s)", libraryPaths.Count);

                var discoveredFiles = _fileDiscoveryService.GetMediaFiles(libraryPaths);

                _logger.Info("Discovered {0} media files across all libraries", discoveredFiles.Count);

                var discoveredFilePaths = new HashSet<string>(discoveredFiles, StringComparer.OrdinalIgnoreCase);
                var filesAdded = 0;
                var filesScanned = 0;

                foreach (var filePath in discoveredFiles)
                {
                    try
                    {
                        filesScanned++;
                        var processed = ProcessFile(filePath, libraryPaths, ref filesAdded);

                        if (filesScanned % 100 == 0)
                        {
                            _logger.Info("Scan progress: {0}/{1} files processed", filesScanned, discoveredFiles.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error processing file: {0}", filePath);
                    }
                }

                var filesRemoved = RemoveOrphanedRecords(libraryPaths, discoveredFilePaths);

                scanJob.FilesScanned = filesScanned;
                scanJob.FilesAdded = filesAdded;
                scanJob.FilesRemoved = filesRemoved;
                scanJob.Status = ScanStatus.Completed;
                scanJob.CompletedAt = DateTime.UtcNow;

                _scanJobRepository.Update(scanJob);

                _logger.Info(
                    "Library scan completed. Scanned: {0}, Added: {1}, Removed: {2}",
                    filesScanned,
                    filesAdded,
                    filesRemoved);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Library scan failed");

                scanJob.Status = ScanStatus.Failed;
                scanJob.ErrorMessage = ex.Message;
                scanJob.CompletedAt = DateTime.UtcNow;

                _scanJobRepository.Update(scanJob);
            }
        }

        private bool ProcessFile(string filePath, List<string> libraryPaths, ref int filesAdded)
        {
            var existing = _mediaFileRepository.FindByPath(filePath);

            if (existing != null)
            {
                var lastWrite = _diskProvider.FileGetLastWrite(filePath);

                if (lastWrite <= existing.LastScanned)
                {
                    _logger.Trace("Skipping unchanged file: {0}", filePath);
                    return false;
                }
            }

            var fileSize = _diskProvider.GetFileSize(filePath);
            var mediaInfo = _mediaInfoExtractor.Extract(filePath);
            var libraryPath = FindLibraryPath(filePath, libraryPaths);

            var mediaFile = existing ?? new MediaFile();

            mediaFile.Path = filePath;
            mediaFile.SizeBytes = fileSize;
            mediaFile.LibraryPath = libraryPath;
            mediaFile.LastScanned = DateTime.UtcNow;

            if (mediaInfo != null)
            {
                mediaFile.BitrateBps = mediaInfo.BitrateBps;
                mediaFile.Codec = mediaInfo.Codec;
                mediaFile.Resolution = mediaInfo.Width > 0 && mediaInfo.Height > 0
                    ? $"{mediaInfo.Width}x{mediaInfo.Height}"
                    : null;
                mediaFile.ResolutionWidth = mediaInfo.Width;
                mediaFile.ResolutionHeight = mediaInfo.Height;
                mediaFile.DurationSeconds = mediaInfo.DurationSeconds;
                mediaFile.ContainerFormat = mediaInfo.ContainerFormat;
            }

            if (existing != null)
            {
                _mediaFileRepository.Update(mediaFile);
                _logger.Debug("Updated media file: {0}", filePath);
            }
            else
            {
                _mediaFileRepository.Insert(mediaFile);
                filesAdded++;
                _logger.Debug("Added new media file: {0}", filePath);
            }

            return true;
        }

        private int RemoveOrphanedRecords(List<string> libraryPaths, HashSet<string> discoveredFilePaths)
        {
            var removed = 0;

            foreach (var libraryPath in libraryPaths)
            {
                var existingRecords = _mediaFileRepository.FindByLibraryPath(libraryPath);

                foreach (var record in existingRecords)
                {
                    if (!discoveredFilePaths.Contains(record.Path))
                    {
                        _logger.Debug("Removing orphaned media file record: {0}", record.Path);
                        _mediaFileRepository.Delete(record.Id);
                        removed++;
                    }
                }
            }

            return removed;
        }

        private static string FindLibraryPath(string filePath, List<string> libraryPaths)
        {
            return libraryPaths
                .Where(lp => filePath.StartsWith(lp, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(lp => lp.Length)
                .FirstOrDefault();
        }
    }
}
