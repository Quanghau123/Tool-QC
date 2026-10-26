import { $, escapeHtml, json, readableError, state } from './shared.js';

export function initializeTestcases({ loadHistory }) {
  async function loadProjects() {
    const projects = await json('/api/projects');
    $('project').innerHTML = projects.map((x) => `<option>${escapeHtml(x.name)}</option>`).join('');
    $('connection').textContent = 'Sẵn sàng';
    await loadTests();
  }
  async function recoverUi() {
    try {
      await loadProjects();
      return true;
    } catch (error) {
      $('connection').textContent = 'Mất kết nối';
      $('console').textContent = readableError(error);
      return false;
    }
  }
  async function loadTests(options = {}) {
    const preserve = options.preserve !== false,
      selected = preserve ? new Set(state.selected) : new Set(),
      order = preserve ? [...state.selectionOrder] : [],
      expanded = preserve ? new Set(state.expanded) : new Set(),
      group = $('group').value;
    const tests = await json(`/api/projects/${encodeURIComponent($('project').value)}/tests`),
      signature = JSON.stringify(tests.map((x) => [x.id, x.name, x.group, x.stepCount, x.tags]));
    if (options.onlyIfChanged && signature === state.testSignature) return false;
    state.tests = tests;
    state.testSignature = signature;
    const ids = new Set(tests.map((x) => x.id)),
      groups = [...new Set(tests.map((x) => x.group))].sort();
    state.selected = new Set([...selected].filter((id) => ids.has(id)));
    state.selectionOrder = order.filter((id) => state.selected.has(id));
    state.selected.forEach((id) => {
      if (!state.selectionOrder.includes(id)) state.selectionOrder.push(id);
    });
    state.expanded = new Set([...expanded].filter((item) => groups.includes(item)));
    if (!state.expanded.size && groups.length) state.expanded.add(groups[0]);
    $('group').innerHTML =
      '<option value="">Tất cả</option>' + groups.map((x) => `<option>${escapeHtml(x)}</option>`).join('');
    $('group').value = groups.includes(group) ? group : '';
    render();
    return true;
  }
  async function refreshTestsInBackground() {
    if (!$('project').value || document.hidden) return;
    try {
      await loadTests({ preserve: true, onlyIfChanged: true });
    } catch {}
  }

  function visible() {
    const q = $('search').value.toLowerCase(),
      g = $('group').value;
    return state.tests.filter(
      (x) => (!g || x.group === g) && (!q || `${x.id} ${x.name} ${x.tags.join(' ')}`.toLowerCase().includes(q)),
    );
  }
  function selectTest(id, checked) {
    if (checked) {
      state.selected.add(id);
      if (!state.selectionOrder.includes(id)) state.selectionOrder.push(id);
    } else {
      state.selected.delete(id);
      state.selectionOrder = state.selectionOrder.filter((item) => item !== id);
    }
  }
  function render() {
    const tests = visible();
    const query = $('search').value.trim();
    const grouped = tests.reduce((result, test) => {
      (result[test.group] ??= []).push(test);
      return result;
    }, {});
    $('count').textContent = `${tests.length}/${state.tests.length}`;
    $('selectedCount').textContent = state.selected.size;
    $('tests').innerHTML =
      Object.keys(grouped)
        .sort()
        .map((group) => {
          const items = grouped[group],
            open = query || $('group').value || state.expanded.has(group);
          const selected = items.filter((x) => state.selected.has(x.id)).length;
          return `<section class="folder ${open ? 'open' : ''}" data-folder="${escapeHtml(group)}"><div class="folder-header"><button class="folder-toggle" data-toggle="${escapeHtml(group)}"><span class="chevron">›</span><span class="folder-icon">📁</span><strong>${escapeHtml(folderName(group))}</strong><span class="folder-count">${items.length} testcase</span></button><label class="folder-select"><input type="checkbox" data-group="${escapeHtml(group)}" ${selected === items.length ? 'checked' : ''}><span>${selected}/${items.length}</span></label></div><div class="folder-content">${items.map(testCard).join('')}</div></section>`;
        })
        .join('') || '<div class="empty">Không tìm thấy testcase phù hợp.</div>';
    document.querySelectorAll('[data-id]').forEach(
      (box) =>
        (box.onchange = () => {
          selectTest(box.dataset.id, box.checked);
          render();
        }),
    );
    document.querySelectorAll('[data-toggle]').forEach(
      (button) =>
        (button.onclick = () => {
          const group = button.dataset.toggle;
          state.expanded.has(group) ? state.expanded.delete(group) : state.expanded.add(group);
          render();
        }),
    );
    document.querySelectorAll('[data-group]').forEach(
      (box) =>
        (box.onchange = () => {
          grouped[box.dataset.group].forEach((x) => selectTest(x.id, box.checked));
          render();
        }),
    );
  }
  function testCard(x) {
    const order = state.selectionOrder.indexOf(x.id) + 1;
    return `<label class="test"><span class="sequence-check"><input type="checkbox" data-id="${escapeHtml(x.id)}" ${state.selected.has(x.id) ? 'checked' : ''}>${order ? `<strong>${order}</strong>` : ''}</span><div><b>${escapeHtml(x.name)}</b><div class="meta">${escapeHtml(x.id)} · ${x.stepCount} bước</div><div class="tags">${x.tags.map((t) => `<span class="tag">${escapeHtml(t)}</span>`).join('')}</div></div>${x.destructive ? '<span class="danger">Thay đổi dữ liệu</span>' : ''}</label>`;
  }
  function folderName(group) {
    return group
      .split(/[-_]/)
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');
  }
  async function run() {
    if (!state.selected.size) return alert('Hãy chọn ít nhất một testcase.');
    const sequential = $('sequentialChain').checked;
    const ids = sequential
      ? state.selectionOrder.filter((id) => state.selected.has(id))
      : state.tests.filter((x) => state.selected.has(x.id)).map((x) => x.id);
    const chosen = ids.map((id) => state.tests.find((x) => x.id === id)).filter(Boolean);
    if (
      chosen.some((x) => x.destructive) &&
      !confirm(`Sắp chạy ${chosen.length} testcase có thay đổi dữ liệu trên môi trường đã cấu hình. Tiếp tục?`)
    )
      return;
    const result = await json('/api/runs', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ project: $('project').value, caseIds: ids, sequentialChain: sequential }),
    });
    state.runId = result.runId;
    $('report').hidden = true;
    $('report').removeAttribute('href');
    $('console').textContent = 'Đang khởi động...';
    $('runStatus').scrollIntoView({ behavior: 'smooth', block: 'start' });
    poll();
  }
  function draftFailureText(run) {
    const lines = (run.lines || []).filter((line) => line && !line.startsWith('Báo cáo HTML:'));
    const useful = lines.slice(-6).join('\n');
    return [
      `Mã lỗi: ${run.failureType || 'TQC-RUN-001'}`,
      `Nhóm lỗi: ${run.failureCategory || 'UNKNOWN'}`,
      `Lỗi: ${run.failureMessage || useful || 'Testcase chạy thất bại.'}`,
      run.fixHint ? `Cách xử lý: ${run.fixHint}` : '',
      useful && run.failureMessage ? `Chi tiết gần nhất:\n${useful}` : '',
    ]
      .filter(Boolean)
      .join('\n');
  }
  async function poll() {
    if (!state.runId) return;
    const run = await json(`/api/runs/${state.runId}`);
    $('runStatus').className = `status ${run.status}`;
    $('runStatus').textContent =
      { running: 'Đang chạy', passed: 'Thành công', failed: 'Thất bại', cancelled: 'Đã dừng' }[run.status] ||
      run.status;
    $('stopRun').hidden = run.status !== 'running';
    const diagnosis = run.failureType
      ? `Error Code: ${run.failureType}\nCategory: ${run.failureCategory || 'UNKNOWN'}\nMessage: ${run.failureMessage || ''}\nSuggested Action: ${run.fixHint || ''}\n\n`
      : '';
    $('console').textContent = diagnosis + (run.lines.join('\n') || 'Đang khởi động...');
    $('console').scrollTop = $('console').scrollHeight;
    if (run.reportPath) {
      $('report').hidden = false;
      $('report').href = `/api/reports/${state.runId}`;
    }
    if (run.status === 'running') setTimeout(poll, 1000);
    else {
      if (state.generatorDraftRunId === run.id) {
        $('runDraft').disabled = !state.generatorPreflightPassed;
        const reportInfo = $('draftReportInfo');
        reportInfo.hidden = false;
        if (run.status === 'passed') {
          $('draftStatus').className = 'message-summary passed';
          $('draftStatus').textContent = '✓ Chạy thử API thành công.';
          reportInfo.innerHTML = run.reportPath
            ? `<b>Báo cáo</b><a href="/api/reports/${encodeURIComponent(run.id)}" target="_blank" rel="noopener">Mở báo cáo trong tab mới</a><div>${escapeHtml(run.reportPath)}</div>`
            : '<b>Kết quả</b>Không có file báo cáo.';
        } else {
          $('draftStatus').className = 'message-summary failed';
          $('draftStatus').textContent = `✕ Chạy thử API thất bại.\n\n${draftFailureText(run)}`;
          reportInfo.innerHTML = run.reportPath
            ? `<b>File báo cáo</b><a href="/api/reports/${encodeURIComponent(run.id)}" target="_blank" rel="noopener">Mở báo cáo chi tiết</a><div>${escapeHtml(run.reportPath)}</div>`
            : '<b>Báo cáo</b>Runner chưa tạo được file báo cáo.';
        }
      }
      loadHistory();
    }
  }
  async function stopRun() {
    if (state.runId && confirm('Dừng workload đang chạy?'))
      await json(`/api/runs/${state.runId}`, { method: 'DELETE' });
  }

  return {
    loadProjects,
    loadTests,
    recoverUi,
    refreshTestsInBackground,
    render,
    run,
    stopRun,
    poll,
    selectTest,
    visible,
  };
}
