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
/// <remarks>
/// Implements Story #9: Patient pressure threshold configuration.
///
/// API Endpoints:
/// - GET  /api/settings - Retrieve current user's settings
/// - PUT  /api/settings - Update current user's settings
///
/// Authorization: Restricted to Patient role only.
/// Non-patient users receive 403 Forbidden.
///
/// Default Settings:
/// - LowPressureThreshold: 50
/// - HighPressureThreshold: 200
///
/// Auto-creation: If patient has no settings, defaults are created on first GET.
/// </remarks>
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
    /// Creates default settings if none exist.
    /// </summary>
    /// <returns>Settings object with low/high thresholds and last updated timestamp</returns>
    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);

        var settings = await _context.PatientSettings
            .FirstOrDefaultAsync(ps => ps.UserId == userId);

        // Auto-create default settings if not found
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
    /// Creates new settings if none exist.
    /// </summary>
    /// <param name="request">Settings update request with low and high thresholds</param>
    /// <returns>Updated settings or validation error</returns>
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
    /// <summary>
    /// Low pressure threshold (1-254).
    /// </summary>
    public int LowThreshold { get; set; }

    /// <summary>
    /// High pressure threshold (2-255).
    /// </summary>
    public int HighThreshold { get; set; }
}
