using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.RecoveryServices.Models;
using Azure.ResourceManager.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azure_Backup_Vault_Snapshots_Cleaner
{
    public class Cleaner
    {
        private readonly ILogger<Cleaner> _logger;

        public Cleaner(ILogger<Cleaner> logger)
        {
            _logger = logger;
        }

        [Function(nameof(CleanSnapshots))]
        public async Task<IActionResult> CleanSnapshots([TimerTrigger("0 */5 * * * *", RunOnStartup = true)] TimerInfo timerInfo)
        {
            _logger.LogInformation("Initiating backup vault items listing...");

            try
            {
                var daysToKeep = Environment.GetEnvironmentVariable("SNAPSHOTS_DAYS_TO_KEEP");
                string? s = default;

                if (string.IsNullOrEmpty(daysToKeep))
                {
                    s = "Environment variable SNAPSHOTS_DAYS_TO_KEEP is not set.";
                    _logger.LogError(s);
                    throw new Exception(s);
                }

                if (!int.TryParse(daysToKeep, out int days) || days < 3 || days > 3650)
                {
                    s = "Environment variable SNAPSHOTS_DAYS_TO_KEEP is not a valid integer. Please use a value between 14 and 3650";
                    _logger.LogError(s);
                    throw new Exception(s);
                }

                TokenCredential cred = new DefaultAzureCredential();

                ArmClient client = new ArmClient(cred);

                var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
                var resourceGroupName = Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP");
                ResourceIdentifier resourceGroupResourceId = ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName);
                ResourceGroupResource resourceGroupResource = client.GetResourceGroupResource(resourceGroupResourceId);

                var filter = "resourceType eq 'Microsoft.Compute/snapshots'";
                var expand = "createdTime";
                var resList = resourceGroupResource.GetGenericResourcesAsync(filter: filter, expand: expand);
                await foreach (var item in resList)
                {
                    if (item.Data.ResourceType.Type == "snapshots")
                    {
                        if (item.Data.CreatedOn.HasValue && item.Data.CreatedOn.Value < DateTime.UtcNow.AddDays(days * -1))
                            _logger.LogInformation($"Deleting snapshot {item.Data.Name}...");
                        // await item.DeleteAsync();
                    }
                }
                
                return new OkObjectResult("OK!");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error @ {nameof(CleanSnapshots)}");
                throw;
            }

            return new OkObjectResult("OK!");
        }
    }
}
