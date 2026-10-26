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

import { messages } from './messages/catalogue.js';

export const $ = (id) => document.getElementById(id);

export function locale() {
  try {
    return localStorage.getItem('toolqc.locale') === 'en' ? 'en' : 'vi';
  } catch {
    return 'vi';
  }
}

export function setLocale(value) {
  const next = value === 'en' ? 'en' : 'vi';
  try {
    localStorage.setItem('toolqc.locale', next);
  } catch {}
  document.documentElement.lang = next;
  window.dispatchEvent(new Event('toolqc:localechange'));
  return next;
}

export function translate(code, fallback = '', values = {}) {
  const entry = messages[code];
  const template = entry?.[locale()] || entry?.en || fallback;
  return template.replace(/\{(\w+)\}/g, (_, key) => String(values[key] ?? `{${key}}`));
}

export function bind(id, event, handler) {
  const element = $(id);
  if (element) element[event] = handler;
}

export async function json(url, options) {
  let response;
  try {
    response = await fetch(url, options);
  } catch (cause) {
    const error = new Error(translate('TQC-UI-001'));
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
    const code =
      payload?.errorCode || payload?.code || (response.status === 405 ? 'TQC-UI-405' : `TQC-HTTP-${response.status}`);
    const message = translate(code, payload?.message || text || `HTTP ${response.status}`);
    const error = new Error(message);
    error.code = code;
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
    ? `${header}\n\n${lines.slice(0, 12).join('\n')}${lines.length > 12 ? `\n\n${translate('TQC-UI-052', '', { count: lines.length - 12 })}` : ''}`
    : header;
}

export function escapeHtml(value) {
  return String(value).replace(
    /[&<>'"]/g,
    (character) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[character],
  );
}
