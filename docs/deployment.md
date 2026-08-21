# Deployment

## Automatic Deployment to Azure Functions

GitHub Actions automatically performs the following when triggered by a push to the `main`/`master` branch (or manual `workflow_dispatch`) via [`.github/workflows/azure-functions-deploy.yml`](../.github/workflows/azure-functions-deploy.yml):

1. Outputs build artifacts with `dotnet publish --configuration Release --output ./output`.
2. Logs in to Azure using OIDC authentication.
3. Previews infrastructure changes with `az deployment group what-if` against `infra/main.bicep`.
4. Applies the Bicep template with `az deployment group create`. If this step fails, the job stops and the Function App code deploy below is skipped.
5. Deploys to the Azure Functions app using `Azure/functions-action`.

## Required GitHub Secrets

| Secret Name | Purpose |
|---|---|
| `AZURE_CLIENT_ID` | Client ID of the Azure AD application used for OIDC authentication |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID of the deployment target |
| `AZURE_FUNCTIONAPP_NAME` | Name of the Function App to deploy to |
| `DISCORD_WEBHOOK_URL` | Bicep parameter (`discordWebhookUrl`) passed to `infra/main.bicep` |
| `ITAD_API_KEY` | Bicep parameter (`itadApiKey`) passed to `infra/main.bicep` |
| `STEAM_PROFILE_ID` | Bicep parameter (`steamProfileId`) passed to `infra/main.bicep`. Stored as a secret (rather than a variable) so GitHub Actions masks it in run logs, even though the value itself is not a credential |

These are configured at the repository level (Settings > Secrets and variables > Actions), consistent with the existing `AZURE_*` secrets above. The workflow's `environment: production` currently has no environment-level secrets or protection rules configured.

## Required GitHub Variables

| Variable Name | Purpose |
|---|---|
| `AZURE_RESOURCE_GROUP` | Resource group name passed to `az deployment group what-if`/`create` |

This is a non-sensitive value, so it is configured as a repository variable rather than a secret.

## CI (Build Verification)

[`.github/workflows/dotnet-ci.yml`](../.github/workflows/dotnet-ci.yml) executes `dotnet restore` → `dotnet build` when triggered by `push`/`pull_request` (target branches: `main`/`master`) and verifies the build succeeds.

