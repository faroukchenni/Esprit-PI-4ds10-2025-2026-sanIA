import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View, Text, TextInput, TouchableOpacity, StyleSheet,
  KeyboardAvoidingView, Platform, ScrollView, Alert, ActivityIndicator,
  Image,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../context/AuthContext';
import { API_BASE_URL } from '../../services/api';
import { colors, gradients, radius, shadows } from '../../theme/theme';

const LOGO = require('../../../assets/logo.jpg');

export default function LoginScreen({ navigation }) {
  const { t } = useTranslation();
  const { login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleLogin = async () => {
    if (!email.trim() || !password.trim()) {
      Alert.alert(t('auth.missingFields'), t('auth.missingFieldsMsg'));
      return;
    }
    setLoading(true);
    try {
      await login(email.trim().toLowerCase(), password);
    } catch (err) {
      const isNetwork =
        err.code === 'ERR_NETWORK' ||
        err.message === 'Network Error' ||
        String(err.message || '').includes('Network');
      const detail =
        err.response?.data?.detail ||
        (isNetwork
          ? `${t('auth.loginFailedDefault')}\n\nAPI: ${API_BASE_URL}\n(${t('auth.networkHint')})`
          : err.message || t('auth.loginFailedDefault'));
      Alert.alert(t('auth.loginFailed'), detail);
    } finally {
      setLoading(false);
    }
  };

  return (
    <LinearGradient colors={gradients.auth} style={styles.gradient}>
      {/* Background glow orbs */}
      <View style={styles.orb1} />
      <View style={styles.orb2} />

      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : 'height'} style={styles.flex}>
        <ScrollView
          contentContainerStyle={styles.scroll}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          {/* Hero section */}
          <View style={styles.heroSection}>
            <View style={styles.logoContainer}>
              <LinearGradient colors={gradients.cta} style={styles.logoGradient}>
                <Image source={LOGO} style={styles.logoImg} resizeMode="contain" />
              </LinearGradient>
            </View>
            <Text style={styles.appName}>SANIA AgriSmart</Text>
            <Text style={styles.tagline}>{t('auth.taglineLogin')}</Text>
          </View>

          {/* Glass card */}
          <View style={styles.card}>
            <Text style={styles.title}>{t('auth.welcomeBack')}</Text>
            <Text style={styles.subtitle}>{t('auth.signInSubtitle')}</Text>

            {/* Email input */}
            <View style={styles.inputWrapper}>
              <View style={styles.inputIconWrap}>
                <Ionicons name="mail-outline" size={18} color={colors.accent} />
              </View>
              <TextInput
                style={styles.input}
                placeholder={t('auth.emailPlaceholder')}
                placeholderTextColor={colors.textMuted}
                value={email}
                onChangeText={setEmail}
                autoCapitalize="none"
                keyboardType="email-address"
                returnKeyType="next"
              />
            </View>

            {/* Password input */}
            <View style={styles.inputWrapper}>
              <View style={styles.inputIconWrap}>
                <Ionicons name="lock-closed-outline" size={18} color={colors.accent} />
              </View>
              <TextInput
                style={[styles.input, { flex: 1 }]}
                placeholder={t('auth.passwordPlaceholder')}
                placeholderTextColor={colors.textMuted}
                value={password}
                onChangeText={setPassword}
                secureTextEntry={!showPassword}
                returnKeyType="done"
                onSubmitEditing={handleLogin}
              />
              <TouchableOpacity onPress={() => setShowPassword(!showPassword)} style={styles.eyeBtn}>
                <Ionicons
                  name={showPassword ? 'eye-outline' : 'eye-off-outline'}
                  size={18}
                  color={colors.textMuted}
                />
              </TouchableOpacity>
            </View>

            {/* Primary CTA — white pill */}
            <TouchableOpacity
              style={[styles.primaryBtn, loading && styles.btnDisabled]}
              onPress={handleLogin}
              disabled={loading}
              activeOpacity={0.88}
            >
              {loading ? (
                <ActivityIndicator color={colors.primaryMid} />
              ) : (
                <>
                  <Text style={styles.sparkle}>✦</Text>
                  <Text style={styles.primaryBtnText}>{t('auth.signIn')}</Text>
                </>
              )}
            </TouchableOpacity>

            {/* Divider */}
            <View style={styles.dividerRow}>
              <View style={styles.dividerLine} />
              <Text style={styles.dividerText}>{t('auth.newHere')}</Text>
              <View style={styles.dividerLine} />
            </View>

            {/* Register link */}
            <TouchableOpacity
              style={styles.secondaryBtn}
              onPress={() => navigation.navigate('Register')}
              activeOpacity={0.85}
            >
              <Ionicons name="person-add-outline" size={17} color={colors.accent} />
              <Text style={styles.secondaryBtnText}>{t('auth.createAccountLink')}</Text>
            </TouchableOpacity>
          </View>

          <Text style={styles.terms}>
            {t('auth.termsHint') ?? 'By continuing, you agree to our Terms of Service and Privacy Policy'}
          </Text>
        </ScrollView>
      </KeyboardAvoidingView>
    </LinearGradient>
  );
}

const styles = StyleSheet.create({
  gradient: { flex: 1 },
  flex: { flex: 1 },

  // Background bokeh orbs
  orb1: {
    position: 'absolute',
    width: 300,
    height: 300,
    borderRadius: 150,
    backgroundColor: 'rgba(74,222,128,0.07)',
    top: -80,
    right: -80,
  },
  orb2: {
    position: 'absolute',
    width: 200,
    height: 200,
    borderRadius: 100,
    backgroundColor: 'rgba(34,197,94,0.06)',
    bottom: 100,
    left: -60,
  },

  scroll: {
    flexGrow: 1,
    justifyContent: 'center',
    paddingHorizontal: 24,
    paddingVertical: 48,
  },

  // Hero
  heroSection: {
    alignItems: 'center',
    marginBottom: 32,
  },
  logoContainer: {
    borderRadius: 30,
    overflow: 'hidden',
    ...shadows.glow,
    marginBottom: 18,
  },
  logoGradient: {
    width: 100,
    height: 100,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 8,
  },
  logoImg: { width: '100%', height: '100%', borderRadius: 18 },
  appName: {
    fontSize: 28,
    fontWeight: '800',
    color: colors.text,
    letterSpacing: -0.5,
    marginBottom: 6,
  },
  tagline: {
    fontSize: 14,
    color: colors.textSecondary,
    textAlign: 'center',
    lineHeight: 20,
    paddingHorizontal: 20,
  },

  // Glass card
  card: {
    backgroundColor: colors.glass,
    borderRadius: radius.xl,
    padding: 24,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    ...shadows.card,
  },
  title: {
    fontSize: 22,
    fontWeight: '800',
    color: colors.text,
    marginBottom: 4,
    letterSpacing: -0.3,
  },
  subtitle: {
    fontSize: 14,
    color: colors.textMuted,
    marginBottom: 24,
    lineHeight: 20,
  },

  // Inputs
  inputWrapper: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255,255,255,0.07)',
    borderWidth: 1,
    borderColor: colors.glassBorder,
    borderRadius: radius.md,
    paddingHorizontal: 14,
    marginBottom: 14,
    height: 54,
  },
  inputIconWrap: {
    width: 32,
    alignItems: 'center',
  },
  input: {
    flex: 1,
    fontSize: 15,
    color: colors.text,
    paddingLeft: 4,
  },
  eyeBtn: { padding: 4 },

  // White pill primary button
  primaryBtn: {
    backgroundColor: colors.white,
    borderRadius: radius.pill,
    height: 56,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    marginTop: 6,
    marginBottom: 20,
    ...shadows.cta,
  },
  btnDisabled: { opacity: 0.78 },
  sparkle: {
    fontSize: 16,
    color: colors.primaryMid,
  },
  primaryBtnText: {
    fontSize: 16,
    fontWeight: '800',
    color: colors.primaryMid,
    letterSpacing: 0.2,
  },

  // Divider
  dividerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 16,
    gap: 10,
  },
  dividerLine: {
    flex: 1,
    height: 1,
    backgroundColor: colors.glassBorder,
  },
  dividerText: {
    fontSize: 12,
    color: colors.textMuted,
    fontWeight: '600',
  },

  // Glass secondary button
  secondaryBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    borderRadius: radius.pill,
    height: 52,
    borderWidth: 1.5,
    borderColor: colors.glassBorder,
    backgroundColor: 'rgba(255,255,255,0.05)',
  },
  secondaryBtnText: {
    fontSize: 15,
    fontWeight: '700',
    color: colors.accent,
  },

  terms: {
    fontSize: 11,
    color: colors.textDim,
    textAlign: 'center',
    marginTop: 20,
    lineHeight: 16,
    paddingHorizontal: 10,
  },
});
