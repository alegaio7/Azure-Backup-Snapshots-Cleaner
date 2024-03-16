# Azure Backup Snapshots Cleaner

A timer-triggered function that deletes old disk snapshots create by Azure Backup Vault.

Backup Vault is one way to protect VM disks in the platform with the ease of a couple of clicks, however, it does not provide a way to automatically remove old snapshots. 

I found (the hard way) that a single disk can have up to 500 snapshots, after which the backup process will fail.

This function is designed to run on a configurable schedule and remove old snapshots based on a set of filters.

A final, global filter is applied to all snapshots, which is an age test. Snapshots newer than this value will not be deleted even if they pass the filters.

## Configuration
The function is built to run in the isolated process worker runtime.
For a complete guide on setting up this project, check [my post here](https://www.alexgaio.com/post/automate-the-removal-of-old-disk-snapshots-from-azure).

### Environment Variables
The function needs the following environment variables to defined:
- AZURE_TENANT_ID: The Azure AD tenant ID. Used by _DefaultAzureCredential_ to authenticate with Azure.
- * AZURE_CLIENT_ID: The application ID as registered in App Registrations in Azure. Used by _DefaultAzureCredential_ to authenticate with Azure in development.
- * AZURE_CLIENT_SECRET: The client secret for the application. Used by _DefaultAzureCredential_ to authenticate with Azure.
- AZURE_SUBSCRIPTION_ID: The Azure subscription ID. Used by the function that reads from Resource Groups to get the list of snapshots.
- AZURE_RESOURCE_GROUP: The Azure resource group that contains the snapshots to be deleted.
- SNAPSHOTS_DAYS_TO_KEEP: The minimum age of snapshots to keep. Snapshots older than this value will be deleted (only if the pass the defined filters).
- FUNCTION_SCHEDULE: The CRON expression that defines the schedule for the function to run. For more information on CRON expressions, see [CRON expressions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer?tabs=python-v2%2Cisolated-process%2Cnodejs-v4&pivots=programming-language-csharp#ncrontab-expressions).

*) Only in development mode.

When in development mode, add these environment variables to **local.settings.json**.
They can be updated automatically in Azure according to the project configuration. Check https://learn.microsoft.com/en-us/azure/azure-functions/functions-develop-local#local-settings-file

Otherwise, add them manually to the function's [app settings section](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings?tabs=portal#settings)

### Filters configuration
Filters are configured in a separate file named **filters.json**. This is because complex structures needed to define these filters cannot be expressed in simple strings, and Azure app settings only work with key-value pairs.

The file defines a collection of filters, which contain these properties:
- Id: The filter ID. This is used to identify the filter in the logs.
- Description: A description of the filter. Not used anywhere else, only for documentation purposes.
- StartsWith: A string that the snapshot name must start with. If this property is not defined, the filter will not be applied.
- Tags: A collection of key-value pairs that the snapshot must have. Tags collection can be empty, in which case the filter will only check the StartsWith property.
  -	MatchOnlyWithName: When set to true, tag evaluation ends if a match in the name is found. When false, the name and value of the tag must match.

### Local Development Configuration
In order to run this function in a development environment, check the following article on how to setup authentication in Azure:
https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/local-development-service-principal?tabs=azure-portal%2Cwindows%2Ccommand-line

