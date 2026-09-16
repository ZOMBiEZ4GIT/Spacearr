using FluentAssertions;
using Spacearr.Arr;

namespace Spacearr.Tests.Arr;

/// <summary>
/// Syncing a large library is the one arr call that is allowed to be slow. Right after a
/// full scan of a ~40k-file library, Sonarr's /api/v3/series took 67-92 s (its own log)
/// against 0.2 s idle, because the scan had pushed its SQLite pages out of the page
/// cache - so the flat 30 s client timeout failed every first sync on a big library.
/// </summary>
public class ArrTimeoutTests
{
    private static readonly ArrTimeouts Fast = new(Request: TimeSpan.FromMilliseconds(150), Bulk: TimeSpan.FromSeconds(10));
    private static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(600);

    private static HttpClient Client(FakeArrHandler handler, string host) =>
        new(handler) { BaseAddress = new Uri($"http://{host}/"), Timeout = Timeout.InfiniteTimeSpan };

    [Fact]
    public async Task Sonarr_sync_survives_a_series_call_slower_than_the_request_timeout()
    {
        var handler = new FakeArrHandler()
            .MapSlow("GET", "/api/v3/series", FakeArrHandler.Fixture("sonarr-series.json"), Slow)
            .MapFixture("GET", "/api/v3/episodefile?seriesId=3", "sonarr-episodefile.json")
            .MapFixture("GET", "/api/v3/episode?seriesId=3", "sonarr-episode.json");

        var items = await new SonarrClient(Client(handler, "sonarr:8989"), "secret", Fast).GetItemsAsync(default);

        items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Sonarr_sync_survives_slow_per_series_calls_too()
    {
        // The per-series episodefile/episode calls run 638-deep on a real library and are
        // just as cold as /series is.
        var handler = new FakeArrHandler()
            .MapFixture("GET", "/api/v3/series", "sonarr-series.json")
            .MapSlow("GET", "/api/v3/episodefile?seriesId=3", FakeArrHandler.Fixture("sonarr-episodefile.json"), Slow)
            .MapSlow("GET", "/api/v3/episode?seriesId=3", FakeArrHandler.Fixture("sonarr-episode.json"), Slow);

        var items = await new SonarrClient(Client(handler, "sonarr:8989"), "secret", Fast).GetItemsAsync(default);

        items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Radarr_sync_survives_a_movie_call_slower_than_the_request_timeout()
    {
        var handler = new FakeArrHandler().MapSlow("GET", "/api/v3/movie", FakeArrHandler.Fixture("radarr-movie.json"), Slow);

        var items = await new RadarrClient(Client(handler, "radarr:7878"), "secret", Fast).GetItemsAsync(default);

        items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_slow_status_check_still_times_out_on_the_request_timeout()
    {
        // Everything that isn't a sync keeps the short budget: "Test" in the connection
        // form must fail fast rather than hang on an unreachable box for minutes.
        var handler = new FakeArrHandler().MapSlow("GET", "/api/v3/system/status", FakeArrHandler.Fixture("arr-status.json"), Slow);

        var act = () => new SonarrClient(Client(handler, "sonarr:8989"), "secret", Fast).GetStatusAsync(default);

        (await act.Should().ThrowAsync<ArrException>()).WithMessage("*Timed out talking to*");
    }

    [Fact]
    public async Task A_sync_slower_than_the_bulk_timeout_still_times_out()
    {
        var timeouts = new ArrTimeouts(Request: TimeSpan.FromMilliseconds(50), Bulk: TimeSpan.FromMilliseconds(150));
        var handler = new FakeArrHandler().MapSlow("GET", "/api/v3/series", FakeArrHandler.Fixture("sonarr-series.json"), Slow);

        var act = () => new SonarrClient(Client(handler, "sonarr:8989"), "secret", timeouts).GetItemsAsync(default);

        (await act.Should().ThrowAsync<ArrException>()).WithMessage("*Timed out talking to*");
    }

    [Fact]
    public async Task Caller_cancellation_propagates_rather_than_becoming_a_timeout()
    {
        var handler = new FakeArrHandler().MapSlow("GET", "/api/v3/series", FakeArrHandler.Fixture("sonarr-series.json"), Slow);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = () => new SonarrClient(Client(handler, "sonarr:8989"), "secret", Fast).GetItemsAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Defaults_are_30_seconds_per_request_and_5_minutes_for_a_sync()
    {
        ArrTimeouts.Default.Request.Should().Be(TimeSpan.FromSeconds(30));
        ArrTimeouts.Default.Bulk.Should().Be(TimeSpan.FromMinutes(5));
    }
}
