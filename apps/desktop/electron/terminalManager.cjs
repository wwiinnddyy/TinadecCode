/**
 * Terminal Manager — Electron main process PTY management.
 *
 * Uses node-pty for real pseudo-terminal support when available, with a
 * graceful fallback to child_process.spawn for environments where the
 * native module cannot be loaded (e.g. ABI mismatch or missing build tools).
 *
 * Each terminal is identified by a unique string ID. Data and exit events
 * are sent back to the renderer via IPC channels scoped per terminal ID.
 */

const { ipcMain, BrowserWindow, app } = require('electron');
const childProcess = require('child_process');
const path = require('node:path');
const fs = require('node:fs');
const os = require('node:os');

// ---- PTY backend selection ----
let pty = null;
try {
  pty = require('node-pty');
} catch (err) {
  console.warn('[terminalManager] node-pty not available, falling back to child_process.spawn:', err.message);
}

const usePty = pty !== null;

/**
 * @typedef {Object} TerminalEntry
 * @property {any} [process]      - node-pty IPty instance or ChildProcess
 * @property {string} id          - unique terminal ID
 * @property {string} shell       - shell executable path
 * @property {string[]} args      - shell arguments
 * @property {string} cwd         - working directory
 * @property {number} cols        - terminal width in columns
 * @property {number} rows        - terminal height in rows
 * @property {boolean} exited     - whether the process has exited
 * @property {boolean} isPty      - whether using real PTY
 * @property {string} title       - terminal display title
 */

/** @type {Map<string, TerminalEntry>} */
const terminals = new Map();

/** @type {Map<string, Set<(data: string) => void>>} */
const dataListeners = new Map();

/** @type {Map<string, Set<(exitCode: number, signal?: number) => void>>} */
const exitListeners = new Map();

let terminalCounter = 0;

/**
 * Generate a unique terminal ID.
 * @returns {string}
 */
function generateId() {
  return `term-${++terminalCounter}`;
}

/**
 * Detect available shell profiles on the current platform.
 * @returns {Array<{id: string, label: string, shell: string, args: string[]}>}
 */
function getAvailableShells() {
  const platform = process.platform;
  const shells = [];

  if (platform === 'win32') {
    // PowerShell (Windows Terminal / pwsh or built-in powershell)
    const pwshPaths = [
      'C:\\Program Files\\PowerShell\\7\\pwsh.exe',
      'C:\\Program Files\\PowerShell\\6\\pwsh.exe',
    ];
    const pwshPath = pwshPaths.find((p) => {
      try { return fs.existsSync(p); } catch { return false; }
    });
    if (pwshPath) {
      shells.push({ id: 'pwsh', label: 'PowerShell 7', shell: pwshPath, args: ['-NoLogo'] });
    }

    // Windows PowerShell (built-in)
    shells.push({
      id: 'powershell',
      label: 'Windows PowerShell',
      shell: 'powershell.exe',
      args: ['-NoLogo'],
    });

    // Command Prompt
    shells.push({
      id: 'cmd',
      label: 'Command Prompt',
      shell: 'cmd.exe',
      args: [],
    });

    // Git Bash (if installed)
    const gitBashPaths = [
      'C:\\Program Files\\Git\\bin\\bash.exe',
      'C:\\Program Files (x86)\\Git\\bin\\bash.exe',
    ];
    const gitBashPath = gitBashPaths.find((p) => {
      try { return fs.existsSync(p); } catch { return false; }
    });
    if (gitBashPath) {
      shells.push({ id: 'gitbash', label: 'Git Bash', shell: gitBashPath, args: ['--login', '-i'] });
    }

    // WSL (if available)
    try {
      const wslCheck = childProcess.execSync('wsl --list --quiet', { encoding: 'utf-8', timeout: 3000 });
      if (wslCheck.trim()) {
        shells.push({ id: 'wsl', label: 'WSL (Ubuntu)', shell: 'wsl.exe', args: [] });
      }
    } catch {
      // WSL not available
    }
  } else if (platform === 'darwin') {
    const zshPath = '/bin/zsh';
    shells.push({ id: 'zsh', label: 'zsh', shell: zshPath, args: ['-l'] });
    shells.push({ id: 'bash', label: 'bash', shell: '/bin/bash', args: ['-l'] });
  } else {
    // Linux
    const bashPath = '/bin/bash';
    shells.push({ id: 'bash', label: 'bash', shell: bashPath, args: ['-l'] });
    try {
      if (fs.existsSync('/bin/zsh')) {
        shells.push({ id: 'zsh', label: 'zsh', shell: '/bin/zsh', args: ['-l'] });
      }
    } catch { /* ignore */ }
  }

  return shells;
}

/**
 * Get the default shell for the current platform.
 * @returns {{shell: string, args: string[]}}
 */
function getDefaultShell() {
  const shells = getAvailableShells();
  const preferred = shells[0] ?? { shell: process.env.SHELL || 'sh', args: [] };
  return { shell: preferred.shell, args: preferred.args };
}

/**
 * Resolve environment variables for the terminal process.
 * Merges process.env with a clean TERM setting.
 * Filters out undefined and null values to avoid issues with child_process.spawn.
 * @param {Record<string, string>} extra
 * @returns {Record<string, string>}
 */
function buildEnv(extra = {}) {
  const cleanEnv = {};
  for (const [key, value] of Object.entries(process.env)) {
    if (value !== undefined && value !== null) {
      cleanEnv[key] = value;
    }
  }
  
  return {
    ...cleanEnv,
    TERM: 'xterm-256color',
    COLORTERM: 'truecolor',
    LANG: process.env.LANG || 'en_US.UTF-8',
    ...extra,
  };
}

/**
 * Create a new terminal process.
 *
 * @param {Object} options
 * @param {string} [options.id]          - pre-assigned terminal ID
 * @param {string} [options.shell]       - shell executable (defaults to platform default)
 * @param {string[]} [options.args]      - shell arguments
 * @param {string} [options.cwd]         - working directory
 * @param {number} [options.cols=80]     - initial terminal width
 * @param {number} [options.rows=24]     - initial terminal height
 * @param {string} [options.title]       - terminal display title
 * @returns {{id: string, shell: string, title: string}}
 */
function createTerminal(options = {}) {
  console.log('[terminalManager] Creating terminal, usePty:', usePty, 'options:', options);
  
  const id = options.id || generateId();
  const defaultShell = getDefaultShell();
  const shell = options.shell || defaultShell.shell;
  const args = options.args || defaultShell.args;
  const cwd = options.cwd || process.env.HOME || process.env.USERPROFILE || os.homedir();
  const cols = options.cols || 80;
  const rows = options.rows || 24;
  const title = options.title || path.basename(shell);
  const env = buildEnv();
  
  console.log('[terminalManager] Terminal config:', { id, shell, args, cwd, cols, rows, title });

  /** @type {TerminalEntry} */
  const entry = {
    id,
    shell,
    args,
    cwd,
    cols,
    rows,
    exited: false,
    isPty: usePty,
    title,
    process: null,
  };

  if (usePty) {
    // ---- Real PTY mode (node-pty) ----
    try {
      const ptyProcess = pty.spawn(shell, args, {
        name: 'xterm-color',
        cols,
        rows,
        cwd,
        env,
      });

      entry.process = ptyProcess;

      ptyProcess.onData((data) => {
        notifyData(id, data);
      });

      ptyProcess.onExit(({ exitCode, signal }) => {
        entry.exited = true;
        notifyExit(id, exitCode, signal);
        terminals.delete(id);
        dataListeners.delete(id);
        exitListeners.delete(id);
      });
    } catch (err) {
      console.error('[terminalManager] Failed to spawn PTY, falling back to spawn:', err.message);
      createSpawnFallback(entry, shell, args, cwd, cols, rows, env);
    }
  } else {
    // ---- Fallback mode (child_process.spawn) ----
    createSpawnFallback(entry, shell, args, cwd, cols, rows, env);
  }

  terminals.set(id, entry);
  return { id, shell, title };
}

/**
 * Create a terminal using child_process.spawn as a fallback.
 * This mode doesn't support full PTY features (no interactive programs like vim),
 * but handles basic command-line interaction.
 *
 * @param {TerminalEntry} entry
 * @param {string} shell
 * @param {string[]} args
 * @param {string} cwd
 * @param {number} cols
 * @param {number} rows
 * @param {Record<string, string>} env
 */
function createSpawnFallback(entry, shell, args, cwd, cols, rows, env) {
  const isWindows = process.platform === 'win32';
  console.log('[terminalManager] Creating spawn fallback for:', shell, 'on Windows:', isWindows);
  
  const child = childProcess.spawn(shell, args, {
    cwd,
    env,
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: false,
    shell: false,
  });

  entry.process = child;
  entry.isPty = false;

  // Convert raw output to string and forward
  const onData = (chunk) => {
    notifyData(entry.id, chunk.toString('utf-8'));
  };

  child.stdout.on('data', onData);
  child.stderr.on('data', onData);

  child.on('error', (err) => {
    console.error('[terminalManager] Spawn error:', err.message);
    notifyData(entry.id, `\r\n\x1b[31mFailed to start process: ${err.message}\x1b[0m\r\n`);
    entry.exited = true;
    notifyExit(entry.id, 1);
    terminals.delete(entry.id);
  });

  child.on('exit', (code, signal) => {
    console.log('[terminalManager] Spawn exited:', { code, signal });
    entry.exited = true;
    notifyExit(entry.id, code ?? 0, signal ? 1 : undefined);
    terminals.delete(entry.id);
    dataListeners.delete(entry.id);
    exitListeners.delete(entry.id);
  });

  // Windows特定：发送初始化命令显示提示符
  if (isWindows && child.stdin && !child.stdin.destroyed) {
    child.stdin.setDefaultEncoding('utf-8');
    
    setTimeout(() => {
      try {
        if (shell.includes('powershell')) {
          child.stdin.write('[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n');
        } else if (shell.includes('cmd')) {
          child.stdin.write('chcp 65001\r\n');
        }
      } catch (e) {
        console.warn('[terminalManager] Init command failed:', e.message);
      }
    }, 200);
  }

  // In spawn mode, we don't have real PTY resize support.
  // The terminal will still function for basic I/O.
}

/**
 * Write data to a terminal's input.
 * @param {string} id - terminal ID
 * @param {string} data - data to write
 */
function writeTerminal(id, data) {
  const entry = terminals.get(id);
  if (!entry || entry.exited) return;

  if (entry.isPty && entry.process && typeof entry.process.write === 'function') {
    entry.process.write(data);
  } else if (entry.process && entry.process.stdin && !entry.process.stdin.destroyed) {
    entry.process.stdin.write(data);
  }
}

/**
 * Resize a terminal.
 * @param {string} id - terminal ID
 * @param {number} cols - new column count
 * @param {number} rows - new row count
 */
function resizeTerminal(id, cols, rows) {
  const entry = terminals.get(id);
  if (!entry || entry.exited) return;

  entry.cols = cols;
  entry.rows = rows;

  if (entry.isPty && entry.process && typeof entry.process.resize === 'function') {
    try {
      entry.process.resize(cols, rows);
    } catch {
      // Ignore resize errors
    }
  }
  // spawn mode doesn't support resize
}

/**
 * Destroy a terminal and kill its process.
 * @param {string} id - terminal ID
 */
function destroyTerminal(id) {
  const entry = terminals.get(id);
  if (!entry) return;

  if (!entry.exited && entry.process) {
    try {
      if (entry.isPty && typeof entry.process.kill === 'function') {
        entry.process.kill();
      } else if (entry.process.kill) {
        entry.process.kill();
      }
    } catch {
      // Process may have already exited
    }
  }

  terminals.delete(id);
  dataListeners.delete(id);
  exitListeners.delete(id);
}

/**
 * Destroy all terminals (called on app quit).
 */
function destroyAllTerminals() {
  for (const id of terminals.keys()) {
    destroyTerminal(id);
  }
}

/**
 * Get info about a terminal.
 * @param {string} id
 * @returns {TerminalEntry | undefined}
 */
function getTerminal(id) {
  return terminals.get(id);
}

/**
 * List all active terminals.
 * @returns {Array<{id: string, shell: string, title: string, exited: boolean}>}
 */
function listTerminals() {
  const result = [];
  for (const [, entry] of terminals) {
    result.push({
      id: entry.id,
      shell: entry.shell,
      title: entry.title,
      exited: entry.exited,
    });
  }
  return result;
}

// ---- Internal event notification ----

/**
 * Notify all data listeners for a terminal.
 * Also sends IPC event to the renderer.
 * @param {string} id
 * @param {string} data
 */
function notifyData(id, data) {
  // Notify local listeners
  const listeners = dataListeners.get(id);
  if (listeners) {
    for (const cb of listeners) {
      try { cb(data); } catch { /* ignore */ }
    }
  }

  // Send via IPC to all browser windows (supports detached panels)
  for (const win of BrowserWindow.getAllWindows()) {
    if (!win.isDestroyed()) {
      win.webContents.send(`terminal:data:${id}`, data);
    }
  }
}

/**
 * Notify all exit listeners for a terminal.
 * Also sends IPC event to the renderer.
 * @param {string} id
 * @param {number} exitCode
 * @param {number} [signal]
 */
function notifyExit(id, exitCode, signal) {
  const listeners = exitListeners.get(id);
  if (listeners) {
    for (const cb of listeners) {
      try { cb(exitCode, signal); } catch { /* ignore */ }
    }
  }

  for (const win of BrowserWindow.getAllWindows()) {
    if (!win.isDestroyed()) {
      win.webContents.send(`terminal:exit:${id}`, { exitCode, signal });
    }
  }
}

// ---- IPC Handler Registration ----

/**
 * Register all terminal-related IPC handlers.
 * Should be called once during app initialization.
 */
function registerTerminalIpc() {
  // Create a new terminal
  ipcMain.handle('terminal:create', async (_event, options) => {
    return createTerminal(options || {});
  });

  // Write data to a terminal
  ipcMain.on('terminal:write', (_event, id, data) => {
    writeTerminal(id, data);
  });

  // Resize a terminal
  ipcMain.on('terminal:resize', (_event, id, cols, rows) => {
    resizeTerminal(id, cols, rows);
  });

  // Destroy a terminal
  ipcMain.on('terminal:destroy', (_event, id) => {
    destroyTerminal(id);
  });

  // Get available shell profiles
  ipcMain.handle('terminal:get-shells', async () => {
    return getAvailableShells();
  });

  // List all active terminals
  ipcMain.handle('terminal:list', async () => {
    return listTerminals();
  });
}

module.exports = {
  createTerminal,
  writeTerminal,
  resizeTerminal,
  destroyTerminal,
  destroyAllTerminals,
  getTerminal,
  listTerminals,
  getAvailableShells,
  getDefaultShell,
  registerTerminalIpc,
};
