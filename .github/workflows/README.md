# GitHub Actions Workflows

This repository contains two GitHub Actions workflows for building the eComBox UWP application.

## Workflows

### 1. MSBuild UWP (`msbuild.yml`)

This is a simple build workflow that compiles the UWP application without packaging.

**Triggers:**
- Push to `master` branch
- Pull requests to `master` branch

**What it does:**
- Builds the application for x64 and x86 platforms
- Uploads build artifacts (without signing)

**Use this workflow for:** Quick CI builds and validation

### 2. Build UWP MSIX (`dotnet-desktop.yml`)

This is the full packaging workflow that creates MSIX packages.

**Triggers:**
- Push to `master` branch
- Pull requests to `master` branch

**What it does:**
- Builds the UWP application
- Creates MSIX packages
- Signs packages (if certificate secrets are configured)
- Uploads MSIX packages as artifacts

**Use this workflow for:** Creating distributable MSIX packages

## Certificate Configuration (Optional)

To sign your MSIX packages, you need to add the following secrets to your GitHub repository:

### Step 1: Encode your certificate

```powershell
$pfx_cert = Get-Content '.\YourCertificate.pfx' -Encoding Byte
[System.Convert]::ToBase64String($pfx_cert) | Out-File 'SigningCertificate_Encoded.txt'
```

### Step 2: Add secrets to GitHub

1. Go to your repository Settings → Secrets and variables → Actions
2. Add the following secrets:
   - `Base64_Encoded_Pfx`: The base64-encoded certificate string from Step 1
   - `Pfx_Key`: The password for your certificate

### Without Certificate Secrets

If you don't configure the certificate secrets, the workflow will:
- Build unsigned MSIX packages (can still be sideloaded for testing)
- OR generate a self-signed test certificate automatically

## Downloading Build Artifacts

After a workflow run completes:

1. Go to the Actions tab in your repository
2. Click on the workflow run
3. Scroll down to the "Artifacts" section
4. Download the artifacts you need:
   - `UWP-Build-x64` / `UWP-Build-x86` (from MSBuild workflow)
   - `MSIX-Package-x64-Release` (from Build UWP MSIX workflow)

## Troubleshooting

### Build fails with "Platform not specified"
- Make sure you're using the correct workflow that specifies platforms

### "Certificate not found" errors
- Either add the certificate secrets or use the unsigned build option

### NuGet restore fails
- The workflow should handle this automatically, but check that all package references are valid

## Local Development

To build locally, you need:
- Visual Studio 2019 or later with UWP workload
- Windows 10 SDK (version 10.0.18362.0 or later)

Build command:
```powershell
msbuild eComBox.sln /p:Configuration=Release /p:Platform=x64
```
