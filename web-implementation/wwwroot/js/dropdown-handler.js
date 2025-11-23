/**
 * Dropdown Click Handler
 * Closes dropdowns when clicking outside of the header
 */

window.addClickOutsideListener = function (element, dotnetHelper) {
    window.addEventListener("click", (e) => {
        if (element && !element.contains(e.target)) {
            dotnetHelper.invokeMethodAsync("CloseMenu");
        }
    });
};
