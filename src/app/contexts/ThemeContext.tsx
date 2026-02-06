import React, { createContext, useContext, useEffect, useState, useCallback } from 'react';
import type { DataDensityType } from '@/app/api/types';

// Extended theme types - more options!
export type ThemeMode = 'light' | 'dark' | 'system';
export type ThemePreset = 
  | 'default'      // Classic orange accent
  | 'midnight'     // Deep blue dark theme
  | 'sunset'       // Warm orange/red theme
  | 'ocean'        // Cool blue/teal theme
  | 'forest'       // Natural green theme
  | 'lavender'     // Soft purple theme
  | 'monochrome'   // Black & white minimal
  | 'coffee'       // Warm brown tones
  | 'nord'         // Nordic-inspired cool colors
  | 'dracula';     // Popular dark purple theme

export type AccentColorType = 'orange' | 'blue' | 'green' | 'purple' | 'rose' | 'teal' | 'amber' | 'indigo' | 'cyan' | 'emerald';
export type FontSizeType = 'small' | 'default' | 'large' | 'extra-large';
export type BorderRadiusType = 'none' | 'small' | 'default' | 'large' | 'full';

// Extended appearance settings
export interface AppearanceSettings {
  themeMode: ThemeMode;
  themePreset: ThemePreset;
  dataDensity: DataDensityType;
  accentColor: AccentColorType;
  fontSize: FontSizeType;
  reduceMotion: boolean;
  highContrast: boolean;
  sidebarCollapsed: boolean;
  showWelcomeBanner: boolean;
  borderRadius: BorderRadiusType;
  // Legacy support
  theme?: 'light' | 'dark' | 'system';
}

const STORAGE_KEY = 'crm_appearance_settings';

const defaultAppearance: AppearanceSettings = {
  themeMode: 'system',
  themePreset: 'default',
  dataDensity: 'comfortable',
  accentColor: 'orange',
  fontSize: 'default',
  reduceMotion: false,
  highContrast: false,
  sidebarCollapsed: false,
  showWelcomeBanner: true,
  borderRadius: 'default',
};

// Get stored settings from localStorage
function getStoredAppearance(): AppearanceSettings {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return defaultAppearance;
    const stored = JSON.parse(raw);
    // Migrate old 'theme' to 'themeMode'
    if (stored.theme && !stored.themeMode) {
      stored.themeMode = stored.theme;
    }
    return { ...defaultAppearance, ...stored };
  } catch {
    return defaultAppearance;
  }
}

// Save settings to localStorage
function saveStoredAppearance(settings: AppearanceSettings) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
  } catch {
    // ignore
  }
}

// Theme preset configurations
export interface ThemePresetConfig {
  name: string;
  description: string;
  forceDark?: boolean;  // Some presets force dark mode
  forceLight?: boolean; // Some presets force light mode
  colors: {
    primary: string;
    primaryDark: string;
    primaryLight: string;
    secondary: string;
    background?: string;
    backgroundSubtle?: string;
    card?: string;
    foreground?: string;
    muted?: string;
    mutedForeground?: string;
    border?: string;
  };
}

export const THEME_PRESETS: Record<ThemePreset, ThemePresetConfig> = {
  default: {
    name: 'Default',
    description: 'Classic orange accent, clean look',
    colors: {
      primary: '#ff7a59',
      primaryDark: '#e85d3a',
      primaryLight: '#ff9b80',
      secondary: '#14b8a6',
    },
  },
  midnight: {
    name: 'Midnight',
    description: 'Deep blue, perfect for night work',
    forceDark: true,
    colors: {
      primary: '#6366f1',
      primaryDark: '#4f46e5',
      primaryLight: '#818cf8',
      secondary: '#8b5cf6',
      background: '#0f0f23',
      backgroundSubtle: '#1a1a2e',
      card: '#16213e',
      foreground: '#e2e8f0',
      muted: '#1e293b',
      mutedForeground: '#94a3b8',
      border: 'rgba(99, 102, 241, 0.2)',
    },
  },
  sunset: {
    name: 'Sunset',
    description: 'Warm orange and red tones',
    colors: {
      primary: '#f97316',
      primaryDark: '#ea580c',
      primaryLight: '#fb923c',
      secondary: '#ef4444',
      background: '#fffbeb',
      backgroundSubtle: '#fef3c7',
    },
  },
  ocean: {
    name: 'Ocean',
    description: 'Cool blue and teal waves',
    colors: {
      primary: '#0ea5e9',
      primaryDark: '#0284c7',
      primaryLight: '#38bdf8',
      secondary: '#06b6d4',
    },
  },
  forest: {
    name: 'Forest',
    description: 'Natural green, earthy feel',
    colors: {
      primary: '#22c55e',
      primaryDark: '#16a34a',
      primaryLight: '#4ade80',
      secondary: '#84cc16',
    },
  },
  lavender: {
    name: 'Lavender',
    description: 'Soft purple, calm and elegant',
    colors: {
      primary: '#a855f7',
      primaryDark: '#9333ea',
      primaryLight: '#c084fc',
      secondary: '#d946ef',
    },
  },
  monochrome: {
    name: 'Monochrome',
    description: 'Minimal black and white',
    colors: {
      primary: '#171717',
      primaryDark: '#0a0a0a',
      primaryLight: '#404040',
      secondary: '#525252',
    },
  },
  coffee: {
    name: 'Coffee',
    description: 'Warm brown, cozy atmosphere',
    colors: {
      primary: '#92400e',
      primaryDark: '#78350f',
      primaryLight: '#b45309',
      secondary: '#a16207',
      background: '#fef7ed',
      backgroundSubtle: '#fed7aa',
    },
  },
  nord: {
    name: 'Nord',
    description: 'Arctic, bluish calm palette',
    forceDark: true,
    colors: {
      primary: '#88c0d0',
      primaryDark: '#81a1c1',
      primaryLight: '#8fbcbb',
      secondary: '#5e81ac',
      background: '#2e3440',
      backgroundSubtle: '#3b4252',
      card: '#434c5e',
      foreground: '#eceff4',
      muted: '#4c566a',
      mutedForeground: '#d8dee9',
      border: 'rgba(136, 192, 208, 0.2)',
    },
  },
  dracula: {
    name: 'Dracula',
    description: 'Popular dark purple theme',
    forceDark: true,
    colors: {
      primary: '#bd93f9',
      primaryDark: '#a679f0',
      primaryLight: '#caa6ff',
      secondary: '#ff79c6',
      background: '#282a36',
      backgroundSubtle: '#1e1f29',
      card: '#44475a',
      foreground: '#f8f8f2',
      muted: '#44475a',
      mutedForeground: '#6272a4',
      border: 'rgba(189, 147, 249, 0.2)',
    },
  },
};

// CSS variable mappings for accent colors
const accentColorVars: Record<AccentColorType, { primary: string; primaryDark: string; primaryLight: string }> = {
  orange: { primary: '#ff7a59', primaryDark: '#e85d3a', primaryLight: '#ff9b80' },
  blue: { primary: '#3b82f6', primaryDark: '#2563eb', primaryLight: '#60a5fa' },
  green: { primary: '#22c55e', primaryDark: '#16a34a', primaryLight: '#4ade80' },
  purple: { primary: '#a855f7', primaryDark: '#9333ea', primaryLight: '#c084fc' },
  rose: { primary: '#f43f5e', primaryDark: '#e11d48', primaryLight: '#fb7185' },
  teal: { primary: '#14b8a6', primaryDark: '#0d9488', primaryLight: '#5eead4' },
  amber: { primary: '#f59e0b', primaryDark: '#d97706', primaryLight: '#fbbf24' },
  indigo: { primary: '#6366f1', primaryDark: '#4f46e5', primaryLight: '#818cf8' },
  cyan: { primary: '#06b6d4', primaryDark: '#0891b2', primaryLight: '#22d3ee' },
  emerald: { primary: '#10b981', primaryDark: '#059669', primaryLight: '#34d399' },
};

const fontSizeVars: Record<FontSizeType, string> = {
  small: '14px',
  default: '16px',
  large: '18px',
  'extra-large': '20px',
};

const borderRadiusVars: Record<BorderRadiusType, string> = {
  none: '0',
  small: '0.375rem',
  default: '0.625rem',
  large: '1rem',
  full: '9999px',
};

const dataDensityVars: Record<DataDensityType, { 
  pagePadding: string; 
  contentGap: string; 
  tablePadding: string;
  cardPadding: string;
  inputHeight: string;
}> = {
  compact: { 
    pagePadding: '0.75rem', 
    contentGap: '0.5rem', 
    tablePadding: '0.5rem 0.75rem',
    cardPadding: '0.75rem',
    inputHeight: '2rem',
  },
  comfortable: { 
    pagePadding: '1rem', 
    contentGap: '1rem', 
    tablePadding: '0.75rem 1rem',
    cardPadding: '1rem',
    inputHeight: '2.5rem',
  },
  spacious: { 
    pagePadding: '1.5rem', 
    contentGap: '1.5rem', 
    tablePadding: '1rem 1.25rem',
    cardPadding: '1.5rem',
    inputHeight: '3rem',
  },
};

interface ThemeContextValue {
  // Current settings
  appearance: AppearanceSettings;
  // Computed resolved theme (accounts for system preference and preset)
  resolvedTheme: 'light' | 'dark';
  // Current preset config
  currentPreset: ThemePresetConfig;
  // Update functions
  setThemeMode: (mode: ThemeMode) => void;
  setThemePreset: (preset: ThemePreset) => void;
  setDataDensity: (density: DataDensityType) => void;
  setAccentColor: (color: AccentColorType) => void;
  setFontSize: (size: FontSizeType) => void;
  setReduceMotion: (reduce: boolean) => void;
  setHighContrast: (high: boolean) => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
  setShowWelcomeBanner: (show: boolean) => void;
  setBorderRadius: (radius: BorderRadiusType) => void;
  // Legacy support
  setTheme: (theme: ThemeMode) => void;
  // Bulk update
  updateAppearance: (updates: Partial<AppearanceSettings>) => void;
  // Reset to defaults
  resetAppearance: () => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

// Hook to detect system preference
function useSystemTheme(): 'light' | 'dark' {
  const [systemTheme, setSystemTheme] = useState<'light' | 'dark'>(() => {
    if (typeof window === 'undefined') return 'light';
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  });

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = (e: MediaQueryListEvent) => {
      setSystemTheme(e.matches ? 'dark' : 'light');
    };
    mediaQuery.addEventListener('change', handler);
    return () => mediaQuery.removeEventListener('change', handler);
  }, []);

  return systemTheme;
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [appearance, setAppearance] = useState<AppearanceSettings>(getStoredAppearance);
  const systemTheme = useSystemTheme();

  // Get current preset config
  const currentPreset = THEME_PRESETS[appearance.themePreset] || THEME_PRESETS.default;

  // Resolve actual theme (considering preset overrides)
  const resolvedTheme = (() => {
    // Preset can force dark or light mode
    if (currentPreset.forceDark) return 'dark';
    if (currentPreset.forceLight) return 'light';
    // Otherwise use user preference
    if (appearance.themeMode === 'system') return systemTheme;
    return appearance.themeMode;
  })();

  // Apply all CSS changes to document
  useEffect(() => {
    const root = document.documentElement;
    
    // Apply dark/light theme class
    if (resolvedTheme === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }

    // Apply theme preset attribute for CSS targeting
    root.setAttribute('data-theme', appearance.themePreset);

    // Apply preset colors
    const presetColors = currentPreset.colors;
    
    // Primary colors (use preset primary, or accent color if preset is default)
    if (appearance.themePreset === 'default') {
      // For default preset, use the user's accent color choice
      const accent = accentColorVars[appearance.accentColor];
      root.style.setProperty('--brand-primary', accent.primary);
      root.style.setProperty('--brand-primary-dark', accent.primaryDark);
      root.style.setProperty('--brand-primary-light', accent.primaryLight);
    } else {
      // For other presets, use the preset's colors
      root.style.setProperty('--brand-primary', presetColors.primary);
      root.style.setProperty('--brand-primary-dark', presetColors.primaryDark);
      root.style.setProperty('--brand-primary-light', presetColors.primaryLight);
    }
    
    // Secondary color
    root.style.setProperty('--brand-secondary', presetColors.secondary);

    // Preset-specific background/card colors (if defined)
    if (presetColors.background) {
      root.style.setProperty('--background', presetColors.background);
    } else {
      root.style.removeProperty('--background');
    }
    if (presetColors.backgroundSubtle) {
      root.style.setProperty('--background-subtle', presetColors.backgroundSubtle);
    } else {
      root.style.removeProperty('--background-subtle');
    }
    if (presetColors.card) {
      root.style.setProperty('--card', presetColors.card);
    } else {
      root.style.removeProperty('--card');
    }
    if (presetColors.foreground) {
      root.style.setProperty('--foreground', presetColors.foreground);
    } else {
      root.style.removeProperty('--foreground');
    }
    if (presetColors.muted) {
      root.style.setProperty('--muted', presetColors.muted);
    } else {
      root.style.removeProperty('--muted');
    }
    if (presetColors.mutedForeground) {
      root.style.setProperty('--muted-foreground', presetColors.mutedForeground);
    } else {
      root.style.removeProperty('--muted-foreground');
    }
    if (presetColors.border) {
      root.style.setProperty('--border', presetColors.border);
    } else {
      root.style.removeProperty('--border');
    }

    // Apply font size
    root.style.setProperty('--font-size', fontSizeVars[appearance.fontSize]);

    // Apply border radius
    root.style.setProperty('--radius', borderRadiusVars[appearance.borderRadius]);

    // Apply data density
    const density = dataDensityVars[appearance.dataDensity];
    root.style.setProperty('--density-page-padding', density.pagePadding);
    root.style.setProperty('--density-content-gap', density.contentGap);
    root.style.setProperty('--density-table-padding', density.tablePadding);
    root.style.setProperty('--density-card-padding', density.cardPadding);
    root.style.setProperty('--density-input-height', density.inputHeight);

    // Apply reduce motion
    if (appearance.reduceMotion) {
      root.classList.add('reduce-motion');
    } else {
      root.classList.remove('reduce-motion');
    }

    // Apply high contrast
    if (appearance.highContrast) {
      root.classList.add('high-contrast');
    } else {
      root.classList.remove('high-contrast');
    }

    // Apply sidebar state
    if (appearance.sidebarCollapsed) {
      root.classList.add('sidebar-collapsed');
    } else {
      root.classList.remove('sidebar-collapsed');
    }
  }, [appearance, resolvedTheme, currentPreset]);

  // Save to localStorage whenever appearance changes
  useEffect(() => {
    saveStoredAppearance(appearance);
  }, [appearance]);

  // Update functions
  const updateAppearance = useCallback((updates: Partial<AppearanceSettings>) => {
    setAppearance(prev => {
      // Handle legacy 'theme' updates
      if (updates.theme && !updates.themeMode) {
        updates.themeMode = updates.theme;
      }
      return { ...prev, ...updates };
    });
  }, []);

  const setThemeMode = useCallback((themeMode: ThemeMode) => updateAppearance({ themeMode }), [updateAppearance]);
  const setThemePreset = useCallback((themePreset: ThemePreset) => updateAppearance({ themePreset }), [updateAppearance]);
  const setDataDensity = useCallback((dataDensity: DataDensityType) => updateAppearance({ dataDensity }), [updateAppearance]);
  const setAccentColor = useCallback((accentColor: AccentColorType) => updateAppearance({ accentColor }), [updateAppearance]);
  const setFontSize = useCallback((fontSize: FontSizeType) => updateAppearance({ fontSize }), [updateAppearance]);
  const setReduceMotion = useCallback((reduceMotion: boolean) => updateAppearance({ reduceMotion }), [updateAppearance]);
  const setHighContrast = useCallback((highContrast: boolean) => updateAppearance({ highContrast }), [updateAppearance]);
  const setSidebarCollapsed = useCallback((sidebarCollapsed: boolean) => updateAppearance({ sidebarCollapsed }), [updateAppearance]);
  const setShowWelcomeBanner = useCallback((showWelcomeBanner: boolean) => updateAppearance({ showWelcomeBanner }), [updateAppearance]);
  const setBorderRadius = useCallback((borderRadius: BorderRadiusType) => updateAppearance({ borderRadius }), [updateAppearance]);
  // Legacy support
  const setTheme = useCallback((theme: ThemeMode) => setThemeMode(theme), [setThemeMode]);

  const resetAppearance = useCallback(() => {
    setAppearance(defaultAppearance);
  }, []);

  return (
    <ThemeContext.Provider value={{
      appearance,
      resolvedTheme,
      currentPreset,
      setThemeMode,
      setThemePreset,
      setTheme,
      setDataDensity,
      setAccentColor,
      setFontSize,
      setReduceMotion,
      setHighContrast,
      setSidebarCollapsed,
      setShowWelcomeBanner,
      setBorderRadius,
      updateAppearance,
      resetAppearance,
    }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
}

// Optional hook for components that might be outside provider
export function useThemeOptional() {
  return useContext(ThemeContext);
}

// Script to prevent flash of wrong theme on page load
// Add this to index.html before any other scripts
export const THEME_INIT_SCRIPT = `
(function() {
  try {
    var stored = localStorage.getItem('crm_appearance_settings');
    if (stored) {
      var settings = JSON.parse(stored);
      var mode = settings.themeMode || settings.theme || 'system';
      var preset = settings.themePreset || 'default';
      var isDark = mode === 'dark' || 
        (mode === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches) ||
        ['midnight', 'nord', 'dracula'].includes(preset);
      if (isDark) {
        document.documentElement.classList.add('dark');
      }
      document.documentElement.setAttribute('data-theme', preset);
    }
  } catch (e) {}
})();
`;
