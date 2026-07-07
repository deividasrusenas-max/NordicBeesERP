// Drop zone file handling for Blazor Server
// This file enables drag & drop file upload with JS interop

// Global object to store file data for drop events
window.dropZoneFiles = {};

// Setup drop zone to handle drag & drop
window.setupDropZone = (dotNetHelper, elementId) => {
    const el = document.getElementById(elementId);
    if (!el) return;

    // Remove existing listeners if any
    if (el._dropZoneSetup) {
        el.removeEventListener('dragover', el._dragHandler);
        el.removeEventListener('drop', el._dropHandler);
    }

    // Create handlers
    el._dragHandler = (e) => {
        e.preventDefault();
        e.stopPropagation();
        el.classList.add('drag-over');
    };

    el._dropHandler = async (e) => {
        e.preventDefault();
        e.stopPropagation();
        el.classList.remove('drag-over');

        const files = e.dataTransfer.files;
        if (files.length > 0) {
            const file = files[0];
            
            // Store file metadata and reference for Blazor to retrieve
            window.dropZoneFiles[elementId] = {
                name: file.name,
                size: file.size,
                mimeType: file.type,
                file: file  // Store the actual file object for later retrieval
            };
            
            // Notify Blazor with just metadata (small payload)
            await dotNetHelper.invokeMethodAsync('OnFileDropped', file.name, file.size, file.type);
        }
    };

    // Add listeners
    el.addEventListener('dragover', el._dragHandler);
    el.addEventListener('drop', el._dropHandler);
    
    el._dropZoneSetup = true;
};

// Cleanup drop zone
window.cleanupDropZone = (elementId) => {
    const el = document.getElementById(elementId);
    if (el && el._dropZoneSetup) {
        el.removeEventListener('dragover', el._dragHandler);
        el.removeEventListener('drop', el._dropHandler);
        delete el._dropZoneSetup;
        delete el._dragHandler;
        delete el._dropHandler;
        delete window.dropZoneFiles[elementId];
    }
};

// Get dropped file base64 content (called by Blazor when needed)
window.getDropFileBase64 = async (elementId) => {
    const data = window.dropZoneFiles[elementId];
    if (!data || !data.file) {
        return null;
    }
    
    // Read file asDataURL and extract base64
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = (evt) => {
            const result = evt.target.result;
            // Split base64 from data URL (e.g., "data:application/pdf;base64,....")
            const base64 = result.split(',')[1];
            resolve(base64);
        };
        reader.onerror = () => {
            reject(new Error('Failed to read file'));
        };
        reader.readAsDataURL(data.file);
    });
};

// Get dropped file metadata
window.getDropFileMetadata = (elementId) => {
    const data = window.dropZoneFiles[elementId];
    if (data) {
        return {
            name: data.name,
            size: data.size,
            mimeType: data.mimeType
        };
    }
    return null;
};

// Download PDF file from base64 data
window.downloadFileFromBase64 = function(base64, fileName, mimeType) {
    const link = document.createElement('a');
    link.href = 'data:' + mimeType + ';base64,' + base64;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.downloadPdf = (link, fileName) => {
    const linkElement = document.createElement('a');
    linkElement.href = link;
    linkElement.download = fileName;
    linkElement.style.display = 'none';
    document.body.appendChild(linkElement);
    linkElement.click();
    document.body.removeChild(linkElement);
};
