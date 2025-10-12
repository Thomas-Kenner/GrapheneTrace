# Mocking Services - TODO

## Overview
Generate realistic mock data to simulate multiple sensor devices for testing and development. Each mock source represents a unique user/device combination with persistent identification.

## Core Mock Services - TODO

### Data Generation
- [ ] **MockPressureDataGenerator.cs** - Generate realistic 32x32 pressure matrices
  - Simulate human sitting patterns with anatomical accuracy
  - Generate pressure gradients based on body weight distribution
  - Include realistic noise and sensor variations
  - Support different body types and sitting positions

- [ ] **MockUserProfiles.cs** - Create diverse user profiles for testing
  - Generate users with different roles (Patient, Clinician, Admin)
  - Create realistic demographic data (age, weight, medical conditions)
  - Assign unique device IDs that persist across sessions
  - Generate alert thresholds based on user profiles

### Device Simulation
- [ ] **MockSensorDevice.cs** - Simulate individual sensor devices
  - Unique device identifier generation and persistence
  - Configurable data generation frequency (1-60 Hz)
  - Simulate connection drops and reconnections
  - Device status updates (Online, Offline, Maintenance, Error)
  - Battery level simulation for wireless devices

- [ ] **MultiDeviceOrchestrator.cs** - Coordinate multiple simultaneous devices
  - Spawn 1-100+ concurrent mock devices
  - Stagger device startup to simulate real-world deployment
  - Load balancing across mock devices
  - Device lifecycle management (start, pause, stop, restart)

## Data Pattern Generation - TODO

### Realistic Pressure Patterns
- [ ] **AnatomicalPatterns.cs** - Human body pressure distribution models
  - Ischial tuberosity pressure points (highest pressure areas)
  - Thigh pressure distribution patterns
  - Coccyx and sacrum pressure zones
  - Asymmetrical sitting patterns for medical conditions

- [ ] **MovementSimulator.cs** - Dynamic pressure changes over time
  - Weight shifting patterns (every 15-30 minutes)
  - Micro-movements and fidgeting
  - Getting up and sitting down transitions
  - Pressure relief movements recommended by clinicians

- [ ] **AlertTriggerSimulator.cs** - Generate conditions that should trigger alerts
  - Sustained high pressure (>threshold for >time)
  - Gradual pressure increase patterns
  - Sudden pressure spikes
  - Loss of contact (user standing up)
  - Edge cases and boundary conditions

### Medical Condition Simulation
- [ ] **SpinalInjuryPatterns.cs** - Simulate pressure patterns for spinal injuries
  - Reduced sensation leading to prolonged pressure
  - Asymmetrical pressure distribution
  - Higher risk pressure thresholds

- [ ] **MobilityImpairedPatterns.cs** - Simulate limited mobility scenarios
  - Reduced frequency of position changes
  - Difficulty with pressure relief movements
  - Extended sitting periods

## Configuration & Control - TODO

### Mock Configuration
- [ ] **MockConfiguration.cs** - Centralized mock behavior configuration
  - Data generation frequency settings
  - Pressure value ranges and distributions
  - Alert trigger probability settings
  - User profile distribution settings
  - Device failure rate simulation

- [ ] **ScenarioManager.cs** - Predefined testing scenarios
  - Normal day scenario (8 hours of typical usage)
  - High-risk scenario (prolonged pressure without relief)
  - Multi-user clinic scenario (10+ patients simultaneously)
  - Device failure scenario (connection drops, battery failures)
  - Emergency scenario (critical pressure levels)

### Data Persistence
- [ ] **MockDataStorage.cs** - Store generated mock data for replay
  - Save generated sessions for debugging
  - Export mock data as CSV files
  - Import real sensor data for replay testing
  - Session management and labeling

- [ ] **DeviceIdentityManager.cs** - Persistent device ID management
  - Generate and store unique device identifiers
  - Map devices to user profiles
  - Handle device registration and deregistration
  - Device metadata storage (model, firmware version, etc.)

## Integration Points - TODO

### Service Integration
- [ ] **MockServiceHost.cs** - Background service for continuous data generation
  - Implement IHostedService for console application
  - Graceful startup and shutdown
  - Health check endpoints
  - Performance monitoring and metrics

- [ ] **DataStreamConnector.cs** - Connect mock data to processing pipeline
  - Interface with real data ingestion services
  - Queue management for high-throughput scenarios
  - Backpressure handling when processing falls behind
  - Error injection for testing error handling

### Testing Support
- [ ] **MockDataValidator.cs** - Verify generated data meets requirements
  - Validate 32x32 matrix structure
  - Check pressure value ranges (1-255)
  - Verify timestamp accuracy and sequencing
  - Anatomical plausibility checks

- [ ] **PerformanceTestData.cs** - Generate data for performance testing
  - High-volume data generation for load testing
  - Concurrent user simulation
  - Memory usage optimization for large datasets
  - Benchmark data generation speed

## Sample Data Scenarios

### Scenario 1: Normal Office Worker
- 8-hour workday with periodic movement
- Lunch break (1-hour gap in data)
- Gradual pressure increase before breaks
- Weight shifting every 20-30 minutes

### Scenario 2: Mobility-Impaired Patient
- Limited movement capability
- Longer periods between position changes
- Higher baseline pressure readings
- Caregiver-assisted pressure relief

### Scenario 3: Clinic Environment
- Multiple patients on different schedules
- Varying session durations (30min - 8hours)
- Device swapping between patients
- Clinical staff monitoring multiple patients

### Scenario 4: Alert Testing
- Gradually increasing pressure to trigger thresholds
- Sustained high pressure periods
- Rapid pressure changes
- False positive scenarios

## Implementation Priority
1. **MockPressureDataGenerator** - Core data generation
2. **MockSensorDevice** - Individual device simulation
3. **AnatomicalPatterns** - Realistic pressure distributions
4. **MultiDeviceOrchestrator** - Multiple concurrent devices
5. **ScenarioManager** - Predefined test scenarios
6. **MockServiceHost** - Integration with main application