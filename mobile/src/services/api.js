import axios from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Platform } from 'react-native';
import * as Device from 'expo-device';
import { BACKEND_BASE_URL } from '../config/backendUrl';

const stripTrailingSlash = (u) => u.replace(/\/$/, '');

/** Bare IP or host → http://host:8000 (FastAPI default). */
function normalizeBackendBase(raw) {
  let s = String(raw).trim();
  if (!s) return null;
  if (!/^https?:\/\//i.test(s)) {
    s = `http://${s}`;
  }
  let u;
  try {
    u = new URL(s);
  } catch {
    return null;
  }
  if (!u.port && u.protocol === 'http:') {
    return `http://${u.hostname}:8000`;
  }
  if (!u.port && u.protocol === 'https:') {
    return `https://${u.hostname}`;
  }
  return stripTrailingSlash(u.origin);
}

// Priority: backendUrl.js (reliable in release) → .env EXPO_PUBLIC_* → emulator defaults
const fromOverride =
  typeof BACKEND_BASE_URL === 'string' && BACKEND_BASE_URL.trim() !== ''
    ? normalizeBackendBase(BACKEND_BASE_URL)
    : null;
const raw = process.env.EXPO_PUBLIC_API_BASE_URL;
const fromEnv =
  raw != null && String(raw).trim() !== ''
    ? normalizeBackendBase(String(raw).trim())
    : null;

// Simulators/emulators cannot use the PC's Wi‑Fi IP the same way as a real phone.
// Android emulator → 10.0.2.2 reaches the host machine; LAN IP in backendUrl.js is for physical devices only.
const isSimulator = !Device.isDevice;
let resolvedBase =
  fromOverride ||
  fromEnv ||
  (Platform.OS === 'android' ? 'http://10.0.2.2:8000' : 'http://localhost:8000');
if (isSimulator) {
  if (Platform.OS === 'android') {
    resolvedBase = fromEnv || 'http://10.0.2.2:8000';
  } else if (Platform.OS === 'ios') {
    resolvedBase = fromEnv || 'http://localhost:8000';
  }
}
export const API_BASE_URL = resolvedBase;

const api = axios.create({
  baseURL: `${API_BASE_URL}/api/v1`,
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' },
});

// Attach JWT token to every request
api.interceptors.request.use(async (config) => {
  const token = await AsyncStorage.getItem('access_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Handle 401 globally (token expired)
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      await AsyncStorage.removeItem('access_token');
      await AsyncStorage.removeItem('user');
    }
    return Promise.reject(error);
  }
);

export default api;
