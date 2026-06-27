# TinadecCode终端功能修复计划

## Context

**问题**：用户点击"新建终端"按钮没有任何反应，终端无法正常使用。

**根因分析**：
1. **最可能**：`node-pty`原生模块与Electron 39的ABI不兼容，导致加载失败
2. **次要**：`child_process.spawn` fallback模式下，非PTY模式可能导致shell无响应
3. **辅助**：错误处理不足，用户看不到任何错误反馈

**数据流**：
```
按钮点击 → handleNewTerminal() → createTerminalInstance()
  → window.tinadec.terminal.create() [IPC]
    → terminalManager.cjs createTerminal()
      → node-pty spawn (主模式) 或 child_process.spawn (fallback)
```

---

## 诊断步骤

### 阶段1：快速检查

**1.1 检查node-pty加载状态**
```powershell
cd d:\github\agent\TinadecCode\apps\desktop
node -e "try { require('node-pty'); console.log('OK'); } catch(e) { console.error('FAILED:', e.message); }"
```

**1.2 在Electron DevTools中验证API**
```javascript
console.log('terminal API:', window.tinadec?.terminal);
window.tinadec.terminal.getShells().then(s => console.log('shells:', s));
```

**1.3 检查主进程日志**
查找：`[terminalManager] node-pty not available` 消息

---

## 修复方案

### 修复1：重新编译node-pty（优先级：高）

**文件**：无需修改文件，执行命令

```powershell
cd d:\github\agent\TinadecCode\apps\desktop

# 清理缓存
rm -rf node_modules/.cache
rm -rf node_modules/node-pty/build

# 重新编译
npm run rebuild:native

# 验证
node -e "require('node-pty') && console.log('node-pty OK')"
```

**如果失败**：需要安装Python和C++构建工具
```powershell
npm install -g windows-build-tools
```

---

### 修复2：增强terminalManager.cjs的错误处理和fallback

**文件**：`d:\github\agent\TinadecCode\apps\desktop\electron\terminalManager.cjs`

**修改点**：

1. **添加详细日志**（在createTerminal函数开头）
```javascript
function createTerminal(options = {}) {
  console.log('[terminalManager] Creating terminal, usePty:', usePty, 'options:', options);
  // ... 现有代码
}
```

2. **改进buildEnv函数**（过滤undefined值）
```javascript
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
```

3. **改进createSpawnFallback函数**（Windows特殊处理）
```javascript
function createSpawnFallback(entry, shell, args, cwd, cols, rows, env) {
  // ... 现有spawn代码
  
  // 增强stdin处理
  if (child.stdin && !child.stdin.destroyed) {
    child.stdin.setDefaultEncoding('utf-8');
    
    // Windows特定：发送初始化命令显示提示符
    if (process.platform === 'win32') {
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
  }
}
```

---

### 修复3：增强useTerminal.ts错误处理

**文件**：`d:\github\agent\TinadecCode\apps\desktop\src\composables\useTerminal.ts`

**修改点**：

1. **添加错误状态**
```typescript
const terminalError = ref<string | null>(null);
```

2. **修改createTerminalInstance**（在catch块中设置错误状态）
```typescript
catch (err) {
  const msg = err instanceof Error ? err.message : String(err);
  console.error('[useTerminal] Failed to create terminal:', msg);
  terminalError.value = msg;
  return null;
}
```

3. **在返回对象中暴露错误状态**
```typescript
return {
  // ... 现有返回
  terminalError,
  clearError: () => { terminalError.value = null; },
}
```

---

### 修复4：增强TerminalPanel.vue错误反馈

**文件**：`d:\github\agent\TinadecCode\apps\desktop\src\components\TerminalPanel.vue`

**修改点**：

1. **添加错误显示UI**（在template中）
```vue
<!-- 在terminal-panel div开头添加 -->
<div v-if="errorMessage" class="terminal-error">
  <span>{{ errorMessage }}</span>
  <button @click="errorMessage = null">×</button>
</div>
```

2. **修改handleNewTerminal**（添加try-catch和错误显示）
```typescript
const errorMessage = ref<string | null>(null);

async function handleNewTerminal(shellId?: string): Promise<void> {
  errorMessage.value = null;
  showShellMenu.value = false;
  
  try {
    const result = await createTerminal({
      shellId: shellId || (availableShells.value[0]?.id ?? 'default'),
      cwd: props.cwd,
      title: availableShells.value.find((s) => s.id === shellId)?.label ?? 'Terminal',
    });
    
    if (!result) {
      errorMessage.value = t('terminal.createFailed');
      return;
    }
    
    await nextTick();
    if (activeTerminalId.value) {
      fitTerminal(activeTerminalId.value);
      setTimeout(() => focusTerminal(activeTerminalId.value!), 100);
    }
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : t('terminal.createFailed');
  }
}
```

3. **添加错误样式**
```css
.terminal-error {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: var(--bg-error);
  color: var(--text-error);
  font-size: 12px;
  border-bottom: 1px solid var(--border-error);
}

.terminal-error button {
  background: none;
  border: none;
  color: inherit;
  cursor: pointer;
}
```

---

### 修复5：确保shell检测健壮性

**文件**：`d:\github\agent\TinadecCode\apps\desktop\electron\terminalManager.cjs`

**修改点**：在getAvailableShells函数末尾添加默认shell
```javascript
function getAvailableShells() {
  // ... 现有检测代码
  
  // 确保至少有一个shell
  if (shells.length === 0) {
    shells.push({
      id: 'default',
      label: 'Default Shell',
      shell: process.platform === 'win32' ? 'cmd.exe' : '/bin/sh',
      args: [],
    });
  }
  
  return shells;
}
```

---

## 验证方法

### 1. 编译验证
```powershell
cd d:\github\agent\TinadecCode\apps\desktop
npm run rebuild:native
node -e "require('node-pty') && console.log('node-pty: OK')"
```

### 2. 功能验证
1. 启动应用：`npm run dev`
2. 打开侧边栏终端面板
3. 点击"+"按钮
4. 选择shell（PowerShell/CMD）
5. 验证终端显示并可输入

### 3. IPC验证（在DevTools控制台）
```javascript
window.tinadec.terminal.create({ shell: 'powershell.exe' })
  .then(r => console.log('Created:', r))
  .catch(e => console.error('Error:', e));
```

### 4. 错误处理验证
- 故意使用无效shell路径
- 验证显示友好的错误消息

---

## 修复文件清单

| 文件 | 修改类型 | 优先级 |
|------|----------|--------|
| `apps/desktop/electron/terminalManager.cjs` | 增强日志、改进fallback、环境变量清理 | 高 |
| `apps/desktop/src/composables/useTerminal.ts` | 添加错误状态管理 | 高 |
| `apps/desktop/src/components/TerminalPanel.vue` | 添加错误UI反馈 | 中 |

---

## 预期结果

1. **如果node-pty编译成功**：终端完全正常工作（交互式shell、resize支持）
2. **如果node-pty失败但fallback正常**：基本终端功能可用（命令执行，但无交互式程序支持）
3. **两种情况**：用户都能看到有意义的错误消息，而不是无响应
