import { $, escapeHtml, json, readableError, state } from './shared.js';

export function initializeGenerator({ loadTests, poll, recoverUi, repairGeneratedDraft }) {
  async function loadGeneratorStatus() {
    const panel = $('generatorProvider');
    if (!panel) return;
    const provider = $('generatorAiProvider')?.value || 'codex-cli';
    panel.className = 'provider-status';
    panel.textContent = `Đang kiểm tra ${provider}...`;
    try {
      const status = await json(`/api/generator/status?provider=${encodeURIComponent(provider)}`);
      if (status.available && status.authenticated) {
        panel.className = 'provider-status passed';
        panel.textContent = `AI sẵn sàng · ${status.provider} · ${status.version || 'đã đăng nhập'}`;
      } else if (status.available) {
        panel.className = 'provider-status warning';
        panel.textContent = `Đã tìm thấy ${status.provider} nhưng chưa đăng nhập.`;
      } else {
        panel.className = 'provider-status failed';
        panel.textContent = `AI không khả dụng · ${status.message || `Không tìm thấy ${provider}`}`;
      }
    } catch (error) {
      panel.className = 'provider-status failed';
      panel.textContent = `Không kiểm tra được AI · ${error.message}`;
    }
  }
  async function loadSourceModules() {
    const sourcePath = $('sourcePath').value.trim(),
      select = $('analysisModule');
    if (!sourcePath) {
      select.innerHTML = '<option value="">Nhập đường dẫn source trước</option>';
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
        '<option value="">Chọn module</option>' +
        modules.map((module) => `<option value="${escapeHtml(module)}">${escapeHtml(module)}</option>`).join('');
    } catch (error) {
      select.innerHTML = '<option value="">Không tải được module</option>';
      $('generatorSummary').className = 'message-summary failed';
      $('generatorSummary').textContent = readableError(error);
    } finally {
      select.disabled = false;
    }
  }
  async function analyzeSource() {
    const sourcePath = $('sourcePath').value.trim();
    if (!sourcePath) return alert('Hãy nhập đường dẫn source.');
    if (!$('project').value) {
      const recovered = await recoverUi();
      if (!recovered) return;
    }
    const mode = $('analysisMode').value,
      scope = $('analysisScope').value,
      module = scope === 'module' ? $('analysisModule').value : null;
    if (scope === 'module' && !module) return alert('Hãy chọn module cần phân tích.');
    if (mode !== 'local' && scope === 'all')
      return alert(
        'Phân tích AI chuyên sâu cần chọn Theo module để AI đọc đầy đủ rule nghiệp vụ và không vượt thời gian.',
      );
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
    $('generatorSummary').textContent =
      mode === 'local'
        ? `⏳ Đang phân tích ${module ? `module ${module}` : 'toàn bộ source'} bằng Roslyn...`
        : `⏳ Roslyn đang quét contract, sau đó AI phân tích chuyên sâu ${module ? `module ${module}` : 'toàn bộ source'}...`;
    try {
      state.generatorAnalysis = await json(`/api/projects/${encodeURIComponent($('project').value)}/generate/analyze`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sourcePath, mode, module, provider: $('generatorAiProvider').value }),
      });
      $('connection').textContent = 'Sẵn sàng';
      $('endpointSearch').disabled = false;
      const auth = state.generatorAnalysis.authentication || {},
        login = auth.loginEndpoints?.[0],
        userApis = auth.userCreationEndpoints?.length || 0,
        authText = login
          ? ` · Login: ${login.method} ${login.path} · ${userApis} API tạo user`
          : ' · Chưa nhận diện được API login',
        provider = state.generatorAnalysis.aiUsed
          ? 'Roslyn + AI'
          : state.generatorAnalysis.fellBack
            ? 'Roslyn (AI fallback)'
            : 'Roslyn';
      $('generatorSummary').className = state.generatorAnalysis.fellBack
        ? 'message-summary warning'
        : 'message-summary passed';
      $('generatorSummary').textContent =
        `✓ ${provider} · ${module ? `module ${module}` : 'toàn bộ source'} · đã quét ${state.generatorAnalysis.scannedFileCount} file · phát hiện ${state.generatorAnalysis.endpoints.length} endpoint${authText} · ${state.generatorAnalysis.warnings.length} cảnh báo\n${state.generatorAnalysis.analysisMessage || ''}`;
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
    const controller = (item.controller || 'Khác').replace(/Controller$/, '');
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
              return `<label class="endpoint-card"><span class="endpoint-sequence"><input type="checkbox" data-endpoint-id="${escapeHtml(item.id)}" ${state.selectedEndpoints.has(item.id) ? 'checked' : ''}>${state.selectedEndpointOrder.indexOf(item.id) >= 0 ? `<strong>${state.selectedEndpointOrder.indexOf(item.id) + 1}</strong>` : ''}</span><div><div class="endpoint-title"><span class="method ${item.method.toLowerCase()}">${escapeHtml(item.method)}</span><b>${escapeHtml(item.path)}</b></div><small>${escapeHtml(item.controller)}.${escapeHtml(item.action)} · ${escapeHtml(item.sourceFile)}:${item.sourceLine}</small><div class="contract-badges"><span>${item.requiresAuthentication ? 'Cần xác thực' : 'Không cần xác thực'}</span><span>${item.parameters.length} tham số</span><span>${item.validations.length} validation</span><span>${item.serviceCalls.length} service call</span><span>${item.branches.length} nhánh</span><span>${item.responseStatuses.length ? item.responseStatuses.join(', ') : 'Chưa có response metadata'}</span></div>${insight ? `<div class="ai-insight"><b>AI · ${insight.confidence}%</b><span>${escapeHtml(insight.summary)}</span>${insight.dependencies?.length ? `<small>Phụ thuộc: ${insight.dependencies.map(escapeHtml).join(', ')}</small>` : ''}${insight.suggestedPredecessors?.length ? `<small>Chạy sau: ${insight.suggestedPredecessors.map(escapeHtml).join(', ')}</small>` : ''}${insight.risks?.length ? `<small>Rủi ro: ${insight.risks.map(escapeHtml).join(', ')}</small>` : ''}${insight.businessRules?.length ? `<details class="business-rules"><summary>${insight.businessRules.length} rule nghiệp vụ</summary>${insight.businessRules.map((rule) => `<div><b>${escapeHtml(rule.rule)}</b><span>Điều kiện: ${escapeHtml(rule.condition)}</span><span>Kết quả: ${escapeHtml(rule.expectedBehavior)}</span><small>${escapeHtml(rule.evidenceFile)}:${rule.evidenceLine}</small><em>Test: ${escapeHtml(rule.testSuggestion)}</em></div>`).join('')}</details>` : ''}${insight.sideEffects?.length ? `<small>Side effect: ${insight.sideEffects.map(escapeHtml).join(', ')}</small>` : ''}</div>` : ''}</div></label>`;
            })
            .join('')}</div></section>`;
        })
        .join('') || '<div class="empty">Không tìm thấy endpoint phù hợp.</div>';
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
    if (!sourcePath || !state.selectedEndpoints.size) return alert('Hãy phân tích source và chọn endpoint.');
    const started = Date.now(),
      mode = $('generatorMode').value;
    $('generatorSummary').className = 'message-summary checking';
    $('generatorSummary').textContent =
      mode === 'local' ? '⏳ Đang sinh bản nháp bằng Roslyn Local...' : '⏳ Đang nhờ AI đọc source và sinh testcase...';
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
      $('toggleDraftJson').textContent = 'Xem JSON';
      $('draftArtifact').textContent = `File tạm: ${result.output}`;
      $('draftStatus').className = 'message-summary';
      $('draftStatus').textContent = 'Bản nháp đã tạo. Nhấn “1. Kiểm tra” để kiểm tra khả năng chạy.';
      $('draftReportInfo').hidden = true;
      $('runDraft').disabled = true;
      $('activateDraft').disabled = true;
      $('generatorProvider').className = `provider-status ${result.fellBack ? 'warning' : 'passed'}`;
      $('generatorProvider').textContent = result.fellBack
        ? `AI không khả dụng · Đã tự dùng ${result.provider}`
        : `Đã dùng ${result.provider}${result.aiUsed ? ' (AI)' : ''}`;
      $('generatorSummary').className = result.fellBack ? 'message-summary warning' : 'message-summary passed';
      $('generatorSummary').textContent =
        `✓ Đã sinh bản nháp sau ${((Date.now() - started) / 1000).toFixed(1)} giây. ${result.message}\nFile: ${result.output}`;
      $('draftEditor').scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (error) {
      $('generatorProvider').className = 'provider-status failed';
      $('generatorProvider').textContent = 'Provider đã chọn không thể hoàn thành.';
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
    $('draftStatus').textContent = '⏳ Đang kiểm tra khả năng chạy · 0 giây';
    $('preflightDraft').disabled = true;
    const progress = setInterval(() => {
      $('draftStatus').textContent =
        `⏳ Đang kiểm tra khả năng chạy · ${Math.floor((Date.now() - started) / 1000)} giây\nKhông gọi API hoặc thay đổi dữ liệu.`;
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
        $('draftStatus').textContent =
          `✓ Testcase có thể chạy thử và lưu chính thức. Hoàn tất sau ${((Date.now() - started) / 1000).toFixed(1)} giây.`;
      } else {
        $('draftStatus').className = 'message-summary warning';
        $('draftStatus').textContent =
          `⚠ Testcase có thể chạy thử, nhưng chưa thể lưu chính thức.\n\nCần bổ sung:\n${result.warnings.map((item) => `• ${item}`).join('\n')}`;
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
      $('draftStatus').textContent =
        `✕ Testcase chưa thể chạy thử sau ${((Date.now() - started) / 1000).toFixed(1)} giây.\n\n${readableError(error)}`;
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
    if (!state.generatorPreflightPassed) return alert('Hãy kiểm tra bản nháp sau lần chỉnh sửa cuối.');
    if (state.selectedEndpointOrder.length > 1 && !$('generatorSequentialChain').checked)
      return alert(
        'Bạn đang chạy nhiều API. Hãy bật "Sinh chuỗi theo thứ tự chọn" để truyền token và dữ liệu giữa các API.',
      );
    if (!confirm('Chạy API thật trên môi trường đã cấu hình? Test mutation có thể thay đổi dữ liệu.')) return;
    $('runDraft').disabled = true;
    $('draftReportInfo').hidden = true;
    $('draftStatus').className = 'message-summary checking';
    $('draftStatus').textContent = '⏳ Đang chạy thử API...';
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
    if (!state.generatorReadyToSave) return alert('Testcase còn thiếu coverage bắt buộc nên chưa thể lưu chính thức.');
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
            `Testcase đã tồn tại và sẽ bị ghi đè.\n\nĐích: ${target}\nTrạng thái: ${error.payload.status || 'update'}\nSHA-256 hiện tại: ${error.payload.currentHash}\n\nXác nhận ghi đè?`,
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
      $('draftStatus').textContent = `Đã lưu testcase: ${result.output}`;
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
