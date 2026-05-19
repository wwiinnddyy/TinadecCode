export function basenameFromPath(path: string): string {
  const trimmed = path.trim().replace(/[\\/]+$/, '');
  const parts = trimmed.split(/[\\/]/);
  return parts.at(-1) || trimmed || 'Project';
}

export function toneForStatus(status: string): 'ok' | 'warn' | 'danger' | 'neutral' {
  if (status === 'ok' || status === 'approved' || status === 'active') return 'ok';
  if (status === 'pending') return 'warn';
  if (status === 'error' || status === 'rejected' || status === 'missing') return 'danger';
  return 'neutral';
}
