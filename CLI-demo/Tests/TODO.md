# Tests - TODO

## Overview
Comprehensive test coverage for all layers with unit, integration, and performance testing.

## Unit/ - TODO
- [ ] **Core.Tests/** - Test domain models, interfaces, and business logic
  - [ ] PressureMapTests.cs - Test matrix operations and validations
  - [ ] UserTests.cs - Test user role management
  - [ ] AlertTests.cs - Test alert condition evaluation
- [ ] **Services.Tests/** - Test business logic services
  - [ ] MetricCalculationServiceTests.cs - Test Peak Pressure and Contact Area calculations
  - [ ] AlertEvaluationServiceTests.cs - Test threshold detection
  - [ ] MockSensorServiceTests.cs - Test data generation
- [ ] **Infrastructure.Tests/** - Test data access and external dependencies
  - [ ] RepositoryTests.cs - Test database operations with in-memory DB
  - [ ] CacheServiceTests.cs - Test caching behavior
- [ ] **Api.Tests/** - Test controllers and middleware
  - [ ] AuthControllerTests.cs - Test authentication flows
  - [ ] DataControllerTests.cs - Test API endpoints

## Integration/ - TODO
- [ ] **DatabaseIntegrationTests.cs** - End-to-end database operations
- [ ] **ApiIntegrationTests.cs** - Full API workflow testing
- [ ] **ServiceIntegrationTests.cs** - Cross-service communication testing
- [ ] **EventHandlerIntegrationTests.cs** - Message bus event processing
- [ ] **DockerIntegrationTests.cs** - Test with real PostgreSQL container

## Performance/ - TODO
- [ ] **DataIngestionPerformanceTests.cs** - Test 100+ concurrent sensor feeds
- [ ] **MetricCalculationPerformanceTests.cs** - Benchmark calculation speed (<50ms)
- [ ] **DatabasePerformanceTests.cs** - Test query performance and bulk inserts
- [ ] **MemoryUsageTests.cs** - Monitor memory consumption under load
- [ ] **ThroughputTests.cs** - Measure data processing throughput
- [ ] **LoadTests.cs** - Stress test with simulated user load

## Testing Strategy
- **Unit Tests**: 80%+ code coverage, fast execution (<1s total)
- **Integration Tests**: Critical workflows, database consistency
- **Performance Tests**: Meet SLA requirements (latency, throughput)
- **Mutation Testing**: Verify test quality with Stryker.NET

## Testing Tools
- **xUnit** - Primary testing framework
- **Moq** - Mocking framework for dependencies
- **FluentAssertions** - Readable test assertions
- **Testcontainers** - Real database testing with Docker
- **NBomber** - Load and performance testing
- **Stryker.NET** - Mutation testing for test quality

## CI/CD Integration
- Run unit tests on every commit
- Integration tests on pull requests
- Performance regression detection
- Code coverage reporting
- Automated test result notifications