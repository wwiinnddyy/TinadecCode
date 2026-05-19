export type ProxyBody = Record<string, unknown> | string | undefined;

export interface ProxyOptions {
  method?: string;
  body?: ProxyBody;
  headers?: HeadersInit;
}

export const coreUrl = process.env.TINADEC_CORE_URL ?? 'http://127.0.0.1:48731';

export function coreEndpoint(path: string): string {
  return new URL(path, coreUrl).toString();
}

export async function proxyJson(path: string, options: ProxyOptions = {}) {
  const body = typeof options.body === 'string'
    ? options.body
    : options.body === undefined
      ? undefined
      : JSON.stringify(options.body);

  const response = await fetch(coreEndpoint(path), {
    method: options.method ?? 'GET',
    headers: {
      accept: 'application/json',
      ...(body ? { 'content-type': 'application/json' } : {}),
      ...options.headers
    },
    body
  });

  const text = await response.text();
  const data = text.length > 0 ? JSON.parse(text) : null;

  return {
    status: response.status,
    data
  };
}

export async function proxySse(path: string, init?: RequestInit): Promise<Response> {
  return fetch(coreEndpoint(path), {
    ...init,
    headers: {
      accept: 'text/event-stream',
      ...(init?.headers ?? {})
    }
  });
}
