# Deployment

## Automatic Deployment to Azure Functions

GitHub Actions automatically performs the following when triggered by a push to the `main`/`master` branch (or manual `workflow_dispatch`) via [`.github/workflows/azure-functions-deploy.yml`](../.github/workflows/azure-functions-deploy.yml):

1. Outputs build artifacts with `dotnet publish --configuration Release --output ./output`.
2. Logs in to Azure using OIDC authentication.
3. Deploys to the Azure Functions app using `Azure/functions-action`.

## Required GitHub Secrets

| Secret Name | Purpose |
|---|---|
| `AZURE_CLIENT_ID` | Client ID of the Azure AD application used for OIDC authentication |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID of the deployment target |
| `AZURE_FUNCTIONAPP_NAME` | Name of the Function App to deploy to |

These are expected to be configured in the `production` environment of the GitHub repository (see `environment: production` in the workflow).

## CI (Build Verification)

[`.github/workflows/dotnet-ci.yml`](../.github/workflows/dotnet-ci.yml) executes `dotnet restore` → `dotnet build` when triggered by `push`/`pull_request` (target branches: `main`/`master`) and verifies the build succeeds.

## Out of Scope

Provisioning procedures for Azure resources themselves (Function App / Storage accounts, etc.) are outside the scope of this document. This document assumes existing Azure resources to which the above workflows deploy code.
