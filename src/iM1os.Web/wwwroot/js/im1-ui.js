window.IM1 = window.IM1 || {};

window.IM1.activateTabs = function activateTabs(root) {
  const tabs = root || document;

  tabs.querySelectorAll("[data-tabs]").forEach((tabRoot) => {
    const buttons = Array.from(tabRoot.querySelectorAll("[data-tab-target]"));
    const panels = Array.from(tabRoot.querySelectorAll(".tab-panel"));

    const activate = (id, updateHash) => {
      const panel = panels.find((item) => item.id === id);
      if (!panel) {
        return;
      }

      buttons.forEach((button) => {
        const isActive = button.dataset.tabTarget === id;
        button.classList.toggle("is-active", isActive);
        button.setAttribute("aria-selected", isActive ? "true" : "false");
      });

      panels.forEach((item) => {
        const isActive = item.id === id;
        item.classList.toggle("is-active", isActive);
        item.hidden = !isActive;
      });

      if (updateHash) {
        history.replaceState(null, "", `#${id}`);
      }
    };

    buttons.forEach((button) => {
      button.addEventListener("click", () => activate(button.dataset.tabTarget, true));
    });

    const initialId = window.location.hash ? window.location.hash.slice(1) : buttons[0]?.dataset.tabTarget;
    activate(initialId, false);
  });
};

window.IM1.activateDialogs = function activateDialogs(root) {
  const scope = root || document;

  scope.querySelectorAll("[data-dialog-open]").forEach((button) => {
    button.addEventListener("click", () => {
      const dialog = document.getElementById(button.dataset.dialogOpen);
      if (!dialog) {
        return;
      }

      dialog.hidden = false;
      const focusTarget = dialog.querySelector("button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])");
      focusTarget?.focus();
    });
  });

  scope.querySelectorAll("[data-dialog-close]").forEach((button) => {
    button.addEventListener("click", () => {
      const dialog = button.closest(".im1-dialog-backdrop");
      if (dialog) {
        dialog.hidden = true;
      }
    });
  });
};

window.IM1.activateDataGrids = function activateDataGrids(root) {
  const scope = root || document;

  scope.querySelectorAll("[data-im1-data-grid]").forEach((grid) => {
    const table = grid.querySelector("table");
    const tbody = table?.querySelector("tbody");
    const search = grid.querySelector(".im1-toolbar-search");
    const form = grid.querySelector(".im1-toolbar");
    const rows = Array.from(grid.querySelectorAll("[data-im1-grid-row]"));
    const status = grid.querySelector("[data-im1-grid-status]");
    const previous = grid.querySelector("[data-im1-grid-prev]");
    const next = grid.querySelector("[data-im1-grid-next]");
    const pageSize = Number.parseInt(grid.dataset.pageSize || "", 10);
    let page = 1;
    let visibleRows = rows;

    const render = () => {
      const totalPages = Number.isFinite(pageSize) && pageSize > 0
        ? Math.max(1, Math.ceil(visibleRows.length / pageSize))
        : 1;

      if (page > totalPages) {
        page = totalPages;
      }

      rows.forEach((row) => {
        const isVisible = visibleRows.includes(row);
        if (!Number.isFinite(pageSize) || pageSize <= 0) {
          row.hidden = !isVisible;
          return;
        }

        const index = visibleRows.indexOf(row);
        const isOnPage = index >= (page - 1) * pageSize && index < page * pageSize;
        row.hidden = !isVisible || !isOnPage;
      });

      if (status) {
        status.textContent = `Page ${page} of ${totalPages} - ${visibleRows.length} rows`;
      }
      if (previous) {
        previous.disabled = page <= 1;
      }
      if (next) {
        next.disabled = page >= totalPages;
      }
    };

    const applySearch = () => {
      const query = (search?.value || "").trim().toLowerCase();
      visibleRows = query
        ? rows.filter((row) => row.textContent.toLowerCase().includes(query))
        : rows;
      page = 1;
      render();
    };

    form?.addEventListener("submit", (event) => {
      if (grid.contains(form)) {
        event.preventDefault();
        applySearch();
      }
    });

    search?.addEventListener("input", applySearch);
    previous?.addEventListener("click", () => {
      page -= 1;
      render();
    });
    next?.addEventListener("click", () => {
      page += 1;
      render();
    });

    grid.querySelectorAll("[data-sortable='true']").forEach((header, columnIndex) => {
      const button = header.querySelector(".im1-grid-sort");
      button?.addEventListener("click", () => {
        const isDescending = button.classList.contains("is-ascending");
        grid.querySelectorAll(".im1-grid-sort").forEach((item) => item.classList.remove("is-ascending", "is-descending"));
        button.classList.add(isDescending ? "is-descending" : "is-ascending");

        rows.sort((left, right) => {
          const leftValue = left.children[columnIndex]?.textContent?.trim() || "";
          const rightValue = right.children[columnIndex]?.textContent?.trim() || "";
          return leftValue.localeCompare(rightValue, undefined, { numeric: true, sensitivity: "base" }) * (isDescending ? -1 : 1);
        });

        rows.forEach((row) => tbody?.appendChild(row));
        applySearch();
      });
    });

    render();
  });
};

window.IM1.activateMarketingHeader = function activateMarketingHeader() {
  const header = document.querySelector("[data-marketing-header]");
  if (!header) {
    return;
  }

  const mobileQuery = window.matchMedia("(max-width: 900px)");
  let previousScrollY = window.scrollY;
  let ticking = false;

  const update = () => {
    const currentScrollY = window.scrollY;
    const scrollingDown = currentScrollY > previousScrollY;

    header.classList.toggle("is-hidden-on-scroll", mobileQuery.matches && scrollingDown && currentScrollY > 80);
    previousScrollY = currentScrollY;
    ticking = false;
  };

  window.addEventListener("scroll", () => {
    if (ticking) {
      return;
    }

    window.requestAnimationFrame(update);
    ticking = true;
  }, { passive: true });

  mobileQuery.addEventListener("change", () => {
    if (!mobileQuery.matches) {
      header.classList.remove("is-hidden-on-scroll");
    }
  });
};

document.addEventListener("keydown", (event) => {
  if (event.key !== "Escape") {
    return;
  }

  document.querySelectorAll(".im1-dialog-backdrop:not([hidden])").forEach((dialog) => {
    dialog.hidden = true;
  });
});

document.addEventListener("DOMContentLoaded", () => {
  window.IM1.activateTabs(document);
  window.IM1.activateDialogs(document);
  window.IM1.activateDataGrids(document);
  window.IM1.activateMarketingHeader();
});
