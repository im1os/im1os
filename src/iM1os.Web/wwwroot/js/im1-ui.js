window.IM1 = window.IM1 || {};

window.IM1.activateTabs = function activateTabs(root) {
  const tabs = root || document;

  tabs.querySelectorAll("[data-im1-tabs], [data-tabs]").forEach((tabRoot) => {
    const panelScope = tabRoot.matches("[data-im1-tabs]") ? tabRoot.parentElement || tabRoot : tabRoot;
    const buttons = Array.from(tabRoot.querySelectorAll("[data-tab-target]"));
    const panels = Array.from(panelScope.querySelectorAll(".tab-panel"));

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

window.IM1.activateSidePanels = function activateSidePanels(root) {
  const scope = root || document;

  scope.querySelectorAll("[data-side-panel-open]").forEach((button) => {
    button.addEventListener("click", () => {
      const panel = document.querySelector(`[data-side-panel='${button.dataset.sidePanelOpen}']`);
      if (!panel) {
        return;
      }

      panel.hidden = false;
      panel.classList.add("is-open");
      panel.querySelector("button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])")?.focus();
    });
  });

  scope.querySelectorAll("[data-side-panel-close]").forEach((button) => {
    button.addEventListener("click", () => {
      const panel = button.closest("[data-side-panel]");
      if (panel) {
        panel.classList.remove("is-open");
        panel.hidden = true;
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
    const exportButton = grid.querySelector("[data-im1-grid-export]");
    const pageSize = Number.parseInt(grid.dataset.pageSize || "", 10);
    const preferenceKey = `im1-grid:${window.location.pathname}:${grid.dataset.gridKey || "default"}`;
    let page = 1;
    let visibleRows = rows;
    let storedPreferences = {};

    try {
      storedPreferences = JSON.parse(localStorage.getItem(preferenceKey) || "{}");
      if (search && storedPreferences.search && !search.value) {
        search.value = storedPreferences.search;
      }
    } catch {
      // Ignore corrupt local preferences.
    }

    const savePreferences = (changes) => {
      storedPreferences = { ...storedPreferences, ...changes };
      localStorage.setItem(preferenceKey, JSON.stringify(storedPreferences));
    };

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
      if (search) {
        savePreferences({ search: search.value });
      }
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
    rows.forEach((row) => {
      const open = () => {
        if (row.dataset.rowUrl) {
          window.location.href = row.dataset.rowUrl;
        }
      };

      row.addEventListener("dblclick", open);
      row.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
          open();
        }
      });
      row.addEventListener("contextmenu", (event) => {
        if (!row.dataset.rowUrl) {
          return;
        }

        event.preventDefault();
        const existing = document.querySelector(".im1-context-menu");
        existing?.remove();
        const menu = document.createElement("div");
        menu.className = "im1-context-menu";
        menu.style.left = `${event.clientX}px`;
        menu.style.top = `${event.clientY}px`;
        const link = document.createElement("a");
        link.href = row.dataset.rowUrl;
        link.textContent = "Edit";
        menu.appendChild(link);
        document.body.appendChild(menu);
      });
    });

    exportButton?.addEventListener("click", () => {
      const headers = Array.from(grid.querySelectorAll("thead th")).map((item) => item.textContent.trim());
      const lines = [headers.join(",")];
      visibleRows.forEach((row) => {
        lines.push(Array.from(row.children).map((cell) => `"${cell.textContent.trim().replaceAll('"', '""')}"`).join(","));
      });
      const blob = new Blob([lines.join("\n")], { type: "text/csv" });
      const link = document.createElement("a");
      link.href = URL.createObjectURL(blob);
      link.download = "im1-grid-export.csv";
      link.click();
      URL.revokeObjectURL(link.href);
    });

    const sortRows = (columnIndex, isDescending, button) => {
      grid.querySelectorAll(".im1-grid-sort").forEach((item) => item.classList.remove("is-ascending", "is-descending"));
      button?.classList.add(isDescending ? "is-descending" : "is-ascending");

      rows.sort((left, right) => {
        const leftValue = left.children[columnIndex]?.textContent?.trim() || "";
        const rightValue = right.children[columnIndex]?.textContent?.trim() || "";
        return leftValue.localeCompare(rightValue, undefined, { numeric: true, sensitivity: "base" }) * (isDescending ? -1 : 1);
      });

      rows.forEach((row) => tbody?.appendChild(row));
      applySearch();
    };

    grid.querySelectorAll("[data-sortable='true']").forEach((header) => {
      const button = header.querySelector(".im1-grid-sort");
      button?.addEventListener("click", () => {
        const columnIndex = header.cellIndex;
        const isDescending = button.classList.contains("is-ascending");
        savePreferences({ sortColumn: columnIndex, sortDescending: isDescending });
        sortRows(columnIndex, isDescending, button);
      });
    });

    if (Number.isInteger(storedPreferences.sortColumn)) {
      const header = Array.from(grid.querySelectorAll("[data-sortable='true']")).find((item) => item.cellIndex === storedPreferences.sortColumn);
      const button = header?.querySelector(".im1-grid-sort");
      if (button) {
        sortRows(storedPreferences.sortColumn, storedPreferences.sortDescending === true, button);
      }
    }

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

window.IM1.activateCompensationForms = function activateCompensationForms(root) {
  const scope = root || document;

  scope.querySelectorAll("[data-compensation-form]").forEach((form) => {
    const payrollType = form.querySelector("[data-payroll-type]");
    const fields = Array.from(form.querySelectorAll("[data-payroll-field]"));

    const render = () => {
      fields.forEach((field) => {
        const isVisible = field.dataset.payrollField === payrollType.value;
        field.hidden = !isVisible;
        field.querySelectorAll("input, select, textarea").forEach((input) => {
          if (!isVisible) {
            input.value = "";
          }
        });
      });
    };

    payrollType?.addEventListener("change", render);
    render();
  });
};

window.IM1.activateTabReturnFields = function activateTabReturnFields(root) {
  const scope = root || document;

  scope.querySelectorAll(".im1-page form").forEach((form) => {
    form.addEventListener("submit", () => {
      const page = form.closest(".im1-page");
      const activeTab = page?.querySelector("[data-tab-target].is-active")?.dataset.tabTarget;
      if (!activeTab) {
        return;
      }

      let input = form.querySelector("input[name='returnTab']");
      if (!input) {
        input = document.createElement("input");
        input.type = "hidden";
        input.name = "returnTab";
        form.appendChild(input);
      }

      input.value = activeTab;
    });
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

document.addEventListener("click", (event) => {
  if (!event.target.closest(".im1-context-menu")) {
    document.querySelector(".im1-context-menu")?.remove();
  }
});

document.addEventListener("DOMContentLoaded", () => {
  window.IM1.activateTabs(document);
  window.IM1.activateDialogs(document);
  window.IM1.activateSidePanels(document);
  window.IM1.activateDataGrids(document);
  window.IM1.activateMarketingHeader();
  window.IM1.activateCompensationForms(document);
  window.IM1.activateTabReturnFields(document);
});
