# デプロイ

## Azure Functions への自動デプロイ

[`.github/workflows/azure-functions-deploy.yml`](../.github/workflows/azure-functions-deploy.yml) により、`main`/`master` ブランチへの push (または手動の `workflow_dispatch`) をトリガーに、GitHub Actions が自動的に以下を実行します。

1. `dotnet publish --configuration Release --output ./output` でビルド成果物を出力する。
2. OIDC 認証で Azure にログインする。
3. `Azure/functions-action` で Azure Functions アプリにデプロイする。

## 必要な GitHub Secrets

| Secret 名 | 用途 |
|---|---|
| `AZURE_CLIENT_ID` | OIDC 認証に使用する Azure AD アプリケーションのクライアント ID |
| `AZURE_TENANT_ID` | Azure AD テナント ID |
| `AZURE_SUBSCRIPTION_ID` | デプロイ先の Azure サブスクリプション ID |
| `AZURE_FUNCTIONAPP_NAME` | デプロイ先の Function App 名 |

これらは GitHub リポジトリの `production` environment に設定されている想定です (ワークフロー内 `environment: production` を参照)。

## CI (ビルド確認)

[`.github/workflows/dotnet-ci.yml`](../.github/workflows/dotnet-ci.yml) が `push`/`pull_request` (対象ブランチ: `main`/`master`) をトリガーに `dotnet restore` → `dotnet build` を実行し、ビルドが通ることを確認します。

## スコープ外

Azure リソース (Function App / Storage アカウントなど) 自体のプロビジョニング手順は、このドキュメントの対象外です。既存の Azure リソースに対して、上記ワークフローがコードをデプロイする、という前提で説明しています。
