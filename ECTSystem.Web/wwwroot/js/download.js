/**
 * Triggers a browser file download from a byte array received via Blazor interop.
 * @param {string} fileName - The suggested file name for the download.
 * @param {string} contentType - The MIME type of the file.
 * @param {Uint8Array} bytes - File content (Blazor marshals byte[] as Uint8Array).
 */
function downloadFileFromBytes(fileName, contentType, bytes) {
    var blob = new Blob([bytes], { type: contentType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

/**
 * Opens a file in a new browser tab from a byte array received via Blazor interop.
 * @param {string} contentType - The MIME type of the file.
 * @param {Uint8Array} bytes - File content (Blazor marshals byte[] as Uint8Array).
 */
function openFileInNewTab(contentType, bytes) {
    var blob = new Blob([bytes], { type: contentType });
    var url = URL.createObjectURL(blob);
    window.open(url, '_blank');
}

/**
 * Programmatically clicks a hidden file input element by its ID.
 * @param {string} inputId - The DOM element ID of the input to click.
 */
function triggerFileInput(inputId) {
    var el = document.getElementById(inputId);
    if (el) {
        el.click();
    }
}
