import React from 'react';
import { StatusBar } from 'expo-status-bar';
import { initI18n } from './src/i18n';
import { AuthProvider } from './src/context/AuthContext';
import AppNavigator from './src/navigation/AppNavigator';

initI18n();

export default function App() {
  return (
    <AuthProvider>
      <StatusBar style="light" />
      <AppNavigator />
    </AuthProvider>
  );
}
