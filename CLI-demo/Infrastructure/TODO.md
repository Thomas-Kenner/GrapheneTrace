# Infrastructure Layer - TODO

## Overview
External dependencies and data access layer. Implements repository patterns and handles database operations.

## Database/ - TODO
- [ ] **GrapheneContext.cs** - EF Core DbContext with all entity configurations
- [ ] **DatabaseSeeder.cs** - Initial data seeding for development
- [ ] **ConnectionFactory.cs** - Database connection management with retry logic

### Database/Repositories/ - TODO
- [ ] **UserRepository.cs** - User CRUD operations with role-based queries
- [ ] **PressureReadingRepository.cs** - Time-series data operations with bulk inserts
- [ ] **CommentRepository.cs** - Comment threading and pagination
- [ ] **AlertRepository.cs** - Alert history and configuration management
- [ ] **DeviceRepository.cs** - Device registration and status tracking
- [ ] **MetricRepository.cs** - Pre-computed metric storage and retrieval

### Database/Migrations/ - TODO
- [ ] Initial migration for core tables
- [ ] User management tables migration
- [ ] Time-series optimizations migration
- [ ] Indexing strategy migration

## Messaging/ - TODO
- [ ] **MessageBus.cs** - In-process event bus using MediatR
- [ ] **DomainEvent.cs** - Base class for domain events
- [ ] **PressureDataReceivedEvent.cs** - Event for new pressure data
- [ ] **AlertTriggeredEvent.cs** - Event for alert conditions

### Messaging/EventHandlers/ - TODO
- [ ] **PressureDataEventHandler.cs** - Process incoming pressure data
- [ ] **AlertEventHandler.cs** - Handle alert notifications
- [ ] **MetricCalculationEventHandler.cs** - Trigger metric calculations
- [ ] **CommentEventHandler.cs** - Handle comment notifications

## Caching/ - TODO
- [ ] **MemoryCacheService.cs** - IMemoryCache wrapper with typed access
- [ ] **CacheKeys.cs** - Centralized cache key management
- [ ] **CacheConfiguration.cs** - TTL and eviction policies

## Configuration
- PostgreSQL connection strings
- Cache expiration policies
- Event bus configuration
- Migration settings