import { $, escapeHtml, json, state } from './shared.js';

export function initializeEnvironmentHistory({ poll, showView }) {
  async function loadEnvironment() {
    state.environmentChanges.clear();
    state.environment = await json(`/api/projects/${encodeURIComponent($('project').value)}/environment`);
    $('environmentGroups').innerHTML = state.environment
      .map(
        (group) =>
          `<section class="environment-group"><h2>${escapeHtml(group.name)}</h2>${group.variables.map((variable) => `<label class="environment-variable"><span><b>${escapeHtml(variable.key)}</b><small class="source ${variable.configured ? 'configured' : ''}">${escapeHtml(variable.source)}</small></span><input data-env="${escapeHtml(variable.key)}" type="${variable.secret ? 'password' : 'text'}" value="${escapeHtml(variable.value)}" placeholder="${variable.secret && variable.configured ? 'Nhập giá trị mới để ghi đè' : 'Chưa cấu hình'}"><button type="button" data-clear-env="${escapeHtml(variable.key)}">Xóa</button></label>`).join('')}</section>`,
      )
      .join('');
    document.querySelectorAll('[data-env]').forEach((input) => {
      input.onfocus = () => {
        if (input.value === '••••••••') input.value = '';
      };
      input.oninput = () => state.environmentChanges.set(input.dataset.env, input.value);
    });
    document.querySelectorAll('[data-clear-env]').forEach(
      (button) =>
        (button.onclick = () => {
          const input = document.querySelector(`[data-env="${CSS.escape(button.dataset.clearEnv)}"]`);
          input.value = '';
          state.environmentChanges.set(button.dataset.clearEnv, '');
        }),
    );
  }
  async function saveEnvironment() {
    if (!state.environmentChanges.size) return alert('Chưa có thay đổi để lưu.');
    await json(`/api/projects/${encodeURIComponent($('project').value)}/environment`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ values: Object.fromEntries(state.environmentChanges) }),
    });
    alert('Đã lưu cấu hình dự án. Lần chạy tiếp theo sẽ sử dụng giá trị mới.');
    await loadEnvironment();
  }

  async function loadHistory() {
    const runs = await json('/api/runs');
    $('historyList').innerHTML =
      runs
        .map(
          (run) =>
            `<button class="history-item" data-run-id="${escapeHtml(run.id)}"><b>${escapeHtml(run.project)}</b><span>${new Date(run.startedAt).toLocaleString('vi-VN')} · ${run.caseIds.length} testcase</span><em class="${run.status}">${escapeHtml(run.status)}</em></button>`,
        )
        .join('') || '<div class="empty">Chưa có lịch sử chạy.</div>';
    document.querySelectorAll('[data-run-id]').forEach(
      (button) =>
        (button.onclick = () => {
          state.runId = button.dataset.runId;
          showView('testsView');
          poll();
        }),
    );
  }
  async function checkEnvironment() {
    const checks = await json(`/api/projects/${encodeURIComponent($('project').value)}/environment/check`, {
      method: 'POST',
    });
    $('connectionChecks').innerHTML =
      checks
        .map(
          (item) =>
            `<div class="connection-check ${item.success ? 'passed' : 'failed'}"><b>${escapeHtml(item.key)}</b><span>${escapeHtml(item.message)} · ${item.durationMs} ms</span></div>`,
        )
        .join('') || '<div class="environment-note">Không tìm thấy URL để kiểm tra.</div>';
  }

  return { checkEnvironment, loadEnvironment, loadHistory, saveEnvironment };
}
