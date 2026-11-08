#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds, publishes and deploys the WeatherAPI solution to Azure.

.DESCRIPTION
    1. Publishes the code with the dotnet CLI (Release) as Linux container images
       (dotnet publish -t:PublishContainer) for:
         WeatherAPI     -> weatherapi-api        (Azure Container App)
         ImageStarter   -> weatherapi-starter    (Azure Container Apps Job)
         ImageGenerator -> weatherapi-generator  (Azure Container Apps Job)
    2. Creates the Azure resources with the Bicep template (infra/main.bicep): storage account with
       blob containers, queues and tables, container registry, Container Apps environment.
    3. Pushes the images to the registry and deploys the API and jobs with the Azure CLI.

    Requirements: PowerShell 7+, .NET 10 SDK, Azure CLI (logged in with `az login`).
    Docker is NOT required: the .NET SDK builds and pushes the images itself.

.PARAMETER ResourceGroup
    Resource group to deploy into. Created if it doesn't exist.

.PARAMETER Location
    Azure region, used when the resource group has to be created.

.PARAMETER UnsplashAccessKey
    Unsplash API access key. Defaults to the UNSPLASH_ACCESS_KEY environment variable.

.PARAMETER ImageTag
    Tag for the container images. Defaults to the current git commit (short SHA) or a timestamp.

.EXAMPLE
    ./deploy.ps1 -UnsplashAccessKey <key>

.EXAMPLE
    $env:UNSPLASH_ACCESS_KEY = '<key>'; ./deploy.ps1 -ResourceGroup rg-weatherapi -Location westeurope
#>
[CmdletBinding()]
param(
    [string] $ResourceGroup = 'rg-weatherapi',
    [string] $Location = 'westeurope',
    [string] $UnsplashAccessKey = $env:UNSPLASH_ACCESS_KEY,
    [string] $ImageTag,
    [string] $NamePrefix = 'weather',
    [int] $ApiMinReplicas = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$template = Join-Path $root 'infra/main.bicep'
$projects = @(
    @{ Path = 'WeatherAPI/WeatherAPI.csproj';         Image = 'weatherapi-api' }
    @{ Path = 'ImageStarter/ImageStarter.csproj';     Image = 'weatherapi-starter' }
    @{ Path = 'ImageGenerator/ImageGenerator.csproj'; Image = 'weatherapi-generator' }
)

function Write-Step([string] $message) {
    Write-Host "`n==> $message" -ForegroundColor Cyan
}

# Native commands don't throw on failure; call this right after each one.
function Assert-ExitCode([string] $what) {
    if ($LASTEXITCODE -ne 0) {
        throw "$what failed with exit code $LASTEXITCODE."
    }
}

# ---------------------------------------------------------------------------------------------
Write-Step 'Checking prerequisites'

foreach ($tool in 'dotnet', 'az') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "'$tool' was not found on PATH."
    }
}

if ([string]::IsNullOrWhiteSpace($UnsplashAccessKey)) {
    throw 'An Unsplash access key is required: pass -UnsplashAccessKey or set $env:UNSPLASH_ACCESS_KEY.'
}

$account = az account show --query '{name:name, id:id}' -o json 2>$null | ConvertFrom-Json
if (-not $account) {
    throw "Not logged in to Azure. Run 'az login' first."
}
Write-Host "Subscription: $($account.name) ($($account.id))"

if (-not $ImageTag) {
    try { $ImageTag = git -C $root rev-parse --short HEAD 2>$null } catch { $ImageTag = $null }
    if (-not $ImageTag) {
        $ImageTag = Get-Date -Format 'yyyyMMddHHmmss'
    }
}
Write-Host "Image tag:    $ImageTag"

# ---------------------------------------------------------------------------------------------
Write-Step 'Building the solution (dotnet CLI)'

dotnet build (Join-Path $root 'WeatherAPI.sln') --configuration Release
Assert-ExitCode 'dotnet build'

# ---------------------------------------------------------------------------------------------
Write-Step "Creating Azure resources (resource group '$ResourceGroup', Bicep: storage, registry, environment)"

if ((az group exists --name $ResourceGroup) -ne 'true') {
    az group create --name $ResourceGroup --location $Location --output none
    Assert-ExitCode 'az group create'
}

$infra = az deployment group create `
    --resource-group $ResourceGroup `
    --name "infra-$ImageTag" `
    --template-file $template `
    --parameters deployApps=false namePrefix=$NamePrefix `
    --query properties.outputs -o json
Assert-ExitCode 'Infrastructure deployment'
$infra = $infra | ConvertFrom-Json

$registryName = $infra.registryName.value
$loginServer = $infra.registryLoginServer.value
Write-Host "Registry:     $loginServer"
Write-Host "Storage:      $($infra.storageAccountName.value)"

# ---------------------------------------------------------------------------------------------
Write-Step 'Publishing container images to the registry (dotnet publish -t:PublishContainer)'

# The .NET SDK pushes with these credentials, so no Docker daemon or `docker login` is needed.
$token = az acr login --name $registryName --expose-token --query accessToken -o tsv
if ($LASTEXITCODE -ne 0 -or -not $token) { throw "Could not get an access token for registry '$registryName'." }
$env:DOTNET_CONTAINER_REGISTRY_UNAME = '00000000-0000-0000-0000-000000000000'
$env:DOTNET_CONTAINER_REGISTRY_PWORD = $token

try {
    foreach ($project in $projects) {
        Write-Host "`n--- $($project.Image):$ImageTag"
        dotnet publish (Join-Path $root $project.Path) `
            --configuration Release `
            --os linux --arch x64 `
            '-t:PublishContainer' `
            "-p:ContainerRegistry=$loginServer" `
            "-p:ContainerRepository=$($project.Image)" `
            "-p:ContainerImageTag=$ImageTag"
        Assert-ExitCode "dotnet publish $($project.Path)"
    }
}
finally {
    Remove-Item Env:DOTNET_CONTAINER_REGISTRY_UNAME, Env:DOTNET_CONTAINER_REGISTRY_PWORD -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------------------------
Write-Step 'Deploying the API (Container App) and workers (Container Apps Jobs)'

# Pass the secret through a parameters file instead of the command line.
$parametersFile = New-TemporaryFile
try {
    @{
        '$schema'      = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
        contentVersion = '1.0.0.0'
        parameters     = @{
            deployApps        = @{ value = $true }
            namePrefix        = @{ value = $NamePrefix }
            imageTag          = @{ value = $ImageTag }
            apiMinReplicas    = @{ value = $ApiMinReplicas }
            unsplashAccessKey = @{ value = $UnsplashAccessKey }
        }
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $parametersFile -Encoding utf8

    $apiUrl = az deployment group create `
        --resource-group $ResourceGroup `
        --name "apps-$ImageTag" `
        --template-file $template `
        --parameters "@$parametersFile" `
        --query properties.outputs.apiUrl.value -o tsv
    Assert-ExitCode 'App deployment'
}
finally {
    Remove-Item $parametersFile -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------------------------
Write-Step 'Smoke test'

# The API can scale to zero, so give it a few attempts. An unknown process id must return 404.
$healthy = $false
foreach ($attempt in 1..10) {
    try {
        $status = (Invoke-WebRequest -Uri "$apiUrl/api/images/smoke-test" -SkipHttpErrorCheck -TimeoutSec 30).StatusCode
    }
    catch {
        $status = $_.Exception.Message
    }

    if ($status -eq 404) {
        $healthy = $true
        break
    }
    Write-Host "Attempt ${attempt}: $status, retrying in 15 s..."
    Start-Sleep -Seconds 15
}

if ($env:GITHUB_OUTPUT) {
    "apiUrl=$apiUrl" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

if (-not $healthy) {
    throw "Deployment finished, but the API at $apiUrl did not respond as expected."
}

Write-Host "`nDeployment complete." -ForegroundColor Green
Write-Host "API:            $apiUrl"
Write-Host "Start a run:    POST $apiUrl/api/images/generate"
Write-Host "Process status: GET  $apiUrl/api/images/{id}"
