# Azure Backup Snapshots Cleaner

A timer-triggered Azure Function that removes old disk snapshots create by Azure Backup Vault.

Azure Backup Vault is one way to protect VM disk in Azure in an easy way, however, it does not provide a way to automatically remove old snapshots. 

I found (the hard way) that a single disk can have up to 500 snapshots, after which the backup process will fail.

This function is designed to run on a schedule and remove old snapshots based on a set of filters.

## Configuration
The function is built to run in the isolated process worker runtime.

### Environment Variables
The function expects the following environment variables to defined:
- FUNCTIONS_WORKER_RUNTIME: "dotnet-isolated"
- AZURE_TENANT_ID: The Azure AD tenant ID. Used by _DefaultAzureCredential_ to authenticate with Azure.
- AZURE_CLIENT_ID: The application ID as registered in App Registrations in Azure. Used by _DefaultAzureCredential_ to authenticate with Azure.
- AZURE_CLIENT_SECRET: The client secret for the application. Used by _DefaultAzureCredential_ to authenticate with Azure.
- AZURE_SUBSCRIPTION_ID: The Azure subscription ID. Used by the functions that read from Resource Groups to get the snapshots.
- AZURE_RESOURCE_GROUP: The Azure resource group name. Used by the functions that read from Resource Groups to get the snapshots.
- SNAPSHOTS_DAYS_TO_KEEP: The minimum age of snapshots to keep. Snapshots older than this value will be deleted (only if the pass the defined filters).
- FUNCTION_SCHEDULE: The CRON expression that defines the schedule for the function to run. For more information on CRON expressions, see [CRON expressions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer?tabs=python-v2%2Cisolated-process%2Cnodejs-v4&pivots=programming-language-csharp#ncrontab-expressions).

When in development mode, add these environment variables to **local.settings.json**.
They can be updated automatically in Azure according to the project configuration. Check https://learn.microsoft.com/en-us/azure/azure-functions/functions-develop-local#local-settings-file

Otherwise, add them manually to the function's [app settings section](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings?tabs=portal#settings)

### Filters configuration
Filters are configured in a separate file named **filters.json**. This is due to the fact that a more complex (json) structure is needed to define these filters, and Azure app settings only work with key-value pairs.

The file defines a collection of filters, which contain these properties:
- Id: The filter ID. This is used to identify the filter in the logs.
- Description: A description of the filter. Not used anywhere else, only for documentation purposes.
- StartsWith: A string that the snapshot name must start with. If this property is not defined, the filter will not be applied.
- Tags: A collection of key-value pairs that the snapshot must have. Tags collection can be empty, in which case the filter will only check the StartsWith property.
  -	MatchOnlyWithName: When set to true, tag evaluation ends if a match in the name is found. When false, the name and value of the tag must match.

### Local Development Configuration
In order to run this function in a development environment, check the following article on how to setup authentication in Azure:
https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/local-development-service-principal?tabs=azure-portal%2Cwindows%2Ccommand-line



