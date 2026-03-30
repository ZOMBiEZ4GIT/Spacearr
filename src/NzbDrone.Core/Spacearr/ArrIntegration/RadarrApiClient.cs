using System;
using System.Collections.Generic;
using System.Net;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Core.Spacearr.ArrIntegration
{
    public interface IRadarrApiClient
    {
        List<RadarrMovie> GetMovies(ArrConnectionSettings settings);
        List<RadarrQualityProfile> GetQualityProfiles(ArrConnectionSettings settings);
        ArrConnectionTestResult TestConnection(ArrConnectionSettings settings);
        void TriggerMovieSearch(ArrConnectionSettings settings, int movieId);
        void UpdateQualityProfile(ArrConnectionSettings settings, int movieId, int qualityProfileId);
    }

    public class RadarrApiClient : IRadarrApiClient
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public RadarrApiClient(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public List<RadarrMovie> GetMovies(ArrConnectionSettings settings)
        {
            var request = BuildRequest(settings, "/api/v3/movie");
            var response = _httpClient.Get<List<RadarrMovie>>(request);
            return response.Resource;
        }

        public List<RadarrQualityProfile> GetQualityProfiles(ArrConnectionSettings settings)
        {
            var request = BuildRequest(settings, "/api/v3/qualityprofile");
            var response = _httpClient.Get<List<RadarrQualityProfile>>(request);
            return response.Resource;
        }

        public ArrConnectionTestResult TestConnection(ArrConnectionSettings settings)
        {
            try
            {
                var request = BuildRequest(settings, "/api/v3/system/status");
                var response = _httpClient.Get<ArrSystemStatus>(request);

                _logger.Info("Successfully connected to Radarr at {0} (version {1})", settings.Url, response.Resource.Version);

                return new ArrConnectionTestResult
                {
                    Success = true,
                    Version = response.Resource.Version
                };
            }
            catch (HttpException ex) when (ex.Response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.Warn("Radarr connection failed: unauthorized. Check API key for {0}", settings.Url);

                return new ArrConnectionTestResult
                {
                    Success = false,
                    ErrorMessage = "Unauthorized: invalid API key"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to connect to Radarr at {0}", settings.Url);

                return new ArrConnectionTestResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public void TriggerMovieSearch(ArrConnectionSettings settings, int movieId)
        {
            var request = BuildRequest(settings, "/api/v3/command");
            request.Method = System.Net.Http.HttpMethod.Post;

            var body = new ArrCommand
            {
                Name = "MoviesSearch",
                MovieIds = new List<int> { movieId }
            };

            request.Headers.ContentType = "application/json";
            request.SetContent(body.ToJson());
            _httpClient.Post(request);

            _logger.Debug("Triggered movie search for movie ID {0} on Radarr", movieId);
        }

        public void UpdateQualityProfile(ArrConnectionSettings settings, int movieId, int qualityProfileId)
        {
            var request = BuildRequest(settings, $"/api/v3/movie/{movieId}");
            request.Method = System.Net.Http.HttpMethod.Put;

            // First get the movie to preserve other fields
            var getRequest = BuildRequest(settings, $"/api/v3/movie/{movieId}");
            var response = _httpClient.Get(getRequest);
            var content = response.Content;

            // Deserialize, update, and re-serialize
            var movie = Json.Deserialize<RadarrMovie>(content);
            if (movie.QualityProfile == null)
            {
                movie.QualityProfile = new RadarrQualityProfileRef();
            }

            movie.QualityProfile.Id = qualityProfileId;

            request.Headers.ContentType = "application/json";
            request.SetContent(movie.ToJson());
            _httpClient.Execute(request);

            _logger.Debug("Updated quality profile for movie ID {0} to profile {1} on Radarr", movieId, qualityProfileId);
        }

        private HttpRequest BuildRequest(ArrConnectionSettings settings, string resource)
        {
            var baseUrl = settings.Url.TrimEnd('/');
            var builder = new HttpRequestBuilder(baseUrl)
                .Resource(resource)
                .Accept(HttpAccept.Json)
                .SetHeader("X-Api-Key", settings.ApiKey);

            return builder.Build();
        }
    }
}
