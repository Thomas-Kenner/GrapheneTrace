# Services Layer - TODO

## Overview
Business logic implementation. Orchestrates domain models and infrastructure to fulfill use cases.

## Ingestion/ - TODO
- [ ] **MockSensorService.cs** - Generate realistic test data streams with device IDs
- [ ] **CsvIngestionService.cs** - Process CSV file imports with validation
- [ ] **DataValidator.cs** - Input validation rules for 32x32 matrices
- [ ] **DeviceRegistrationService.cs** - Handle new device connections
- [ ] **DataStreamOrchestrator.cs** - Coordinate multiple concurrent data streams

## Processing/ - TODO
- [ ] **MetricCalculationService.cs** - Compute Peak Pressure Index and Contact Area %
- [ ] **AlertEvaluationService.cs** - Check alert conditions against user thresholds
- [ ] **PressureAnalysisService.cs** - Advanced analysis algorithms (high-pressure regions)
- [ ] **BackgroundProcessingService.cs** - Hosted service for continuous processing
- [ ] **BatchProcessor.cs** - Batch processing for historical data analysis

## Reporting/ - TODO
- [ ] **ReportGenerationService.cs** - Create comparative reports (day-over-day, etc.)
- [ ] **TimeSeriesAggregator.cs** - Roll up data for different time periods (1h, 6h, 24h)
- [ ] **MetricTrendAnalyzer.cs** - Identify trends in pressure data over time
- [ ] **ExportService.cs** - Export reports in CSV and JSON formats

## Communication/ - TODO
- [ ] **CommentService.cs** - Comment thread management with timestamp association
- [ ] **NotificationService.cs** - Real-time notifications for alerts and comments
- [ ] **UserInteractionService.cs** - Handle user feedback and dashboard interactions
- [ ] **ClinicalReviewService.cs** - Clinician tools for reviewing flagged periods

## Existing Files
- **Mocking/** - Contains existing mock service infrastructure

## Integration Points
- Uses Core interfaces and models
- Calls Infrastructure repositories
- Publishes domain events via message bus
- Implements business rules and workflows