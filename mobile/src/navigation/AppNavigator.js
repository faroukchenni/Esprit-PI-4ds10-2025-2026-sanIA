import React from 'react';
import { ActivityIndicator, View, Text, Platform, StyleSheet } from 'react-native';
import { useTranslation } from 'react-i18next';
import { NavigationContainer, DarkTheme } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';

import { useAuth } from '../context/AuthContext';
import { colors, gradients } from '../theme/theme';

import LoginScreen from '../screens/auth/LoginScreen';
import RegisterScreen from '../screens/auth/RegisterScreen';
import HomeScreen from '../screens/main/HomeScreen';
import ScanScreen from '../screens/main/ScanScreen';
import ResultScreen from '../screens/main/ResultScreen';
import HistoryScreen from '../screens/main/HistoryScreen';
import ProfileScreen from '../screens/main/ProfileScreen';

const Stack = createNativeStackNavigator();
const Tab = createBottomTabNavigator();

const navTheme = {
  ...DarkTheme,
  colors: {
    ...DarkTheme.colors,
    primary: colors.accent,
    background: colors.bg,
    card: colors.bgElevated,
    text: colors.text,
    border: colors.border,
    notification: colors.accent,
  },
};

function ScanStack() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen name="ScanCamera" component={ScanScreen} />
      <Stack.Screen name="Result" component={ResultScreen} />
    </Stack.Navigator>
  );
}

function MainTabs() {
  const { t } = useTranslation();
  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        headerShown: false,
        tabBarActiveTintColor: colors.accent,
        tabBarInactiveTintColor: colors.tabInactive,
        tabBarBackground: () => (
          <View style={StyleSheet.absoluteFill}>
            <LinearGradient
              colors={gradients.tabBar}
              style={StyleSheet.absoluteFill}
              start={{ x: 0, y: 0 }}
              end={{ x: 0, y: 1 }}
            />
            {/* top border glow */}
            <View style={styles.tabTopBorder} />
          </View>
        ),
        tabBarStyle: {
          backgroundColor: 'transparent',
          borderTopWidth: 0,
          height: Platform.OS === 'ios' ? 72 : 66,
          paddingBottom: Platform.OS === 'ios' ? 22 : 12,
          paddingTop: 10,
          elevation: 0,
          shadowColor: '#000',
          shadowOffset: { width: 0, height: -8 },
          shadowOpacity: 0.4,
          shadowRadius: 20,
        },
        tabBarLabelStyle: {
          fontSize: 10,
          fontWeight: '700',
          letterSpacing: 0.3,
        },
        tabBarIcon: ({ focused, color, size }) => {
          const icons = {
            Home: focused ? 'home' : 'home-outline',
            Scan: focused ? 'scan-circle' : 'scan-circle-outline',
            History: focused ? 'time' : 'time-outline',
            Profile: focused ? 'person' : 'person-outline',
          };
          return (
            <View style={focused ? styles.tabIconFocused : null}>
              <Ionicons
                name={icons[route.name]}
                size={focused ? size + 2 : size}
                color={color}
              />
            </View>
          );
        },
      })}
    >
      <Tab.Screen name="Home" component={HomeScreen} options={{ tabBarLabel: t('tabs.home') }} />
      <Tab.Screen name="Scan" component={ScanStack} options={{ tabBarLabel: t('tabs.detect') }} />
      <Tab.Screen name="History" component={HistoryScreen} options={{ tabBarLabel: t('tabs.history') }} />
      <Tab.Screen name="Profile" component={ProfileScreen} options={{ tabBarLabel: t('tabs.profile') }} />
    </Tab.Navigator>
  );
}

function AuthStack() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen name="Login" component={LoginScreen} />
      <Stack.Screen name="Register" component={RegisterScreen} />
    </Stack.Navigator>
  );
}

export default function AppNavigator() {
  const { user, loading } = useAuth();
  const { t } = useTranslation();

  if (loading) {
    return (
      <LinearGradient colors={gradients.auth} style={styles.loadingContainer}>
        {/* Glow orb */}
        <View style={styles.loadingOrb} />
        <View style={styles.loadingContent}>
          {/* Icon badge */}
          <View style={styles.loadingIconWrap}>
            <LinearGradient colors={gradients.cta} style={styles.loadingIconGrad}>
              <Ionicons name="leaf" size={32} color="#fff" />
            </LinearGradient>
          </View>
          <Text style={styles.loadingTitle}>{t('loading.title')}</Text>
          <Text style={styles.loadingSubtitle}>{t('loading.subtitle')}</Text>
          {/* Progress bar */}
          <View style={styles.loadingBarTrack}>
            <LinearGradient
              colors={gradients.cta}
              style={styles.loadingBarFill}
              start={{ x: 0, y: 0 }}
              end={{ x: 1, y: 0 }}
            />
          </View>
          <ActivityIndicator size="small" color={colors.accent} style={{ marginTop: 16 }} />
        </View>
      </LinearGradient>
    );
  }

  return (
    <NavigationContainer theme={navTheme}>
      {user ? <MainTabs /> : <AuthStack />}
    </NavigationContainer>
  );
}

const styles = StyleSheet.create({
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingOrb: {
    position: 'absolute',
    width: 280,
    height: 280,
    borderRadius: 140,
    backgroundColor: 'rgba(74,222,128,0.08)',
    top: '25%',
    alignSelf: 'center',
  },
  loadingContent: {
    alignItems: 'center',
    paddingHorizontal: 40,
  },
  loadingIconWrap: {
    borderRadius: 28,
    overflow: 'hidden',
    marginBottom: 24,
    ...{
      shadowColor: '#22c55e',
      shadowOffset: { width: 0, height: 8 },
      shadowOpacity: 0.45,
      shadowRadius: 20,
      elevation: 10,
    },
  },
  loadingIconGrad: {
    width: 80,
    height: 80,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingTitle: {
    fontSize: 24,
    fontWeight: '800',
    color: '#fff',
    letterSpacing: -0.3,
    textAlign: 'center',
  },
  loadingSubtitle: {
    fontSize: 14,
    color: 'rgba(74,222,128,0.75)',
    marginTop: 8,
    textAlign: 'center',
    lineHeight: 20,
  },
  loadingBarTrack: {
    width: 160,
    height: 3,
    backgroundColor: 'rgba(255,255,255,0.1)',
    borderRadius: 2,
    overflow: 'hidden',
    marginTop: 28,
  },
  loadingBarFill: {
    width: '65%',
    height: '100%',
    borderRadius: 2,
  },
  tabTopBorder: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    height: 1,
    backgroundColor: 'rgba(74,222,128,0.15)',
  },
  tabIconFocused: {
    backgroundColor: 'rgba(74,222,128,0.15)',
    borderRadius: 10,
    paddingHorizontal: 6,
    paddingVertical: 2,
  },
});
