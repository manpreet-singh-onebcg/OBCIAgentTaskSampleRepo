# Security Configuration Guide

## Overview
This document provides instructions for securely configuring the AgenticTaskManager application without hardcoded credentials.

## Development Environment Setup

### 1. Initialize User Secrets
```bash
cd AgenticTaskManager.API
dotnet user-secrets init
```

### 2. Generate Secure Keys
Use the KeyGenerator utility to create cryptographically secure keys:

```csharp
// Run this code to generate keys
KeyGeneratorConsole.PrintSecureKeys();
```

### 3. Set User Secrets (Development)
```bash
# Encryption key for sensitive data
dotnet user-secrets set "Security:EncryptionKey" "YOUR_GENERATED_AES256_KEY"

# JWT signing key
dotnet user-secrets set "Security:JwtSecretKey" "YOUR_GENERATED_JWT_KEY"

# Admin password hash (use SecurityHelper.HashPassword to generate)
dotnet user-secrets set "Security:AdminPasswordHash" "YOUR_HASHED_ADMIN_PASSWORD"

# API keys for external services
dotnet user-secrets set "ApiKeys:ExternalService" "YOUR_EXTERNAL_API_KEY"
dotnet user-secrets set "ApiKeys:BackupService" "YOUR_BACKUP_API_KEY"

# Database connection string with credentials
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=AgenticTasks;User=dbuser;Password=dbpassword;TrustServerCertificate=True;"
```

## Production Environment Setup

### 1. Environment Variables
Set these environment variables in your production environment:

```bash
# Security settings
Security__EncryptionKey=YOUR_PRODUCTION_AES256_KEY
Security__JwtSecretKey=YOUR_PRODUCTION_JWT_KEY
Security__AdminPasswordHash=YOUR_PRODUCTION_ADMIN_HASH

# API keys
ApiKeys__ExternalService=YOUR_PRODUCTION_EXTERNAL_API_KEY
ApiKeys__BackupService=YOUR_PRODUCTION_BACKUP_API_KEY

# Database connection
ConnectionStrings__DefaultConnection="Server=prod-server;Database=AgenticTasks;User=prod_user;Password=prod_password;TrustServerCertificate=True;"
```

### 2. Azure Key Vault (Recommended for Azure deployments)
```json
{
  "KeyVault": {
    "Endpoint": "https://your-keyvault.vault.azure.net/",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id"
  }
}
```

### 3. Kubernetes Secrets (For Kubernetes deployments)
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: taskmanager-secrets
type: Opaque
stringData:
  Security__EncryptionKey: "YOUR_BASE64_ENCRYPTION_KEY"
  Security__JwtSecretKey: "YOUR_BASE64_JWT_KEY"
  Security__AdminPasswordHash: "YOUR_HASHED_PASSWORD"
  ConnectionStrings__DefaultConnection: "YOUR_CONNECTION_STRING"
```

## Security Best Practices

### 1. Key Rotation
- Rotate encryption keys regularly (every 90 days recommended)
- Update JWT signing keys periodically
- Change API keys when team members leave

### 2. Access Control
- Limit access to production secrets to essential personnel only
- Use role-based access control for secret management systems
- Audit secret access regularly

### 3. Monitoring
- Monitor for failed authentication attempts
- Log configuration access (without logging the actual secrets)
- Set up alerts for unusual access patterns

### 4. Development Guidelines
- Never commit secrets to version control
- Use different secrets for each environment
- Regularly scan code for accidentally committed secrets

## Troubleshooting

### Common Issues

1. **"Configuration key not found" errors**
   - Ensure all required secrets are set in user secrets (development) or environment variables (production)
   - Check that the key names match exactly (case-sensitive)

2. **"Invalid key format" errors**
   - Ensure encryption keys are valid Base64 strings
   - Use the KeyGenerator utility to create properly formatted keys

3. **Authentication failures**
   - Verify that password hashes are generated using the SecurityHelper.HashPassword method
   - Ensure JWT secret keys are sufficiently long (64+ characters recommended)

### Validation Commands
```bash
# List current user secrets
dotnet user-secrets list

# Clear all user secrets (be careful!)
dotnet user-secrets clear

# Remove specific secret
dotnet user-secrets remove "Security:EncryptionKey"
```

## Migration from Hardcoded Credentials

### Steps to migrate existing code:
1. Identify all hardcoded credentials in the codebase
2. Replace static credential references with dependency injection
3. Update configuration to use SecurityConfiguration class
4. Generate new secure keys using KeyGenerator
5. Set up proper secret management for each environment
6. Test thoroughly in development environment
7. Deploy with proper secret management in production

### Code Changes Required:
- Replace `ProblematicUtilities` with `SecureUtilities`
- Update `SecurityHelper` to use injected configuration
- Register services in DI container using `ServiceCollectionExtensions`
- Update controllers and services to use injected dependencies

## Compliance Notes

This security setup addresses:
- ? No hardcoded credentials in source code
- ? Secure key generation using cryptographically strong methods
- ? Proper separation of configuration by environment
- ? Secure storage of sensitive configuration
- ? Regular key rotation capability
- ? Audit trail for secret access
- ? Principle of least privilege for secret access