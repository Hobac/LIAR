const API_ENDPOINT = "http://localhost:8080/check";
const HEALTH_ENDPOINT = "http://localhost:8080/health";
const HEALTH_CHECK_INTERVAL = 5000;

const form = document.querySelector("#verification-form");
const input = document.querySelector("#source-text");
const count = document.querySelector("#character-count");
const button = document.querySelector("#verify-button");
const buttonLabel = button.querySelector(".button-label");
const message = document.querySelector("#message");
const highlightedText = document.querySelector("#highlighted-text");
const inputFrame = document.querySelector(".input-frame");
const resultStatistics = document.querySelector("#result-statistics");
const systemState = document.querySelector("#system-state");
const systemStateLabel = document.querySelector("#system-state-label");
const proofPopover = document.querySelector("#proof-popover");

let healthCheckInProgress = false;
let activeClaim = null;
let activeClaimLine = 0;
let hideProofTimer = null;
const claimProofs = new WeakMap();

const truthValues = {
  0: "unknown",
  1: "true",
  2: "false",
  3: "unknown",
  void: "unknown",
  true: "true",
  false: "false",
  unknown: "unknown",
};

input.addEventListener("input", () => {
  count.textContent = `${input.value.length.toLocaleString()} / 10,000`;
  hideMessage();
  clearVerifiedText();
});

input.addEventListener("scroll", () => {
  highlightedText.scrollTop = input.scrollTop;
  highlightedText.scrollLeft = input.scrollLeft;
  if (activeClaim) positionProofPopover(activeClaim);
});

highlightedText.addEventListener("mousedown", (event) => {
  const target = event.target;
  if (!(target instanceof Element)) return;

  const claim = target.closest(".claim-highlight");
  if (claim) {
    event.preventDefault();
    input.focus();

    const start = Number(claim.dataset.start);
    const end = Number(claim.dataset.end);
    const bounds = claim.getBoundingClientRect();
    const position = event.clientX < bounds.left + bounds.width / 2 ? start : end;
    input.setSelectionRange(position, position);
  }
});

highlightedText.addEventListener("pointerover", (event) => {
  const target = event.target;
  if (!(target instanceof Element)) return;

  const claim = target.closest(".claim-highlight");
  if (claim && !claim.contains(event.relatedTarget)) {
    showProofPopover(claim, event.clientX, event.clientY);
  }
});

highlightedText.addEventListener("pointerout", (event) => {
  const target = event.target;
  if (!(target instanceof Element)) return;

  const claim = target.closest(".claim-highlight");
  if (claim && !claim.contains(event.relatedTarget)) scheduleProofHide();
});

highlightedText.addEventListener("focusin", (event) => {
  const target = event.target;
  if (target instanceof Element) {
    const claim = target.closest(".claim-highlight");
    if (claim) showProofPopover(claim);
  }
});

highlightedText.addEventListener("focusout", scheduleProofHide);
proofPopover.addEventListener("pointerenter", cancelProofHide);
proofPopover.addEventListener("pointerleave", scheduleProofHide);
proofPopover.addEventListener("focusin", cancelProofHide);
proofPopover.addEventListener("focusout", scheduleProofHide);
window.addEventListener("resize", () => {
  if (activeClaim) positionProofPopover(activeClaim);
});
window.addEventListener(
  "scroll",
  () => {
    if (activeClaim) positionProofPopover(activeClaim);
  },
  true,
);

checkApiHealth();
setInterval(checkApiHealth, HEALTH_CHECK_INTERVAL);

form.addEventListener("submit", async (event) => {
  event.preventDefault();

  const sourceText = input.value;
  if (!sourceText.trim()) {
    showMessage("Enter at least one statement before starting verification.");
    input.focus();
    return;
  }

  setLoading(true);
  hideMessage();
  clearVerifiedText();

  try {
    const response = await fetch(API_ENDPOINT, {
      method: "POST",
      headers: { "Content-Type": "text/plain; charset=utf-8" },
      body: sourceText,
    });

    if (!response.ok) {
      throw new Error(`The verification service returned status ${response.status}.`);
    }

    const statements = await response.json();
    if (!Array.isArray(statements)) {
      throw new Error("The verification service returned an unexpected response.");
    }

    renderResults(sourceText, statements);
  } catch {
    showMessage("Could not verify — backend unavailable.");
  } finally {
    setLoading(false);
  }
});

function normaliseStatement(statement) {
  const rawTruthValue = statement.TruthValue ?? statement.truthValue ?? "unknown";
  const truthValue = truthValues[String(rawTruthValue).toLowerCase()] ?? "unknown";

  return {
    text: String(statement.Text ?? statement.text ?? "").trim(),
    truthValue,
    proof: String(statement.Proof ?? statement.proof ?? "").trim(),
  };
}

function renderResults(sourceText, rawStatements) {
  const statements = rawStatements.map(normaliseStatement).filter((statement) => statement.text);

  if (statements.length === 0) {
    showMessage("No verifiable statements were detected in this text.");
    return;
  }

  highlightedText.replaceChildren(buildHighlightedText(sourceText, statements));
  renderStatistics(statements);
  highlightedText.hidden = false;
  inputFrame.classList.add("verified");
  highlightedText.scrollTop = input.scrollTop;
  highlightedText.scrollLeft = input.scrollLeft;
  input.blur();
}

function renderStatistics(statements) {
  const totals = statements.reduce(
    (summary, statement) => {
      summary[statement.truthValue] += 1;
      return summary;
    },
    { true: 0, false: 0, unknown: 0 },
  );

  const items = [
    { label: "Claims", value: statements.length, className: "total" },
    { label: "True", value: totals.true, className: "true" },
    { label: "False", value: totals.false, className: "false" },
    { label: "Unknown", value: totals.unknown, className: "unknown" },
  ];

  resultStatistics.replaceChildren(
    ...items.map((item, index) => {
      const statistic = document.createElement("span");
      statistic.className = `statistic ${item.className}`;

      const label = document.createElement("span");
      label.className = "statistic-label";
      label.textContent = item.label;

      const value = document.createElement("strong");
      value.textContent = String(item.value);

      statistic.append(label, value);

      if (index > 0) {
        const percentage = document.createElement("span");
        percentage.className = "statistic-percentage";
        percentage.textContent = `${Math.round((item.value / statements.length) * 100)}%`;
        statistic.append(percentage);
      }

      return statistic;
    }),
  );

  resultStatistics.hidden = false;
}

function buildHighlightedText(sourceText, statements) {
  const fragment = document.createDocumentFragment();
  const matches = [];

  statements.forEach((statement, statementIndex) => {
    let searchFrom = 0;
    while (searchFrom < sourceText.length) {
      const index = sourceText.indexOf(statement.text, searchFrom);
      if (index === -1) break;
      matches.push({ start: index, end: index + statement.text.length, statement, statementIndex });
      searchFrom = index + statement.text.length;
    }
  });

  matches.sort((a, b) => a.start - b.start || b.end - a.end || a.statementIndex - b.statementIndex);

  let cursor = 0;
  matches.forEach((match) => {
    if (match.start < cursor) return;
    fragment.append(document.createTextNode(sourceText.slice(cursor, match.start)));

    const mark = document.createElement("mark");
    mark.className = `claim-highlight ${match.statement.truthValue}`;
    mark.tabIndex = 0;
    mark.dataset.start = String(match.start);
    mark.dataset.end = String(match.end);

    const claimText = document.createElement("span");
    claimText.textContent = sourceText.slice(match.start, match.end);

    mark.setAttribute("aria-describedby", "proof-popover");
    claimProofs.set(mark, match.statement);
    mark.append(claimText);
    fragment.append(mark);
    cursor = match.end;
  });

  fragment.append(document.createTextNode(sourceText.slice(cursor)));
  return fragment;
}

function showProofPopover(claim, pointerX, pointerY) {
  cancelProofHide();
  activeClaim = claim;
  activeClaimLine = findClaimLine(claim, pointerX, pointerY);
  const statement = claimProofs.get(claim);
  if (!statement) return;

  const status = document.createElement("strong");
  status.className = "proof-status";
  status.textContent = capitalise(statement.truthValue);

  const proof = document.createElement("span");
  proof.className = "proof-copy";
  appendLinkedText(proof, statement.proof || "No proof was returned by the API.");

  proofPopover.replaceChildren(status, proof);
  proofPopover.classList.remove("true", "false", "unknown");
  proofPopover.classList.add(statement.truthValue);
  proofPopover.hidden = false;
  positionProofPopover(claim);
}

function positionProofPopover(claim) {
  if (proofPopover.hidden || !claim.isConnected) return;

  const margin = 12;
  const gap = 10;
  const claimLines = Array.from(claim.getClientRects());
  const claimBounds = claimLines[activeClaimLine] ?? claim.getBoundingClientRect();
  const popoverBounds = proofPopover.getBoundingClientRect();
  const maxLeft = Math.max(margin, window.innerWidth - popoverBounds.width - margin);
  const left = Math.min(Math.max(claimBounds.left, margin), maxLeft);

  let top = claimBounds.bottom + gap;
  let opensAbove = false;
  if (top + popoverBounds.height > window.innerHeight - margin) {
    top = claimBounds.top - popoverBounds.height - gap;
    opensAbove = true;
  }
  top = Math.max(margin, Math.min(top, window.innerHeight - popoverBounds.height - margin));

  const claimCenter = claimBounds.left + claimBounds.width / 2;
  const arrowLeft = Math.min(
    Math.max(claimCenter - left - 9, 10),
    popoverBounds.width - 28,
  );

  proofPopover.style.left = `${left}px`;
  proofPopover.style.top = `${top}px`;
  proofPopover.style.setProperty("--arrow-left", `${arrowLeft}px`);
  proofPopover.classList.toggle("above", opensAbove);
  proofPopover.classList.toggle("below", !opensAbove);
}

function findClaimLine(claim, pointerX, pointerY) {
  const lines = Array.from(claim.getClientRects());
  if (!lines.length || pointerX === undefined || pointerY === undefined) return 0;

  const directMatch = lines.findIndex(
    (line) =>
      pointerY >= line.top &&
      pointerY <= line.bottom &&
      pointerX >= line.left &&
      pointerX <= line.right,
  );
  if (directMatch >= 0) return directMatch;

  let closestLine = 0;
  let closestDistance = Number.POSITIVE_INFINITY;
  lines.forEach((line, index) => {
    const centerY = line.top + line.height / 2;
    const distance = Math.abs(pointerY - centerY);
    if (distance < closestDistance) {
      closestDistance = distance;
      closestLine = index;
    }
  });
  return closestLine;
}

function scheduleProofHide() {
  cancelProofHide();
  hideProofTimer = setTimeout(hideProofPopover, 180);
}

function cancelProofHide() {
  if (hideProofTimer !== null) {
    clearTimeout(hideProofTimer);
    hideProofTimer = null;
  }
}

function hideProofPopover() {
  proofPopover.hidden = true;
  proofPopover.replaceChildren();
  activeClaim = null;
  activeClaimLine = 0;
  hideProofTimer = null;
}

function appendLinkedText(container, text) {
  const urlPattern = /https?:\/\/[^\s<>"']+/gi;
  let cursor = 0;

  for (const match of text.matchAll(urlPattern)) {
    container.append(document.createTextNode(text.slice(cursor, match.index)));

    const rawUrl = match[0];
    const url = rawUrl.replace(/[),.;!?]+$/, "");
    const trailingText = rawUrl.slice(url.length);
    const link = document.createElement("a");
    link.href = url;
    link.target = "_blank";
    link.rel = "noopener noreferrer";
    link.textContent = url;
    container.append(link, document.createTextNode(trailingText));
    cursor = match.index + rawUrl.length;
  }

  container.append(document.createTextNode(text.slice(cursor)));
}

async function checkApiHealth() {
  if (healthCheckInProgress) return;
  healthCheckInProgress = true;

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 3000);

  try {
    const response = await fetch(HEALTH_ENDPOINT, {
      method: "GET",
      cache: "no-store",
      signal: controller.signal,
    });
    setSystemState(response.ok ? "online" : "offline");
  } catch {
    setSystemState("offline");
  } finally {
    clearTimeout(timeout);
    healthCheckInProgress = false;
  }
}

function setSystemState(state) {
  systemState.classList.remove("checking", "online", "offline");
  systemState.classList.add(state);

  systemStateLabel.textContent =
    state === "online" ? "Verification system online" : "Verification system offline";
}

function setLoading(isLoading) {
  button.disabled = isLoading;
  button.setAttribute("aria-busy", String(isLoading));
  buttonLabel.textContent = isLoading ? "Verifying…" : "Verify statements";
}

function showMessage(text) {
  message.textContent = text;
  message.hidden = false;
}

function hideMessage() {
  message.hidden = true;
  message.textContent = "";
}

function clearVerifiedText() {
  if (highlightedText.hidden) return;
  highlightedText.hidden = true;
  highlightedText.replaceChildren();
  resultStatistics.hidden = true;
  resultStatistics.replaceChildren();
  inputFrame.classList.remove("verified");
  hideProofPopover();
}

function capitalise(value) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
