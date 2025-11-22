/**
 * Browser Notification Handler
 *
 * Provides functions for requesting permission and displaying OS-level desktop notifications
 * with system notification sounds.
 *
 * Author: SID:2412494
 */

/**
 * Checks if the browser supports the Notification API
 * @returns {boolean} True if notifications are supported
 */
export function isNotificationSupported() {
    return 'Notification' in window;
}

/**
 * Gets the current notification permission status
 * @returns {string} 'granted', 'denied', or 'default'
 */
export function getNotificationPermission() {
    if (!isNotificationSupported()) {
        return 'denied';
    }
    return Notification.permission;
}

/**
 * Requests permission to show notifications
 * @returns {Promise<string>} Resolves to 'granted', 'denied', or 'default'
 */
export async function requestNotificationPermission() {
    if (!isNotificationSupported()) {
        console.warn('Notifications are not supported in this browser');
        return 'denied';
    }

    if (Notification.permission === 'granted') {
        return 'granted';
    }

    try {
        const permission = await Notification.requestPermission();
        return permission;
    } catch (error) {
        console.error('Error requesting notification permission:', error);
        return 'denied';
    }
}

/**
 * Shows a desktop notification with system sound
 * @param {string} title - The notification title
 * @param {object} options - Notification options
 * @param {string} options.body - The notification body text
 * @param {string} options.icon - URL to an icon image
 * @param {string} options.tag - A tag to identify the notification (prevents duplicates)
 * @param {boolean} options.requireInteraction - Keep notification visible until user interacts
 * @returns {boolean} True if notification was shown, false otherwise
 */
export function showNotification(title, options = {}) {
    if (!isNotificationSupported()) {
        console.warn('Notifications are not supported in this browser');
        return false;
    }

    if (Notification.permission !== 'granted') {
        console.warn('Notification permission not granted');
        return false;
    }

    try {
        // Create notification - browser automatically plays system notification sound
        const notification = new Notification(title, {
            body: options.body || '',
            icon: options.icon || '/favicon.ico',
            tag: options.tag || `notification-${Date.now()}`,
            requireInteraction: options.requireInteraction || false,
            // silent: false is the default, which means sound will play
            silent: false
        });

        // Optional: Handle notification click
        notification.onclick = function(event) {
            event.preventDefault();
            window.focus();
            notification.close();
        };

        return true;
    } catch (error) {
        console.error('Error showing notification:', error);
        return false;
    }
}
