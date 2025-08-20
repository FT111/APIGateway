# API Gateway CRUD Endpoints

This document describes the CRUD endpoints that have been implemented for the API Gateway entities.

## Authentication

All endpoints require JWT authentication. First, obtain a token from the auth endpoint:

```http
POST /auth/token
Content-Type: application/json

{
  "username": "your_username",
  "password": "your_password"
}
```

Include the token in subsequent requests:
```http
Authorization: Bearer <your_jwt_token>
```

## Entity CRUD Endpoints

### Users
- `GET /users` - Get all users (passwords hidden)
- `GET /users/{id}` - Get user by ID
- `POST /users` - Create new user
- `PUT /users/{id}` - Update user
- `DELETE /users/{id}` - Delete user

### Endpoints (Gateway Route Configuration)
- `GET /endpoints` - Get all endpoints
- `GET /endpoints/{id}` - Get endpoint by ID
- `POST /endpoints` - Create new endpoint
- `PUT /endpoints/{id}` - Update endpoint
- `DELETE /endpoints/{id}` - Delete endpoint

### Targets (Backend Services)
- `GET /targets` - Get all targets
- `GET /targets/{id}` - Get target by ID
- `POST /targets` - Create new target
- `PUT /targets/{id}` - Update target
- `DELETE /targets/{id}` - Delete target

### Pipes (Processing Pipelines)
- `GET /pipes` - Get all pipes
- `GET /pipes/{id}` - Get pipe by ID
- `POST /pipes` - Create new pipe
- `PUT /pipes/{id}` - Update pipe
- `DELETE /pipes/{id}` - Delete pipe

### Events (System Events)
- `GET /events` - Get all events (with filtering options)
- `GET /events/{id}` - Get event by ID
- `GET /events/endpoint/{endpointId}` - Get events for specific endpoint
- `POST /events` - Create new event
- `PUT /events/{id}` - Update event
- `PUT /events/{id}/dismiss` - Dismiss event
- `DELETE /events/{id}` - Delete event

### Plugin Data
- `GET /plugin-data` - Get all plugin data
- `GET /plugin-data/{namespace}/{key}` - Get specific plugin data
- `GET /plugin-data/namespace/{namespace}` - Get all data for namespace
- `POST /plugin-data` - Create new plugin data
- `PUT /plugin-data/{namespace}/{key}` - Update plugin data
- `DELETE /plugin-data/{namespace}/{key}` - Delete plugin data

### Plugin Configs
- `GET /plugin-configs` - Get all plugin configs
- `GET /plugin-configs/pipe/{pipeId}` - Get configs for specific pipe
- `GET /plugin-configs/namespace/{namespace}` - Get configs for namespace
- `GET /plugin-configs/{namespace}/{key}` - Get specific config
- `POST /plugin-configs` - Create new config
- `PUT /plugin-configs/{namespace}/{key}` - Update config
- `DELETE /plugin-configs/{namespace}/{key}` - Delete config

### Pipe Services
- `GET /pipe-services` - Get all pipe services
- `GET /pipe-services/pipe/{pipeId}` - Get services for specific pipe
- `POST /pipe-services` - Create new pipe service
- `PUT /pipe-services/{pipeId}/{pluginTitle}/{serviceTitle}` - Update service
- `DELETE /pipe-services/{pipeId}/{pluginTitle}/{serviceTitle}` - Delete service

### Requests (Request Logs)
- `GET /requests` - Get all requests (with pagination and filtering)
- `GET /requests/{id}` - Get request by ID
- `GET /requests/endpoint/{endpointId}` - Get requests for endpoint
- `POST /requests` - Create new request log
- `DELETE /requests/{id}` - Delete request
- `DELETE /requests/cleanup` - Cleanup old requests

## Example Usage

### Create a new target:
```http
POST /targets
Authorization: Bearer <token>
Content-Type: application/json

{
  "schema": "https",
  "host": "api.example.com",
  "basePath": "/v1",
  "fallback": false
}
```

### Create an endpoint that routes to the target:
```http
POST /endpoints
Authorization: Bearer <token>
Content-Type: application/json

{
  "path": "/api/*",
  "targetPathPrefix": "/v1",
  "targetId": "target-guid-here",
  "pipeId": "pipe-guid-here"
}
```

### Get events with filtering:
```http
GET /events?isDismissed=false&isWarning=true&metaType=error
Authorization: Bearer <token>
```

### Get requests with pagination:
```http
GET /requests?page=1&pageSize=25&fromDate=2024-01-01T00:00:00Z
Authorization: Bearer <token>
```

## Response Formats

All endpoints return JSON responses with appropriate HTTP status codes:
- `200 OK` - Successful GET/PUT operations
- `201 Created` - Successful POST operations  
- `204 No Content` - Successful DELETE operations
- `400 Bad Request` - Invalid request data
- `401 Unauthorized` - Missing or invalid authentication
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server errors

Error responses include a descriptive message:
```json
{
  "error": "Resource not found",
  "details": "The requested endpoint with ID 'xyz' was not found"
}
```