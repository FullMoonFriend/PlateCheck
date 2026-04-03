using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.Functions;

public class LookupPlateFunction
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LookupPlateFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LookupPlateFunction(IHttpClientFactory httpClientFactory, ILogger<LookupPlateFunction> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [Function("LookupPlate")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lookup-plate")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var plate = query["plate"]?.Trim().ToUpperInvariant();
        var state = query["state"]?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(plate) || string.IsNullOrEmpty(state))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("plate and state query parameters are required.");
            return badRequest;
        }

        var apiKey = Environment.GetEnvironmentVariable("AUTO_DEV_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("AUTO_DEV_API_KEY is not configured.");
            return errorResponse;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var url = $"https://api.auto.dev/plate/{state}/{plate}?state={state}";

        var apiResponse = await client.GetAsync(url);
        var body = await apiResponse.Content.ReadAsStringAsync();

        if (!apiResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Auto.dev returned {Status}: {Body}", apiResponse.StatusCode, body);
            var errorResponse = req.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse.WriteStringAsync("Plate lookup service error.");
            return errorResponse;
        }

        var json = JsonNode.Parse(body);

        var vin = json?["vin"]?.GetValue<string>();
        var year = json?["year"]?.ToString();
        var make = json?["make"]?.GetValue<string>();
        var model = json?["model"]?.GetValue<string>();
        var trim = json?["trim"]?.GetValue<string>();
        var drivetrain = json?["drivetrain"]?.GetValue<string>();
        var engine = json?["engine"]?.GetValue<string>();
        var transmission = json?["transmission"]?.GetValue<string>();

        var result = new VehicleResult
        {
            PlateNumber = plate,
            State = state,
            Vin = vin,
            Year = year,
            Make = make,
            Model = model,
            Trim = trim,
            DriveType = drivetrain,
            Transmission = transmission,
            Success = vin != null || make != null,
            ErrorMessage = (vin == null && make == null)
                ? "No vehicle found for this plate/state combination."
                : null
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions));
        return response;
    }
}
