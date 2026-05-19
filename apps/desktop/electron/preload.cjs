const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('tinadec', {
  gatewayUrl: () => process.env.TINADEC_GATEWAY_URL ?? 'http://127.0.0.1:48730',
  openProjectDialog: () => ipcRenderer.invoke('tinadec:open-project')
});
