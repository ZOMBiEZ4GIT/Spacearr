using System;
using System.Collections.Generic;
using System.Net;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Core.Spacearr.ArrIntegration
{
    public interface ISonarrApiClient
    {
        List<SonarrSeries> GetSeries(ArrConnectionSettings settings);
        List<SonarrEpisodeFile> GetEpisodeFiles(ArrConnectionSettings settings, int seriesId);
        List<SonarrQualityProfile> GetQualityProfiles(ArrConnectionSettings settings);
        ArrConnectionTestResult TestConnection(ArrConnectionSettings settings);
        void TriggerEpisodeSearch(ArrConnectionSettings settings, List<int> episodeFileIds);
        void UpdateSeriesQualityProfile(ArrConnectionSettings settings, int seriesId, int qualityProfileId);
    }

    public class SonarrApiClient : ISonarrApiClient
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public SonarrApiClient(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public List<SonarrSeries> GetSeries(ArrConnectionSettings settings)
        {
            var request = BuildRequest(settings, "/api/v3/series");
            var response = _httpClient.Get<List<SonarrSeries>>(request);
            return response.Resource;
        }

        public List<SonarrEpisodeFile> GetEpisodeFiles(ArrConnectionSettings settings, int seriesId)
        {
            var request = BuildRequest(settings, "/api/v3/episodefile");
            request.Url = request.Url.AddQueryParam("seriesId", seriesId.ToString());
            var response = _httpClient.Get<List<SonarrEpisodeFile>>(request);
            return response.Resource;
        }

        public List<SonarrQualityProfile> GetQualityProfiles(ArrConnectionSettings settings)
        {
            var request = BuildRequest(settings, "/api/v3/qualityprofile");
            var response = _httpClient.Get<List<SonarrQualityProfile>>(request);
            return response.Resource;
        }

        public ArrConnectionTestResult TestConnection(ArrConnectionSettings settings)
        {
            try
            {
                var request = BuildRequest(settings, "/api/v3/system/status");
                var response = _httpClient.Get<ArrSystemStatus>(request);

                _logger.Info("Successfully connected to Sonarr at {0} (version {1})", settings.Url, response.Resource.Version);

                return new ArrConnectionTestResult
                {
                    Success = true,
                    Version = response.Resource.Version
                };
            }
            catch (HttpException ex) when (ex.Response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.Warn("Sonarr connection failed: unauthorized. Check API key for {0}", settings.Url);

                return new ArrConnectionTestResult
                {
                    Success = false,
                    ErrorMessage = "Unauthorized: invalid API key"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to connect to Sonarr at {0}", settings.Url);

                return new ArrConnectionTestResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public void TriggerEpisodeSearch(ArrConnectionSettings settings, List<int> episodeFileIds)
        {
            var request = BuildRequest(settings, "/api/v3/command");
            request.Method = System.Net.Http.HttpMethod.Post;

            var body = new ArrCommand
            {
                Name = "EpisodeSearch",
                EpisodeIds = episodeFileIds
            };

            request.Headers.ContentType = "application/json";
            request.SetContent(body.ToJson());
            _httpClient.Post(request);

            _logger.Debug("Triggered episode search for {0} episode(s) on Sonarr", episodeFileIds.Count);
        }

        public void UpdateSeriesQualityProfile(ArrConnectionSettings settings, int seriesId, int qualityProfileId)
        {
            // Get the series first to preserve other fields
            var getRequest = BuildRequest(settings, $"/api/v3/series/{seriesId}");
            var response = _httpClient.Get(getRequest);
            var series = Json.Deserialize<SonarrSeries>(response.Content);
            series.QualityProfileId = qualityProfileId;

            var putRequest = BuildRequest(settings, $"/api/v3/series/{seriesId}");
            putRequest.Method = System.Net.Http.HttpMethod.Put;
            putRequest.Headers.ContentType = "application/json";
            putRequest.SetContent(series.ToJson());
            _httpClient.Execute(putRequest);

            _logger.Debug("Updated quality profile for series ID {0} to profile {1} on Sonarr", seriesId, qualityProfileId);
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
