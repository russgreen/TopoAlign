/**
 * Load video conditionally from a URL
 * @param {string} videoUrl - The URL to the video file
 * @param {string} sectionId - The ID of the section containing the video (default: 'video-section')
 * @param {string} videoId - Optional specific video element ID
 */
function loadVideoIfAvailable(videoUrl, sectionId = 'video-section', videoId = null) {
    const videoSection = document.getElementById(sectionId);
    if (!videoSection) {
        console.warn(`Video section with ID '${sectionId}' not found`);
        return;
    }

    const video = videoId ?
        document.getElementById(videoId) :
        videoSection.querySelector('video');

    if (!video) {
        console.warn('Video element not found');
        return;
    }

    // Clear any existing sources first
    video.removeAttribute('src');
    const sources = video.querySelectorAll('source');
    sources.forEach(source => source.remove());

    // Create new source element
    const source = document.createElement('source');
    source.src = videoUrl;
    source.type = 'video/mp4'; // Add MIME type
    video.appendChild(source);

    // Also set src directly on video element as fallback
    video.src = videoUrl;

    // Set up event listeners before loading
    video.addEventListener('loadedmetadata', function () {
        console.log('Video metadata loaded successfully');
        videoSection.style.display = 'block';
    });

    video.addEventListener('canplay', function () {
        console.log('Video can play');
        // Ensure controls are visible
        video.controls = true;
    });

    video.addEventListener('error', function (e) {
        console.error('Video error:', e);
        console.log('Video not accessible at URL:', videoUrl);
        videoSection.style.display = 'none';
    });

    // Set preload and load the video
    video.preload = 'metadata';
    video.controls = true; // Ensure controls are enabled
    video.muted = false; // Default audio to be on
    video.load();
}

// Auto-initialize if config is available
document.addEventListener('DOMContentLoaded', function () {
    if (typeof VIDEO_URL !== 'undefined') {
        loadVideoIfAvailable(VIDEO_URL);
    }
});