using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using DeviceDetectorNET;
using DeviceDetectorNET.Parser;
using LogVisits.Helpers;
using LogVisits.Models;
using LogVisits.Services;

namespace LogVisit.Functions
{
    public class LogVisits
    {
        private readonly VisitorService _visitorService;
        private readonly ILogger<LogVisits> _logger;

        public LogVisits(VisitorService visitorService, ILogger<LogVisits> logger)
        {
            _visitorService = visitorService ?? throw new ArgumentNullException(nameof(visitorService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("LogVisit")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("Received a new visitor log request.");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Received request body: {requestBody}");

                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    return CreateErrorResponse(req, HttpStatusCode.BadRequest, "Request body is empty.");
                }

                var visit = JsonSerializer.Deserialize<VisitorLog>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (visit == null || string.IsNullOrWhiteSpace(visit.pageVisited))
                {
                    return CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid or missing 'pageVisited' field.");
                }

                visit.date = DateTime.UtcNow;
                visit.ipAddress = GetIpAddress(ExtractHeaderValue(req, "X-Forwarded-For")) ?? "111.1.1.1";

                string userAgent = ExtractHeaderValue(req, "User-Agent") ?? "Unknown";
                visit.referrer = ExtractHeaderValue(req, "Referer") ?? "Unknown";

                // Parse user-agent details
                var deviceInfo = GetDeviceInfo(userAgent);
                visit.browser = deviceInfo.Browser;
                visit.device = deviceInfo.Device;
                //visit.os = deviceInfo.OperatingSystem;

                _logger.LogInformation("Logging visit for page {PageVisited} from IP {IpAddress}.", visit.pageVisited, visit.ipAddress);

                // Ensure only one log per day per IP per page
                var result = await _visitorService.LogVisitAsync(visit);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = result });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LogVisit function.");
                return CreateErrorResponse(req, HttpStatusCode.InternalServerError, $"Internal Server Error: {ex.Message}");
            }
        }

        private static string? GetIpAddress(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return null;

            foreach (var ip in ipAddress.Split(',').Select(ip => ip.Split(':')[0].Trim()))
                if (IPAddress.TryParse(ip, out _))
                    return ip;

            return null;
        }

        private static string ExtractHeaderValue(HttpRequestData req, string headerName)
        {
            return req.Headers.TryGetValues(headerName, out var values) ? values.FirstOrDefault()?.Split(',')[0]?.Trim() : null;
        }

        private static HttpResponseData CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string errorMessage)
        {
            var errorResponse = req.CreateResponse(statusCode);
            errorResponse.WriteString(JsonSerializer.Serialize(new { success = false, error = errorMessage }));
            return errorResponse;
        }

        private static (string Device, string Browser, string OperatingSystem) GetDeviceInfo(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return ("Unknown", "Unknown", "Unknown");

            var deviceDetector = new DeviceDetector(userAgent);
            deviceDetector.Parse();

            string device = deviceDetector.GetDeviceName() ?? "Unknown";
            string os = deviceDetector.GetOs()?.Match?.Name ?? "Unknown";
            string browser = deviceDetector.GetClient()?.Match?.Name ?? "Unknown";

            return (device, browser, os);
        }
    }
}
