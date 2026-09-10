using FluentAssertions;
using Spacearr.Actions;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;

namespace Spacearr.Tests.Actions;

public class ConfirmTokensTests
{
    private sealed class FakeClock : IClock { public DateTime UtcNow { get; set; } = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc); }

    [Fact]
    public void Token_validates_for_same_request_and_not_for_a_changed_one()
    {
        var clock = new FakeClock();
        var tokens = new ConfirmTokens(clock);
        var req = new ActionRequest(ActionType.Replace, 7, 4);
        var token = tokens.Issue(req);
        tokens.Validate(req, token).Should().BeTrue();
        tokens.Validate(req with { TargetProfileId = 5 }, token).Should().BeFalse();
        tokens.Validate(req with { Unmonitor = true }, token).Should().BeFalse();
        tokens.Validate(req, token + "x").Should().BeFalse();
    }

    [Fact]
    public void Token_expires_after_ten_minutes()
    {
        var clock = new FakeClock();
        var tokens = new ConfirmTokens(clock);
        var req = new ActionRequest(ActionType.Delete, 1, null);
        var token = tokens.Issue(req);
        clock.UtcNow = clock.UtcNow.AddMinutes(9);
        tokens.Validate(req, token).Should().BeTrue();
        clock.UtcNow = clock.UtcNow.AddMinutes(2);
        tokens.Validate(req, token).Should().BeFalse();
    }
}
