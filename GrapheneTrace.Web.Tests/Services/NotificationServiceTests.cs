using GrapheneTrace.Web.Services;
using Microsoft.JSInterop;
using Moq;

namespace GrapheneTrace.Web.Tests.Services;

/// <summary>
/// Unit tests for NotificationService.
/// Author: SID:2412494
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. IsNotificationSupportedAsync returns true when browser supports notifications
/// 2. IsNotificationSupportedAsync returns false when browser doesn't support notifications
/// 3. IsNotificationSupportedAsync returns false when JS module fails to load
/// 4. GetPermissionAsync returns correct permission status
/// 5. GetPermissionAsync returns "denied" on JS error
/// 6. RequestPermissionAsync returns granted status
/// 7. RequestPermissionAsync returns denied status
/// 8. RequestPermissionAsync returns "denied" on JS error
/// 9. ShowNotificationAsync successfully shows notification with all parameters
/// 10. ShowNotificationAsync successfully shows notification with minimal parameters
/// 11. ShowNotificationAsync returns false when JS call fails
/// 12. Service properly loads JS module on first use (lazy loading)
/// 13. Service disposes JS module correctly
///
/// Testing Strategy:
/// - Uses Moq to mock IJSRuntime for JavaScript interop
/// - Tests both success and error scenarios for each method
/// - Verifies lazy loading behavior of JavaScript module
/// - Ensures proper error handling and fallback values
/// </remarks>
public class NotificationServiceTests : IAsyncDisposable
{
    private Mock<IJSRuntime> _mockJSRuntime;
    private Mock<IJSObjectReference> _mockJSModule;
    private NotificationService _service;

    public NotificationServiceTests()
    {
        _mockJSRuntime = new Mock<IJSRuntime>();
        _mockJSModule = new Mock<IJSObjectReference>();

        // Setup JS module import
        _mockJSRuntime
            .Setup(js => js.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/notificationHandler.js")))
            .ReturnsAsync(_mockJSModule.Object);

        _service = new NotificationService(_mockJSRuntime.Object);
    }

    [Fact]
    public async Task IsNotificationSupportedAsync_ReturnsTrueWhenBrowserSupports()
    {
        // Arrange
        _mockJSModule
            .Setup(m => m.InvokeAsync<bool>("isNotificationSupported", It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsNotificationSupportedAsync();

        // Assert
        Assert.True(result);
        _mockJSModule.Verify(m => m.InvokeAsync<bool>("isNotificationSupported", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task IsNotificationSupportedAsync_ReturnsFalseWhenBrowserDoesNotSupport()
    {
        // Arrange
        _mockJSModule
            .Setup(m => m.InvokeAsync<bool>("isNotificationSupported", It.IsAny<object[]>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsNotificationSupportedAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsNotificationSupportedAsync_ReturnsFalseOnJSError()
    {
        // Arrange
        _mockJSModule
            .Setup(m => m.InvokeAsync<bool>("isNotificationSupported", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JS Error"));

        // Act
        var result = await _service.IsNotificationSupportedAsync();

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("granted")]
    [InlineData("denied")]
    [InlineData("default")]
    public async Task GetPermissionAsync_ReturnsCorrectPermissionStatus(string permission)
    {
        // Arrange
        _mockJSModule
            .Setup(m => m.InvokeAsync<string>("getNotificationPermission", It.IsAny<object[]>()))
            .ReturnsAsync(permission);

        // Act
        var result = await _service.GetPermissionAsync();

        // Assert
        Assert.Equal(permission, result);
        _mockJSModule.Verify(m => m.InvokeAsync<string>("getNotificationPermission", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task GetPermissionAsync_ReturnsDeniedOnJSError()
    {
        // Arrange
        _mockJSModule
            .Setup(m => m.InvokeAsync<string>("getNotificationPermission", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JS Error"));

        // Act
        var result = await _service.GetPermissionAsync();

        // Assert
        Assert.Equal("denied", result);
    }

    [Fact]
    public async Task RequestPermissionAsync_ReturnsGrantedStatus()
    {
        // Arrange
        _mockJSModule
            .Setup(m => m.InvokeAsync<string>("requestNotificationPermission", It.IsAny<object[]>()))
            .ReturnsAsync("granted");

        // Act
        var result = await _service.RequestPermissionAsync();

        // Assert
        Assert.Equal("granted", result);
        _mockJSModule.Verify(m => m.InvokeAsync<string>("requestNotificationPermission", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task RequestPermissionAsync_ReturnsDeniedStatus()
    {
        // Arrange
        _mockJSModule
            .Setup(m => m.InvokeAsync<string>("requestNotificationPermission", It.IsAny<object[]>()))
            .ReturnsAsync("denied");

        // Act
        var result = await _service.RequestPermissionAsync();

        // Assert
        Assert.Equal("denied", result);
    }

    [Fact]
    public async Task RequestPermissionAsync_ReturnsDeniedOnJSError()
    {
        // Arrange
        _mockJSModule
            .Setup(m => m.InvokeAsync<string>("requestNotificationPermission", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JS Error"));

        // Act
        var result = await _service.RequestPermissionAsync();

        // Assert
        Assert.Equal("denied", result);
    }

    [Fact]
    public async Task ShowNotificationAsync_SuccessfullyShowsNotificationWithAllParameters()
    {
        // Arrange
        var title = "Test Notification";
        var body = "Test body";
        var icon = "/test-icon.png";
        var tag = "test-tag";
        var requireInteraction = true;

        _mockJSModule
            .Setup(m => m.InvokeAsync<bool>(
                "showNotification",
                It.Is<object[]>(args =>
                    args.Length == 2 &&
                    args[0].ToString() == title)))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ShowNotificationAsync(
            title: title,
            body: body,
            icon: icon,
            tag: tag,
            requireInteraction: requireInteraction);

        // Assert
        Assert.True(result);
        _mockJSModule.Verify(m => m.InvokeAsync<bool>(
            "showNotification",
            It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task ShowNotificationAsync_SuccessfullyShowsNotificationWithMinimalParameters()
    {
        // Arrange
        var title = "Test Notification";

        _mockJSModule
            .Setup(m => m.InvokeAsync<bool>(
                "showNotification",
                It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ShowNotificationAsync(title: title);

        // Assert
        Assert.True(result);
        _mockJSModule.Verify(m => m.InvokeAsync<bool>(
            "showNotification",
            It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task ShowNotificationAsync_ReturnsFalseOnJSError()
    {
        // Arrange
        _mockJSModule
            .Setup(m => m.InvokeAsync<bool>("showNotification", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JS Error"));

        // Act
        var result = await _service.ShowNotificationAsync("Test");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ServiceLoadsJSModuleLazilyOnFirstUse()
    {
        // Arrange
        _mockJSModule
            .Setup(m => m.InvokeAsync<bool>("isNotificationSupported", It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // Act - First call should trigger module load
        await _service.IsNotificationSupportedAsync();

        // Assert - Verify module was imported
        _mockJSRuntime.Verify(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "./js/notificationHandler.js")),
            Times.Once);

        // Act - Second call should not trigger another import
        await _service.IsNotificationSupportedAsync();

        // Assert - Still only imported once (lazy loading)
        _mockJSRuntime.Verify(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task ServiceDisposesJSModuleCorrectly()
    {
        // Arrange - Force module to load
        _mockJSModule
            .Setup(m => m.InvokeAsync<bool>("isNotificationSupported", It.IsAny<object[]>()))
            .ReturnsAsync(true);

        await _service.IsNotificationSupportedAsync();

        // Act
        await _service.DisposeAsync();

        // Assert
        _mockJSModule.Verify(m => m.DisposeAsync(), Times.Once);
    }

    public async ValueTask DisposeAsync()
    {
        await _service.DisposeAsync();
    }
}
