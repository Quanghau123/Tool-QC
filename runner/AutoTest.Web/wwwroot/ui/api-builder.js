import { $, escapeHtml, json, locale, readableError, state, translate } from './shared.js';

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
        const group = operation.group || translate('TQC-UI-057');
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
      ? `<option value="">${translate('TQC-UI-058')}</option>` + options
      : `<option value="">${translate('TQC-UI-059')}</option>`;
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
    apiBuilderSourceStatus(localSourcePath ? translate('TQC-UI-060') : translate('TQC-UI-061'));
    try {
      state.apiBuilderContract = await json(apiBuilderContractUrl(), request);
      updateApiBuilderContractControls();
      apiBuilderSourceStatus(translate('TQC-UI-062', '', { provider: state.apiBuilderContract.provider }), 'passed');
      apiBuilderStatus(
        translate('TQC-UI-063', '', {
          count: state.apiBuilderContract.operations.length,
          provider: state.apiBuilderContract.provider,
        }),
        'passed',
      );
    } catch (error) {
      state.apiBuilderContract = null;
      updateApiBuilderContractControls();
      const instruction = translate('TQC-UI-064');
      apiBuilderSourceStatus(instruction, 'failed');
      apiBuilderStatus(`${readableError(error)}\n\n${instruction}`, 'failed');
    }
  }
  function addApiBuilderOperation() {
    if (!state.apiBuilderWorkspace) return apiBuilderStatus(translate('TQC-UI-065'), 'failed');
    const operation = selectedApiBuilderOperation();
    if (!operation) return apiBuilderStatus(translate('TQC-UI-066'), 'failed');
    state.apiBuilderWorkspace = {
      ...state.apiBuilderWorkspace,
      sourceFingerprint: state.apiBuilderContract.fingerprint,
      steps: [...state.apiBuilderWorkspace.steps, operationStep(operation, state.apiBuilderWorkspace.steps.length + 1)],
    };
    apiBuilderStatus(translate('TQC-UI-067', '', { operation: `${operation.method} ${operation.path}` }), 'passed');
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
    return `<label class="api-builder-field"><span><b>${escapeHtml(field.name)}</b><small>${escapeHtml(field.type)} · ${escapeHtml(field.location || 'body')} · ${escapeHtml(field.jsonPath)}</small></span><span class="api-builder-controls"><input aria-label="${translate('TQC-UI-159', '', { field: escapeHtml(field.name) })}" data-api-value="${index}:${fieldIndex}" value="${escapeHtml(value)}" ${binding ? 'disabled' : ''}><select aria-label="${translate('TQC-UI-160', '', { field: escapeHtml(field.name) })}" data-api-request="${index}:${fieldIndex}" ${field.sensitive ? 'disabled' : ''}><option value="">${translate('TQC-UI-093')}</option>${options}</select></span></label>`;
  }
  function responseFieldMarkup(field, index, fieldIndex) {
    return `<div class="api-builder-field"><span><b>${escapeHtml(field.name)}</b><small>${escapeHtml(field.type)} · ${escapeHtml(field.jsonPath)}</small></span><button type="button" class="compact" data-api-save="${index}:${fieldIndex}">${translate('TQC-UI-089')}</button></div>`;
  }
  function stepMarkup(workspace, variables, step, index) {
    const requestFields = step.requestFields
        .map((field, fieldIndex) => requestFieldMarkup(workspace, step, field, index, fieldIndex, variables))
        .join(''),
      responseFields =
        step.responseFields.map((field, fieldIndex) => responseFieldMarkup(field, index, fieldIndex)).join('') ||
        `<div class="empty">${translate('TQC-UI-033')}</div>`;
    return `<article class="api-builder-step"><div class="api-builder-step-title"><strong>${translate('TQC-UI-077', '', { number: index + 1, name: escapeHtml(step.name) })}</strong><span class="method ${escapeHtml(step.method.toLowerCase())}">${escapeHtml(step.method)}</span><code>${escapeHtml(step.path)}</code><button type="button" class="compact api-builder-remove" data-api-remove="${index}">${translate('TQC-UI-090')}</button></div><section><h4>${translate('TQC-UI-091')}</h4>${requestFields}</section><section><h4>${translate('TQC-UI-092')}</h4>${responseFields}</section></article>`;
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
    $('apiBuilderSteps').innerHTML = steps || `<div class="empty">${translate('TQC-UI-032')}</div>`;
    $('apiBuilderVariables').innerHTML = variables.length
      ? variables
          .map(
            (variable) =>
              `<article><b>${escapeHtml(variable.name)}</b><small>${escapeHtml(variable.sourceStep)} · ${escapeHtml(variable.type)} · ${escapeHtml(variable.jsonPath)}</small></article>`,
          )
          .join('')
      : `<div class="empty">${translate('TQC-UI-031')}</div>`;
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
    apiBuilderStatus(translate('TQC-UI-068', '', { operation: `${removed.method} ${removed.path}` }), 'passed');
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
      name = prompt(translate('TQC-UI-069', '', { field: field.name }));
    if (!name) return;
    const normalized = name.trim();
    if (!/^[\p{L}\p{N}_]+$/u.test(normalized)) return apiBuilderStatus(translate('TQC-UI-034'), 'failed');
    if (apiBuilderVariables().some((item) => item.name.toLowerCase() === normalized.toLowerCase()))
      return apiBuilderStatus(translate('TQC-UI-035'), 'failed');
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
    apiBuilderStatus(translate('TQC-UI-070', '', { name: normalized }), 'passed');
    renderApiBuilder();
  }
  async function createApiBuilderWorkspace() {
    if (!$('project').value) return;
    try {
      state.apiBuilderWorkspace = await json(
        `/api/projects/${encodeURIComponent($('project').value)}/api-builder/workspaces`,
        { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ owner: 'local' }) },
      );
      apiBuilderStatus(translate('TQC-UI-071', '', { id: state.apiBuilderWorkspace.id }), 'passed');
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
      apiBuilderStatus(
        translate('TQC-UI-072', '', { time: new Date(workspace.updatedAt).toLocaleTimeString(locale()) }),
        'passed',
      );
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
      apiBuilderStatus(translate('TQC-UI-073', '', { id }), 'passed');
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
      $('draftArtifact').textContent = translate('TQC-UI-074', '', { id: workspace.id });
      $('draftEditor').hidden = false;
      $('draftJsonPanel').hidden = true;
      $('toggleDraftJson').textContent = translate('TQC-UI-036');
      $('draftStatus').className = 'message-summary';
      $('draftStatus').textContent = translate('TQC-UI-075');
      $('draftReportInfo').hidden = true;
      $('runDraft').disabled = true;
      $('activateDraft').disabled = true;
      showTool('generatorTool');
      $('draftEditor').scrollIntoView({ behavior: 'smooth', block: 'start' });
      apiBuilderStatus(translate('TQC-UI-076'), 'passed');
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
