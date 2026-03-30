using System.Collections.Generic;
using NLog;

namespace NzbDrone.Core.Spacearr.ArrIntegration
{
    public interface IQualityProfileCache
    {
        void RefreshRadarrProfiles(List<RadarrQualityProfile> profiles);
        void RefreshSonarrProfiles(List<SonarrQualityProfile> profiles);
        string GetProfileName(MediaSource source, int profileId);
        void Clear();
    }

    public class QualityProfileCache : IQualityProfileCache
    {
        private readonly Logger _logger;
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
        private readonly object _lock = new object();

        public QualityProfileCache(Logger logger)
        {
            _logger = logger;
        }

        public void RefreshRadarrProfiles(List<RadarrQualityProfile> profiles)
        {
            lock (_lock)
            {
                // Remove old Radarr entries
                var keysToRemove = new List<string>();
                foreach (var key in _cache.Keys)
                {
                    if (key.StartsWith("radarr:"))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                }

                foreach (var profile in profiles)
                {
                    _cache[$"radarr:{profile.Id}"] = profile.Name;
                }

                _logger.Debug("Refreshed Radarr quality profile cache with {0} profiles", profiles.Count);
            }
        }

        public void RefreshSonarrProfiles(List<SonarrQualityProfile> profiles)
        {
            lock (_lock)
            {
                // Remove old Sonarr entries
                var keysToRemove = new List<string>();
                foreach (var key in _cache.Keys)
                {
                    if (key.StartsWith("sonarr:"))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                }

                foreach (var profile in profiles)
                {
                    _cache[$"sonarr:{profile.Id}"] = profile.Name;
                }

                _logger.Debug("Refreshed Sonarr quality profile cache with {0} profiles", profiles.Count);
            }
        }

        public string GetProfileName(MediaSource source, int profileId)
        {
            var prefix = source == MediaSource.Radarr ? "radarr" : "sonarr";
            var key = $"{prefix}:{profileId}";

            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var name))
                {
                    return name;
                }
            }

            _logger.Warn("Quality profile not found in cache: {0}", key);
            return null;
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }
    }
}
