# Infrastructure

`main.bicep` deploys everything into one resource group:

| Resource | Module | Purpose |
|---|---|---|
| Storage account | `modules/storage.bicep` | Blob containers `weather-images` (private, SAS) and `temp-backgrounds`; queues `generation-processes`, `weather-images` (+ `-poison`); tables `CreationProcesses`, `GeneratedImages` |
| Container registry + identity | `modules/registry.bicep` | Basic ACR; user-assigned identity with `AcrPull` used by the API and jobs (no registry passwords) |
| Container Apps environment | `modules/container-apps-environment.bicep` | Consumption environment + Log Analytics |
| API | `modules/api-container-app.bicep` | `WeatherAPI` Container App, external HTTPS ingress |
| Starter job | `modules/starter-worker-container.bicep` | `ImageStarter` event-driven job on the `generation-processes` queue |
| Generator job | `modules/generator-worker-container.bicep` | `ImageGenerator` event-driven job on the `weather-images` queue; one execution per station message (fan-out, up to 50 in parallel) |

The GitHub workflow (`.github/workflows/build.yml`) deploys in two phases: `deployApps=false` (infrastructure), push images, then `deployApps=true` (apps and jobs).

## One-time setup

GitHub Actions logs in with a **user-assigned managed identity** that trusts the repo through a
federated credential (OIDC). Unlike an app registration, this needs no Entra directory permissions,
only Owner (or Contributor + User Access Administrator) on the resource group.

```bash
az login
az account set --subscription "<subscription name or id>"

# Resource providers (only needed once per subscription)
for p in Microsoft.App Microsoft.ContainerRegistry Microsoft.OperationalInsights Microsoft.Storage Microsoft.ManagedIdentity; do
  az provider register --namespace "$p" --wait
done

RG=rg-weatherapi
az group create --name "$RG" --location westeurope

# Identity + federated credential (repo:<owner>/<repo>:environment:production) + Owner on $RG
az deployment group create \
  --resource-group "$RG" \
  --name github-identity \
  --template-file infra/bootstrap/github-identity.bicep \
  --parameters repository=Ihor713289/WeatherAPI \
  --query properties.outputs -o json
```

The output contains `clientId`, `tenantId` and `subscriptionId`. Add them as repository secrets,
in the GitHub UI under **Settings → Secrets and variables → Actions**, or with the GitHub CLI:

```bash
OUT=$(az deployment group show -g "$RG" -n github-identity --query properties.outputs -o json)
gh secret set AZURE_CLIENT_ID       --body "$(jq -r .clientId.value <<< "$OUT")"
gh secret set AZURE_TENANT_ID       --body "$(jq -r .tenantId.value <<< "$OUT")"
gh secret set AZURE_SUBSCRIPTION_ID --body "$(jq -r .subscriptionId.value <<< "$OUT")"
gh secret set UNSPLASH_ACCESS_KEY   # paste the key when prompted
# Optional, only for a different resource group name:
# gh variable set AZURE_RESOURCE_GROUP --body "$RG"
```

Notes:

- The deploy job uses `environment: production`; the federated credential's subject must match
  (`repo:Ihor713289/WeatherAPI:environment:production`), otherwise login fails with `AADSTS70021`.
- The identity can't create resource groups, so the workflow only checks that `$RG` exists.
- If you delete the resource group, the identity goes with it. Re-run the bootstrap step afterwards
  and update `AZURE_CLIENT_ID` (the client id changes).

## Manual deployment (deploy.ps1)

`deploy.ps1` in the repository root does the whole deployment from your machine, and the GitHub
workflow runs the same script. It needs PowerShell 7 (`pwsh`), the .NET 10 SDK and the Azure CLI.
Docker is not needed: the images are built and pushed by `dotnet publish -t:PublishContainer`.

```bash
az login
pwsh ./deploy.ps1 -UnsplashAccessKey "<key>"     # or: export UNSPLASH_ACCESS_KEY=<key>; pwsh ./deploy.ps1
# options: -ResourceGroup rg-weatherapi -Location westeurope -ImageTag <tag> -ApiMinReplicas 1
```

Steps: `dotnet build` -> `main.bicep` with `deployApps=false` (storage, registry, environment) ->
`dotnet publish -t:PublishContainer` for the three projects, pushed to the registry ->
`main.bicep` with `deployApps=true` (Container App + Jobs) -> smoke test against the API.
