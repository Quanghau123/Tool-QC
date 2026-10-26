import { $, escapeHtml, json, readableError, state } from './shared.js';

export function initializeUnitX() {
  function workspaceUrl() {
    return `/api/projects/${encodeURIComponent($('project').value)}/unitx/workspaces/${encodeURIComponent(state.unitXWorkspace.id)}`;
  }

  function status(message, kind = '') {
    const panel = $('unitXStatus');
    panel.className = `message-summary ${kind}`;
    panel.textContent = message;
  }

  function render() {
    const workspace = state.unitXWorkspace;
    $('saveUnitXWorkspace').disabled = !workspace;
    $('runUnitXWorkspace').disabled = !workspace;
    if (!workspace) {
      $('unitXEditor').innerHTML = '';
      return;
    }

    const table = workspace.table;
    const rule = table.rules[0];
    const row = table.rows[0];
    const values = row?.values || {};
    $('unitXEditor').innerHTML =
      `<label>Tên bảng<input id="unitXName" value="${escapeHtml(table.name)}"></label><section class="unitx-card"><h3>Rule</h3><label>Đầu ra<input id="unitXOutput" value="${escapeHtml(rule?.output || 'result')}"></label><label>Operator<select id="unitXOperator">${['add', 'subtract', 'multiply', 'divide', 'round', 'ifGreaterThan'].map((operator) => `<option value="${operator}" ${rule?.operator === operator ? 'selected' : ''}>${operator}</option>`).join('')}</select></label><label>Input (cách nhau dấu phẩy)<input id="unitXInputs" value="${escapeHtml((rule?.inputs || ['amount', 'rate']).join(', '))}"></label></section><section class="unitx-card"><h3>Dòng kiểm thử</h3><label>Tên dòng<input id="unitXRowName" value="${escapeHtml(row?.name || 'Dòng 1')}"></label><label>Giá trị JSON object<input id="unitXValues" value="${escapeHtml(JSON.stringify(values))}"></label><label>Kết quả mong đợi<input id="unitXExpected" type="number" value="${row?.expected ?? 0}"></label></section>`;
  }

  function readEditor() {
    const values = JSON.parse($('unitXValues').value);
    return {
      name: $('unitXName').value.trim(),
      rules: [
        {
          output: $('unitXOutput').value.trim(),
          operator: $('unitXOperator').value,
          inputs: $('unitXInputs')
            .value.split(',')
            .map((value) => value.trim())
            .filter(Boolean),
        },
      ],
      rows: [{ name: $('unitXRowName').value.trim(), values, expected: Number($('unitXExpected').value) }],
    };
  }

  async function createWorkspace() {
    try {
      state.unitXWorkspace = await json(`/api/projects/${encodeURIComponent($('project').value)}/unitx/workspaces`, {
        method: 'POST',
      });
      state.unitXWorkspace = {
        ...state.unitXWorkspace,
        table: {
          name: 'Bảng quyết định mới',
          rules: [{ output: 'result', operator: 'add', inputs: ['amount', 'fee'] }],
          rows: [{ name: 'Dòng 1', values: { amount: 1, fee: 2 }, expected: 3 }],
        },
      };
      render();
      status('Đã tạo bảng UnitX mới.', 'passed');
    } catch (error) {
      status(readableError(error), 'failed');
    }
  }

  async function saveWorkspace() {
    if (!state.unitXWorkspace) return;
    try {
      const workspace = { ...state.unitXWorkspace, table: readEditor() };
      state.unitXWorkspace = await json(workspaceUrl(), {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ workspace, expectedRevision: workspace.revision }),
      });
      localStorage.setItem('toolqc.unitXWorkspace', state.unitXWorkspace.id);
      status('Đã lưu bảng UnitX.', 'passed');
      render();
    } catch (error) {
      status(readableError(error), 'failed');
    }
  }

  async function runWorkspace() {
    if (!state.unitXWorkspace) return;
    await saveWorkspace();
    if (!state.unitXWorkspace) return;
    try {
      const result = await json(`${workspaceUrl()}/run`, { method: 'POST' });
      $('unitXResults').hidden = false;
      $('unitXResults').innerHTML = result.rows
        .map(
          (row) =>
            `<article class="${row.passed ? 'passed' : 'failed'}"><b>${escapeHtml(row.name)}</b><span>Actual: ${escapeHtml(row.actual ?? '—')} · Expected: ${escapeHtml(row.expected)}</span>${row.error ? `<small>${escapeHtml(row.error)}</small>` : ''}</article>`,
        )
        .join('');
      status(
        `${result.passedCount}/${result.rows.length} dòng đạt.`,
        result.passedCount === result.rows.length ? 'passed' : 'failed',
      );
    } catch (error) {
      status(readableError(error), 'failed');
    }
  }

  async function restoreWorkspace() {
    const id = localStorage.getItem('toolqc.unitXWorkspace');
    if (!id || !$('project').value) return;
    try {
      state.unitXWorkspace = await json(
        `/api/projects/${encodeURIComponent($('project').value)}/unitx/workspaces/${encodeURIComponent(id)}`,
      );
      render();
      status('Đã khôi phục bảng UnitX.', 'passed');
    } catch {
      localStorage.removeItem('toolqc.unitXWorkspace');
    }
  }

  return { createWorkspace, render, restoreWorkspace, runWorkspace, saveWorkspace };
}
