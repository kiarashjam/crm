using System.Net;
using System.Text;
using System.Text.Json;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>Sending a spreadsheet to the importer, as the import pages do.</summary>
public static class ImportFiles
{
    /// <summary>The file's bytes as the request carries them.</summary>
    public static string Content(string csv) => Convert.ToBase64String(Encoding.UTF8.GetBytes(csv));

    public static async Task<JsonElement> OkJson(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public static Task<HttpResponseMessage> Analyze(HttpClient client, string kind, string csv) =>
        client.PostAsync("/api/imports/analyze", Body(new { kind, fileName = $"{kind}s.csv", content = Content(csv) }));

    /// <param name="columns">The field for each column of the file, in order; null leaves one out.</param>
    public static Task<HttpResponseMessage> Preview(HttpClient client, string kind, string csv, string?[] columns, string duplicates = "skip", object? extra = null) =>
        client.PostAsync("/api/imports/preview", Body(Request(kind, csv, columns, duplicates, extra)));

    public static Task<HttpResponseMessage> Import(HttpClient client, string kind, string csv, string?[] columns, string duplicates = "skip", object? extra = null) =>
        client.PostAsync("/api/imports", Body(Request(kind, csv, columns, duplicates, extra)));

    private static Dictionary<string, object?> Request(string kind, string csv, string?[] columns, string duplicates, object? extra)
    {
        var request = new Dictionary<string, object?>
        {
            ["kind"] = kind,
            ["fileName"] = $"{kind}s.csv",
            ["content"] = Content(csv),
            ["columns"] = columns,
            ["duplicates"] = duplicates,
        };
        if (extra != null)
            foreach (var property in JsonSerializer.SerializeToElement(extra, Json).EnumerateObject())
                request[property.Name] = property.Value;
        return request;
    }

    /// <summary>The row of a result with this spreadsheet row number.</summary>
    public static JsonElement Row(JsonElement result, int row) =>
        result.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("row").GetInt32() == row);

    public static string Action(JsonElement result, int row) => Row(result, row).GetProperty("action").GetString()!;

    public static string Message(JsonElement result, int row) => Row(result, row).GetProperty("message").GetString() ?? "";

    public static IReadOnlyList<string> Notes(JsonElement result, int row) =>
        Row(result, row).GetProperty("notes").EnumerateArray().Select(n => n.GetString()!).ToList();

    public static IReadOnlyList<string> Changes(JsonElement result, int row) =>
        Row(result, row).GetProperty("changes").EnumerateArray().Select(n => n.GetString()!).ToList();
}
