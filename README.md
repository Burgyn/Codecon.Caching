# Codecon.Caching

A demo project for demonstrating ASP.NET Core caching techniques using a products API.

## Overview

This project demonstrates a simple products API with:
- 150,000 product entries generated with AutoBogus
- Search by category (intentionally without an index to demonstrate caching benefits)
- API versioning to allow multiple caching implementations for comparison
- Docker Compose setup for easy deployment with SQL Server

## Running the Application

### Using Docker Compose

```bash
# Start the SQL Server and API containers
docker-compose up -d

# Access the API at http://localhost:5000
```

### Running Locally

```bash
# Start only the SQL Server container
docker-compose up -d mssql

# Run the API locally
cd src/Codecon.Api
dotnet run
```

## API Endpoints

- `GET /api/products/v1?category={categoryName}` - Get products by category (V1 - No caching)

The initial database seed will create 150,000 product records with various categories like:
- Electronics
- Clothing
- Home & Kitchen
- Books
- Sports
- Toys
- and more...

## Project Structure

- The API uses minimal API approach with vertical slice architecture
- Database seeding is done automatically on first run
- Docker setup includes both SQL Server and the API for easy testing

## Future Versions

This project is designed for demonstrating various caching approaches in future versions:
- V1: No caching (current)
- V2: Memory caching (planned)
- V3: Distributed caching (planned)
- V4: Output caching (planned) 