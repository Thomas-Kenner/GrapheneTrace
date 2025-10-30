/**
 * Dropdown Click Handler
 * Closes dropdowns when clicking outside of the header
 */

window.setupDropdownClickHandler = function(dotnetObj, headerElement) {
    document.addEventListener('click', function(event) {
        // Check if the click is outside the header
        if (headerElement && !headerElement.contains(event.target)) {
            // Call the C# method to close dropdowns
            dotnetObj.invokeMethodAsync('CloseDropdownsFromJS');
        }
    });
};
