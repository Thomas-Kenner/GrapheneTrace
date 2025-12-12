/**
 * Dropdown Click Handler
 * Closes dropdowns when clicking outside of the header
 * Author: SID:2412494 - Added backwards compatibility alias for setupDropdownClickHandler
 */

window.addClickOutsideListener = function (element, dotnetHelper) {
    window.addEventListener("click", (e) => {
        if (element && !element.contains(e.target)) {
            dotnetHelper.invokeMethodAsync("CloseMenu");
        }
    });
};

// Backwards compatibility: alias for old function name used in previous versions
// This prevents "setupDropdownClickHandler was undefined" errors during transition
window.setupDropdownClickHandler = function(dotnetObj, headerElement) {
    document.addEventListener('click', function(event) {
        if (headerElement && !headerElement.contains(event.target)) {
            dotnetObj.invokeMethodAsync('CloseDropdownsFromJS');
        }
    });
};
