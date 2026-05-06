/**
 * URL du backend pour **téléphone réel** (même Wi‑Fi que le PC).
 * L’émulateur Android ignore cette valeur et utilise automatiquement http://10.0.2.2:8000.
 *
 * 1. ipconfig → IPv4 Wi‑Fi si l’IP change.
 * 2. Backend : `backend/run_network.ps1` avec --host 0.0.0.0 (pas 127.0.0.1).
 * 3. Rebuild après changement : npx expo run:android
 */
export const BACKEND_BASE_URL = 'http://192.168.1.168:8000';
