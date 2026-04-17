# Graduation Project - Docker Setup Guide

Before running the project, make sure you have the following installed:

- **Docker Desktop** (version 20.10 or higher)
  - Windows/Mac: [Download Docker Desktop](https://www.docker.com/products/docker-desktop)
  - Linux: [Install Docker Engine](https://docs.docker.com/engine/install/)

Verify installation:
```bash
docker --version
docker-compose --version
```

---

## 🚀 Quick Start

### Clone the Repository
```bash
git clone https://github.com/maryamshaban-p/graduationProject.git
cd graduationProject
```

### Start All Services
```bash
docker-compose up --build
```
### now you can use APIs

This command will:
- ✅ Build the .NET API Docker image
- ✅ Start PostgreSQL 18 database
- ✅ Start the ASP.NET Core Web API
- ✅ Apply all EF Core migrations automatically
- ✅ Make the API accessible at `http://localhost:5132`


---

## 🛠️ Common Commands

### Start Services (detached mode)
```bash
docker-compose up -d
```

### Stop Services
```bash
docker-compose down
```

### Stop and Remove Everything (including database data)
```bash
docker-compose down -v
```

### Rebuild After Code Changes
```bash
docker-compose up --build
```

### View Logs
```bash
# All services
docker-compose logs -f

# API only
docker-compose logs -f api

# Database only
docker-compose logs -f postgres
```

### Restart a Specific Service
```bash
docker-compose restart api
```
