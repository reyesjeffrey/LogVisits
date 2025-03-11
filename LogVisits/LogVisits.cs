using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using LogVisits.Helpers;
using LogVisits.Models;
using LogVisits.Services;

namespace LogVisit.Functions
{
    public class LogVisits
    {
        private readonly VisitorService _visitorService;
        // private readonly ILogger<LogVisits> _logger;


        public LogVisits(VisitorService visitorService, ILogger<LogVisits> logger)
        {
            _visitorService = visitorService ?? throw new ArgumentNullException(nameof(visitorService));
            // _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("LogVisit")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            // _logger.LogInformation("Received a new visitor log request.");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                // _logger.LogInformation($"Received request body: {requestBody}");

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
                visit.ipAddress = ExtractHeaderValue(req, "X-Forwarded-For") ?? "111.1.1.1";
                visit.browser = ExtractHeaderValue(req, "User-Agent") ?? "Unknown";
                visit.referrer = ExtractHeaderValue(req, "Referer") ?? "Unknown";
                visit.device = DeviceHelper.GetDeviceType(visit.browser);

                // _logger.LogInformation("Logging visit for page {PageVisited} from IP {IpAddress}.", visit.pageVisited, visit.ipAddress);

                // Ensure only one log per day per IP per page
                var result = await _visitorService.LogVisitAsync(visit);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = result });

                return response;
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "Error processing LogVisit function.");
                return CreateErrorResponse(req, HttpStatusCode.InternalServerError, $"Internal Server Error.{ex.Message}");
            }
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
    }
}
