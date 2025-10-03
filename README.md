# Basket.Filter

A smart basket filtering system that validates shopping baskets against merchant catalogs and eligibility rules.

---

## 🚀 Live API

**Base URL:** `https://basket-filter-api-7mnj62hzza-el.a.run.app`

**Swagger Docs:** [https://basket-filter-api-7mnj62hzza-el.a.run.app/swagger](https://basket-filter-api-7mnj62hzza-el.a.run.app/swagger)

---

## ✨ Features

- **Smart Filtering** - Validates basket items against merchant catalogs and eligibility rules
  
- **Merchant Onboarding** - Easy merchant and catalog management
  
- **Caching System** - Built-in Redis cache for optimized performance
  
- **Cloud Deployed** - Hosted on Google Cloud Run with Firestore database

---

## 📋 API Endpoints

### Basket Filter
- `POST /api/BasketFilter/filter` - Filter and validate basket items

### Catalog Management
- `POST /api/Catalog/upload` - Upload catalog items
- `GET /api/Catalog/item` - Get catalog item details
- `DELETE /api/Catalog` - Clear catalog data

### Merchant Management
- `POST /api/Merchant/onboard` - Onboard new merchant
- `GET /api/Merchant/template` - Get merchant template

### Cache Management
- `GET /api/cache/stats` - View cache statistics
- `POST /api/cache/clear` - Clear cache data

---

## 🔧 Tech Stack

- **.NET 8** - Web API framework
- **Firestore** - NoSQL database
- **Redis** - Caching layer
- **Google Cloud Run** - Container hosting
- **Swagger** - API documentation

---

## 📦 Prerequisites

Before you begin, ensure you have:

- .NET 8 SDK
- Google Cloud account
- Firebase/Firestore project
- Redis instance (local or cloud)
- Postman (for testing)

---

## 🖥️ Local Development Setup

git clone <your-repo-url>

cd basket-filter-api

Download service account key from Firebase Console

Place it in project root as 'serviceAccountKey.json'

Set environment variable:

export GOOGLE_APPLICATION_CREDENTIALS="./serviceAccountKey.json"

Update appsettings.json:

{
"Redis": {
"ConnectionString": "localhost:6379"
}
}

dotnet restore

dotnet build

dotnet run

API will be available at: `https://localhost:7xxx`


## ☁️ Cloud Deployment (Google Cloud Run)

curl https://sdk.cloud.google.com | bash

exec -l $SHELL

gcloud auth login

gcloud config set project YOUR_PROJECT_ID

docker build -t gcr.io/YOUR_PROJECT_ID/basket-filter-api .

docker push gcr.io/YOUR_PROJECT_ID/basket-filter-api

gcloud run deploy basket-filter-api 

--image gcr.io/YOUR_PROJECT_ID/basket-filter-api 

--platform managed 

--region us-east1 

--allow-unauthenticated 

--set-env-vars="GOOGLE_APPLICATION_CREDENTIALS=/app/serviceAccountKey.json"

Create Firestore database in Google Cloud Console

Add service account key to container

Set appropriate IAM permissions

gcloud redis instances create basket-cache --size=1 --region=us-east1

gcloud redis instances describe basket-cache --region=us-east1

## 🔐 Environment Variables

**Required Variables:**

GOOGLE_APPLICATION_CREDENTIALS=/path/to/serviceAccountKey.json
REDIS_CONNECTION_STRING=your-redis-host:6379
ASPNETCORE_ENVIRONMENT=Production

**Optional Variables:**

FIRESTORE_PROJECT_ID=your-project-id
CACHE_EXPIRATION_MINUTES=30

## 🧪 Testing Setup

Open Postman
Import → Upload the collection JSON
Set environment variable: baseUrl = https://basket-filter-api-7mnj62hzza-el.a.run.app

Run /api/Merchant/onboard first
Upload catalog via /api/Catalog/upload
Test filtering with /api/BasketFilter/filter
Check cache stats at /api/cache/stats


---

## 🔧 Troubleshooting

**Issue: Firestore connection fails**
- ✓ Verify service account key path
- ✓ Check IAM permissions
- ✓ Ensure Firestore API is enabled

**Issue: Redis connection timeout**
- ✓ Check Redis instance is running
- ✓ Verify connection string
- ✓ Check network/firewall rules

**Issue: Cloud Run deployment fails**
- ✓ Check Docker image builds locally
- ✓ Verify all environment variables
- ✓ Review Cloud Run logs: gcloud run logs read

---

## 📞 Support

For issues or questions:
- **Swagger Documentation:** `/swagger`
- **Cloud Run Logs:** `gcloud run logs read`
- **Firestore Console:** Firebase Console

---

**Built by Filterix Team for WGS Hackathon 2025**
