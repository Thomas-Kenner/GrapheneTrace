/**
 * Unit tests for notificationHandler.js
 *
 * Test Coverage:
 * 1. isNotificationSupported returns true when Notification API exists
 * 2. isNotificationSupported returns false when Notification API doesn't exist
 * 3. getNotificationPermission returns current permission status
 * 4. getNotificationPermission returns 'denied' when API not supported
 * 5. requestNotificationPermission returns 'granted' when user approves
 * 6. requestNotificationPermission returns 'denied' when user denies
 * 7. requestNotificationPermission returns 'granted' if already granted
 * 8. requestNotificationPermission returns 'denied' when API not supported
 * 9. showNotification creates notification with all parameters
 * 10. showNotification creates notification with minimal parameters
 * 11. showNotification returns false when API not supported
 * 12. showNotification returns false when permission not granted
 * 13. showNotification handles click event correctly
 * 14. showNotification uses default values for optional parameters
 *
 * Author: SID:2412494
 *
 * Testing Strategy:
 * - Mocks browser Notification API using global window object
 * - Tests both success and error scenarios
 * - Verifies permission handling
 * - Ensures proper parameter passing to Notification constructor
 *
 * Note: These tests use Jest. To run:
 * 1. Install Jest: npm install --save-dev jest @babel/preset-env
 * 2. Configure Jest in package.json
 * 3. Run: npm test
 */

import {
    isNotificationSupported,
    getNotificationPermission,
    requestNotificationPermission,
    showNotification
} from './notificationHandler.js';

describe('notificationHandler', () => {
    let originalNotification;
    let mockNotification;

    beforeEach(() => {
        // Save original Notification
        originalNotification = global.Notification;

        // Create mock Notification constructor
        mockNotification = jest.fn();
        mockNotification.permission = 'default';
        mockNotification.requestPermission = jest.fn();

        // Setup global Notification
        global.Notification = mockNotification;
    });

    afterEach(() => {
        // Restore original Notification
        global.Notification = originalNotification;
        jest.clearAllMocks();
    });

    describe('isNotificationSupported', () => {
        test('returns true when Notification API exists', () => {
            // Arrange - Notification already set up in beforeEach

            // Act
            const result = isNotificationSupported();

            // Assert
            expect(result).toBe(true);
        });

        test('returns false when Notification API does not exist', () => {
            // Arrange
            delete global.Notification;

            // Act
            const result = isNotificationSupported();

            // Assert
            expect(result).toBe(false);
        });
    });

    describe('getNotificationPermission', () => {
        test('returns "granted" when permission is granted', () => {
            // Arrange
            global.Notification.permission = 'granted';

            // Act
            const result = getNotificationPermission();

            // Assert
            expect(result).toBe('granted');
        });

        test('returns "denied" when permission is denied', () => {
            // Arrange
            global.Notification.permission = 'denied';

            // Act
            const result = getNotificationPermission();

            // Assert
            expect(result).toBe('denied');
        });

        test('returns "default" when permission not yet requested', () => {
            // Arrange
            global.Notification.permission = 'default';

            // Act
            const result = getNotificationPermission();

            // Assert
            expect(result).toBe('default');
        });

        test('returns "denied" when Notification API not supported', () => {
            // Arrange
            delete global.Notification;

            // Act
            const result = getNotificationPermission();

            // Assert
            expect(result).toBe('denied');
        });
    });

    describe('requestNotificationPermission', () => {
        test('returns "granted" when user approves permission', async () => {
            // Arrange
            global.Notification.requestPermission = jest.fn().mockResolvedValue('granted');

            // Act
            const result = await requestNotificationPermission();

            // Assert
            expect(result).toBe('granted');
            expect(global.Notification.requestPermission).toHaveBeenCalledTimes(1);
        });

        test('returns "denied" when user denies permission', async () => {
            // Arrange
            global.Notification.requestPermission = jest.fn().mockResolvedValue('denied');

            // Act
            const result = await requestNotificationPermission();

            // Assert
            expect(result).toBe('denied');
            expect(global.Notification.requestPermission).toHaveBeenCalledTimes(1);
        });

        test('returns "granted" without requesting if already granted', async () => {
            // Arrange
            global.Notification.permission = 'granted';
            global.Notification.requestPermission = jest.fn();

            // Act
            const result = await requestNotificationPermission();

            // Assert
            expect(result).toBe('granted');
            expect(global.Notification.requestPermission).not.toHaveBeenCalled();
        });

        test('returns "denied" when Notification API not supported', async () => {
            // Arrange
            delete global.Notification;

            // Act
            const result = await requestNotificationPermission();

            // Assert
            expect(result).toBe('denied');
        });

        test('returns "denied" when requestPermission throws error', async () => {
            // Arrange
            global.Notification.requestPermission = jest.fn().mockRejectedValue(new Error('Permission error'));

            // Act
            const result = await requestNotificationPermission();

            // Assert
            expect(result).toBe('denied');
        });
    });

    describe('showNotification', () => {
        beforeEach(() => {
            // Setup permission as granted for showNotification tests
            global.Notification.permission = 'granted';

            // Mock notification instance
            mockNotification.mockImplementation(function(title, options) {
                this.title = title;
                this.options = options;
                this.close = jest.fn();
                this.onclick = null;
            });
        });

        test('creates notification with all parameters', () => {
            // Arrange
            const title = 'Test Notification';
            const options = {
                body: 'Test body',
                icon: '/test-icon.png',
                tag: 'test-tag',
                requireInteraction: true
            };

            // Act
            const result = showNotification(title, options);

            // Assert
            expect(result).toBe(true);
            expect(mockNotification).toHaveBeenCalledWith(title, {
                body: 'Test body',
                icon: '/test-icon.png',
                tag: 'test-tag',
                requireInteraction: true,
                silent: false
            });
        });

        test('creates notification with minimal parameters', () => {
            // Arrange
            const title = 'Test Notification';

            // Act
            const result = showNotification(title);

            // Assert
            expect(result).toBe(true);
            expect(mockNotification).toHaveBeenCalled();
            const callArgs = mockNotification.mock.calls[0];
            expect(callArgs[0]).toBe(title);
            expect(callArgs[1]).toMatchObject({
                body: '',
                silent: false
            });
        });

        test('returns false when Notification API not supported', () => {
            // Arrange
            delete global.Notification;

            // Act
            const result = showNotification('Test');

            // Assert
            expect(result).toBe(false);
        });

        test('returns false when permission not granted', () => {
            // Arrange
            global.Notification.permission = 'denied';

            // Act
            const result = showNotification('Test');

            // Assert
            expect(result).toBe(false);
            expect(mockNotification).not.toHaveBeenCalled();
        });

        test('uses default icon when not provided', () => {
            // Arrange
            const title = 'Test';

            // Act
            showNotification(title, {});

            // Assert
            const callArgs = mockNotification.mock.calls[0];
            expect(callArgs[1].icon).toBe('/favicon.ico');
        });

        test('generates unique tag when not provided', () => {
            // Arrange
            const title = 'Test';
            const dateSpy = jest.spyOn(Date, 'now').mockReturnValue(12345);

            // Act
            showNotification(title, {});

            // Assert
            const callArgs = mockNotification.mock.calls[0];
            expect(callArgs[1].tag).toBe('notification-12345');
            dateSpy.mockRestore();
        });

        test('sets onclick handler that focuses window and closes notification', () => {
            // Arrange
            const mockClose = jest.fn();
            const mockFocus = jest.fn();
            const mockPreventDefault = jest.fn();

            global.window = { focus: mockFocus };

            mockNotification.mockImplementation(function(title, options) {
                this.close = mockClose;
                this.onclick = null;
            });

            // Act
            showNotification('Test');

            // Get the created notification instance
            const notificationInstance = mockNotification.mock.results[0].value;

            // Simulate click event
            const mockEvent = { preventDefault: mockPreventDefault };
            notificationInstance.onclick(mockEvent);

            // Assert
            expect(mockPreventDefault).toHaveBeenCalled();
            expect(mockFocus).toHaveBeenCalled();
            expect(mockClose).toHaveBeenCalled();
        });

        test('returns false when notification creation throws error', () => {
            // Arrange
            mockNotification.mockImplementation(() => {
                throw new Error('Notification error');
            });

            // Act
            const result = showNotification('Test');

            // Assert
            expect(result).toBe(false);
        });

        test('sets silent to false for notification sound', () => {
            // Arrange
            const title = 'Test';

            // Act
            showNotification(title, { body: 'test' });

            // Assert
            const callArgs = mockNotification.mock.calls[0];
            expect(callArgs[1].silent).toBe(false);
        });
    });
});
