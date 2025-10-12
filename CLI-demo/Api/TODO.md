# API Layer - TODO

## Overview
Web API endpoints with authentication, authorization, and request/response handling.

## Controllers/ - TODO
- [ ] **AuthController.cs** - JWT authentication (login, refresh, logout)
- [ ] **UsersController.cs** - User management (CRUD, role assignment)
- [ ] **DataController.cs** - Pressure data queries with time range filtering
- [ ] **AlertController.cs** - Alert configuration and history management
- [ ] **CommentController.cs** - Comment CRUD operations with threading
- [ ] **ReportsController.cs** - Report generation and export endpoints
- [ ] **DevicesController.cs** - Device registration and status monitoring
- [ ] **MetricsController.cs** - Real-time and historical metrics access

## Middleware/ - TODO
- [ ] **AuthenticationMiddleware.cs** - JWT token validation and user context
- [ ] **ExceptionHandlingMiddleware.cs** - Global error handling and logging
- [ ] **RequestLoggingMiddleware.cs** - API request/response logging
- [ ] **RateLimitingMiddleware.cs** - Prevent API abuse and DOS attacks
- [ ] **CorsMiddleware.cs** - Cross-origin request handling

## DTOs/ - TODO
- [ ] **UserDto.cs** - User data transfer objects (create, update, response)
- [ ] **PressureDataDto.cs** - Pressure reading DTOs with time ranges
- [ ] **AlertDto.cs** - Alert configuration and history DTOs
- [ ] **CommentDto.cs** - Comment thread DTOs with user information
- [ ] **ReportDto.cs** - Report generation request/response DTOs
- [ ] **MetricDto.cs** - Metric snapshot and trend DTOs
- [ ] **AuthDto.cs** - Authentication request/response DTOs

## API Features
- RESTful endpoints following OpenAPI standards
- JWT-based authentication with refresh tokens
- Role-based authorization (Patient, Clinician, Admin)
- Request validation using FluentValidation
- Auto-generated Swagger documentation
- Standardized error responses
- Pagination for large datasets
- Real-time data via SignalR (future enhancement)