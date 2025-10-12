# Implementation Plan - GrapheneTrace Sensore System

## 1. Concrete Measurable Goals

### Core Data Processing
- **G1.1**: Process 32x32 pressure matrices (1,024 data points) with values 1-255 in under 50ms per frame
- **G1.2**: Achieve zero data loss with guaranteed persistence for all incoming sensor readings
- **G1.3**: Support concurrent processing of at least 100 simultaneous sensor feeds
- **G1.4**: Maintain sub-second latency from data ingestion to alert generation

### User Management & Access Control
- **G2.1**: Implement three-tier authentication system (Patient, Clinician, Admin) with JWT-based authorization
- **G2.2**: Enable clinicians to access all assigned patients' data within 2 seconds
- **G2.3**: Admin account creation for other users completed in under 3 database transactions
- **G2.4**: Maintain audit trail for all data access with user, timestamp, and action type

### Alert System
- **G3.1**: Detect high-pressure regions (>10 contiguous pixels above threshold) within 100ms
- **G3.2**: Generate user alerts within 500ms of threshold breach detection
- **G3.3**: Flag alert periods in database with millisecond-precision timestamps
- **G3.4**: Support configurable alert thresholds per patient profile

### Metrics Extraction
- **G4.1**: Calculate Peak Pressure Index excluding regions <10 pixels in O(n) time
- **G4.2**: Compute Contact Area % with ±1% accuracy
- **G4.3**: Generate time-series metrics for periods: 1h, 6h, 24h, 7d, 30d
- **G4.4**: Cache calculated metrics with 5-minute TTL for performance

### Reporting & Visualization
- **G5.1**: Generate comparative reports (day-over-day, hour-over-hour) in under 2 seconds
- **G5.2**: Web visualization (Blazor) updates at minimum 10 FPS for real-time monitoring
- **G5.3**: Support export of reports in CSV and JSON formats
- **G5.4**: Maintain 6-month rolling window of historical data for comparisons

### Communication System
- **G6.1**: Associate comments with timestamps accurate to 10ms
- **G6.2**: Enable threaded clinician-patient discussions with message ordering
- **G6.3**: Notification system for new comments within 30 seconds
- **G6.4**: Support markdown formatting in comments for clinical notes

## 2. Overall Implementation Architecture

### System Architecture Pattern: **Blazor Server + Background Services (Modular Monolith)**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                Browser (Blazor)                             │
│     Razor Components  │ MudBlazor UI  │ LiveCharts  │ Auth (Cookies/JWT)    │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
                           SignalR (real-time updates)
                                      │
┌─────────────────────────────────────▼───────────────────────────────────────┐
│                          ASP.NET Core Blazor Server                         │
│  • Razor Pages/Components  • Controllers (exports)  • AuthN/AuthZ (Identity)│
│  • SignalR Hubs            • Minimal APIs (optional)                        │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
                   ┌──────────────────┼──────────────────────────┐
                   │                  │                          │
        ┌──────────▼──────────┐  ┌────▼──────────────┐  ┌────────▼─────────┐
        │  Ingestion Workers  │  │ Processing Engine │  │  Caching Layer   │
        │ (Hosted Services)   │  │  (Metrics/Alerts) │  │ (Memory/Redis)   │
        │ • Mock Sensors      │  │ • Metric Calc     │  │ • Hot data cache │
        │ • CSV Import        │  │ • Alert Engine    │  │                  │
        │ • Validation        │  │ • Persistence     │  │                  │
        └─────────────────────┘  └────┬──────────────┘  └──────────────────┘
                                      │
                            ┌─────────▼─────────┐
                            │  PostgreSQL 16    │
                            │  • Time-series    │
                            │  • Users & Roles  │
                            │  • Alerts/Comments│
                            └───────────────────┘
```

### Data Flow Architecture

1. **Ingestion Phase**
   - Hosted services simulate/ingest sensor streams (unique device IDs)
   - Data validated against 32x32 matrix constraints
   - Tagged with user ID, timestamp, and session ID
   - Enqueued to in-process channels for processing

2. **Processing Pipeline**
   - Background services consume channels for parallel metric computation
   - Real-time alert evaluation against user-specific thresholds
   - Batch writes to PostgreSQL every 100ms for efficiency
   - SignalR broadcasts updated metrics/alerts to connected Blazor clients

3. **Storage Strategy**
   - **Hot Storage**: Last 24 hours in memory/Redis cache
   - **Warm Storage**: Last 30 days in PostgreSQL with indexes
   - **Cold Storage**: >30 days in compressed format

4. **Query & UI Optimization**
   - Materialized views for common metric aggregations
   - Pre-computed rollups for time-series data
   - Denormalized read models for report generation
   - UI virtualization for large tables and lazy-loading for charts

### Database Schema Design

```sql
-- Core Tables
users (id, type, email, created_at, clinician_id)
devices (id, user_id, device_identifier, last_seen)
pressure_readings (id, device_id, timestamp, raw_data, processed_data)
pressure_metrics (reading_id, peak_pressure, contact_area, timestamp)

-- Alert System
alert_rules (id, user_id, threshold_type, threshold_value, enabled)
alert_history (id, reading_id, rule_id, triggered_at, acknowledged_at)

-- Communication
comments (id, user_id, reading_id, timestamp, content, parent_id)
notifications (id, user_id, type, payload, created_at, read_at)

-- Indexing Strategy
- Composite index on (device_id, timestamp) for time-series queries
- Partial index on alerts WHERE acknowledged_at IS NULL
- GIN index on comments.content for full-text search
```

## 3. Namespace Structure

### Root: `GrapheneTrace`

```
GrapheneTrace/
│
├── GrapheneTrace.Core/                 # Domain models and interfaces
│   ├── Models/
│   │   ├── PressureMap.cs             # [Existing] Core pressure matrix model
│   │   ├── User.cs                    # User entity with role management
│   │   ├── Device.cs                  # Sensor device representation
│   │   └── Alert.cs                   # Alert configuration and history
│   ├── Interfaces/
│   │   ├── IDataIngestion.cs          # Contract for data intake
│   │   ├── IMetricCalculator.cs       # Metric computation interface
│   │   └── IAlertEngine.cs            # Alert evaluation interface
│   └── Enums/
│       ├── UserRole.cs                # Patient, Clinician, Admin
│       └── AlertSeverity.cs           # Low, Medium, High, Critical
│
├── GrapheneTrace.Infrastructure/       # External dependencies & data access
│   ├── Database/
│   │   ├── GrapheneContext.cs         # EF Core DbContext
│   │   ├── Repositories/
│   │   │   ├── UserRepository.cs
│   │   │   ├── PressureReadingRepository.cs
│   │   │   └── CommentRepository.cs
│   │   └── Migrations/                # Database version control
│   ├── Messaging/
│   │   ├── MessageBus.cs              # In-process event bus
│   │   └── EventHandlers/             # Domain event processors
│   └── Caching/
│       └── MemoryCacheService.cs      # IMemoryCache/Redis wrapper
│
├── GrapheneTrace.Services/             # Business and background processing
│   ├── Ingestion/
│   │   ├── MockSensorService.cs       # Generates test data streams
│   │   ├── CsvIngestionService.cs     # Processes CSV imports
│   │   └── DataValidator.cs           # Input validation rules
│   ├── Processing/
│   │   ├── MetricCalculationService.cs # Computes all metrics
│   │   ├── AlertEvaluationService.cs   # Checks alert conditions
│   │   └── PressureAnalysisService.cs  # Advanced analysis algorithms
│   ├── Reporting/
│   │   ├── ReportGenerationService.cs  # Creates comparative reports
│   │   └── TimeSeriesAggregator.cs     # Time-based rollups
│   └── Communication/
│       ├── CommentService.cs           # Comment thread management
│       └── NotificationService.cs      # Real-time notifications
│
├── GrapheneTrace.Web/                  # Blazor Server host (UI + API endpoints)
│   ├── Pages/                          # Razor pages and routes
│   ├── Components/                     # Reusable Blazor components
│   ├── Hubs/                           # SignalR hubs for real-time updates
│   ├── Controllers/                    # Export endpoints, optional APIs
│   ├── wwwroot/                        # Static assets
│   ├── App.razor
│   └── _Imports.razor
│
└── GrapheneTrace.Tests/                # Test projects
    ├── Unit/                           # Unit tests per namespace
    ├── Integration/                    # Database & API tests
    └── Performance/                    # Load & stress tests
```

### Namespace Responsibilities

- **Core**: Pure domain logic, no external dependencies, immutable models
- **Infrastructure**: Database access, caching, external service integration
- **Services**: Business rules, orchestration, background processing
- **Web**: Blazor UI, SignalR hubs, controllers for exports/auth, request mapping
- **Tests**: Comprehensive test coverage including performance benchmarks

## 4. Required Dependencies

### NuGet Packages

```xml
<!-- Database & ORM -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.4" />
<PackageReference Include="EFCore.NamingConventions" Version="8.0.3" />
<PackageReference Include="EntityFrameworkCore.Triggered" Version="3.2.2" />

<!-- Authentication & Security -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.8" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />

<!-- Background Processing -->
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.14" />
<PackageReference Include="Hangfire.PostgreSql" Version="1.20.9" />

<!-- Caching -->
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.8" />

<!-- Messaging & Events -->
<PackageReference Include="MediatR" Version="12.3.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />

<!-- Blazor UI & Real-time -->
<PackageReference Include="MudBlazor" />
<PackageReference Include="LiveChartsCore.SkiaSharpView.Blazor" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />

<!-- API Development -->
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.7.0" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
<PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.0.1" />

<!-- Testing -->
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Testcontainers.PostgreSql" Version="3.9.0" />
<PackageReference Include="NBomber" Version="5.6.1" />

<!-- Utilities -->
<PackageReference Include="Polly" Version="8.4.1" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.2" />
<PackageReference Include="Serilog.Sinks.PostgreSQL" Version="2.3.0" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

### Docker Dependencies

```yaml
# docker-compose.yml services
- PostgreSQL 16 (Primary database)
- PostgreSQL 16 (Read replica)
- Redis 7 (Caching layer)
- Prometheus (Metrics collection)
- Grafana (Metrics visualization)
```

### Development Tools

- **Docker Desktop**: Container orchestration
- **pgAdmin**: PostgreSQL management
- **Postman/Insomnia**: API testing
- **Playwright**: UI testing for Blazor
- **k6**: Load testing framework

## Implementation Priority

### Phase 1: Foundation (Week 1-2)
1. Set up PostgreSQL with Docker
2. Implement Core domain models
3. Create mock sensor service and ingestion pipeline (hosted services)
4. Create Blazor Server project with base layout and navigation

### Phase 2: Real-Time Processing & UI (Week 2-3)
1. Metric calculation service
2. Alert evaluation engine
3. SignalR hub and client integration
4. Real-time Blazor components (heat map, metrics panel) at 10+ FPS

### Phase 3: User Management (Week 3-4)
1. Identity-based authentication (Patients, Clinicians, Admins)
2. Role-based access control and protected routes
3. Data access pages (patients, devices, sessions)
4. Comment system (threaded) with real-time notifications

### Phase 4: Optimization & Reporting (Week 4-5)
1. Caching layer implementation (memory/Redis)
2. Performance testing and tuning (p95 <100ms processing)
3. Report generation service and exports (CSV/JSON)
4. Enhanced web dashboard (charts, filters, virtualization)

### Phase 5: Production Readiness (Week 5-6)
1. Comprehensive testing suite (unit, integration, UI)
2. Monitoring and logging
3. Documentation
4. Deployment configuration

## Success Metrics

- **Performance**: <100ms p95 latency for data processing
- **Reliability**: 99.9% uptime, zero data loss
- **Scalability**: Support 1000+ concurrent connections
- **Usability**: Alert generation within 1 second of condition detection
- **Maintainability**: >80% test coverage, <10% code duplication