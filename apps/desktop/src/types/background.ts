/**
 * Background customization types for TinadecCode Desktop
 * Supports image, video, and HTML backgrounds with opacity/blur controls
 */

export type BackgroundType = 'none' | 'image' | 'video' | 'html'

export interface BackgroundSettings {
  type: BackgroundType
  source: string           // URL or local file path
  opacity: number          // 0-100
  blur: number             // 0-20px
  size: 'cover' | 'contain' | 'auto'
  position: 'center' | 'top' | 'bottom' | 'left' | 'right'
  repeat: 'no-repeat' | 'repeat' | 'repeat-x' | 'repeat-y'
}

export interface PanelStyleSettings {
  opacity: number          // 0-100
  blur: number             // 0-20px
  effect: 'opaque' | 'translucent' | 'blur'
}

export interface AppearanceSettings {
  theme: 'dark' | 'light' | 'system'
  accentColor: string
  background: BackgroundSettings
  panelStyles: {
    sidebar: PanelStyleSettings
    chatPanel: PanelStyleSettings
    contextPanel: PanelStyleSettings
  }
}

export const DEFAULT_BACKGROUND_SETTINGS: BackgroundSettings = {
  type: 'none',
  source: '',
  opacity: 100,
  blur: 0,
  size: 'cover',
  position: 'center',
  repeat: 'no-repeat',
}

export const DEFAULT_PANEL_STYLE_SETTINGS: PanelStyleSettings = {
  opacity: 100,
  blur: 0,
  effect: 'opaque',
}
