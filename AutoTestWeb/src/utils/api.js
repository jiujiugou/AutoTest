import { reactive } from 'vue';

export const toastState = reactive({
  visible: false,
  message: ''
});

let hideTimer = null;

export function showToast(msg, duration = 1800) {
  toastState.message = msg == null ? '' : String(msg);
  toastState.visible = true;

  if (hideTimer) clearTimeout(hideTimer);
  hideTimer = setTimeout(() => {
    toastState.visible = false;
  }, duration);
}

export function httpTargetTemplate() {
  return {
    type: 'HTTP',
    method: 'GET',
    url: 'https://example.com/',
    headers: {},
    body: '',
    timeoutMs: 8000
  };
}

export function tcpTargetTemplate() {
  return {
    type: 'TCP',
    host: '127.0.0.1',
    port: 80,
    timeoutMs: 3000
  };
}

export function now() {
  return Date.now();
}

export function uuid() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  const s4 = () => Math.floor((1 + Math.random()) * 0x10000).toString(16).slice(1);
  return `${s4()}${s4()}-${s4()}-${s4()}-${s4()}-${s4()}${s4()}${s4()}`;
}

export async function clipboardWriteText(text) {
  if (typeof navigator !== 'undefined' && navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
    await navigator.clipboard.writeText(text);
    return;
  }
  throw new Error('clipboard not available');
}

export async function apiFetch(url, options = {}) {
  const requestInit = { ...options };
  const method = (requestInit.method || 'GET').toUpperCase();

  const headers = new Headers(requestInit.headers || {});
  if ((method === 'POST' || method === 'PUT' || method === 'PATCH') && requestInit.body != null) {
    if (!headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  }
  if (!headers.has('Accept')) headers.set('Accept', 'application/json');
  requestInit.headers = headers;

  const res = await fetch(url, requestInit);

  const contentType = res.headers.get('content-type') || '';
  const isJson = contentType.includes('application/json');

  const readBody = async () => {
    if (res.status === 204) return null;
    if (isJson) {
      try {
        return await res.json();
      } catch {
        return null;
      }
    }
    return await res.text();
  };

  const data = await readBody();

  if (!res.ok) {
    const message =
      (data && typeof data === 'object' && 'message' in data && typeof data.message === 'string' && data.message) ||
      (typeof data === 'string' && data.trim()) ||
      `${res.status} ${res.statusText}`;
    throw new Error(message);
  }

  return data;
}
