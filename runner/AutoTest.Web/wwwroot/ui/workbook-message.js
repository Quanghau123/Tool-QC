import { $, escapeHtml, json, readableError, state, translate } from './shared.js';

export function initializeWorkbookMessage({ showTool }) {
  async function scanMessages() {
    const sourcePath = $('messageSourcePath').value.trim();
    if (!sourcePath) return alert(translate('TQC-UI-015'));
    $('scanMessages').disabled = true;
    $('messageSummary').textContent = translate('TQC-UI-016');
    $('messageResults').innerHTML = '';
    try {
      state.messageScan = await json('/api/message-scans', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sourcePath }),
      });
      $('messageSummary').textContent = translate('TQC-UI-044', '', {
        files: state.messageScan.scannedFileCount,
        messages: state.messageScan.messageCount,
        modules: state.messageScan.moduleCount,
        skipped: state.messageScan.skippedFileCount,
      });
      $('messageSearch').disabled = false;
      $('downloadMessages').hidden = false;
      $('downloadMessages').href = `/api/message-scans/${encodeURIComponent(state.messageScan.scanId)}/excel`;
      renderMessages();
    } catch (error) {
      state.messageScan = null;
      $('messageSummary').textContent = error.message;
      $('messageSearch').disabled = true;
      $('downloadMessages').hidden = true;
    } finally {
      $('scanMessages').disabled = false;
    }
  }

  async function analyzeWorkbook() {
    const file = $('workbookFile').files[0];
    if (!file) return alert(translate('TQC-UI-018'));
    const form = new FormData();
    form.append('workbook', file);
    $('analyzeWorkbook').disabled = true;
    $('workbookSummary').className = 'message-summary checking';
    $('workbookSummary').textContent = translate('TQC-UI-019');
    try {
      const result = await json(`/api/projects/${encodeURIComponent($('project').value)}/workbooks/analyze`, {
        method: 'POST',
        body: form,
      });
      state.workbookUploadId = result.uploadId;
      state.workbookCases = result.result.analysis?.cases || result.result.cases || [];
      state.selectedWorkbookCases.clear();
      $('workbookSummary').className = 'message-summary passed';
      $('workbookSummary').textContent = `✓ ${result.originalFileName} · ${translate('TQC-UI-046', '', {
        cases: state.workbookCases.length,
      })} · SHA-256 ${result.result.workbook?.sha256 || result.result.sha256 || translate('TQC-UI-045')}`;
      renderWorkbookCases();
    } catch (error) {
      state.workbookUploadId = null;
      state.workbookCases = [];
      state.selectedWorkbookCases.clear();
      $('workbookSummary').className = 'message-summary failed';
      $('workbookSummary').textContent = readableError(error);
      renderWorkbookCases();
    } finally {
      $('analyzeWorkbook').disabled = false;
    }
  }

  function renderWorkbookCases() {
    const selectedCount = state.selectedWorkbookCases.size;
    const supported = state.workbookCases.filter((item) => item.supported);
    $('workbookSelectedCount').textContent = selectedCount;
    $('generateWorkbookCases').disabled = !state.workbookUploadId || selectedCount === 0;
    $('selectAllWorkbookCases').disabled = !state.workbookUploadId || supported.length === 0;
    $('clearWorkbookCases').disabled = selectedCount === 0;
    $('workbookCases').setAttribute('aria-disabled', state.workbookUploadId ? 'false' : 'true');
    $('workbookCases').innerHTML =
      state.workbookCases
        .map(
          (item) =>
            `<label class="workbook-case ${item.supported ? '' : 'blocked'}"><input type="checkbox" data-workbook-case="${escapeHtml(item.id)}" ${item.supported && state.workbookUploadId ? '' : 'disabled'} ${state.selectedWorkbookCases.has(item.id) ? 'checked' : ''}><span><b>${escapeHtml(item.title || item.id)}</b><small>${escapeHtml(item.sheet || item.family || 'VERIFY')} · ${escapeHtml(item.sourceRange || '')}</small>${item.blockedReason ? `<em>${escapeHtml(item.blockedReason)}</em>` : ''}</span></label>`,
        )
        .join('') || `<div class="empty">${translate('TQC-UI-020')}</div>`;
    document.querySelectorAll('[data-workbook-case]').forEach(
      (input) =>
        (input.onchange = () => {
          if (input.checked) state.selectedWorkbookCases.add(input.dataset.workbookCase);
          else state.selectedWorkbookCases.delete(input.dataset.workbookCase);
          renderWorkbookCases();
        }),
    );
  }

  async function generateWorkbookCases() {
    if (!state.workbookUploadId || state.selectedWorkbookCases.size === 0) return alert(translate('TQC-UI-021'));
    $('generateWorkbookCases').disabled = true;
    try {
      const response = await json(`/api/projects/${encodeURIComponent($('project').value)}/workbooks/generate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ uploadId: state.workbookUploadId, caseIds: [...state.selectedWorkbookCases] }),
      });
      const items = response.result?.items || [];
      const generated = items.filter((item) => item.generated);
      const blocked = items.filter((item) => !item.generated);
      state.workbookUploadId = null;
      state.selectedWorkbookCases.clear();
      $('workbookSummary').className = blocked.length ? 'message-summary warning' : 'message-summary passed';
      $('workbookSummary').textContent = translate('TQC-UI-047', '', {
        generated: generated.length,
        blocked: blocked.length,
        output: response.output,
      });
      if (generated.length === 1) {
        const item = generated[0];
        state.generatorReviewId = response.reviewId;
        state.generatorPreflightPassed = false;
        state.generatorReadyToSave = false;
        state.generatorDraftRunId = null;
        $('draftJson').value = item.json;
        $('draftArtifact').textContent = translate('TQC-UI-048', '', { output: response.output });
        $('draftGroup').value = response.group;
        $('draftFileName').value = `workbook-${item.caseId.replace(/[^a-z0-9]+/gi, '-').toLowerCase()}.json`;
        $('draftEditor').hidden = false;
        $('draftJsonPanel').hidden = true;
        $('toggleDraftJson').textContent = translate('TQC-UI-036');
        $('draftStatus').className = 'message-summary';
        $('draftStatus').textContent = translate('TQC-UI-049');
        $('draftReportInfo').hidden = true;
        $('runDraft').disabled = true;
        $('activateDraft').disabled = true;
        showTool('generatorTool');
        $('draftEditor').scrollIntoView({ behavior: 'smooth', block: 'start' });
      } else if (generated.length > 1) {
        $('workbookCases').innerHTML = items
          .map(
            (item) =>
              `<article class="workbook-case ${item.generated ? '' : 'blocked'}"><span><b>${escapeHtml(item.caseId)}</b><small>${item.generated ? translate('TQC-UI-050') : escapeHtml(item.error || translate('TQC-UI-051'))}</small></span></article>`,
          )
          .join('');
      }
    } catch (error) {
      $('workbookSummary').className = 'message-summary failed';
      $('workbookSummary').textContent = readableError(error);
    } finally {
      renderWorkbookCases();
    }
  }

  function renderMessages() {
    if (!state.messageScan) return;
    const query = $('messageSearch').value.trim().toLowerCase();
    $('messageResults').innerHTML =
      state.messageScan.modules
        .map((module) => {
          const messages = module.messages.filter(
            (item) =>
              !query ||
              `${module.name} ${item.key} ${item.vietnamese || ''} ${item.english || ''}`.toLowerCase().includes(query),
          );
          if (!messages.length) return '';
          return `<section class="message-module"><h2>${escapeHtml(module.name)} <span>${messages.length}/${module.count}</span></h2>${messages.map((item) => `<article class="message-row"><div><b>${escapeHtml(item.key)}</b><small>${item.sourceFiles.map(escapeHtml).join('<br>')}</small></div><p><strong>VI</strong>${escapeHtml(item.vietnamese || '—')}</p><p><strong>EN</strong>${escapeHtml(item.english || '—')}</p></article>`).join('')}</section>`;
        })
        .join('') || `<div class="empty">${translate('TQC-UI-017')}</div>`;
  }

  function selectAllWorkbookCases() {
    state.workbookCases.filter((item) => item.supported).forEach((item) => state.selectedWorkbookCases.add(item.id));
    renderWorkbookCases();
  }

  function clearWorkbookCases() {
    state.selectedWorkbookCases.clear();
    renderWorkbookCases();
  }

  return {
    analyzeWorkbook,
    clearWorkbookCases,
    generateWorkbookCases,
    renderMessages,
    renderWorkbookCases,
    scanMessages,
    selectAllWorkbookCases,
  };
}
