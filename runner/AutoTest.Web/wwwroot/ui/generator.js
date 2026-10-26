import { $, escapeHtml, json, readableError, state, translate } from './shared.js';

export function initializeGenerator({ loadTests, poll, recoverUi, repairGeneratedDraft }) {
  async function loadGeneratorStatus() {
    const panel = $('generatorProvider');
    if (!panel) return;
    const provider = $('generatorAiProvider')?.value || 'codex-cli';
    panel.className = 'provider-status';
    panel.textContent = translate('TQC-UI-115', '', { provider });
    try {
      const status = await json(`/api/generator/status?provider=${encodeURIComponent(provider)}`);
      if (status.available && status.authenticated) {
        panel.className = 'provider-status passed';
        panel.textContent = translate('TQC-UI-116', '', {
          provider: status.provider,
          version: status.version || translate('TQC-UI-124'),
        });
      } else if (status.available) {
        panel.className = 'provider-status warning';
        panel.textContent = translate('TQC-UI-117', '', { provider: status.provider });
      } else {
        panel.className = 'provider-status failed';
        panel.textContent = translate('TQC-UI-118', '', {
          reason: status.message || translate('TQC-UI-125', '', { provider }),
        });
      }
    } catch (error) {
      panel.className = 'provider-status failed';
      panel.textContent = translate('TQC-UI-098', '', { detail: error.message });
    }
  }
  async function loadSourceModules() {
    const sourcePath = $('sourcePath').value.trim(),
      select = $('analysisModule');
    if (!sourcePath) {
      select.innerHTML = `<option value="">${translate('TQC-UI-022')}</option>`;
      return;
    }
    select.disabled = true;
    try {
      const modules = await json(`/api/projects/${encodeURIComponent($('project').value)}/generate/modules`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sourcePath }),
      });
      select.innerHTML =
        `<option value="">${translate('TQC-UI-023')}</option>` +
        modules.map((module) => `<option value="${escapeHtml(module)}">${escapeHtml(module)}</option>`).join('');
    } catch (error) {
      select.innerHTML = `<option value="">${translate('TQC-UI-024')}</option>`;
      $('generatorSummary').className = 'message-summary failed';
      $('generatorSummary').textContent = readableError(error);
    } finally {
      select.disabled = false;
    }
  }
  async function analyzeSource() {
    const sourcePath = $('sourcePath').value.trim();
    if (!sourcePath) return alert(translate('TQC-UI-015'));
    if (!$('project').value) {
      const recovered = await recoverUi();
      if (!recovered) return;
    }
    const mode = $('analysisMode').value,
      scope = $('analysisScope').value,
      module = scope === 'module' ? $('analysisModule').value : null;
    if (scope === 'module' && !module) return alert(translate('TQC-UI-025'));
    if (mode !== 'local' && scope === 'all') return alert(translate('TQC-UI-099'));
    localStorage.setItem('toolqc.sourcePath', sourcePath);
    state.generatorAnalysis = null;
    state.selectedEndpoints = new Set();
    state.selectedEndpointOrder = [];
    state.expandedEndpointModules = new Set();
    $('endpointResults').innerHTML = '';
    $('endpointSearch').disabled = true;
    $('generateTests').disabled = true;
    $('analyzeSource').disabled = true;
    $('generatorSummary').className = 'message-summary checking';
    const analysisScope = module ? translate('TQC-UI-103', '', { module }) : translate('TQC-UI-102');
    $('generatorSummary').textContent =
      mode === 'local'
        ? translate('TQC-UI-100', '', { scope: analysisScope })
        : translate('TQC-UI-101', '', { scope: analysisScope });
    try {
      state.generatorAnalysis = await json(`/api/projects/${encodeURIComponent($('project').value)}/generate/analyze`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sourcePath, mode, module, provider: $('generatorAiProvider').value }),
      });
      $('connection').textContent = translate('TQC-UI-002');
      $('endpointSearch').disabled = false;
      const auth = state.generatorAnalysis.authentication || {},
        login = auth.loginEndpoints?.[0],
        userApis = auth.userCreationEndpoints?.length || 0,
        authText = login
          ? ` · ${translate('TQC-UI-106', '', { method: login.method, path: login.path, count: userApis })}`
          : ` · ${translate('TQC-UI-107')}`,
        provider = state.generatorAnalysis.aiUsed
          ? translate('TQC-UI-104')
          : state.generatorAnalysis.fellBack
            ? translate('TQC-UI-105')
            : 'Roslyn';
      $('generatorSummary').className = state.generatorAnalysis.fellBack
        ? 'message-summary warning'
        : 'message-summary passed';
      $('generatorSummary').textContent = `✓ ${provider} · ${translate('TQC-UI-108', '', {
        files: state.generatorAnalysis.scannedFileCount,
        endpoints: state.generatorAnalysis.endpoints.length,
        authentication: authText,
        warnings: state.generatorAnalysis.warnings.length,
        message: state.generatorAnalysis.analysisMessage || '',
      })}`;
      renderEndpoints();
    } catch (error) {
      state.generatorAnalysis = null;
      $('generatorSummary').className = 'message-summary failed';
      $('generatorSummary').textContent = readableError(error);
      if (error.code === 'TQC-UI-001' || error.code === 'TQC-UI-405') setTimeout(recoverUi, 1500);
    } finally {
      $('analyzeSource').disabled = false;
    }
  }
  function endpointModule(item) {
    const controller = (item.controller || translate('TQC-UI-057')).replace(/Controller$/, '');
    const parts = (item.sourceFile || '').split(/[\\/]/);
    const moduleIndex = parts.findIndex((part) => part.toLowerCase() === 'controllers');
    const folder = moduleIndex >= 0 && parts.length > moduleIndex + 2 ? parts[moduleIndex + 1] : controller;
    return folder || controller;
  }
  function selectEndpoint(id, checked) {
    if (checked) {
      state.selectedEndpoints.add(id);
      if (!state.selectedEndpointOrder.includes(id)) state.selectedEndpointOrder.push(id);
    } else {
      state.selectedEndpoints.delete(id);
      state.selectedEndpointOrder = state.selectedEndpointOrder.filter((item) => item !== id);
    }
  }
  function renderEndpoints() {
    if (!state.generatorAnalysis) return;
    const query = $('endpointSearch').value.trim().toLowerCase(),
      insights = state.generatorAnalysis.aiInsights || {};
    const endpoints = state.generatorAnalysis.endpoints.filter(
      (item) =>
        !query ||
        `${item.method} ${item.path} ${item.controller} ${item.action} ${endpointModule(item)} ${insights[item.id]?.summary || ''} ${(insights[item.id]?.businessRules || []).map((rule) => rule.rule).join(' ')}`
          .toLowerCase()
          .includes(query),
    );
    const grouped = endpoints.reduce((result, item) => {
      const module = insights[item.id]?.module || endpointModule(item);
      (result[module] ??= []).push(item);
      return result;
    }, {});
    $('endpointSelectedCount').textContent = state.selectedEndpoints.size;
    $('generateTests').disabled = !state.selectedEndpoints.size;
    $('endpointResults').innerHTML =
      Object.keys(grouped)
        .sort()
        .map((module) => {
          const items = grouped[module];
          const open = query || state.expandedEndpointModules.has(module);
          const selected = items.filter((item) => state.selectedEndpoints.has(item.id)).length;
          return `<section class="endpoint-module ${open ? 'open' : ''}"><div class="endpoint-module-header"><button type="button" class="endpoint-module-toggle" data-endpoint-module="${escapeHtml(module)}"><span class="chevron">›</span><span class="folder-icon">📁</span><strong>${escapeHtml(module)}</strong><span>${items.length} API</span></button><label><input type="checkbox" data-endpoint-module-select="${escapeHtml(module)}" ${selected === items.length ? 'checked' : ''}><span>${selected}/${items.length}</span></label></div><div class="endpoint-module-content">${items
            .map((item) => {
              const insight = insights[item.id];
              return `<label class="endpoint-card"><span class="endpoint-sequence"><input type="checkbox" data-endpoint-id="${escapeHtml(item.id)}" ${state.selectedEndpoints.has(item.id) ? 'checked' : ''}>${state.selectedEndpointOrder.indexOf(item.id) >= 0 ? `<strong>${state.selectedEndpointOrder.indexOf(item.id) + 1}</strong>` : ''}</span><div><div class="endpoint-title"><span class="method ${item.method.toLowerCase()}">${escapeHtml(item.method)}</span><b>${escapeHtml(item.path)}</b></div><small>${escapeHtml(item.controller)}.${escapeHtml(item.action)} · ${escapeHtml(item.sourceFile)}:${item.sourceLine}</small><div class="contract-badges"><span>${translate(item.requiresAuthentication ? 'TQC-UI-109' : 'TQC-UI-110')}</span><span>${translate('TQC-UI-111', '', { count: item.parameters.length })}</span><span>${translate('TQC-UI-112', '', { count: item.validations.length })}</span><span>${translate('TQC-UI-113', '', { count: item.serviceCalls.length })}</span><span>${translate('TQC-UI-114', '', { count: item.branches.length })}</span><span>${item.responseStatuses.length ? item.responseStatuses.join(', ') : translate('TQC-UI-115')}</span></div>${insight ? `<div class="ai-insight"><b>AI · ${insight.confidence}%</b><span>${escapeHtml(insight.summary)}</span>${insight.dependencies?.length ? `<small>${translate('TQC-UI-116', '', { items: insight.dependencies.map(escapeHtml).join(', ') })}</small>` : ''}${insight.suggestedPredecessors?.length ? `<small>${translate('TQC-UI-117', '', { items: insight.suggestedPredecessors.map(escapeHtml).join(', ') })}</small>` : ''}${insight.risks?.length ? `<small>${translate('TQC-UI-118', '', { items: insight.risks.map(escapeHtml).join(', ') })}</small>` : ''}${insight.businessRules?.length ? `<details class="business-rules"><summary>${translate('TQC-UI-119', '', { count: insight.businessRules.length })}</summary>${insight.businessRules.map((rule) => `<div><b>${escapeHtml(rule.rule)}</b><span>${translate('TQC-UI-120', '', { value: escapeHtml(rule.condition) })}</span><span>${translate('TQC-UI-121', '', { value: escapeHtml(rule.expectedBehavior) })}</span><small>${escapeHtml(rule.evidenceFile)}:${rule.evidenceLine}</small><em>Test: ${escapeHtml(rule.testSuggestion)}</em></div>`).join('')}</details>` : ''}${insight.sideEffects?.length ? `<small>Side effect: ${insight.sideEffects.map(escapeHtml).join(', ')}</small>` : ''}</div>` : ''}</div></label>`;
            })
            .join('')}</div></section>`;
        })
        .join('') || `<div class="empty">${translate('TQC-UI-027')}</div>`;
    document.querySelectorAll('[data-endpoint-id]').forEach(
      (box) =>
        (box.onchange = () => {
          selectEndpoint(box.dataset.endpointId, box.checked);
          renderEndpoints();
        }),
    );
    document.querySelectorAll('[data-endpoint-module]').forEach(
      (button) =>
        (button.onclick = () => {
          const module = button.dataset.endpointModule;
          state.expandedEndpointModules.has(module)
            ? state.expandedEndpointModules.delete(module)
            : state.expandedEndpointModules.add(module);
          renderEndpoints();
        }),
    );
    document.querySelectorAll('[data-endpoint-module-select]').forEach(
      (box) =>
        (box.onchange = () => {
          grouped[box.dataset.endpointModuleSelect].forEach((item) => selectEndpoint(item.id, box.checked));
          renderEndpoints();
        }),
    );
  }
  async function generateTests() {
    const sourcePath = $('sourcePath').value.trim();
    if (!sourcePath || !state.selectedEndpoints.size) return alert(translate('TQC-UI-026'));
    const started = Date.now(),
      mode = $('generatorMode').value;
    $('generatorSummary').className = 'message-summary checking';
    $('generatorSummary').textContent = mode === 'local' ? translate('TQC-UI-124') : translate('TQC-UI-125');
    $('generateTests').disabled = true;
    try {
      const result = await json(`/api/projects/${encodeURIComponent($('project').value)}/generate/draft`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sourcePath,
          endpointIds: state.selectedEndpointOrder.filter((id) => state.selectedEndpoints.has(id)),
          mode,
          provider: $('generatorAiProvider').value,
        }),
      });
      state.generatorReviewId = result.reviewId;
      state.generatorPreflightPassed = false;
      state.generatorReadyToSave = false;
      state.generatorDraftRunId = null;
      $('draftJson').value = result.json;
      $('draftEditor').hidden = false;
      $('draftJsonPanel').hidden = true;
      $('toggleDraftJson').textContent = translate('TQC-UI-036');
      $('draftArtifact').textContent = translate('TQC-UI-048', '', { output: result.output });
      $('draftStatus').className = 'message-summary';
      $('draftStatus').textContent = translate('TQC-UI-126');
      $('draftReportInfo').hidden = true;
      $('runDraft').disabled = true;
      $('activateDraft').disabled = true;
      $('generatorProvider').className = `provider-status ${result.fellBack ? 'warning' : 'passed'}`;
      $('generatorProvider').textContent = result.fellBack
        ? translate('TQC-UI-127', '', { provider: result.provider })
        : translate('TQC-UI-128', '', {
            provider: result.provider,
            ai: result.aiUsed ? translate('TQC-UI-129') : '',
          });
      $('generatorSummary').className = result.fellBack ? 'message-summary warning' : 'message-summary passed';
      $('generatorSummary').textContent = `✓ ${translate('TQC-UI-130', '', {
        seconds: ((Date.now() - started) / 1000).toFixed(1),
        message: result.message,
        output: result.output,
      })}`;
      $('draftEditor').scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (error) {
      $('generatorProvider').className = 'provider-status failed';
      $('generatorProvider').textContent = translate('TQC-UI-131');
      $('generatorSummary').className = 'message-summary failed';
      $('generatorSummary').textContent = readableError(error);
    } finally {
      $('generateTests').disabled = !state.selectedEndpoints.size;
    }
  }
  async function preflightDraft() {
    if (!state.generatorReviewId) return;
    const started = Date.now();
    $('draftStatus').className = 'message-summary checking';
    $('draftStatus').textContent = translate('TQC-UI-132', '', { seconds: 0 });
    $('preflightDraft').disabled = true;
    const progress = setInterval(() => {
      $('draftStatus').textContent = translate('TQC-UI-132', '', {
        seconds: Math.floor((Date.now() - started) / 1000),
      });
    }, 1000);
    try {
      const result = await json(`/api/projects/${encodeURIComponent($('project').value)}/generate/preflight`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          reviewId: state.generatorReviewId,
          json: $('draftJson').value,
          sequentialChain: $('generatorSequentialChain').checked,
        }),
      });
      state.generatorPreflightPassed = true;
      state.generatorReadyToSave = result.readyToSave;
      $('runDraft').disabled = false;
      $('activateDraft').disabled = !result.readyToSave;
      if (result.readyToSave) {
        $('draftStatus').className = 'message-summary passed';
        $('draftStatus').textContent = `✓ ${translate('TQC-UI-133', '', {
          seconds: ((Date.now() - started) / 1000).toFixed(1),
        })}`;
      } else {
        $('draftStatus').className = 'message-summary warning';
        $('draftStatus').textContent = `⚠ ${translate('TQC-UI-134', '', {
          warnings: result.warnings.map((item) => `• ${item}`).join('\n'),
        })}`;
      }
    } catch (error) {
      state.generatorPreflightPassed = false;
      state.generatorReadyToSave = false;
      $('runDraft').disabled = true;
      $('activateDraft').disabled = true;
      if (error.code === 'TQC-GEN-005' && $('draftRepairMode')?.value === 'auto' && repairGeneratedDraft) {
        clearInterval(progress);
        $('preflightDraft').disabled = false;
        await repairGeneratedDraft({ automatic: true });
        return;
      }
      $('draftStatus').className = 'message-summary failed';
      $('draftStatus').textContent = `✕ ${translate('TQC-UI-135', '', {
        seconds: ((Date.now() - started) / 1000).toFixed(1),
        error: readableError(error),
      })}`;
    } finally {
      clearInterval(progress);
      $('preflightDraft').disabled = false;
    }
  }
  function readApiError(error) {
    try {
      const parsed = JSON.parse(error.message);
      return [parsed.code, parsed.message, parsed.detail].filter(Boolean).join('\n');
    } catch {
      return error.message;
    }
  }
  async function runDraft() {
    if (!state.generatorPreflightPassed) return alert(translate('TQC-UI-028'));
    if (state.selectedEndpointOrder.length > 1 && !$('generatorSequentialChain').checked)
      return alert(translate('TQC-UI-136'));
    if (!confirm(translate('TQC-UI-137'))) return;
    $('runDraft').disabled = true;
    $('draftReportInfo').hidden = true;
    $('draftStatus').className = 'message-summary checking';
    $('draftStatus').textContent = translate('TQC-UI-030');
    try {
      const result = await json(`/api/projects/${encodeURIComponent($('project').value)}/generate/run`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          reviewId: state.generatorReviewId,
          json: $('draftJson').value,
          sequentialChain: $('generatorSequentialChain').checked,
        }),
      });
      state.generatorDraftRunId = result.runId;
      state.runId = result.runId;
      poll();
    } catch (error) {
      $('draftStatus').className = 'message-summary failed';
      $('draftStatus').textContent = readableError(error);
      $('runDraft').disabled = false;
    }
  }
  async function activateDraft() {
    if (!state.generatorReadyToSave) return alert(translate('TQC-UI-029'));
    $('activateDraft').disabled = true;
    const request = {
      reviewId: state.generatorReviewId,
      json: $('draftJson').value,
      group: $('draftGroup').value,
      fileName: $('draftFileName').value,
    };
    try {
      let result;
      try {
        result = await json(`/api/projects/${encodeURIComponent($('project').value)}/generate/activate`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(request),
        });
      } catch (error) {
        if (error.status !== 409 || !error.payload?.currentHash) throw error;
        const target = error.payload.output || `${request.group}/${request.fileName}.json`;
        if (
          !confirm(
            translate('TQC-UI-138', '', {
              target,
              status: error.payload.status || 'update',
              hash: error.payload.currentHash,
            }),
          )
        )
          return;
        result = await json(`/api/projects/${encodeURIComponent($('project').value)}/generate/activate`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ ...request, confirmOverwrite: true, expectedSha256: error.payload.currentHash }),
        });
      }
      $('draftStatus').className = 'message-summary passed';
      $('draftStatus').textContent = translate('TQC-UI-139', '', { output: result.output });
      await loadTests();
    } catch (error) {
      $('draftStatus').className = 'message-summary failed';
      $('draftStatus').textContent = readableError(error);
    } finally {
      $('activateDraft').disabled = !state.generatorReadyToSave;
    }
  }

  return {
    activateDraft,
    analyzeSource,
    generateTests,
    loadGeneratorStatus,
    loadSourceModules,
    preflightDraft,
    renderEndpoints,
    runDraft,
  };
}
