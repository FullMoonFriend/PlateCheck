using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.Functions;

public class OcrPlateFunction
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OcrPlateFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OcrPlateFunction(IHttpClientFactory httpClientFactory, ILogger<OcrPlateFunction> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [Function("OcrPlate")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ocr-plate")] HttpRequestData req)
    {
        var apiKey = Environment.GetEnvironmentVariable("PLATE_RECOGNIZER_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            var err = req.CreateResponse(HttpStatusCode.InternalServerError);
            await err.WriteStringAsync("PLATE_RECOGNIZER_API_KEY is not configured.");
            return err;
        }

        // Client sends raw image bytes; Content-Type header carries the MIME type (image/jpeg etc.)
        req.Headers.TryGetValues("Content-Type", out var ctHeaders);
        var mimeType = ctHeaders?.FirstOrDefault() ?? "image/jpeg";

        var imageBytes = new MemoryStream();
        await req.Body.CopyToAsync(imageBytes);
        imageBytes.Position = 0;

        // Build multipart for Plate Recognizer, adding mmc=true and regions=us
        using var outContent = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageBytes.ToArray());
        imageContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        outContent.Add(imageContent, "upload", "plate.jpg");
        outContent.Add(new StringContent("true"), "mmc");
        outContent.Add(new StringContent("us"), "regions");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Token", apiKey);

        var prResponse = await client.PostAsync(
            "https://api.platerecognizer.com/v1/plate-reader/", outContent);
        var prBody = await prResponse.Content.ReadAsStringAsync();

        if (!prResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Plate Recognizer returned {Status}: {Body}", prResponse.StatusCode, prBody);
            var errResp = req.CreateResponse(HttpStatusCode.BadGateway);
            await errResp.WriteStringAsync("OCR service error.");
            return errResp;
        }

        var json = JsonNode.Parse(prBody);
        var results = json?["results"]?.AsArray();

        if (results == null || results.Count == 0)
        {
            var notFound = req.CreateResponse(HttpStatusCode.OK);
            notFound.Headers.Add("Content-Type", "application/json");
            await notFound.WriteStringAsync(JsonSerializer.Serialize(
                new VehicleResult { Success = false, ErrorMessage = "No license plate detected in the image." },
                JsonOptions));
            return notFound;
        }

        var best = results[0]!;
        var plate = best["plate"]?.GetValue<string>()?.ToUpperInvariant() ?? string.Empty;

        // Region detected by Plate Recognizer (e.g. "us-ca" → "CA")
        var regionCode = best["region"]?["code"]?.GetValue<string>();
        var state = regionCode?.Contains('-') == true
            ? regionCode.Split('-')[1].ToUpperInvariant()
            : string.Empty;

        // Best MMC prediction
        var mmcPrediction = best["mmc"]?["predictions"]?.AsArray()?.FirstOrDefault();
        var make  = mmcPrediction?["make"]?.GetValue<string>();
        var model = mmcPrediction?["model"]?.GetValue<string>();
        var color = mmcPrediction?["color"]?.GetValue<string>();

        int? yearFrom = mmcPrediction?["year_from"]?.GetValue<int>();
        int? yearTo   = mmcPrediction?["year_to"]?.GetValue<int>();
        var yearRange = (yearFrom, yearTo) switch
        {
            ({ } f, { } t) when f == t => f.ToString(),
            ({ } f, { } t)             => $"{f}–{t}",
            ({ } f, null)              => $"{f}+",
            _                          => null
        };

        var vehicleType = best["vehicle"]?["type"]?.GetValue<string>();

        var result = new VehicleResult
        {
            PlateNumber = plate,
            State       = state,
            Make        = make,
            Model       = model,
            Color       = color,
            YearRange   = yearRange,
            VehicleType = vehicleType,
            Success     = true,
            DataSource  = "photo"
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions));
        return response;
    }
}
