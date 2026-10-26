import { $, escapeHtml, json, readableError, state } from './shared.js';

export function initializeApiBuilder({ showTool }) {
  function apiBuilderWorkspaceUrl() {
    return `/api/projects/${encodeURIComponent($('project').value)}/api-builder/workspaces/${encodeURIComponent(state.apiBuilderWorkspace.id)}`;
  }
  function apiBuilderContractUrl() {
    return `/api/projects/${encodeURIComponent($('project').value)}/api-builder/contract`;
  }
  function selectedApiBuilderOperation() {
    return state.apiBuilderContract?.operations.find((operation) => operation.id === $('apiBuilderOperation').value);
  }
  function fieldCopy(field) {
    return {
      name: field.name,
      jsonPath: field.jsonPath,
      type: field.type,
      value: '',
      id: field.id,
      location: field.location,
      sensitive: field.sensitive,
    };
  }
  function operationStep(operation, number) {
    return {
      name: `${operation.name || operation.id} ${number}`,
      method: operation.method,
      path: operation.path,
      operationId: operation.id,
      contractFingerprint: state.apiBuilderContract.fingerprint,
      requestFields: operation.requestFields.map(fieldCopy),
      responseFields: operation.responseFields.map(fieldCopy),
    };
  }
  function updateApiBuilderContractControls() {
    const select = $('apiBuilderOperation'),
      operations = state.apiBuilderContract?.operations || [];
    select.disabled = !operations.length;
    const groups = operations.reduce((result, operation) => {
        const group = operation.group || 'Khác';
        (result[group] ??= []).push(operation);
        return result;
      }, {}),
      options = Object.entries(groups)
        .sort(([left], [right]) => left.localeCompare(right, 'vi'))
        .map(
          ([group, items]) =>
            `<optgroup label="${escapeHtml(group)}">${items.map((operation) => `<option value="${escapeHtml(operation.id)}">${escapeHtml(operation.method)} ${escapeHtml(operation.path)} · ${escapeHtml(operation.name || operation.id)}</option>`).join('')}</optgroup>`,
        )
        .join('');
    select.innerHTML = operations.length
      ? '<option value="">Chọn operation</option>' + options
      : '<option value="">Chưa tải được contract</option>';
    $('addApiBuilderOperation').disabled = !state.apiBuilderWorkspace || !operations.length;
  }
  async function loadApiBuilderContract() {
    if (!$('project').value) return;
    const localSourcePath = $('apiBuilderLocalSourcePath').value.trim(),
      request = { method: 'POST' };
    if (localSourcePath) {
      request.headers = { 'Content-Type': 'application/json' };
      request.body = JSON.stringify({ localSourcePath });
    }
    apiBuilderSourceStatus(
      localSourcePath ? 'Đang khám phá source local...' : 'Đang tải mapping hoặc cấu hình project...',
    );
    try {
      state.apiBuilderContract = await json(apiBuilderContractUrl(), request);
      updateApiBuilderContractControls();
      apiBuilderSourceStatus(`Nguồn contract: ${state.apiBuilderContract.provider}.`, 'passed');
      apiBuilderStatus(
        `Đã tải ${state.apiBuilderContract.operations.length} operation từ ${state.apiBuilderContract.provider}. Chọn operation để thêm vào chuỗi.`,
        'passed',
      );
    } catch (error) {
      state.apiBuilderContract = null;
      updateApiBuilderContractControls();
      const instruction = 'Nhập thư mục source local hợp lệ hoặc dùng mapping/cấu hình portable của project.';
      apiBuilderSourceStatus(instruction, 'failed');
      apiBuilderStatus(`${readableError(error)}\n\n${instruction}`, 'failed');
    }
  }
  function addApiBuilderOperation() {
    if (!state.apiBuilderWorkspace) return apiBuilderStatus('Tạo workspace trước khi thêm operation.', 'failed');
    const operation = selectedApiBuilderOperation();
    if (!operation) return apiBuilderStatus('Chọn một operation trước khi thêm.', 'failed');
    state.apiBuilderWorkspace = {
      ...state.apiBuilderWorkspace,
      sourceFingerprint: state.apiBuilderContract.fingerprint,
      steps: [...state.apiBuilderWorkspace.steps, operationStep(operation, state.apiBuilderWorkspace.steps.length + 1)],
    };
    apiBuilderStatus(
      `Đã thêm ${operation.method} ${operation.path}. Lưu response thành biến rồi bind vào bước sau.`,
      'passed',
    );
    renderApiBuilder();
  }
  function apiBuilderSourceStatus(message, kind = '') {
    const panel = $('apiBuilderSourceStatus');
    panel.className = `api-builder-source-status ${kind}`;
    panel.textContent = message;
  }
  function apiBuilderStatus(message, kind = '') {
    const panel = $('apiBuilderStatus');
    panel.className = `message-summary ${kind}`;
    panel.textContent = message;
  }
  function apiBuilderVariables() {
    return state.apiBuilderWorkspace?.variables || [];
  }
  function bindingFor(workspace, step, field) {
    return (workspace.bindings || []).find(
      (binding) => binding.targetStep === step.name && binding.targetFieldId === field.id,
    );
  }
  function requestFieldMarkup(workspace, step, field, index, fieldIndex, variables) {
    const binding = bindingFor(workspace, step, field),
      value = binding ? '$' + '{' + binding.sourceVariable + '}' : field.value,
      stepIndex = workspace.steps.indexOf(step),
      options = variables
        .filter(
          (variable) =>
            workspace.steps.findIndex((item) => item.name === variable.sourceStep) < stepIndex &&
            variable.type === field.type &&
            !field.sensitive,
        )
        .map(
          (variable) =>
            `<option value="${escapeHtml(variable.name)}" ${binding?.sourceVariable === variable.name ? 'selected' : ''}>${escapeHtml(variable.name)} · ${escapeHtml(variable.sourceStep)}</option>`,
        )
        .join('');
    return `<label class="api-builder-field"><span><b>${escapeHtml(field.name)}</b><small>${escapeHtml(field.type)} · ${escapeHtml(field.location || 'body')} · ${escapeHtml(field.jsonPath)}</small></span><span class="api-builder-controls"><input aria-label="Giá trị ${escapeHtml(field.name)}" data-api-value="${index}:${fieldIndex}" value="${escapeHtml(value)}" ${binding ? 'disabled' : ''}><select aria-label="Bind biến vào ${escapeHtml(field.name)}" data-api-request="${index}:${fieldIndex}" ${field.sensitive ? 'disabled' : ''}><option value="">Nhập thủ công</option>${options}</select></span></label>`;
  }
  function responseFieldMarkup(field, index, fieldIndex) {
    return `<div class="api-builder-field"><span><b>${escapeHtml(field.name)}</b><small>${escapeHtml(field.type)} · ${escapeHtml(field.jsonPath)}</small></span><button type="button" class="compact" data-api-save="${index}:${fieldIndex}">Lưu thành biến</button></div>`;
  }
  function stepMarkup(workspace, variables, step, index) {
    const requestFields = step.requestFields
        .map((field, fieldIndex) => requestFieldMarkup(workspace, step, field, index, fieldIndex, variables))
        .join(''),
      responseFields =
        step.responseFields.map((field, fieldIndex) => responseFieldMarkup(field, index, fieldIndex)).join('') ||
        '<div class="empty">Contract chưa có field response.</div>';
    return `<article class="api-builder-step"><div class="api-builder-step-title"><strong>Bước ${index + 1}: ${escapeHtml(step.name)}</strong><span class="method ${escapeHtml(step.method.toLowerCase())}">${escapeHtml(step.method)}</span><code>${escapeHtml(step.path)}</code><button type="button" class="compact api-builder-remove" data-api-remove="${index}">Xóa</button></div><section><h4>Request</h4>${requestFields}</section><section><h4>Response</h4>${responseFields}</section></article>`;
  }
  function renderApiBuilder() {
    const workspace = state.apiBuilderWorkspace;
    $('saveApiWorkspace').disabled = !workspace;
    $('compileApiWorkspace').disabled = !workspace;
    updateApiBuilderContractControls();
    if (!workspace) {
      $('apiBuilderSteps').innerHTML = '';
      $('apiBuilderVariables').innerHTML = '';
      return;
    }
    const variables = apiBuilderVariables(),
      steps = workspace.steps.map((step, index) => stepMarkup(workspace, variables, step, index)).join('');
    $('apiBuilderSteps').innerHTML = steps || '<div class="empty">Chọn operation để thêm bước đầu tiên.</div>';
    $('apiBuilderVariables').innerHTML = variables.length
      ? variables
          .map(
            (variable) =>
              `<article><b>${escapeHtml(variable.name)}</b><small>${escapeHtml(variable.sourceStep)} · ${escapeHtml(variable.type)} · ${escapeHtml(variable.jsonPath)}</small></article>`,
          )
          .join('')
      : '<div class="empty">Chưa có biến nào được lưu.</div>';
    document
      .querySelectorAll('[data-api-request]')
      .forEach((select) => (select.onchange = () => assignApiBuilderVariable(select)));
    document
      .querySelectorAll('[data-api-value]')
      .forEach((input) => (input.oninput = () => updateApiBuilderFieldValue(input)));
    document
      .querySelectorAll('[data-api-save]')
      .forEach((button) => (button.onclick = () => saveApiBuilderVariable(button)));
    document
      .querySelectorAll('[data-api-remove]')
      .forEach((button) => (button.onclick = () => removeApiBuilderOperation(Number(button.dataset.apiRemove))));
  }
  function copyApiBuilderSteps(workspace) {
    return workspace.steps.map((step) => ({
      ...step,
      requestFields: step.requestFields.map((field) => ({ ...field })),
      responseFields: step.responseFields.map((field) => ({ ...field })),
    }));
  }
  function updateApiBuilderFieldValue(input) {
    const [stepIndex, fieldIndex] = input.dataset.apiValue.split(':').map(Number),
      workspace = state.apiBuilderWorkspace,
      steps = copyApiBuilderSteps(workspace);
    steps[stepIndex].requestFields[fieldIndex].value = input.value;
    state.apiBuilderWorkspace = { ...workspace, steps };
  }
  function removeApiBuilderOperation(index) {
    const workspace = state.apiBuilderWorkspace,
      removed = workspace?.steps[index];
    if (!workspace || !removed) return;
    const variables = workspace.variables.filter((variable) => variable.sourceStep !== removed.name),
      bindings = (workspace.bindings || []).filter(
        (binding) =>
          binding.targetStep !== removed.name &&
          !workspace.variables
            .filter((variable) => variable.sourceStep === removed.name)
            .some((variable) => variable.name === binding.sourceVariable),
      ),
      steps = workspace.steps
        .filter((_, stepIndex) => stepIndex !== index)
        .map((step, stepIndex) => {
          const name = `${step.operationId || step.name} ${stepIndex + 1}`;
          return { ...step, name };
        }),
      renamedSteps = workspace.steps
        .filter((_, stepIndex) => stepIndex !== index)
        .map((step, stepIndex) => ({ from: step.name, to: steps[stepIndex].name })),
      renamedVariables = variables.map((variable) => ({
        ...variable,
        sourceStep: renamedSteps.find((step) => step.from === variable.sourceStep)?.to || variable.sourceStep,
      })),
      renamedBindings = bindings.map((binding) => ({
        ...binding,
        targetStep: renamedSteps.find((step) => step.from === binding.targetStep)?.to || binding.targetStep,
      }));
    state.apiBuilderWorkspace = { ...workspace, steps, variables: renamedVariables, bindings: renamedBindings };
    apiBuilderStatus(`Đã xóa ${removed.method} ${removed.path} cùng biến và binding phụ thuộc.`, 'passed');
    renderApiBuilder();
  }
  function assignApiBuilderVariable(select) {
    const [stepIndex, fieldIndex] = select.dataset.apiRequest.split(':').map(Number),
      name = select.value,
      workspace = state.apiBuilderWorkspace,
      steps = copyApiBuilderSteps(workspace),
      field = steps[stepIndex].requestFields[fieldIndex],
      bindings = (workspace.bindings || []).filter(
        (binding) => binding.targetStep !== steps[stepIndex].name || binding.targetFieldId !== field.id,
      );
    if (name) bindings.push({ sourceVariable: name, targetStep: steps[stepIndex].name, targetFieldId: field.id });
    field.value = name ? `\${${name}}` : '';
    state.apiBuilderWorkspace = { ...workspace, steps, bindings };
    renderApiBuilder();
  }
  function saveApiBuilderVariable(button) {
    const [stepIndex, fieldIndex] = button.dataset.apiSave.split(':').map(Number),
      workspace = state.apiBuilderWorkspace,
      field = workspace.steps[stepIndex].responseFields[fieldIndex],
      name = prompt(`Tên biến cho ${field.name}:`);
    if (!name) return;
    const normalized = name.trim();
    if (!/^[\p{L}\p{N}_]+$/u.test(normalized))
      return apiBuilderStatus('Tên biến chỉ gồm chữ, số hoặc dấu gạch dưới.', 'failed');
    if (apiBuilderVariables().some((item) => item.name.toLowerCase() === normalized.toLowerCase()))
      return apiBuilderStatus('Tên biến đã tồn tại trong workspace.', 'failed');
    const variable = {
      name: normalized,
      sourceStep: workspace.steps[stepIndex].name,
      jsonPath: field.jsonPath,
      type: field.type,
      preview: '',
      createdAt: new Date().toISOString(),
    };
    state.apiBuilderWorkspace = {
      ...workspace,
      variables: [...workspace.variables, variable].sort((left, right) =>
        left.name.localeCompare(right.name, 'vi', { sensitivity: 'base' }),
      ),
    };
    apiBuilderStatus(
      `Đã lưu biến ${normalized}. Chỉ bind được vào request của bước sau có kiểu tương thích.`,
      'passed',
    );
    renderApiBuilder();
  }
  async function createApiBuilderWorkspace() {
    if (!$('project').value) return;
    try {
      state.apiBuilderWorkspace = await json(
        `/api/projects/${encodeURIComponent($('project').value)}/api-builder/workspaces`,
        { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ owner: 'local' }) },
      );
      apiBuilderStatus(
        `Workspace ${state.apiBuilderWorkspace.id} đã sẵn sàng. Contract được tải tự động theo cấu hình dự án.`,
        'passed',
      );
      renderApiBuilder();
    } catch (error) {
      apiBuilderStatus(readableError(error), 'failed');
    }
  }
  async function saveApiBuilderWorkspace() {
    if (!state.apiBuilderWorkspace) return false;
    try {
      const workspace = await json(apiBuilderWorkspaceUrl(), {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          workspace: state.apiBuilderWorkspace,
          expectedRevision: state.apiBuilderWorkspace.revision,
        }),
      });
      state.apiBuilderWorkspace = workspace;
      localStorage.setItem('toolqc.apiBuilderWorkspace', workspace.id);
      apiBuilderStatus(`Đã lưu workspace lúc ${new Date(workspace.updatedAt).toLocaleTimeString('vi-VN')}.`, 'passed');
      renderApiBuilder();
      return true;
    } catch (error) {
      apiBuilderStatus(readableError(error), 'failed');
      return false;
    }
  }
  async function restoreApiBuilderWorkspace() {
    const id = localStorage.getItem('toolqc.apiBuilderWorkspace');
    if (!id || !$('project').value) return;
    try {
      state.apiBuilderWorkspace = await json(
        `/api/projects/${encodeURIComponent($('project').value)}/api-builder/workspaces/${encodeURIComponent(id)}`,
      );
      apiBuilderStatus(`Đã khôi phục workspace ${id}.`, 'passed');
      renderApiBuilder();
    } catch {
      localStorage.removeItem('toolqc.apiBuilderWorkspace');
    }
  }
  async function compileApiBuilderWorkspace() {
    if (!state.apiBuilderWorkspace || !(await saveApiBuilderWorkspace())) return;
    try {
      const workspace = state.apiBuilderWorkspace,
        result = await json(`${apiBuilderWorkspaceUrl()}/compile`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ caseId: `api-builder-${workspace.id}` }),
        });
      state.generatorReviewId = `api-builder-${workspace.id}`;
      state.generatorPreflightPassed = false;
      state.generatorReadyToSave = false;
      state.generatorDraftRunId = null;
      $('draftJson').value = result.json;
      $('draftGroup').value = 'api-builder';
      $('draftFileName').value = `${workspace.id}.json`;
      $('draftArtifact').textContent = `Bản nháp từ API Builder workspace ${workspace.id}.`;
      $('draftEditor').hidden = false;
      $('draftJsonPanel').hidden = true;
      $('toggleDraftJson').textContent = 'Xem JSON';
      $('draftStatus').className = 'message-summary';
      $('draftStatus').textContent =
        'Bản nháp API Builder đã tạo. Nhấn “1. Kiểm tra” để xác nhận coverage và khả năng chạy.';
      $('draftReportInfo').hidden = true;
      $('runDraft').disabled = true;
      $('activateDraft').disabled = true;
      showTool('generatorTool');
      $('draftEditor').scrollIntoView({ behavior: 'smooth', block: 'start' });
      apiBuilderStatus('Đã biên dịch workspace thành draft schema-v2.', 'passed');
    } catch (error) {
      apiBuilderStatus(readableError(error), 'failed');
    }
  }

  return {
    addOperation: addApiBuilderOperation,
    compileWorkspace: compileApiBuilderWorkspace,
    createWorkspace: createApiBuilderWorkspace,
    loadContract: loadApiBuilderContract,
    render: renderApiBuilder,
    restoreWorkspace: restoreApiBuilderWorkspace,
    saveWorkspace: saveApiBuilderWorkspace,
  };
}
