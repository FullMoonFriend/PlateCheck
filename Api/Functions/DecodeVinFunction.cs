using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.Functions;

public class DecodeVinFunction
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DecodeVinFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DecodeVinFunction(IHttpClientFactory httpClientFactory, ILogger<DecodeVinFunction> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [Function("DecodeVin")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "decode-vin")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var vin = query["vin"]?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(vin))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("vin query parameter is required.");
            return badRequest;
        }

        var client = _httpClientFactory.CreateClient();
        var url = $"https://vpic.nhtsa.dot.gov/api/vehicles/decodevin/{vin}?format=json";

        var nhtsaResponse = await client.GetAsync(url);
        var body = await nhtsaResponse.Content.ReadAsStringAsync();

        if (!nhtsaResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("NHTSA returned {Status}: {Body}", nhtsaResponse.StatusCode, body);
            var errorResponse = req.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse.WriteStringAsync("VIN decode service error.");
            return errorResponse;
        }

        var json = JsonNode.Parse(body);
        var results = json?["Results"]?.AsArray();

        string? Get(string variable) =>
            results?.FirstOrDefault(r => r?["Variable"]?.GetValue<string>() == variable)
                   ?["Value"]?.GetValue<string>()
                   ?.Trim()
                   .NullIfEmpty();

        var result = new VehicleResult
        {
            Vin = vin,
            Year = Get("Model Year"),
            Make = Get("Make"),
            Model = Get("Model"),
            Trim = Get("Trim"),
            BodyStyle = Get("Body Class"),
            FuelType = Get("Fuel Type - Primary"),
            EngineDisplacement = Get("Displacement (L)"),
            Cylinders = Get("Engine Number of Cylinders"),
            Transmission = Get("Transmission Style"),
            DriveType = Get("Drive Type"),
            Success = true
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions));
        return response;
    }
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "Not Applicable" ? null : value;
}
