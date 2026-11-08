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

```powershell
$subscriptionId = az account show --query id -o tsv
$tenantId       = az account show --query tenantId -o tsv
$rg             = "rg-weatherapi"
$repo           = "Ihor713289/WeatherAPI"

az group create --name $rg --location westeurope

# App registration that GitHub Actions logs in as (OIDC, no client secret).
$appId = az ad app create --display-name "github-weatherapi" --query appId -o tsv
az ad sp create --id $appId

# Owner on the resource group: Contributor is not enough, because the template
# creates a role assignment (AcrPull for the apps' identity).
az role assignment create --assignee $appId --role Owner `
  --scope "/subscriptions/$subscriptionId/resourceGroups/$rg"

# Trust the workflow's "production" environment.
@"
{ "name": "github-production", "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:${repo}:environment:production", "audiences": ["api://AzureADTokenExchange"] }
"@ | Out-File fic.json -Encoding utf8
az ad app federated-credential create --id $appId --parameters fic.json
Remove-Item fic.json

"AZURE_CLIENT_ID=$appId"; "AZURE_TENANT_ID=$tenantId"; "AZURE_SUBSCRIPTION_ID=$subscriptionId"
```

Then in GitHub, under **Settings → Secrets and variables → Actions**:

- **Secrets**: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `UNSPLASH_ACCESS_KEY`
- **Variables** (optional): `AZURE_RESOURCE_GROUP` (default `rg-weatherapi`), `AZURE_LOCATION` (default `westeurope`)

If the subscription has never used these services, register the resource providers once:

```powershell
foreach ($p in "Microsoft.App","Microsoft.ContainerRegistry","Microsoft.OperationalInsights","Microsoft.Storage","Microsoft.ManagedIdentity") {
  az provider register --namespace $p
}
```

## Manual deployment

```powershell
az deployment group create -g rg-weatherapi -f infra/main.bicep -p deployApps=false
# push weatherapi-api / weatherapi-starter / weatherapi-generator images with tag <tag>
az deployment group create -g rg-weatherapi -f infra/main.bicep `
  -p deployApps=true imageTag=<tag> unsplashAccessKey=<key>
```
