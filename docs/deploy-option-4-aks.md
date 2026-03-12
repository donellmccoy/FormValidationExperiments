# Option 4 — Azure Kubernetes Service (AKS) Deployment Plan

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                     Azure Kubernetes Service                      │
│                                                                  │
│  ┌────────────────┐                                              │
│  │  Ingress        │                                              │
│  │  Controller     │                                              │
│  │  (nginx/AGIC)   │                                              │
│  └───┬────────┬───┘                                              │
│      │        │                                                  │
│      ▼        ▼                                                  │
│  ┌────────┐ ┌────────┐                                           │
│  │ Web    │ │ API    │     ┌──────────────┐  ┌──────────────┐    │
│  │ Deploy │ │ Deploy │────▶│  Azure SQL    │  │  Azure       │    │
│  │ (1-3)  │ │ (2-5)  │     │  (Entra Auth) │  │  Container   │    │
│  └────────┘ └────────┘     └──────────────┘  │  Registry     │    │
│                                              └──────────────┘    │
└──────────────────────────────────────────────────────────────────┘
```

---

## Prerequisites

- Azure subscription with Contributor access
- Azure CLI (`az`) with `aks` extension
- `kubectl` installed
- `helm` installed (for ingress controller)
- Docker Desktop installed locally
- .NET 10 SDK installed
- GitHub repo connected
- Azure SQL Database already provisioned

---

## Phase 1 — Provision AKS Cluster

### 1.1 Create AKS Cluster

```bash
az aks create \
  --name aks-ectsystem-prod \
  --resource-group rg-ectsystem-prod \
  --location centralus \
  --node-count 2 \
  --node-vm-size Standard_B2s \
  --enable-managed-identity \
  --enable-workload-identity \
  --enable-oidc-issuer \
  --network-plugin azure \
  --generate-ssh-keys
```

### 1.2 Create Azure Container Registry & Link to AKS

```bash
az acr create \
  --name acrectsystem \
  --resource-group rg-ectsystem-prod \
  --sku Basic

az aks update \
  --name aks-ectsystem-prod \
  --resource-group rg-ectsystem-prod \
  --attach-acr acrectsystem
```

### 1.3 Get Cluster Credentials

```bash
az aks get-credentials \
  --name aks-ectsystem-prod \
  --resource-group rg-ectsystem-prod
```

---

## Phase 2 — Create Dockerfiles

Use the same Dockerfiles from [Option 3](deploy-option-3-container-apps.md), Phase 1:
- `ECTSystem.Api/Dockerfile` — multi-stage .NET build
- `ECTSystem.Web/Dockerfile` — multi-stage build with nginx static serving
- `ECTSystem.Web/nginx.conf` — SPA fallback routing

### 2.1 Build and Push Images

```bash
az acr build --registry acrectsystem --image ectsystem-api:v1 -f ECTSystem.Api/Dockerfile .
az acr build --registry acrectsystem --image ectsystem-web:v1 -f ECTSystem.Web/Dockerfile .
```

---

## Phase 3 — Kubernetes Manifests

### 3.1 Namespace

Create `k8s/namespace.yaml`:

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: ectsystem
```

### 3.2 API Deployment

Create `k8s/api-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ectsystem-api
  namespace: ectsystem
  labels:
    app: ectsystem-api
spec:
  replicas: 2
  selector:
    matchLabels:
      app: ectsystem-api
  template:
    metadata:
      labels:
        app: ectsystem-api
    spec:
      containers:
        - name: api
          image: acrectsystem.azurecr.io/ectsystem-api:v1
          ports:
            - containerPort: 8080
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: ConnectionStrings__DefaultConnection
              valueFrom:
                secretKeyRef:
                  name: ectsystem-secrets
                  key: sql-connection-string
          resources:
            requests:
              cpu: "250m"
              memory: "512Mi"
            limits:
              cpu: "500m"
              memory: "1Gi"
          readinessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 5
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 15
            periodSeconds: 10
```

### 3.3 API Service

Create `k8s/api-service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: ectsystem-api-svc
  namespace: ectsystem
spec:
  selector:
    app: ectsystem-api
  ports:
    - port: 80
      targetPort: 8080
  type: ClusterIP
```

### 3.4 Web Deployment

Create `k8s/web-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ectsystem-web
  namespace: ectsystem
  labels:
    app: ectsystem-web
spec:
  replicas: 1
  selector:
    matchLabels:
      app: ectsystem-web
  template:
    metadata:
      labels:
        app: ectsystem-web
    spec:
      containers:
        - name: web
          image: acrectsystem.azurecr.io/ectsystem-web:v1
          ports:
            - containerPort: 80
          resources:
            requests:
              cpu: "100m"
              memory: "128Mi"
            limits:
              cpu: "250m"
              memory: "256Mi"
```

### 3.5 Web Service

Create `k8s/web-service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: ectsystem-web-svc
  namespace: ectsystem
spec:
  selector:
    app: ectsystem-web
  ports:
    - port: 80
      targetPort: 80
  type: ClusterIP
```

### 3.6 Secrets

```bash
kubectl create secret generic ectsystem-secrets \
  --namespace ectsystem \
  --from-literal=sql-connection-string="Server=sql-ect-dev-cus.database.windows.net;Database=ECT;Authentication=Active Directory Default;"
```

> **Production:** Use Azure Key Vault with the Secrets Store CSI Driver instead of Kubernetes secrets.

---

## Phase 4 — Ingress Controller

### 4.1 Install nginx Ingress Controller

```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

helm install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace \
  --set controller.replicaCount=2
```

### 4.2 Create Ingress Resource

Create `k8s/ingress.yaml`:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: ectsystem-ingress
  namespace: ectsystem
  annotations:
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/use-regex: "true"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - ectsystem.mil
        - api.ectsystem.mil
      secretName: ectsystem-tls
  rules:
    - host: ectsystem.mil
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: ectsystem-web-svc
                port:
                  number: 80
    - host: api.ectsystem.mil
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: ectsystem-api-svc
                port:
                  number: 80
```

---

## Phase 5 — Horizontal Pod Autoscaler

Create `k8s/api-hpa.yaml`:

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: ectsystem-api-hpa
  namespace: ectsystem
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: ectsystem-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
```

---

## Phase 6 — Deploy Everything

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/api-service.yaml
kubectl apply -f k8s/web-deployment.yaml
kubectl apply -f k8s/web-service.yaml
kubectl apply -f k8s/ingress.yaml
kubectl apply -f k8s/api-hpa.yaml
```

---

## Phase 7 — CI/CD with GitHub Actions

```yaml
name: Deploy to AKS

on:
  push:
    branches: [main]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Build API Image
        run: az acr build --registry acrectsystem --image ectsystem-api:${{ github.sha }} -f ECTSystem.Api/Dockerfile .

      - name: Build Web Image
        run: az acr build --registry acrectsystem --image ectsystem-web:${{ github.sha }} -f ECTSystem.Web/Dockerfile .

      - uses: azure/aks-set-context@v4
        with:
          cluster-name: aks-ectsystem-prod
          resource-group: rg-ectsystem-prod

      - name: Update Deployments
        run: |
          kubectl set image deployment/ectsystem-api \
            api=acrectsystem.azurecr.io/ectsystem-api:${{ github.sha }} \
            -n ectsystem
          kubectl set image deployment/ectsystem-web \
            web=acrectsystem.azurecr.io/ectsystem-web:${{ github.sha }} \
            -n ectsystem
          kubectl rollout status deployment/ectsystem-api -n ectsystem
          kubectl rollout status deployment/ectsystem-web -n ectsystem
```

---

## Rolling Updates & Rollback

Kubernetes deployments use rolling updates by default. Rollback is built-in:

```bash
# Check rollout history
kubectl rollout history deployment/ectsystem-api -n ectsystem

# Rollback to previous version
kubectl rollout undo deployment/ectsystem-api -n ectsystem

# Rollback to specific revision
kubectl rollout undo deployment/ectsystem-api -n ectsystem --to-revision=3
```

---

## Cost Estimate

| Resource | Configuration | Monthly Cost (approx.) |
|----------|--------------|----------------------|
| AKS Cluster | Free control plane | $0 |
| Node Pool | 2x Standard_B2s | ~$62 |
| Container Registry | Basic | ~$5 |
| Load Balancer | Standard | ~$18 |
| Azure SQL | S0 (10 DTU) | ~$15 |
| **Total** | | **~$100/month** |

> **Note:** AKS has a free control plane. Cost is primarily in worker nodes. Scale with additional nodes as load grows.

---

## Checklist

- [ ] AKS cluster provisioned
- [ ] ACR created and linked to AKS
- [ ] Dockerfiles created
- [ ] Images built and pushed
- [ ] Kubernetes namespace created
- [ ] Secrets configured (or Key Vault CSI driver installed)
- [ ] API and Web deployments applied
- [ ] Services created
- [ ] Ingress controller installed
- [ ] Ingress resource configured with TLS
- [ ] HPA configured
- [ ] CI/CD pipeline configured
- [ ] Health check endpoint implemented on API
- [ ] EF Core migrations applied
- [ ] Monitoring configured (Azure Monitor / Prometheus)
