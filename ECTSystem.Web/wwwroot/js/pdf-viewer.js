/**
 * PDF viewer interop helpers for Blazor.
 * Creates object URLs from byte arrays for iframe display.
 */
window.pdfViewerInterop = {
    /**
     * Creates a blob URL from a base64-encoded PDF byte array and optionally sets an iframe src.
     * @param {string} base64 - Base64-encoded PDF content.
     * @param {string} [iframeSelector] - CSS selector for an iframe to set the src on.
     * @returns {string} A blob URL pointing to the PDF.
     */
    createBlobUrl: function (base64, iframeSelector) {
        var byteChars = atob(base64);
        var byteNumbers = new Uint8Array(byteChars.length);
        for (var i = 0; i < byteChars.length; i++) {
            byteNumbers[i] = byteChars.charCodeAt(i);
        }
        var blob = new Blob([byteNumbers], { type: 'application/pdf' });
        var url = URL.createObjectURL(blob);
        if (iframeSelector) {
            var iframe = document.querySelector(iframeSelector);
            if (iframe) {
                iframe.src = url;
            }
        }
        return url;
    },

    /**
     * Revokes a previously created blob URL to free memory.
     * @param {string} url - The blob URL to revoke.
     */
    revokeBlobUrl: function (url) {
        if (url) {
            URL.revokeObjectURL(url);
        }
    }
};
