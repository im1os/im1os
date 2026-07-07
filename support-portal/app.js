(function () {
  var downloadUrl = window.IM1_REMOTE_SUPPORT_WINDOWS_DOWNLOAD_URL;
  var downloadLink = document.querySelector("[data-download-windows]");

  if (!downloadLink || !downloadUrl) {
    return;
  }

  downloadLink.href = downloadUrl;
  downloadLink.addEventListener("click", function () {
    downloadLink.classList.add("is-downloading");
    downloadLink.setAttribute("aria-label", "Starting Windows download");
  });
})();
