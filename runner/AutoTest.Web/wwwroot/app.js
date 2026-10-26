import { initializeArchitecture } from './architecture.js';
import { initializeApiBuilder } from './ui/api-builder.js';
import { initializeEnvironmentHistory } from './ui/environment-history.js';
import { initializeGeneratorRepair } from './generator-repair.js';
import { initializeGenerator } from './ui/generator.js';
import { $, bind, locale, setLocale, state, translate } from './ui/shared.js';
import { initializeTestcases } from './ui/testcases.js';
import { initializeUnitX } from './ui/unitx.js';
import { initializeWorkbookMessage } from './ui/workbook-message.js';

let apiBuilder;
let environmentHistory;
let generator;
let testcases;
let unitX;
let workbookMessage;

async function showView(view) {
  document.querySelectorAll('[data-view]').forEach((item) => {
    const active = item.dataset.view === view;
    item.classList.toggle('nav-active', active);
    item.setAttribute('aria-selected', active);
  });
  ['testsView', 'environmentView', 'historyView', 'toolsView'].forEach((id) => ($(id).hidden = id !== view));
  const tests = view === 'testsView';
  $('testSidebar').hidden = !tests;
  $('runPanel').hidden = !tests;
  $('mainLayout').classList.toggle('environment-mode', !tests);
  if (view === 'environmentView') await environmentHistory.loadEnvironment();
  if (view === 'historyView') await environmentHistory.loadHistory();
}

function showTool(tool) {
  document.querySelectorAll('[data-tool]').forEach((button) => {
    button.classList.toggle('tool-active', button.dataset.tool === tool);
  });
  ['generatorTool', 'apiBuilderTool', 'unitXTool', 'workbookTool', 'architectureTool', 'messageScannerTool'].forEach(
    (id) => ($(id).hidden = id !== tool),
  );
  if (tool === 'generatorTool') generator.loadGeneratorStatus();
  if (tool === 'apiBuilderTool') {
    apiBuilder.render();
    apiBuilder.restoreWorkspace();
    apiBuilder.loadContract();
  }
  if (tool === 'unitXTool') {
    unitX.render();
    unitX.restoreWorkspace();
  }
  if (tool === 'workbookTool') workbookMessage.renderWorkbookCases();
}

function initializeFeatures() {
  apiBuilder = initializeApiBuilder({ showTool });
  const repairGeneratedDraft = initializeGeneratorRepair();
  environmentHistory = initializeEnvironmentHistory({
    poll: () => testcases.poll(),
    showView,
  });
  testcases = initializeTestcases({ loadHistory: () => environmentHistory.loadHistory() });
  generator = initializeGenerator({
    loadTests: () => testcases.loadTests(),
    poll: () => testcases.poll(),
    recoverUi: () => testcases.recoverUi(),
    repairGeneratedDraft,
  });
  unitX = initializeUnitX();
  workbookMessage = initializeWorkbookMessage({ showTool });
  initializeArchitecture();
}

function renderLocaleSensitiveContent() {
  document.querySelectorAll('[data-i18n]').forEach((element) => {
    element.textContent = translate(element.dataset.i18n);
  });
  document.querySelectorAll('[data-i18n-aria]').forEach((element) => {
    element.setAttribute('aria-label', translate(element.dataset.i18nAria));
  });
  testcases.render();
  generator.renderEndpoints();
  workbookMessage.renderWorkbookCases();
  workbookMessage.renderMessages();
  apiBuilder.render();
  unitX.render();
}

bind('locale', 'onchange', () => setLocale($('locale').value));
$('locale').value = locale();
window.addEventListener('toolqc:localechange', renderLocaleSensitiveContent);
bind('loadApiBuilderContract', 'onclick', () => apiBuilder.loadContract());
bind('newApiWorkspace', 'onclick', () => apiBuilder.createWorkspace());
bind('addApiBuilderOperation', 'onclick', () => apiBuilder.addOperation());
bind('saveApiWorkspace', 'onclick', () => apiBuilder.saveWorkspace());
bind('compileApiWorkspace', 'onclick', () => apiBuilder.compileWorkspace());
bind('newUnitXWorkspace', 'onclick', () => unitX.createWorkspace());
bind('saveUnitXWorkspace', 'onclick', () => unitX.saveWorkspace());
bind('runUnitXWorkspace', 'onclick', () => unitX.runWorkspace());
bind('analyzeWorkbook', 'onclick', () => workbookMessage.analyzeWorkbook());
bind('selectAllWorkbookCases', 'onclick', () => workbookMessage.selectAllWorkbookCases());
bind('clearWorkbookCases', 'onclick', () => workbookMessage.clearWorkbookCases());
bind('generateWorkbookCases', 'onclick', () => workbookMessage.generateWorkbookCases());
bind('toggleDraftJson', 'onclick', () => {
  const panel = $('draftJsonPanel');
  panel.hidden = !panel.hidden;
  $('toggleDraftJson').textContent = panel.hidden ? translate('TQC-UI-036') : translate('TQC-UI-037');
});
bind('project', 'onchange', async () => {
  await testcases.loadTests({ preserve: false });
  await environmentHistory.loadEnvironment();
  if (!$('apiBuilderTool').hidden) {
    $('apiBuilderLocalSourcePath').value = '';
    await apiBuilder.loadContract();
  }
});
bind('search', 'oninput', () => testcases.render());
bind('group', 'onchange', () => testcases.render());
bind('run', 'onclick', () => testcases.run());
bind('stopRun', 'onclick', () => testcases.stopRun());
bind('checkEnvironment', 'onclick', () => environmentHistory.checkEnvironment());
bind('refreshHistory', 'onclick', () => environmentHistory.loadHistory());
bind('analyzeSource', 'onclick', () => generator.analyzeSource());
bind('sourcePath', 'oninput', () => localStorage.setItem('toolqc.sourcePath', $('sourcePath').value));
bind('analysisScope', 'onchange', async () => {
  const byModule = $('analysisScope').value === 'module';
  $('analysisModuleLabel').hidden = !byModule;
  if (byModule) await generator.loadSourceModules();
});
bind('generatorAiProvider', 'onchange', () => generator.loadGeneratorStatus());
bind('analysisMode', 'onchange', async () => {
  const deep = $('analysisMode').value !== 'local';
  if (deep && $('analysisScope').value === 'all') {
    $('analysisScope').value = 'module';
    $('analysisModuleLabel').hidden = false;
    await generator.loadSourceModules();
  }
});
bind('endpointSearch', 'oninput', () => generator.renderEndpoints());
bind('generateTests', 'onclick', () => generator.generateTests());
bind('preflightDraft', 'onclick', () => generator.preflightDraft());
bind('runDraft', 'onclick', () => generator.runDraft());
bind('activateDraft', 'onclick', () => generator.activateDraft());
bind('draftJson', 'oninput', () => {
  state.generatorPreflightPassed = false;
  state.generatorReadyToSave = false;
  $('runDraft').disabled = true;
  $('activateDraft').disabled = true;
  $('draftStatus').textContent = translate('TQC-UI-043');
});
bind('scanMessages', 'onclick', () => workbookMessage.scanMessages());
bind('messageSearch', 'oninput', () => workbookMessage.renderMessages());
bind('selectVisible', 'onclick', () => {
  testcases.visible().forEach((item) => testcases.selectTest(item.id, true));
  testcases.render();
});
bind('clear', 'onclick', () => {
  state.selected.clear();
  state.selectionOrder = [];
  testcases.render();
});
bind('expandAll', 'onclick', () => {
  state.tests.forEach((item) => state.expanded.add(item.group));
  testcases.render();
});
bind('collapseAll', 'onclick', () => {
  state.expanded.clear();
  testcases.render();
});
bind('saveEnvironment', 'onclick', () => environmentHistory.saveEnvironment());
document.querySelectorAll('[data-view]').forEach((button) => (button.onclick = () => showView(button.dataset.view)));
document.querySelectorAll('[data-tool]').forEach((button) => (button.onclick = () => showTool(button.dataset.tool)));

initializeFeatures();
renderLocaleSensitiveContent();
const savedSourcePath = localStorage.getItem('toolqc.sourcePath');
if (savedSourcePath) $('sourcePath').value = savedSourcePath;
generator.loadGeneratorStatus();
setInterval(() => testcases.refreshTestsInBackground(), 2000);
window.addEventListener('focus', () => testcases.refreshTestsInBackground());
testcases.recoverUi();
