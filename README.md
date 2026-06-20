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

## ⚠️ Required: Download the AI Model Files (do this first)

The Python AI service (`PythonService`) loads a fine-tuned T5 summarization model from `PythonService/Model/`. These model weights are **not included in the Git repo** (too large / gitignored), so you must download and place them manually **before** running `docker-compose up --build` — otherwise the `python-ai` container will crash-loop with an error like:
```
OSError: Error no file named model.safetensors, or pytorch_model.bin, found in directory /app/Model.
```

### Steps:

1. **Download and extract this Drive folder:**
   `drive-download-20260525T120708Z-3-001` (shared internally — ask a teammate if you don't have access)

2. **Also download this individual file:**
   👉 https://drive.google.com/file/d/1jSR1g4iO4Dyqi4s_83spCai7fNB-0rTD/view?usp=sharing

3. **Extract / move everything directly into `PythonService/Model/`** so the folder looks like this (files directly inside `Model/`, not in a subfolder):
```
PythonService/Model/
├── config.json
├── generation_config.json
├── model.safetensors        ← from the individual file link above
├── special_tokens_map.json
├── spiece.model
└── tokenizer_config.json
```

4. **(Linux/Mac) Fix ownership/permissions if needed:**
```bash
sudo chown -R $USER:$USER PythonService/Model
chmod -R u+rw PythonService/Model
```

5. **Sanity check before building** — confirm the weights file isn't empty or a tiny placeholder:
```bash
ls -la PythonService/Model/
file PythonService/Model/model.safetensors
```
This should report a large binary file (hundreds of MB), not plain text.

Once the files are in place, continue with the steps below as normal.

---

## 🚀 Quick Start

### Clone the Repository
```bash
git clone https://github.com/AmiraMohamedM/GraduationProject-Backend.git
cd GraduationProject-Backend
```

### Start All Services
```bash
docker-compose up --build
```
### now you can use APIs

This command will:
- ✅ Build the .NET API Docker image
- ✅ Build the Python AI service Docker image
- ✅ Start PostgreSQL 18 database
- ✅ Start the ASP.NET Core Web API
- ✅ Start the Python FastAPI AI/ML service
- ✅ Apply all EF Core migrations automatically
- ✅ Make the API accessible at `http://localhost:5000/swagger/index.html`
- ✅ Make the Python AI service accessible at `http://localhost:8000/docs`


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