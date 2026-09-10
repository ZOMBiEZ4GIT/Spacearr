using System.Security.Cryptography;
using System.Text;
using Spacearr.Infrastructure;

namespace Spacearr.Actions;

public sealed class ConfirmTokens
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private readonly IClock _clock;
    public ConfirmTokens(IClock clock) => _clock = clock;

    public string Issue(ActionRequest r) => Issue(r, _clock.UtcNow.Add(Lifetime));

    public DateTime ExpiryFor(string token) =>
        long.TryParse(token.Split('.')[0], out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;

    private string Issue(ActionRequest r, DateTime expires)
    {
        var payload = Payload(r, expires);
        var mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
        return $"{expires.Ticks}.{Convert.ToHexString(mac).ToLowerInvariant()}";
    }

    public bool Validate(ActionRequest r, string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        var parts = token.Split('.');
        if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks)) return false;
        var expires = new DateTime(ticks, DateTimeKind.Utc);
        if (expires < _clock.UtcNow) return false;
        var expected = Issue(r with { ConfirmToken = null }, expires);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(token));
    }

    private static string Payload(ActionRequest r, DateTime expires) =>
        $"{r.Type}|{r.ItemId}|{r.TargetProfileId}|{r.Unmonitor}|{expires.Ticks}";
}
