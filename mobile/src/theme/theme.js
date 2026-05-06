/**
 * SANIA AgriSmart — Plant Doctor Dark Design System
 * Deep forest green + glassmorphism + vivid lime accent
 */

export const colors = {
  // ── Backgrounds ─────────────────────────────────────────
  bg: '#091a10',
  bgDeep: '#050d08',
  bgElevated: '#0d2318',
  bgSurface: '#102b1a',
  bgCard: 'rgba(255,255,255,0.07)',

  // ── Forest Greens ────────────────────────────────────────
  primary: '#0f2e1a',
  primaryMid: '#1a4d2e',
  primaryLight: '#25663d',

  // ── Vivid Accent (lime/bright green) ─────────────────────
  accent: '#4ade80',
  accentMid: '#22c55e',
  accentDark: '#16a34a',
  accentSoft: 'rgba(74,222,128,0.15)',
  accentGlow: 'rgba(74,222,128,0.35)',

  // ── Glassmorphism Surfaces ───────────────────────────────
  glass: 'rgba(255,255,255,0.08)',
  glassMid: 'rgba(255,255,255,0.12)',
  glassBorder: 'rgba(255,255,255,0.15)',
  glassGreen: 'rgba(74,222,128,0.1)',
  glassGreenBorder: 'rgba(74,222,128,0.28)',
  glassDark: 'rgba(0,0,0,0.28)',

  // ── Text ────────────────────────────────────────────────
  text: '#FFFFFF',
  textSecondary: 'rgba(255,255,255,0.72)',
  textMuted: 'rgba(255,255,255,0.45)',
  textDim: 'rgba(255,255,255,0.25)',

  // ── Status ───────────────────────────────────────────────
  danger: '#f87171',
  dangerBg: 'rgba(248,113,113,0.15)',
  offline: '#fca5a5',
  warning: '#fbbf24',
  warningBg: 'rgba(251,191,36,0.15)',

  // ── Legacy aliases (keep existing screen code working) ───
  mint: 'rgba(74,222,128,0.15)',
  mintDark: 'rgba(74,222,128,0.75)',
  gold: '#fbbf24',
  goldSoft: 'rgba(251,191,36,0.2)',
  cyan: '#4ade80',
  cyanSoft: 'rgba(74,222,128,0.15)',
  tabInactive: 'rgba(255,255,255,0.38)',
  border: 'rgba(255,255,255,0.1)',
  borderStrong: 'rgba(255,255,255,0.2)',
  white: '#FFFFFF',
};

export const gradients = {
  // ── App background ───────────────────────────────────────
  bg: ['#050d08', '#091a10', '#0d2318'],

  // ── Hero / Home header ───────────────────────────────────
  hero: ['#040c07', '#081510', '#0d2318', '#112a1c'],
  heroMesh: ['rgba(74,222,128,0.18)', 'rgba(34,197,94,0.06)'],

  // ── Short headers ────────────────────────────────────────
  headerShort: ['#040c07', '#081510', '#0d2318'],

  // ── Auth screens ─────────────────────────────────────────
  auth: ['#030907', '#071210', '#0b1e14', '#0f2a1a'],

  // ── CTA buttons ──────────────────────────────────────────
  cta: ['#15803d', '#16a34a', '#22c55e', '#4ade80'],
  ctaAlt: ['#1a4d2e', '#1e5c36', '#25663d'],
  ctaShine: ['rgba(255,255,255,0.2)', 'rgba(255,255,255,0)'],

  // ── Card surfaces ────────────────────────────────────────
  cardShine: ['rgba(255,255,255,0.06)', 'rgba(255,255,255,0)'],
  statWash: ['rgba(255,255,255,0.1)', 'rgba(255,255,255,0.04)'],

  // ── Tab bar ──────────────────────────────────────────────
  tabBar: ['rgba(3,9,5,0.97)', 'rgba(6,15,9,0.99)'],

  // ── Accent accents ───────────────────────────────────────
  aurora: ['#22c55e', '#4ade80', '#86efac'],
  featureCard: ['rgba(74,222,128,0.18)', 'rgba(34,197,94,0.06)'],
  featureCardAlt: ['rgba(255,255,255,0.1)', 'rgba(255,255,255,0.04)'],
};

export const radius = {
  sm: 10,
  md: 16,
  lg: 22,
  xl: 30,
  pill: 999,
};

export const shadows = {
  card: {
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.45,
    shadowRadius: 22,
    elevation: 10,
  },
  soft: {
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 14,
    elevation: 5,
  },
  tab: {
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: -6 },
    shadowOpacity: 0.4,
    shadowRadius: 18,
    elevation: 16,
  },
  cta: {
    shadowColor: '#22c55e',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.4,
    shadowRadius: 24,
    elevation: 12,
  },
  glow: {
    shadowColor: '#4ade80',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.55,
    shadowRadius: 20,
    elevation: 6,
  },
};
