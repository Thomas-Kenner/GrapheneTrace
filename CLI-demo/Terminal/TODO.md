# Console Interface - TODO

## Overview
Temporary visualization solution for displaying pressure data and metrics in the terminal.

## Display/ - TODO
- [ ] **HeatMapRenderer.cs** - ASCII/Unicode heat map visualization of 32x32 matrices
- [ ] **MetricDashboard.cs** - Real-time metric display (Peak Pressure, Contact Area %)
- [ ] **ColorMapper.cs** - Map pressure values (1-255) to console colors/characters
- [ ] **ProgressIndicator.cs** - Show data processing status and throughput
- [ ] **AlertDisplay.cs** - Visual alert notifications in console
- [ ] **TimeSeriesChart.cs** - ASCII-based charts for metric trends
- [ ] **UserInterface.cs** - Main console UI coordinator

## Commands/ - TODO
- [ ] **StartCommand.cs** - Initialize mock sensors and start data processing
- [ ] **StopCommand.cs** - Gracefully stop all services
- [ ] **StatusCommand.cs** - Display system status and connected devices
- [ ] **ExportCommand.cs** - Export data to CSV files
- [ ] **ConfigCommand.cs** - Configure alert thresholds and display settings
- [ ] **HistoryCommand.cs** - View historical data and replay sessions
- [ ] **HelpCommand.cs** - Display available commands and usage

## Display Features
- Real-time pressure map updates (target 10+ FPS)
- Color-coded pressure intensity using ANSI colors
- Scrollable metric history
- Split-screen view (heat map + metrics)
- Keyboard shortcuts for common actions
- Configurable display refresh rates
- Alert highlighting with blinking/bright colors

## Technologies
- **Spectre.Console** for rich terminal UI
- **System.Console** for basic output
- **ANSI escape codes** for colors and positioning
- **Unicode characters** for heat map visualization

## Future Migration
This console interface serves as a temporary solution. Once UI technology is decided, these visualization algorithms can be adapted for:
- Web-based dashboard
- Desktop WPF/Avalonia application
- Mobile app interface