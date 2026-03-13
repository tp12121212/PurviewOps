# Deployment

Target: Azure Container Apps.

Resources:
- Container Apps environment
- API app + Worker app
- Azure Storage queue + blob container
- Key Vault
- Application Insights

Use Bicep files in `infra/bicep` and the CI pipeline in `.github/workflows/ci-cd.yml`.
