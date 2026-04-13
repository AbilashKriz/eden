# ERUS FAAS API Gateway

A reverse proxy API Gateway service built with YARP (Yet Another Reverse Proxy) for the Fleet platform. Handles authentication, authorization, token exchange, and dynamic service routing.

## Features

- **Multi-scheme Authentication**: Azure AD B2C (users) and Azure AD (M2M services)
- **Token Exchange**: Replaces incoming tokens with internal service tokens
- **Dynamic Service Routing**: Routes requests to downstream services based on URL path
- **Rate Limiting**: Per-client IP rate limiting protection
- **Circuit Breaker**: Resilient token acquisition with Polly
- **Correlation ID Propagation**: Distributed tracing support
- **Health Checks**: Liveness and readiness probes with IdP connectivity check

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- Azure AD B2C tenant (for user authentication)
- Azure AD app registrations (for M2M authentication)

### Configuration

Key configuration sections in `appsettings.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id>",
    "ClientSecret": "<client-secret>"
  },
  "AzureAdB2C": {
    "Instance": "<b2c-instance>",
    "Domain": "<b2c-domain>",
    "ClientId": "<client-id>",
    "SignUpSignInPolicyId": "<policy-id>"
  },
  "DynamicRouting": {
    "AllowedServices": [
      "fleet-account-service",
      "fleet-onboarding-service",
      "fleet-company-service"
    ]
  },
  "RateLimiting": {
    "PermitLimit": 100,
    "WindowSeconds": 60
  }
}
```

### Running Locally

```bash
dotnet run --project Erus.Faas.ApiGateway
```

## Build and Test

```bash
# Build
dotnet build

# Run unit tests
dotnet test Erus.Faas.ApiGateway.Tests

# Run integration tests
dotnet test Erus.Faas.ApiGateway.IntegrationTests
```

## Kubernetes Deployment

### Health Check Endpoints

| Endpoint | Purpose | Used For |
|----------|---------|----------|
| `/health/live` | Liveness check | K8s liveness probe |
| `/health/ready` | Readiness check (includes IdP connectivity) | K8s readiness probe |

### Probe Configuration

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: fleet-apigateway-service
spec:
  template:
    spec:
      containers:
        - name: apigateway
          image: fleet-apigateway-service:latest
          ports:
            - containerPort: 8080
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 15
            timeoutSeconds: 5
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          resources:
            requests:
              memory: "256Mi"
              cpu: "100m"
            limits:
              memory: "512Mi"
              cpu: "500m"
          env:
            - name: ASPNETCORE_URLS
              value: "http://0.0.0.0:8080"
```

### Resource Recommendations

| Environment | CPU Request | CPU Limit | Memory Request | Memory Limit |
|-------------|-------------|-----------|----------------|--------------|
| Development | 100m | 500m | 256Mi | 512Mi |
| Production | 250m | 1000m | 512Mi | 1Gi |

### Network Policy Example

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: apigateway-network-policy
spec:
  podSelector:
    matchLabels:
      app: fleet-apigateway-service
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: ingress-nginx
      ports:
        - protocol: TCP
          port: 8080
  egress:
    # Allow DNS
    - to:
        - namespaceSelector: {}
      ports:
        - protocol: UDP
          port: 53
    # Allow downstream services
    - to:
        - podSelector:
            matchLabels:
              tier: backend
      ports:
        - protocol: TCP
          port: 8080
    # Allow Azure AD (for token acquisition)
    - to:
        - ipBlock:
            cidr: 0.0.0.0/0
      ports:
        - protocol: TCP
          port: 443
```

## Architecture

### Request Flow

1. Client sends request with Bearer token (B2C user or M2M service token)
2. Gateway validates token via Azure AD B2C or Azure AD
3. Authorization policies verify required scopes/roles
4. Token exchange replaces incoming token with internal service token
5. Request is proxied to downstream service with:
   - Internal Bearer token
   - `X-User-Id` header (for user tokens)
   - `X-Client-Id` header (for all tokens)
   - `X-Correlation-Id` header

### Security Features

- **CORS**: Configurable allowed origins
- **Rate Limiting**: 100 requests/minute per client IP (configurable)
- **Service Allowlist**: Only configured services can be routed to
- **Security Headers**: X-Content-Type-Options, HSTS (production)
- **Circuit Breaker**: Protects against IdP failures

## Contributing

1. Create a feature branch from `main`
2. Make changes with tests
3. Submit a pull request
4. Ensure CI pipeline passes (tests + security scanning)
