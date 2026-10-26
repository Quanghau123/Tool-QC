export const state = {
  tests: [],
  selected: new Set(),
  selectionOrder: [],
  expanded: new Set(),
  environment: [],
  environmentChanges: new Map(),
  runId: null,
  messageScan: null,
  generatorAnalysis: null,
  selectedEndpoints: new Set(),
  selectedEndpointOrder: [],
  expandedEndpointModules: new Set(),
  generatorReviewId: null,
  generatorPreflightPassed: false,
  generatorReadyToSave: false,
  generatorDraftRunId: null,
  workbookUploadId: null,
  workbookCases: [],
  selectedWorkbookCases: new Set(),
  apiBuilderWorkspace: null,
  apiBuilderContract: null,
  unitXWorkspace: null,
  testSignature: '',
};

export const $ = (id) => document.getElementById(id);

export function bind(id, event, handler) {
  const element = $(id);
  if (element) element[event] = handler;
}

export async function json(url, options) {
  let response;
  try {
    response = await fetch(url, options);
  } catch (cause) {
    const error = new Error('Không kết nối được Tool-QC UI server. Chờ vài giây rồi thử lại.');
    error.code = 'TQC-UI-001';
    error.detail = cause?.message || 'Failed to fetch';
    throw error;
  }
  if (!response.ok) {
    const text = await response.text();
    let payload;
    try {
      payload = JSON.parse(text);
    } catch {}
    const message =
      response.status === 405
        ? 'UI server đang chạy phiên bản cũ hoặc vừa khởi động lại. Hãy tải lại trang.'
        : payload?.message || text || `HTTP ${response.status}`;
    const error = new Error(message);
    error.code = payload?.code || (response.status === 405 ? 'TQC-UI-405' : `TQC-HTTP-${response.status}`);
    error.detail = payload?.detail;
    error.status = response.status;
    error.payload = payload;
    throw error;
  }
  return response.json();
}

export function readableError(error) {
  const detail = String(error.detail || '').trim();
  const lines = detail.split(/\r?\n/).filter(Boolean);
  const header = error.code ? `${error.code} — ${error.message}` : error.message;
  return lines.length
    ? `${header}\n\n${lines.slice(0, 12).join('\n')}${lines.length > 12 ? `\n\n... và ${lines.length - 12} lỗi khác.` : ''}`
    : header;
}

export function escapeHtml(value) {
  return String(value).replace(
    /[&<>'"]/g,
    (character) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[character],
  );
}
