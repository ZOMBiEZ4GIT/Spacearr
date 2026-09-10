using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spacearr.Jobs;

/// <summary>
/// Shared JSON options for everything serialised by the job subsystem outside
/// the ASP.NET response pipeline (SSE payloads, the <see cref="Job.Summary"/>
/// column). Mirrors the camelCase + string-enum convention configured for
/// HTTP responses in Program.cs so job types/statuses read the same way
/// everywhere (e.g. "scan", not "0").
/// </summary>
internal static class JobJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
