# Core Domain Layer - TODO

## Overview
Pure domain logic with no external dependencies. Contains immutable models, interfaces, and enums.

## Models/ - TODO
- [ ] **User.cs** - User entity with role management (Patient, Clinician, Admin)
- [ ] **Device.cs** - Sensor device representation with unique identifiers
- [ ] **Alert.cs** - Alert configuration and history tracking
- [ ] **Comment.cs** - Comment entity for user feedback system
- [ ] **Notification.cs** - Notification model for real-time alerts
- [ ] **MetricSnapshot.cs** - Calculated metrics storage model
- [ ] **Session.cs** - User session tracking for device connections

## Interfaces/ - TODO
- [ ] **IDataIngestion.cs** - Contract for data intake services
- [ ] **IMetricCalculator.cs** - Interface for metric computation services
- [ ] **IAlertEngine.cs** - Interface for alert evaluation logic
- [ ] **IRepository<T>.cs** - Generic repository pattern interface
- [ ] **IUserService.cs** - User management service contract
- [ ] **ICommentService.cs** - Comment management interface
- [ ] **INotificationService.cs** - Real-time notification interface

## Enums/ - TODO
- [ ] **UserRole.cs** - Patient, Clinician, Admin roles
- [ ] **AlertSeverity.cs** - Low, Medium, High, Critical levels
- [ ] **AlertType.cs** - PressureThreshold, ContactArea, SystemError
- [ ] **DeviceStatus.cs** - Online, Offline, Maintenance, Error
- [ ] **NotificationType.cs** - Alert, Comment, System, Report

## Design Principles
- All models must be immutable after creation
- No dependencies on external libraries except System.*
- Rich domain models with behavior, not anemic data containers
- Validation logic embedded in domain entities