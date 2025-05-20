# Codecon.Caching

A demo project for demonstrating ASP.NET Core caching techniques using a products API.

## Overview

This project demonstrates different caching strategies in a web application:
- **No Caching (v1)** - Direct database queries on each request
- **Response Caching (v2)** - In-memory caching of query results
- **Output Caching (v3)** - Server-side caching of entire API responses
- **ETag Caching (v4)** - Using HTTP ETag headers for client validation

The demo includes:
- Backend API (.NET 9) with four caching implementation versions
- Simple frontend to visualize and compare caching performance
- 850,000 product entries to demonstrate performance differences

## Project Structure

- `src/Codecon.Api/` - Backend API with minimal API approach and vertical slice architecture
- `src/webapp/` - Frontend with vanilla JavaScript and Bootstrap
- Docker setup for easy PostgreSQL database deployment

## Prerequisites

- .NET 9 SDK
- Docker and Docker Compose (for the database)
- A web browser with developer tools
- Basic HTTP server (Python's built-in server works fine)

## Running the Application

### Step 1: Start the Database

```bash
# Start PostgreSQL in Docker
docker-compose up -d
```

### Step 2: Run the Backend API

```bash
# Navigate to the API project
cd src/Codecon.Api

# Run the API (it will be available at http://localhost:5000)
dotnet run
```

The API automatically seeds the database with product data on first run. This process generates 850,000 products and might take several minutes.

### Step 3: Serve the Frontend

You can use any simple HTTP server to serve the frontend files. For example, with Python:

```bash
# Navigate to the webapp directory
cd src/webapp

# Start a simple HTTP server on port 8080
python -m http.server 8080
```

Or with Python 3:
```bash
python3 -m http.server 8080
```

### Step 4: Access the Application

Open your browser and navigate to:
```
http://localhost:8080
```

## Using the Demo

1. The application starts in the "No Caching" tab
2. Enter a category in the search field (e.g., "Electronics", "Books", "Clothing")
3. Click the search button or press Enter
4. Note the request time displayed in the top right
5. Switch between different caching tabs to compare performance:
   - **No Caching (v1)** - Each request goes directly to the database
   - **Response Cache (v2)** - Server caches query results in memory for 20 seconds
   - **Output Cache (v3)** - Server caches entire HTTP responses for 20 seconds
   - **ETag Caching (v4)** - Uses HTTP ETags to avoid sending unchanged data

## Available Product Categories

The database is seeded with products in the following categories:
- Electronics
- Clothing
- Home & Kitchen
- Books
- Sports
- Toys
- Beauty
- Automotive
- Health
- Garden
- Furniture
- Jewelry
- Office
- Food
- Tools
- Baby
- Pet Supplies

## API Endpoints

- `GET /api/products/v1?category={categoryName}` - Get products without caching
- `GET /api/products/v2?category={categoryName}` - Get products with response caching (memory cache)
- `GET /api/products/v3?category={categoryName}` - Get products with output caching
- `GET /api/products/v4?category={categoryName}` - Get products with ETag caching

## Troubleshooting

- **CORS Errors**: Make sure the backend API is running with CORS properly configured. The `UseCors` middleware should be placed before `UseOutputCache` in the pipeline.
- **Database Connection**: If the database connection fails, check that PostgreSQL is running in Docker.
- **API Connection**: Verify that the frontend is using the correct API URL (configured in `src/webapp/js/app.js`).
- **First Run Time**: Initial database seeding of 850,000 products may take several minutes. Check API logs for progress. 