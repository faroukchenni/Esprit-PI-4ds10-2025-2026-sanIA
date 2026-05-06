# Sania AgriSmart Platform

A production-style smart agriculture dashboard with role-based access control, real-time sensor monitoring, and livestock management.

## Tech Stack
- **Backend:** FastAPI, SQLAlchemy, PostgreSQL, JWT Auth
- **Frontend:** React, TypeScript, Tailwind CSS, Recharts, Leaflet
- **DevOps:** Docker, Docker Compose

## Getting Started

### Prerequisites
- Docker & Docker Compose

### Run the application
```bash
docker-compose up --build
```

## Credentials & Roles
- **Cooperative Admin:** `admin@agrismart.tn` / `Admin123!`
  - Can manage all farms, view global analytics, and register new cooperatives.
- **Farmer (Mohamed):** `farmer@agrismart.tn` / `Farmer123!`
  - Manages his own fields, sensors, livestock, and receives direct IA alerts.

## Key Features Implemented
- 🛰️ **Geospatial Intelligence**: NDVI mapping and spatial polygon rendering.
- 🐮 **Livestock Blockchain**: Full medical history, vaccinations, and treatment tracking.
- 🌍 **IA Disease Detection**: Image analysis logs with confidence scoring.
- 💧 **Precision Irrigation**: Real-time telemetry monitoring and water usage tracking.
- 🔔 **Smarter Alerting**: Priority-based notifications for critical farm events.

## Deployment
1. Run `docker-compose up --build`.
2. Populate data: `docker exec -it sania_backend python scripts/seed.py`.
3. Access UI at `http://localhost:3000`.
