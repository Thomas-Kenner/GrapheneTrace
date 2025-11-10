# Patient Pressure Threshold Settings Implementation Plan

**Story #9**: As a patient, I want to set and change my (low and?) high pressure threshold so that I can adjust it for alerts as I see fit and adapt it for my needs.

**Branch**: `pressure-threshold-settings-impl`
**Author**: Thomas (SID:2412494)
**Status**: Planning

---

## Overview

Implement a settings page and backend API for patients to configure their pressure alert thresholds. The system will store low and high pressure thresholds that trigger alerts when exceeded during monitoring.

### Key Requirements
- Patients can view their current threshold settings
- Patients can update their low and high pressure thresholds
- Settings are persisted per patient in the database
- Input validation ensures thresholds are within valid sensor ranges (configurable)
- Validation ensures low threshold < high threshold
- Default thresholds are applied for new patients (configurable)

### ⚙️ Configuration-Driven Design
All pressure threshold ranges and defaults are now **configurable via appsettings.json** to accommodate different sensor hardware specifications without code changes.

**Configuration file:** `appsettings.json` → `PressureThresholds` section

**Configurable values:**
- `MinValue` / `MaxValue`: Absolute sensor range (default: 1-255)
- `LowThresholdMin` / `LowThresholdMax`: Allowed range for low threshold (default: 1-254)
- `HighThresholdMin` / `HighThresholdMax`: Allowed range for high threshold (default: 2-255)
- `DefaultLowThreshold` / `DefaultHighThreshold`: Defaults for new patients (default: 50, 200)

**Validation:** Configuration is validated at application startup. Invalid configurations prevent startup with clear error messages.

---

## Database Schema

### Option 1: Extend ApplicationUser (Recommended for MVP)
Add threshold columns directly to `ApplicationUser` (AspNetUsers table):
```csharp
public int? LowPressureThreshold { get; set; } = 50;   // Default: 50
public int? HighPressureThreshold { get; set; } = 200; // Default: 200
```

**Pros:**
- Simple implementation - no new tables or migrations
- Direct access via Identity user object
- Good performance (no joins needed)
- Sufficient for single-patient-settings scenario

**Cons:**
- Mixes domain logic with Identity framework
- Harder to extend if we need versioned settings history

### Option 2: Separate PatientSettings Table (Future-proof)
Create dedicated `PatientSettings` table with 1:1 relationship:
```csharp
public class PatientSettings
{
    public Guid PatientSettingsId { get; set; }
    public Guid UserId { get; set; }  // FK to AspNetUsers
    public int LowPressureThreshold { get; set; } = 50;
    public int HighPressureThreshold { get; set; } = 200;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;
}
```

**Pros:**
- Clean separation of concerns
- Easier to add settings history/audit trail later
- Matches CLAUDE.md architecture (references `patient_settings` table)
- Prepared for future expansion (notification preferences, display settings, etc.)

**Cons:**
- Requires migration
- Slightly more complex queries

**Decision:** Use **Option 2** to align with CLAUDE.md architecture and medical device best practices (audit trail).

---

## Implementation Tasks

### 1. Database Layer

#### 1.1 Create PatientSettings Model
**File**: `web-implementation/Models/PatientSettings.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace GrapheneTrace.Web.Models;

/// <summary>
/// Patient-specific settings for pressure monitoring alerts.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Implements Story #9: Patient threshold configuration
///
/// Design Pattern: Domain entity with validation attributes
///
/// Pressure Values:
/// - Sensor range: 1-255 (1=no pressure, 255=saturation)
/// - Default low threshold: 50 (early warning)
/// - Default high threshold: 200 (urgent alert)
///
/// Validation Rules:
/// - LowPressureThreshold must be between 1-254
/// - HighPressureThreshold must be between 2-255
/// - LowPressureThreshold must be less than HighPressureThreshold
/// </remarks>
public class PatientSettings
{
    [Key]
    public Guid PatientSettingsId { get; set; }

    /// <summary>
    /// Foreign key to patient user account.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Low pressure threshold for early warning alerts.
    /// Default: 50
    /// </summary>
    [Required]
    [Range(1, 254, ErrorMessage = "Low threshold must be between 1 and 254")]
    public int LowPressureThreshold { get; set; } = 50;

    /// <summary>
    /// High pressure threshold for urgent alerts.
    /// Default: 200
    /// </summary>
    [Required]
    [Range(2, 255, ErrorMessage = "High threshold must be between 2 and 255")]
    public int HighPressureThreshold { get; set; } = 200;

    /// <summary>
    /// Timestamp when settings were created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when settings were last modified.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to patient user.
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;
}
```

#### 1.2 Update ApplicationDbContext
**File**: `web-implementation/Data/ApplicationDbContext.cs`

Add DbSet and configure relationship:
```csharp
public DbSet<PatientSettings> PatientSettings { get; set; } = null!;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Configure PatientSettings -> User relationship
    modelBuilder.Entity<PatientSettings>()
        .HasOne(ps => ps.User)
        .WithMany()
        .HasForeignKey(ps => ps.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    // Add unique index on UserId (one settings record per patient)
    modelBuilder.Entity<PatientSettings>()
        .HasIndex(ps => ps.UserId)
        .IsUnique();
}
```

#### 1.3 Create EF Core Migration
```bash
cd web-implementation
dotnet ef migrations add AddPatientSettings
dotnet ef database update
```

---

### 2. Backend API Layer

#### 2.1 Create SettingsController
**File**: `web-implementation/Controllers/SettingsController.cs`

```csharp
using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Controllers;

/// <summary>
/// Handles patient settings management via API endpoints.
/// Author: SID:2412494
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Patient")]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<SettingsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/settings
    /// Retrieves current user's pressure threshold settings.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);

        var settings = await _context.PatientSettings
            .FirstOrDefaultAsync(ps => ps.UserId == userId);

        // Create default settings if not found
        if (settings == null)
        {
            settings = new PatientSettings
            {
                PatientSettingsId = Guid.NewGuid(),
                UserId = userId,
                LowPressureThreshold = 50,
                HighPressureThreshold = 200,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PatientSettings.Add(settings);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created default settings for patient {UserId}", userId);
        }

        return Ok(new
        {
            lowThreshold = settings.LowPressureThreshold,
            highThreshold = settings.HighPressureThreshold,
            updatedAt = settings.UpdatedAt
        });
    }

    /// <summary>
    /// PUT /api/settings
    /// Updates current user's pressure threshold settings.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
    {
        // Validate request
        if (request.LowThreshold < 1 || request.LowThreshold > 254)
        {
            return BadRequest(new { error = "Low threshold must be between 1 and 254" });
        }

        if (request.HighThreshold < 2 || request.HighThreshold > 255)
        {
            return BadRequest(new { error = "High threshold must be between 2 and 255" });
        }

        if (request.LowThreshold >= request.HighThreshold)
        {
            return BadRequest(new { error = "Low threshold must be less than high threshold" });
        }

        var userId = Guid.Parse(_userManager.GetUserId(User)!);

        var settings = await _context.PatientSettings
            .FirstOrDefaultAsync(ps => ps.UserId == userId);

        if (settings == null)
        {
            // Create new settings if not found
            settings = new PatientSettings
            {
                PatientSettingsId = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.PatientSettings.Add(settings);
        }

        // Update thresholds
        settings.LowPressureThreshold = request.LowThreshold;
        settings.HighPressureThreshold = request.HighThreshold;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Patient {UserId} updated thresholds: low={Low}, high={High}",
            userId, request.LowThreshold, request.HighThreshold);

        return Ok(new
        {
            message = "Settings updated successfully",
            lowThreshold = settings.LowPressureThreshold,
            highThreshold = settings.HighPressureThreshold,
            updatedAt = settings.UpdatedAt
        });
    }
}

/// <summary>
/// Request model for updating patient settings.
/// </summary>
public class UpdateSettingsRequest
{
    public int LowThreshold { get; set; }
    public int HighThreshold { get; set; }
}
```

---

### 3. Frontend UI Layer

#### 3.1 Create Settings Page Component
**File**: `web-implementation/Components/Pages/Patient/Settings/Index.razor`

```razor
@page "/patient/settings"
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Patient")]
@using System.Net.Http.Headers
@inject HttpClient Http
@inject IHttpContextAccessor HttpContextAccessor
@inject IJSRuntime JS

@*
    Patient Settings Page
    Author: SID:2412494
    Route: /patient/settings

    Purpose: Allow patients to configure pressure alert thresholds.
    Implements Story #9.

    Features:
    - View current low/high pressure thresholds
    - Update thresholds with real-time validation
    - Visual feedback on save success/error
    - Responsive form layout
*@

<div style="padding: 2rem;">
    <div style="max-width: 48rem; margin: 0 auto;">
        <h1 style="font-size: 2rem; font-weight: bold; margin-bottom: 0.5rem;">
            Pressure Alert Settings
        </h1>
        <p style="color: #6b7280; margin-bottom: 2rem;">
            Configure your pressure monitoring thresholds to receive alerts when readings exceed safe levels.
        </p>

        @if (isLoading)
        {
            <div style="text-align: center; padding: 3rem; color: #6b7280;">
                Loading settings...
            </div>
        }
        else if (errorMessage != null)
        {
            <div style="background: #fee; border: 1px solid #fcc; color: #c00; padding: 1rem; border-radius: 0.5rem; margin-bottom: 1rem;">
                @errorMessage
            </div>
        }
        else
        {
            <div style="background: white; border: 1px solid #e5e7eb; border-radius: 0.5rem; padding: 2rem;">
                <EditForm Model="@settingsModel" OnValidSubmit="@HandleSubmit">
                    <DataAnnotationsValidator />

                    <!-- Low Threshold -->
                    <div style="margin-bottom: 1.5rem;">
                        <label style="display: block; font-weight: 500; margin-bottom: 0.5rem;">
                            Low Pressure Threshold
                        </label>
                        <p style="color: #6b7280; font-size: 0.875rem; margin-bottom: 0.5rem;">
                            You'll receive an early warning when pressure reaches this level.
                        </p>
                        <InputNumber @bind-Value="settingsModel.LowThreshold"
                                     style="width: 100%; padding: 0.5rem; border: 1px solid #d1d5db; border-radius: 0.375rem;"
                                     min="1" max="254" />
                        <ValidationMessage For="@(() => settingsModel.LowThreshold)" />
                        <p style="color: #6b7280; font-size: 0.75rem; margin-top: 0.25rem;">
                            Valid range: 1-254 (current: @settingsModel.LowThreshold)
                        </p>
                    </div>

                    <!-- High Threshold -->
                    <div style="margin-bottom: 1.5rem;">
                        <label style="display: block; font-weight: 500; margin-bottom: 0.5rem;">
                            High Pressure Threshold
                        </label>
                        <p style="color: #6b7280; font-size: 0.875rem; margin-bottom: 0.5rem;">
                            You'll receive an urgent alert when pressure reaches this level.
                        </p>
                        <InputNumber @bind-Value="settingsModel.HighThreshold"
                                     style="width: 100%; padding: 0.5rem; border: 1px solid #d1d5db; border-radius: 0.375rem;"
                                     min="2" max="255" />
                        <ValidationMessage For="@(() => settingsModel.HighThreshold)" />
                        <p style="color: #6b7280; font-size: 0.75rem; margin-top: 0.25rem;">
                            Valid range: 2-255 (current: @settingsModel.HighThreshold)
                        </p>
                    </div>

                    <!-- Validation Error -->
                    @if (validationError != null)
                    {
                        <div style="background: #fee; border: 1px solid #fcc; color: #c00; padding: 1rem; border-radius: 0.5rem; margin-bottom: 1rem;">
                            @validationError
                        </div>
                    }

                    <!-- Success Message -->
                    @if (successMessage != null)
                    {
                        <div style="background: #efe; border: 1px solid #cfc; color: #060; padding: 1rem; border-radius: 0.5rem; margin-bottom: 1rem;">
                            @successMessage
                        </div>
                    }

                    <!-- Action Buttons -->
                    <div style="display: flex; gap: 1rem;">
                        <button type="submit"
                                disabled="@isSaving"
                                style="background: #2563eb; color: white; padding: 0.75rem 1.5rem; border: none; border-radius: 0.375rem; cursor: pointer; font-weight: 500; flex: 1;">
                            @(isSaving ? "Saving..." : "Save Settings")
                        </button>
                        <button type="button"
                                @onclick="LoadSettings"
                                disabled="@isSaving"
                                style="background: #6b7280; color: white; padding: 0.75rem 1.5rem; border: none; border-radius: 0.375rem; cursor: pointer; font-weight: 500;">
                            Reset
                        </button>
                    </div>
                </EditForm>

                <!-- Information Panel -->
                <div style="background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 0.5rem; padding: 1rem; margin-top: 2rem;">
                    <h3 style="font-weight: 600; margin-bottom: 0.5rem;">Understanding Pressure Values</h3>
                    <ul style="color: #6b7280; font-size: 0.875rem; margin-left: 1.5rem;">
                        <li>Sensor range: 1-255</li>
                        <li>Value 1: No pressure detected</li>
                        <li>Value 255: Maximum sensor capacity</li>
                        <li>Default low threshold: 50 (early warning)</li>
                        <li>Default high threshold: 200 (urgent alert)</li>
                    </ul>
                    <p style="color: #6b7280; font-size: 0.875rem; margin-top: 0.5rem;">
                        <strong>Last updated:</strong> @lastUpdated?.ToString("MMM dd, yyyy h:mm tt")
                    </p>
                </div>
            </div>
        }

        <!-- Back to Dashboard -->
        <div style="margin-top: 1.5rem;">
            <a href="/patient/dashboard" style="color: #2563eb; text-decoration: none;">
                ← Back to Dashboard
            </a>
        </div>
    </div>
</div>

@code {
    private SettingsModel settingsModel = new();
    private bool isLoading = true;
    private bool isSaving = false;
    private string? errorMessage;
    private string? validationError;
    private string? successMessage;
    private DateTime? lastUpdated;

    protected override async Task OnInitializedAsync()
    {
        await LoadSettings();
    }

    private async Task LoadSettings()
    {
        try
        {
            isLoading = true;
            errorMessage = null;
            successMessage = null;
            validationError = null;
            StateHasChanged();

            // Get auth token from cookie
            var token = HttpContextAccessor.HttpContext?.Request.Cookies[".AspNetCore.Identity.Application"];
            Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Http.GetAsync("/api/settings");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SettingsResponse>();
                if (result != null)
                {
                    settingsModel.LowThreshold = result.LowThreshold;
                    settingsModel.HighThreshold = result.HighThreshold;
                    lastUpdated = result.UpdatedAt;
                }
            }
            else
            {
                errorMessage = "Failed to load settings. Please try again.";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading settings: {ex.Message}";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task HandleSubmit()
    {
        try
        {
            // Client-side validation
            validationError = null;
            successMessage = null;

            if (settingsModel.LowThreshold >= settingsModel.HighThreshold)
            {
                validationError = "Low threshold must be less than high threshold.";
                return;
            }

            isSaving = true;
            StateHasChanged();

            var response = await Http.PutAsJsonAsync("/api/settings", new
            {
                lowThreshold = settingsModel.LowThreshold,
                highThreshold = settingsModel.HighThreshold
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SettingsResponse>();
                if (result != null)
                {
                    lastUpdated = result.UpdatedAt;
                }
                successMessage = "Settings saved successfully!";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                validationError = $"Failed to save settings: {error}";
            }
        }
        catch (Exception ex)
        {
            validationError = $"Error saving settings: {ex.Message}";
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private class SettingsModel
    {
        [Range(1, 254, ErrorMessage = "Low threshold must be between 1 and 254")]
        public int LowThreshold { get; set; } = 50;

        [Range(2, 255, ErrorMessage = "High threshold must be between 2 and 255")]
        public int HighThreshold { get; set; } = 200;
    }

    private class SettingsResponse
    {
        public int LowThreshold { get; set; }
        public int HighThreshold { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
```

#### 3.2 Add Navigation Link to Patient Dashboard
**File**: `web-implementation/Components/Pages/Patient/Dashboard.razor`

Add settings link in the Quick Actions section:
```html
<a href="/patient/settings"
   style="display: inline-block; background: #2563eb; color: white; padding: 0.5rem 1rem;
          border-radius: 0.375rem; text-decoration: none; font-weight: 500;">
    Manage Alert Settings
</a>
```

---

## Testing Plan

### 4.1 Unit Tests
**File**: `GrapheneTrace.Web.Tests/Controllers/SettingsControllerTests.cs`

Test cases:
- ✅ GET returns default settings for new patient
- ✅ GET returns existing settings for patient with settings
- ✅ PUT creates new settings when none exist
- ✅ PUT updates existing settings
- ✅ PUT rejects low threshold >= high threshold
- ✅ PUT rejects low threshold < 1 or > 254
- ✅ PUT rejects high threshold < 2 or > 255
- ✅ Unauthorized users cannot access endpoints

### 4.2 Integration Tests
- ✅ Patient can navigate to settings page
- ✅ Settings form displays current values
- ✅ Validation prevents invalid submissions
- ✅ Success message appears after save
- ✅ Settings persist across page reloads
- ✅ Non-patient users cannot access settings page

### 4.3 Manual Testing Checklist
- [ ] Create patient account and login
- [ ] Navigate to /patient/settings
- [ ] Verify default values (50, 200) appear
- [ ] Change thresholds and save
- [ ] Reload page and verify changes persisted
- [ ] Test validation: low > high (should fail)
- [ ] Test validation: low = 0 (should fail)
- [ ] Test validation: high = 256 (should fail)
- [ ] Test Reset button restores current values
- [ ] Verify unauthorized access redirects to /access-denied

---

## Deployment Checklist

- [ ] Create database migration
- [ ] Apply migration to development database
- [ ] Test locally with docker-compose
- [ ] Create unit tests for SettingsController
- [ ] Code review and PR approval
- [ ] Merge to main branch
- [ ] Apply migration to production database
- [ ] Update UserStories.md: mark Story #9 as complete

---

## Future Enhancements (Out of Scope)

- [ ] Settings history/audit trail (track all changes with timestamps)
- [ ] Admin override capability (clinician can adjust patient thresholds)
- [ ] Recommended threshold suggestions based on patient profile
- [ ] Notification preferences (email, SMS, in-app)
- [ ] Threshold visualization on pressure heatmap
- [ ] Import/export settings for multiple devices

---

## Notes

- Default thresholds align with CLAUDE.md specifications
- API uses cookie-based authentication (same as AccountController)
- Follows existing project patterns (Blazor Server + MVC API hybrid)
- Settings are per-patient, not per-device (device assignments handled separately)
- UpdatedAt timestamp enables "settings last changed" UI display
