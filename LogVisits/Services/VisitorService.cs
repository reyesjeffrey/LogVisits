using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LogVisits.Models;

namespace LogVisits.Services
{
    public class VisitorService : IDisposable
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Microsoft.Azure.Cosmos.Container _container;
        // private readonly ILogger<VisitorService> _logger;

        public VisitorService(IConfiguration config, ILogger<VisitorService> logger)
        {
            // _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                var connectionString = config["CosmosDB"];
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new ArgumentException("CosmosDB connection string is missing from configuration.");
                }

                _cosmosClient = new CosmosClient(connectionString);
                _container = _cosmosClient.GetContainer(config["CosmosDbDatabase"], config["CosmosDbContainer"]);
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "Error initializing CosmosDB client.");
                throw;
            }
        }

        public async Task<string> LogVisitAsync(VisitorLog visit)
        {
            try
            {
                if (visit == null || string.IsNullOrWhiteSpace(visit.pageVisited))
                {
                    throw new ArgumentException("Invalid visitor log data.");
                }

                visit.date = DateTime.UtcNow;

                string visitDate = visit.date.ToString("yyyy-MM-dd");


                // Check Cosmos DB to check if a log already exists for this IP, page, and date
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.ipAddress = @ip AND c.pageVisited = @page AND STARTSWITH(c.date, @date)")
                    .WithParameter("@ip", visit.ipAddress)
                    .WithParameter("@page", visit.pageVisited)
                    .WithParameter("@date", visitDate);

                using FeedIterator<VisitorLog> feedIterator = _container.GetItemQueryIterator<VisitorLog>(query);

                if (feedIterator.HasMoreResults)
                {
                    var existingLogs = await feedIterator.ReadNextAsync();
                    if (existingLogs.Any())
                    {
                        // _logger.LogInformation("Skipping log for {pageVisited} from {ipAddress} as a record already exists today.", visit.pageVisited, visit.ipAddress);
                        return "Log already exists for today.";
                    }
                }

                // _logger.LogInformation("Inserting visit for {pageVisited} from IP {ipAddress}.", visit.pageVisited, visit.ipAddress);

                // Only insert if no duplicate log exists
                visit.id = Guid.NewGuid().ToString();
                await _container.CreateItemAsync(visit, new PartitionKey(visit.ipAddress));

                // _logger.LogInformation("Visit logged successfully.");
                return "Visit logged successfully.";
            }
            catch (CosmosException cosmosEx)
            {
                // _logger.LogError(cosmosEx, "CosmosDB error: {StatusCode}", cosmosEx.StatusCode);
                return $"CosmosDB Error: {cosmosEx.Message}";
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "Failed to log visit.");
                return $"Error: {ex.Message}";
            }
        }

        public void Dispose()
        {
            _cosmosClient?.Dispose();
        }
    }
}
