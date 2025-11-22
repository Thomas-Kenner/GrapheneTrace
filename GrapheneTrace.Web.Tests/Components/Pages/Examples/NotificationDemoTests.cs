using Bunit;
using GrapheneTrace.Web.Components.Pages.Examples;
using GrapheneTrace.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GrapheneTrace.Web.Tests.Components.Pages.Examples;

/// <summary>
/// Unit tests for NotificationDemo Blazor component.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. Component renders correctly when notifications are not supported
/// 2. Component renders correctly when permission is not granted
/// 3. Component renders correctly when permission is granted
/// 4. Permission request button triggers RequestPermissionAsync
/// 5. Component updates UI after permission is granted
/// 6. Basic notification button calls ShowNotificationAsync
/// 7. Pressure alert notification button calls ShowNotificationAsync with correct parameters
/// 8. Equipment fault notification button calls ShowNotificationAsync with correct parameters
/// 9. Clinician message notification button calls ShowNotificationAsync with correct parameters
/// 10. Persistent notification button calls ShowNotificationAsync with requireInteraction=true
/// 11. Component displays success message after successful notification
/// 12. Component displays error message after failed notification
/// 13. Component checks support and permission on first render
///
/// Testing Strategy:
/// - Uses bUnit for Blazor component testing
/// - Mocks NotificationService to control behavior
/// - Verifies UI rendering based on different states
/// - Tests user interactions (button clicks)
/// - Ensures proper service method calls with correct parameters
/// </remarks>
public class NotificationDemoTests : TestContext
{
    private Mock<NotificationService> _mockNotificationService;

    public NotificationDemoTests()
    {
        // Create mock NotificationService
        // Note: NotificationService requires IJSRuntime in constructor, so we mock it
        var mockJSRuntime = new Mock<Microsoft.JSInterop.IJSRuntime>();
        _mockNotificationService = new Mock<NotificationService>(mockJSRuntime.Object);

        // Register mock service
        Services.AddSingleton(_mockNotificationService.Object);
    }

    [Fact]
    public void ComponentRendersCorrectlyWhenNotificationsNotSupported()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(false);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("denied");

        // Act
        var cut = RenderComponent<NotificationDemo>();

        // Assert
        var alert = cut.Find(".alert-warning");
        Assert.Contains("Browser Not Supported", alert.TextContent);
        Assert.Contains("does not support the Notification API", alert.TextContent);
    }

    [Fact]
    public void ComponentRendersCorrectlyWhenPermissionNotGranted()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("default");

        // Act
        var cut = RenderComponent<NotificationDemo>();

        // Assert - Should show permission request card
        cut.WaitForAssertion(() =>
        {
            var heading = cut.Find("h5:contains('Step 1: Request Permission')");
            Assert.NotNull(heading);
        });

        var requestButton = cut.Find("button:contains('Request Permission')");
        Assert.NotNull(requestButton);
    }

    [Fact]
    public void ComponentRendersCorrectlyWhenPermissionGranted()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("granted");

        // Act
        var cut = RenderComponent<NotificationDemo>();

        // Assert - Should show test notification buttons
        cut.WaitForAssertion(() =>
        {
            var heading = cut.Find("h5:contains('Test Notifications')");
            Assert.NotNull(heading);
        });

        var basicButton = cut.Find("button:contains('Show Basic Notification')");
        Assert.NotNull(basicButton);
    }

    [Fact]
    public async Task PermissionRequestButtonTriggersRequestPermissionAsync()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("default");

        _mockNotificationService
            .Setup(s => s.RequestPermissionAsync())
            .ReturnsAsync("granted");

        var cut = RenderComponent<NotificationDemo>();

        // Act
        cut.WaitForAssertion(() =>
        {
            var requestButton = cut.Find("button:contains('Request Permission')");
            requestButton.Click();
        });

        // Assert
        await Task.Delay(100); // Give time for async operation
        _mockNotificationService.Verify(s => s.RequestPermissionAsync(), Times.Once);
    }

    [Fact]
    public async Task BasicNotificationButtonCallsShowNotificationAsync()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("granted");

        _mockNotificationService
            .Setup(s => s.ShowNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(true);

        var cut = RenderComponent<NotificationDemo>();

        // Act
        cut.WaitForAssertion(() =>
        {
            var basicButton = cut.Find("button:contains('Show Basic Notification')");
            basicButton.Click();
        });

        // Assert
        await Task.Delay(100);
        _mockNotificationService.Verify(s => s.ShowNotificationAsync(
            "Test Notification",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task PressureAlertButtonCallsShowNotificationAsyncWithCorrectParameters()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("granted");

        _mockNotificationService
            .Setup(s => s.ShowNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(true);

        var cut = RenderComponent<NotificationDemo>();

        // Act
        cut.WaitForAssertion(() =>
        {
            var pressureButton = cut.Find("button:contains('Simulate Pressure Alert')");
            pressureButton.Click();
        });

        // Assert
        await Task.Delay(100);
        _mockNotificationService.Verify(s => s.ShowNotificationAsync(
            It.Is<string>(t => t.Contains("Pressure Alert")),
            It.Is<string>(b => b.Contains("Patient bed A3")),
            It.IsAny<string>(),
            "pressure-alert-demo",
            false), Times.Once);
    }

    [Fact]
    public async Task EquipmentFaultButtonCallsShowNotificationAsyncWithCorrectParameters()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("granted");

        _mockNotificationService
            .Setup(s => s.ShowNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(true);

        var cut = RenderComponent<NotificationDemo>();

        // Act
        cut.WaitForAssertion(() =>
        {
            var faultButton = cut.Find("button:contains('Simulate Equipment Fault')");
            faultButton.Click();
        });

        // Assert
        await Task.Delay(100);
        _mockNotificationService.Verify(s => s.ShowNotificationAsync(
            It.Is<string>(t => t.Contains("Equipment Fault")),
            It.Is<string>(b => b.Contains("Sensor malfunction")),
            It.IsAny<string>(),
            "equipment-fault-demo",
            false), Times.Once);
    }

    [Fact]
    public async Task ClinicianMessageButtonCallsShowNotificationAsyncWithCorrectParameters()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("granted");

        _mockNotificationService
            .Setup(s => s.ShowNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(true);

        var cut = RenderComponent<NotificationDemo>();

        // Act
        cut.WaitForAssertion(() =>
        {
            var messageButton = cut.Find("button:contains('Simulate Clinician Message')");
            messageButton.Click();
        });

        // Assert
        await Task.Delay(100);
        _mockNotificationService.Verify(s => s.ShowNotificationAsync(
            It.Is<string>(t => t.Contains("Message from Dr. Smith")),
            It.IsAny<string>(),
            It.IsAny<string>(),
            "clinician-message-demo",
            false), Times.Once);
    }

    [Fact]
    public async Task PersistentNotificationButtonCallsShowNotificationAsyncWithRequireInteraction()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("granted");

        _mockNotificationService
            .Setup(s => s.ShowNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(true);

        var cut = RenderComponent<NotificationDemo>();

        // Act
        cut.WaitForAssertion(() =>
        {
            var persistentButton = cut.Find("button:contains('Show Persistent Notification')");
            persistentButton.Click();
        });

        // Assert
        await Task.Delay(100);
        _mockNotificationService.Verify(s => s.ShowNotificationAsync(
            It.Is<string>(t => t.Contains("Critical Alert")),
            It.IsAny<string>(),
            It.IsAny<string>(),
            "persistent-demo",
            true), Times.Once); // requireInteraction should be true
    }

    [Fact]
    public async Task ComponentDisplaysSuccessMessageAfterSuccessfulNotification()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("granted");

        _mockNotificationService
            .Setup(s => s.ShowNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(true);

        var cut = RenderComponent<NotificationDemo>();

        // Act
        cut.WaitForAssertion(() =>
        {
            var basicButton = cut.Find("button:contains('Show Basic Notification')");
            basicButton.Click();
        });

        // Assert
        await Task.Delay(100);
        cut.WaitForAssertion(() =>
        {
            var successAlert = cut.Find(".alert-success");
            Assert.Contains("Success", successAlert.TextContent);
            Assert.Contains("Notification sent successfully", successAlert.TextContent);
        });
    }

    [Fact]
    public async Task ComponentDisplaysErrorMessageAfterFailedNotification()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("granted");

        _mockNotificationService
            .Setup(s => s.ShowNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(false); // Simulate failure

        var cut = RenderComponent<NotificationDemo>();

        // Act
        cut.WaitForAssertion(() =>
        {
            var basicButton = cut.Find("button:contains('Show Basic Notification')");
            basicButton.Click();
        });

        // Assert
        await Task.Delay(100);
        cut.WaitForAssertion(() =>
        {
            var errorAlert = cut.Find(".alert-danger");
            Assert.Contains("Failed", errorAlert.TextContent);
            Assert.Contains("Could not send notification", errorAlert.TextContent);
        });
    }

    [Fact]
    public async Task ComponentChecksSupportAndPermissionOnFirstRender()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.IsNotificationSupportedAsync())
            .ReturnsAsync(true);

        _mockNotificationService
            .Setup(s => s.GetPermissionAsync())
            .ReturnsAsync("granted");

        // Act
        var cut = RenderComponent<NotificationDemo>();

        // Assert
        await Task.Delay(100);
        _mockNotificationService.Verify(s => s.IsNotificationSupportedAsync(), Times.Once);
        _mockNotificationService.Verify(s => s.GetPermissionAsync(), Times.Once);
    }
}
