import React, { useState } from 'react';
import { 
  Palette, Sun, Moon, Monitor, Check, RotateCcw, 
  Type, Square, Sparkles,
  Eye, EyeOff, Zap, ZapOff, PanelLeftClose, PanelLeft,
  Home, LayoutGrid
} from 'lucide-react';
import { toast } from 'sonner';
import { 
  useTheme, 
  THEME_PRESETS,
  type AccentColorType, 
  type FontSizeType, 
  type BorderRadiusType,
  type ThemeMode,
  type ThemePreset,
} from '@/app/contexts/ThemeContext';
import type { UserSettings, UpdateUserSettingsRequest, DataDensityType } from '@/app/api/types';

export interface AppearanceSectionProps {
  settings: UserSettings;
  updateSettings: (updates: UpdateUserSettingsRequest) => void;
}

// Theme mode options
const THEME_MODES: { value: ThemeMode; label: string; icon: React.ElementType; description: string }[] = [
  { value: 'light', label: 'Light', icon: Sun, description: 'Bright and clean' },
  { value: 'dark', label: 'Dark', icon: Moon, description: 'Easy on eyes' },
  { value: 'system', label: 'Auto', icon: Monitor, description: 'Match device' },
];

// Theme preset options with preview colors
const THEME_PRESET_OPTIONS: { value: ThemePreset; emoji: string }[] = [
  { value: 'default', emoji: '🎨' },
  { value: 'midnight', emoji: '🌙' },
  { value: 'sunset', emoji: '🌅' },
  { value: 'ocean', emoji: '🌊' },
  { value: 'forest', emoji: '🌲' },
  { value: 'lavender', emoji: '💜' },
  { value: 'coffee', emoji: '☕' },
  { value: 'nord', emoji: '❄️' },
  { value: 'dracula', emoji: '🧛' },
  { value: 'monochrome', emoji: '⬛' },
];

// Data density options with visual examples
const DATA_DENSITIES: { value: DataDensityType; label: string; description: string; scale: number }[] = [
  { value: 'compact', label: 'Compact', description: 'More content, less space', scale: 0.8 },
  { value: 'comfortable', label: 'Comfortable', description: 'Balanced (recommended)', scale: 1 },
  { value: 'spacious', label: 'Spacious', description: 'Extra breathing room', scale: 1.2 },
];

// Accent color options (only shown for default preset)
const ACCENT_COLORS: { value: AccentColorType; label: string; color: string }[] = [
  { value: 'orange', label: 'Orange', color: '#ff7a59' },
  { value: 'blue', label: 'Blue', color: '#3b82f6' },
  { value: 'green', label: 'Green', color: '#22c55e' },
  { value: 'purple', label: 'Purple', color: '#a855f7' },
  { value: 'rose', label: 'Rose', color: '#f43f5e' },
  { value: 'teal', label: 'Teal', color: '#14b8a6' },
  { value: 'amber', label: 'Amber', color: '#f59e0b' },
  { value: 'indigo', label: 'Indigo', color: '#6366f1' },
  { value: 'cyan', label: 'Cyan', color: '#06b6d4' },
  { value: 'emerald', label: 'Emerald', color: '#10b981' },
];

// Font size options
const FONT_SIZES: { value: FontSizeType; label: string; size: string }[] = [
  { value: 'small', label: 'Small', size: '14px' },
  { value: 'default', label: 'Default', size: '16px' },
  { value: 'large', label: 'Large', size: '18px' },
  { value: 'extra-large', label: 'XL', size: '20px' },
];

// Border radius options
const BORDER_RADII: { value: BorderRadiusType; label: string; radius: string }[] = [
  { value: 'none', label: 'Sharp', radius: '0' },
  { value: 'small', label: 'Subtle', radius: '4px' },
  { value: 'default', label: 'Rounded', radius: '8px' },
  { value: 'large', label: 'Soft', radius: '12px' },
  { value: 'full', label: 'Pill', radius: '9999px' },
];

// Toggle Switch Component
function ToggleSwitch({ 
  checked, 
  onChange, 
  label, 
  description,
  iconOn,
  iconOff 
}: { 
  checked: boolean; 
  onChange: (checked: boolean) => void;
  label: string;
  description: string;
  iconOn?: React.ElementType;
  iconOff?: React.ElementType;
}) {
  const IconOn = iconOn;
  const IconOff = iconOff;
  
  return (
    <label className="flex items-center justify-between p-4 bg-slate-50 dark:bg-slate-800/50 rounded-xl cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors group">
      <div className="flex items-center gap-3">
        {(IconOn && IconOff) && (
          <div className={`w-10 h-10 rounded-lg flex items-center justify-center transition-colors ${
            checked 
              ? 'bg-[var(--brand-primary)]/20' 
              : 'bg-slate-200 dark:bg-slate-700'
          }`}>
            {checked 
              ? <IconOn className="w-5 h-5" style={{ color: 'var(--brand-primary)' }} />
              : <IconOff className="w-5 h-5 text-slate-500 dark:text-slate-400" />
            }
          </div>
        )}
        <div>
          <p className="font-medium text-slate-900 dark:text-slate-100">{label}</p>
          <p className="text-sm text-slate-500 dark:text-slate-400">{description}</p>
        </div>
      </div>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className="relative w-12 h-7 rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2"
        style={{ 
          backgroundColor: checked ? 'var(--brand-primary)' : undefined,
        }}
      >
        {!checked && <span className="absolute inset-0 rounded-full bg-slate-300 dark:bg-slate-600" />}
        <span className={`absolute top-0.5 left-0.5 w-6 h-6 bg-white rounded-full shadow transition-transform ${
          checked ? 'translate-x-5' : ''
        }`} />
      </button>
    </label>
  );
}

// Section Header Component
function SectionHeader({ icon: Icon, title, description }: { icon: React.ElementType; title: string; description: string }) {
  return (
    <div className="flex items-start gap-3 mb-4">
      <div className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0" style={{ backgroundColor: 'var(--brand-primary)', opacity: 0.15 }}>
        <Icon className="w-5 h-5" style={{ color: 'var(--brand-primary)' }} />
      </div>
      <div>
        <h3 className="font-semibold text-slate-900 dark:text-slate-100">{title}</h3>
        <p className="text-sm text-slate-500 dark:text-slate-400">{description}</p>
      </div>
    </div>
  );
}

export function AppearanceSection({ settings, updateSettings }: AppearanceSectionProps) {
  const theme = useTheme();
  const [showSaved, setShowSaved] = useState(false);

  // Show feedback when settings change
  const handleChange = <T,>(setter: (value: T) => void, value: T, settingsKey?: keyof UpdateUserSettingsRequest, settingsValue?: unknown) => {
    setter(value);
    if (settingsKey) {
      updateSettings({ [settingsKey]: settingsValue ?? value });
    }
    setShowSaved(true);
    setTimeout(() => setShowSaved(false), 2000);
  };

  const handleReset = () => {
    theme.resetAppearance();
    updateSettings({
      theme: 'system',
      dataDensity: 'comfortable',
      sidebarCollapsed: false,
      showWelcomeBanner: true,
    });
    toast.success('Appearance settings reset to defaults');
  };

  const currentPreset = THEME_PRESETS[theme.appearance.themePreset];

  return (
    <div className="p-6 space-y-8">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold text-slate-900 dark:text-slate-100 flex items-center gap-2">
            <Palette className="w-5 h-5" style={{ color: 'var(--brand-primary)' }} />
            Appearance
          </h2>
          <p className="text-slate-600 dark:text-slate-400 text-sm mt-1">
            Make it yours - changes apply instantly everywhere
          </p>
        </div>
        <div className="flex items-center gap-2">
          {showSaved && (
            <span className="flex items-center gap-1 text-sm text-green-600 dark:text-green-400 animate-in fade-in slide-in-from-right-2">
              <Check className="w-4 h-4" />
              Applied!
            </span>
          )}
          <button
            type="button"
            onClick={handleReset}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
          >
            <RotateCcw className="w-4 h-4" />
            Reset
          </button>
        </div>
      </div>

      {/* Theme Presets - Main Feature */}
      <div className="space-y-4">
        <SectionHeader 
          icon={Sparkles} 
          title="Theme Presets" 
          description="Choose a complete look for your workspace"
        />
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
          {THEME_PRESET_OPTIONS.map((preset) => {
            const config = THEME_PRESETS[preset.value];
            const isSelected = theme.appearance.themePreset === preset.value;
            const isDark = config.forceDark;
            
            return (
              <button
                key={preset.value}
                onClick={() => handleChange(theme.setThemePreset, preset.value)}
                className={`relative flex flex-col items-center gap-2 p-3 border-2 rounded-xl transition-all ${
                  isSelected
                    ? 'border-[var(--brand-primary)] ring-2 ring-[var(--brand-primary)]/20'
                    : 'border-slate-200 dark:border-slate-700 hover:border-slate-300 dark:hover:border-slate-600'
                }`}
              >
                {isSelected && (
                  <div className="absolute -top-1.5 -right-1.5 w-5 h-5 rounded-full flex items-center justify-center" style={{ backgroundColor: 'var(--brand-primary)' }}>
                    <Check className="w-3 h-3 text-white" />
                  </div>
                )}
                {/* Preview Box */}
                <div 
                  className="w-full h-12 rounded-lg overflow-hidden border"
                  style={{ 
                    backgroundColor: config.colors.background || (isDark ? '#1e293b' : '#ffffff'),
                    borderColor: config.colors.border || (isDark ? 'rgba(255,255,255,0.1)' : 'rgba(0,0,0,0.1)')
                  }}
                >
                  <div 
                    className="h-3"
                    style={{ backgroundColor: isDark ? 'rgba(255,255,255,0.05)' : 'rgba(0,0,0,0.05)' }}
                  />
                  <div className="flex items-center gap-1 p-1.5">
                    <div className="w-6 h-2 rounded" style={{ backgroundColor: config.colors.primary }} />
                    <div className="w-4 h-2 rounded" style={{ backgroundColor: config.colors.secondary }} />
                  </div>
                </div>
                <div className="text-center">
                  <span className="text-lg">{preset.emoji}</span>
                  <p className={`text-xs font-medium mt-0.5 ${isSelected ? 'text-[var(--brand-primary)]' : 'text-slate-700 dark:text-slate-300'}`}>
                    {config.name}
                  </p>
                </div>
              </button>
            );
          })}
        </div>
        {/* Preset description */}
        <p className="text-sm text-slate-500 dark:text-slate-400 text-center italic">
          {currentPreset.description}
          {currentPreset.forceDark && ' (Always dark)'}
        </p>
      </div>

      {/* Theme Mode (Light/Dark/System) - only if preset doesn't force a mode */}
      {!currentPreset.forceDark && !currentPreset.forceLight && (
        <div className="space-y-4">
          <SectionHeader 
            icon={Sun} 
            title="Mode" 
            description="Light, dark, or match your system"
          />
          <div className="flex gap-2">
            {THEME_MODES.map((mode) => {
              const Icon = mode.icon;
              const isSelected = theme.appearance.themeMode === mode.value;
              return (
                <button
                  key={mode.value}
                  onClick={() => handleChange(theme.setThemeMode, mode.value, 'theme', mode.value)}
                  className={`flex-1 flex flex-col items-center gap-2 p-3 border-2 rounded-xl transition-all ${
                    isSelected
                      ? 'border-[var(--brand-primary)] bg-[var(--brand-primary)]/5'
                      : 'border-slate-200 dark:border-slate-700 hover:border-slate-300 dark:hover:border-slate-600'
                  }`}
                >
                  <Icon className={`w-5 h-5 ${isSelected ? 'text-[var(--brand-primary)]' : 'text-slate-500 dark:text-slate-400'}`} style={isSelected ? { color: 'var(--brand-primary)' } : {}} />
                  <span className={`text-sm font-medium ${isSelected ? 'text-[var(--brand-primary)]' : 'text-slate-700 dark:text-slate-300'}`} style={isSelected ? { color: 'var(--brand-primary)' } : {}}>
                    {mode.label}
                  </span>
                </button>
              );
            })}
          </div>
        </div>
      )}

      {/* Accent Color - only for default preset */}
      {theme.appearance.themePreset === 'default' && (
        <div className="space-y-4">
          <SectionHeader 
            icon={Palette} 
            title="Accent Color" 
            description="Your personal touch on buttons and highlights"
          />
          <div className="flex flex-wrap gap-2">
            {ACCENT_COLORS.map((color) => {
              const isSelected = theme.appearance.accentColor === color.value;
              return (
                <button
                  key={color.value}
                  onClick={() => handleChange(theme.setAccentColor, color.value)}
                  className={`relative group p-1 rounded-full transition-all ${
                    isSelected ? 'ring-2 ring-offset-2 ring-slate-400 dark:ring-slate-500' : ''
                  }`}
                  title={color.label}
                >
                  <div 
                    className="w-8 h-8 rounded-full shadow-md transition-transform group-hover:scale-110"
                    style={{ backgroundColor: color.color }}
                  />
                  {isSelected && (
                    <div className="absolute inset-0 flex items-center justify-center">
                      <Check className="w-4 h-4 text-white drop-shadow-md" />
                    </div>
                  )}
                </button>
              );
            })}
          </div>
        </div>
      )}

      {/* Data Density */}
      <div className="space-y-4">
        <SectionHeader 
          icon={LayoutGrid} 
          title="Data Density" 
          description="How much information to show at once"
        />
        <div className="grid grid-cols-3 gap-3">
          {DATA_DENSITIES.map((d) => {
            const isSelected = theme.appearance.dataDensity === d.value;
            return (
              <button
                key={d.value}
                onClick={() => handleChange(theme.setDataDensity, d.value, 'dataDensity', d.value)}
                className={`flex flex-col items-center gap-2 p-4 border-2 rounded-xl transition-all ${
                  isSelected
                    ? 'border-[var(--brand-primary)] bg-[var(--brand-primary)]/5'
                    : 'border-slate-200 dark:border-slate-700 hover:border-slate-300 dark:hover:border-slate-600'
                }`}
              >
                {/* Visual density preview */}
                <div className="w-full space-y-1" style={{ transform: `scale(${d.scale})`, transformOrigin: 'center' }}>
                  {[1, 2, 3].map((i) => (
                    <div key={i} className="h-1.5 rounded bg-slate-300 dark:bg-slate-600" style={{ width: `${100 - i * 15}%` }} />
                  ))}
                </div>
                <div className="text-center mt-1">
                  <p className={`text-sm font-medium ${isSelected ? '' : 'text-slate-700 dark:text-slate-300'}`} style={isSelected ? { color: 'var(--brand-primary)' } : {}}>
                    {d.label}
                  </p>
                  <p className="text-xs text-slate-500 dark:text-slate-400">{d.description}</p>
                </div>
              </button>
            );
          })}
        </div>
      </div>

      {/* Font Size */}
      <div className="space-y-4">
        <SectionHeader 
          icon={Type} 
          title="Text Size" 
          description="Adjust for better readability"
        />
        <div className="flex gap-2">
          {FONT_SIZES.map((f) => {
            const isSelected = theme.appearance.fontSize === f.value;
            return (
              <button
                key={f.value}
                onClick={() => handleChange(theme.setFontSize, f.value)}
                className={`flex-1 flex flex-col items-center gap-1 p-3 border-2 rounded-xl transition-all ${
                  isSelected
                    ? 'border-[var(--brand-primary)] bg-[var(--brand-primary)]/5'
                    : 'border-slate-200 dark:border-slate-700 hover:border-slate-300 dark:hover:border-slate-600'
                }`}
              >
                <span 
                  className={`font-bold ${isSelected ? '' : 'text-slate-600 dark:text-slate-400'}`}
                  style={{ fontSize: f.size, color: isSelected ? 'var(--brand-primary)' : undefined }}
                >
                  Aa
                </span>
                <span className={`text-xs ${isSelected ? '' : 'text-slate-500 dark:text-slate-400'}`} style={isSelected ? { color: 'var(--brand-primary)' } : {}}>
                  {f.label}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      {/* Corner Style */}
      <div className="space-y-4">
        <SectionHeader 
          icon={Square} 
          title="Corner Style" 
          description="Roundness of buttons and cards"
        />
        <div className="flex gap-2">
          {BORDER_RADII.map((r) => {
            const isSelected = theme.appearance.borderRadius === r.value;
            return (
              <button
                key={r.value}
                onClick={() => handleChange(theme.setBorderRadius, r.value)}
                className={`flex-1 flex flex-col items-center gap-2 p-3 border-2 rounded-xl transition-all ${
                  isSelected
                    ? 'border-[var(--brand-primary)] bg-[var(--brand-primary)]/5'
                    : 'border-slate-200 dark:border-slate-700 hover:border-slate-300 dark:hover:border-slate-600'
                }`}
              >
                <div 
                  className={`w-8 h-8 border-2 ${isSelected ? 'border-[var(--brand-primary)] bg-[var(--brand-primary)]/10' : 'border-slate-300 dark:border-slate-600'}`}
                  style={{ borderRadius: r.radius, borderColor: isSelected ? 'var(--brand-primary)' : undefined }}
                />
                <span className={`text-xs font-medium ${isSelected ? '' : 'text-slate-600 dark:text-slate-400'}`} style={isSelected ? { color: 'var(--brand-primary)' } : {}}>
                  {r.label}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      {/* Accessibility */}
      <div className="space-y-4">
        <SectionHeader 
          icon={Eye} 
          title="Accessibility" 
          description="Options for comfort and visibility"
        />
        <div className="space-y-3">
          <ToggleSwitch
            checked={theme.appearance.reduceMotion}
            onChange={(v) => handleChange(theme.setReduceMotion, v)}
            label="Reduce Motion"
            description="Minimize animations throughout the app"
            iconOn={ZapOff}
            iconOff={Zap}
          />
          <ToggleSwitch
            checked={theme.appearance.highContrast}
            onChange={(v) => handleChange(theme.setHighContrast, v)}
            label="High Contrast"
            description="Stronger borders and text contrast"
            iconOn={Eye}
            iconOff={EyeOff}
          />
        </div>
      </div>

      {/* Layout */}
      <div className="space-y-4">
        <SectionHeader 
          icon={PanelLeft} 
          title="Layout" 
          description="Navigation and page elements"
        />
        <div className="space-y-3">
          <ToggleSwitch
            checked={theme.appearance.sidebarCollapsed}
            onChange={(v) => handleChange(theme.setSidebarCollapsed, v, 'sidebarCollapsed', v)}
            label="Compact Sidebar"
            description="Show icons only in navigation"
            iconOn={PanelLeftClose}
            iconOff={PanelLeft}
          />
          <ToggleSwitch
            checked={theme.appearance.showWelcomeBanner}
            onChange={(v) => handleChange(theme.setShowWelcomeBanner, v, 'showWelcomeBanner', v)}
            label="Welcome Banner"
            description="Personalized greeting on dashboard"
            iconOn={Home}
            iconOff={Home}
          />
        </div>
      </div>

      {/* Live Preview */}
      <div className="space-y-4 pt-6 border-t border-slate-200 dark:border-slate-700">
        <div className="flex items-center gap-2 mb-4">
          <Eye className="w-5 h-5" style={{ color: 'var(--brand-primary)' }} />
          <h3 className="font-semibold text-slate-900 dark:text-slate-100">Live Preview</h3>
        </div>
        
        <div className="p-4 rounded-2xl border-2 border-dashed border-slate-300 dark:border-slate-600">
          <div className="rounded-xl overflow-hidden shadow-lg" style={{ backgroundColor: 'var(--card, #ffffff)' }}>
            {/* Mini header */}
            <div className="px-4 py-3 border-b flex items-center justify-between" style={{ borderColor: 'var(--border)' }}>
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-lg" style={{ backgroundColor: 'var(--brand-primary)' }} />
                <div className="w-24 h-3 rounded" style={{ backgroundColor: 'var(--muted, #e5e7eb)' }} />
              </div>
              <div className="flex items-center gap-2">
                <div className="w-8 h-8 rounded-full" style={{ backgroundColor: 'var(--muted, #e5e7eb)' }} />
              </div>
            </div>
            
            {/* Mini content */}
            <div className="p-4 flex gap-4">
              {/* Sidebar */}
              <div className={`shrink-0 space-y-2 ${theme.appearance.sidebarCollapsed ? 'w-8' : 'w-28'}`}>
                {[1, 2, 3].map((i) => (
                  <div key={i} className={`flex items-center gap-2 p-2 rounded-lg ${i === 1 ? '' : ''}`} style={i === 1 ? { backgroundColor: 'var(--brand-primary)', opacity: 0.15 } : {}}>
                    <div className="w-4 h-4 rounded" style={{ backgroundColor: i === 1 ? 'var(--brand-primary)' : 'var(--muted, #e5e7eb)' }} />
                    {!theme.appearance.sidebarCollapsed && (
                      <div className="flex-1 h-2 rounded" style={{ backgroundColor: i === 1 ? 'var(--brand-primary)' : 'var(--muted, #e5e7eb)', opacity: i === 1 ? 0.5 : 1 }} />
                    )}
                  </div>
                ))}
              </div>
              
              {/* Main area */}
              <div className="flex-1 space-y-3">
                <div className="h-4 w-32 rounded" style={{ backgroundColor: 'var(--foreground, #1f2937)', opacity: 0.7 }} />
                <div className="grid grid-cols-3 gap-2">
                  {[1, 2, 3].map((i) => (
                    <div key={i} className="p-3 rounded-lg border" style={{ borderColor: 'var(--border)', borderRadius: 'var(--radius)' }}>
                      <div className="h-2 w-full rounded mb-2" style={{ backgroundColor: 'var(--muted, #e5e7eb)' }} />
                      <div className="h-3 w-12 rounded" style={{ backgroundColor: 'var(--brand-primary)', opacity: 0.3 }} />
                    </div>
                  ))}
                </div>
                <div className="flex gap-2 justify-end mt-4">
                  <button 
                    className="px-3 py-1.5 text-xs font-medium border" 
                    style={{ borderRadius: 'var(--radius)', borderColor: 'var(--border)', color: 'var(--foreground, #1f2937)' }}
                  >
                    Cancel
                  </button>
                  <button 
                    className="px-3 py-1.5 text-xs font-medium text-white"
                    style={{ borderRadius: 'var(--radius)', backgroundColor: 'var(--brand-primary)' }}
                  >
                    Save
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
