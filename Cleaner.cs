using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.RecoveryServices.Models;
using Azure.ResourceManager.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure_Backup_Snapshots_Cleaner
{
    public class Cleaner
    {
        private readonly ILogger<Cleaner> _logger;
        private readonly IOptions<SnapshotFilterOptions> _filters;

        private const int MIN_SNAPSHOT_DAYS = 3;
        private const int MAX_SNAPSHOT_DAYS = 3650;

        public Cleaner(ILogger<Cleaner> logger, IOptions<SnapshotFilterOptions> filters)
        {
            _logger = logger;
            _filters = filters;
        }

#if DEBUG
        [Function(nameof(ManualTrigger))]
        public async Task ManualTrigger([HttpTrigger(AuthorizationLevel.Admin, "get")] HttpRequest req)
        {
            _logger.LogInformation($"Calling ${nameof(CleanSnapshots)} manually... ");
            await CleanSnapshots(new TimerInfo());
        }
#endif

        [Function(nameof(CleanSnapshots))]
        public async Task CleanSnapshots([TimerTrigger("%FUNCTION_SCHEDULE%")] TimerInfo timerInfo)
        {
            string? s = default;

            _logger.LogInformation("Initiating snapshots listing...");

            try
            {
                if (_filters is null || _filters.Value is null)
                {
                    s = "Filters are not set.";
                    throw new Exception(s);
                }

                var filters = _filters.Value.Filters;
                if (filters is null || filters.Count == 0)
                {
                    s = "Filters collection is empty. Please check filters configuration file.";
                    throw new Exception(s);
                }

                var daysToKeep = Environment.GetEnvironmentVariable("SNAPSHOTS_DAYS_TO_KEEP");

                if (string.IsNullOrEmpty(daysToKeep))
                {
                    s = "Environment variable SNAPSHOTS_DAYS_TO_KEEP is not set.";
                    throw new Exception(s);
                }

                if (!int.TryParse(daysToKeep, out int days) || days < MIN_SNAPSHOT_DAYS || days > MAX_SNAPSHOT_DAYS)
                {
                    s = $"Environment variable SNAPSHOTS_DAYS_TO_KEEP is not a valid integer. Please use a value between {MIN_SNAPSHOT_DAYS} and {MAX_SNAPSHOT_DAYS}";
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
                var itemsToDeleteList = new List<GenericResource>();
                await foreach (var item in resList)
                {
                    if (item.Data.ResourceType.Type == "snapshots")
                    {
                        // check that the snapshot passes the filters
                        foreach (var f in filters)
                        {
                            var filterId = CheckIsSnapshotFiltered(item, f);
                            if (filterId.HasValue)
                            {
                                _logger.LogInformation($"Snapshot {item.Data.Name}, created on {item.Data.CreatedOn?.ToString("s")} passed filter id {filterId}.");
                                
                                if (item.Data.CreatedOn.HasValue && item.Data.CreatedOn.Value < DateTime.UtcNow.AddDays(days * -1))
                                {
                                    itemsToDeleteList.Add(item);
                                    _logger.LogWarning($"The snapshot {item.Data.Name} is marked for deletion.");
                                }
                                else
                                    _logger.LogInformation($"Snapshot {item.Data.Name} was outside the deletion time window");
                            }
                        }
                    }
                }

                foreach (var item in itemsToDeleteList)
                {
                    _logger.LogWarning($"Deleting snapshot {item.Data.Name}...");
                    var armOp = await item.DeleteAsync(Azure.WaitUntil.Completed);
                    var azResponse = armOp.UpdateStatus();
                    if (azResponse.IsError)
                        // TODO Add more useful error information
                        _logger.LogError($"Error deleting snapshot {item.Data.Name}: {azResponse.Status}");
                    else
                        _logger.LogWarning($"Snapshot {item.Data.Name} deleted.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error @ {nameof(CleanSnapshots)}");
                throw;
            }
        }

        /// <summary>
        /// Checks whether a snapshot item passes a defined filter. If so, it returns the filter id, otherwise it returns null
        /// </summary>
        /// <param name="item"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        private int? CheckIsSnapshotFiltered(GenericResource item, SnapshotFilterOptions.SnapshotFilter filter)
        {
            var itemName = item.Data.Name.ToLower();

            if (!string.IsNullOrEmpty(filter.StartsWith))
            {
                if (!itemName.StartsWith(filter.StartsWith.ToLower()))
                    return null;
            }

            if (filter.Tags != null && filter.Tags.Count > 0)
            {
                // If filters are defined, but the item has no tags, it's not a match
                if (item.Data.Tags == null || item.Data.Tags.Count == 0)
                    return null;

                foreach (var tag in filter.Tags)
                {
                    if (item.Data.Tags.ContainsKey(tag.Name))
                    {
                        if (tag.MatchOnlyWithName)
                            return filter.Id;

                        if (string.IsNullOrEmpty(tag.Value))
                            continue;

                        var value = item.Data.Tags[tag.Name];
                        if (!string.IsNullOrEmpty(value) && value.ToLower() == tag.Value.ToLower())
                            return filter.Id;
                    }
                }
            }

            return null;
        }
    }
}
