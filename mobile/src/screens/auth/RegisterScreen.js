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
import { colors, gradients, radius, shadows } from '../../theme/theme';

const LOGO = require('../../../assets/logo.jpg');

export default function RegisterScreen({ navigation }) {
  const { t } = useTranslation();
  const { register } = useAuth();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleRegister = async () => {
    if (!name.trim() || !email.trim() || !password || !confirm) {
      Alert.alert(t('auth.missingFields'), t('auth.missingAllFields'));
      return;
    }
    if (password !== confirm) {
      Alert.alert(t('auth.passwordMismatch'), t('auth.passwordMismatchMsg'));
      return;
    }
    if (password.length < 6) {
      Alert.alert(t('auth.weakPassword'), t('auth.weakPasswordMsg'));
      return;
    }
    setLoading(true);
    try {
      await register(name.trim(), email.trim().toLowerCase(), password);
      Alert.alert(t('auth.accountCreated'), t('auth.accountCreatedMsg'), [
        { text: t('auth.ok'), onPress: () => navigation.navigate('Login') },
      ]);
    } catch (err) {
      const msg = err.response?.data?.detail || t('auth.registerError');
      Alert.alert(t('auth.error'), msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <LinearGradient colors={gradients.auth} style={styles.gradient}>
      {/* Background bokeh orbs */}
      <View style={styles.orb1} />
      <View style={styles.orb2} />
      <View style={styles.orb3} />

      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : 'height'} style={styles.flex}>
        <ScrollView
          contentContainerStyle={styles.scroll}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          {/* Header row */}
          <View style={styles.headerRow}>
            <TouchableOpacity style={styles.backBtn} onPress={() => navigation.goBack()}>
              <Ionicons name="arrow-back" size={20} color={colors.text} />
            </TouchableOpacity>
            {/* Step dots */}
            <View style={styles.dotsRow}>
              <View style={styles.dotInactive} />
              <View style={styles.dotActive} />
              <View style={styles.dotInactive} />
            </View>
            <View style={{ width: 40 }} />
          </View>

          {/* Logo + Title */}
          <View style={styles.heroSection}>
            <View style={styles.logoContainer}>
              <LinearGradient colors={gradients.cta} style={styles.logoGradient}>
                <Image source={LOGO} style={styles.logoImg} resizeMode="contain" />
              </LinearGradient>
            </View>
            <Text style={styles.title}>Join SANIA</Text>
            <Text style={styles.subtitle}>{t('auth.createFarmerAccount')}</Text>
          </View>

          {/* Glass form card */}
          <View style={styles.card}>
            {/* Name */}
            <View style={styles.inputWrapper}>
              <View style={styles.inputIconWrap}>
                <Ionicons name="person-outline" size={18} color={colors.accent} />
              </View>
              <TextInput
                style={styles.input}
                placeholder={t('auth.fullName')}
                placeholderTextColor={colors.textMuted}
                value={name}
                onChangeText={setName}
                returnKeyType="next"
              />
            </View>

            {/* Email */}
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

            {/* Password */}
            <View style={styles.inputWrapper}>
              <View style={styles.inputIconWrap}>
                <Ionicons name="lock-closed-outline" size={18} color={colors.accent} />
              </View>
              <TextInput
                style={[styles.input, { flex: 1 }]}
                placeholder={t('auth.passwordMin')}
                placeholderTextColor={colors.textMuted}
                value={password}
                onChangeText={setPassword}
                secureTextEntry={!showPassword}
                returnKeyType="next"
              />
              <TouchableOpacity onPress={() => setShowPassword(!showPassword)} style={styles.eyeBtn}>
                <Ionicons
                  name={showPassword ? 'eye-outline' : 'eye-off-outline'}
                  size={18}
                  color={colors.textMuted}
                />
              </TouchableOpacity>
            </View>

            {/* Confirm password */}
            <View style={styles.inputWrapper}>
              <View style={styles.inputIconWrap}>
                <Ionicons name="shield-checkmark-outline" size={18} color={colors.accent} />
              </View>
              <TextInput
                style={styles.input}
                placeholder={t('auth.confirmPassword')}
                placeholderTextColor={colors.textMuted}
                value={confirm}
                onChangeText={setConfirm}
                secureTextEntry={!showPassword}
                returnKeyType="done"
                onSubmitEditing={handleRegister}
              />
            </View>

            {/* Primary CTA — white pill */}
            <TouchableOpacity
              style={[styles.primaryBtn, loading && styles.btnDisabled]}
              onPress={handleRegister}
              disabled={loading}
              activeOpacity={0.88}
            >
              {loading ? (
                <ActivityIndicator color={colors.primaryMid} />
              ) : (
                <>
                  <Text style={styles.sparkle}>✦</Text>
                  <Text style={styles.primaryBtnText}>{t('auth.createAccount')}</Text>
                </>
              )}
            </TouchableOpacity>

            {/* Divider */}
            <View style={styles.dividerRow}>
              <View style={styles.dividerLine} />
              <Text style={styles.dividerText}>{t('auth.alreadyHaveAccount')}</Text>
              <View style={styles.dividerLine} />
            </View>

            {/* Sign in link */}
            <TouchableOpacity
              style={styles.secondaryBtn}
              onPress={() => navigation.goBack()}
              activeOpacity={0.85}
            >
              <Ionicons name="log-in-outline" size={17} color={colors.accent} />
              <Text style={styles.secondaryBtnText}>{t('auth.signInLink')}</Text>
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
    width: 280,
    height: 280,
    borderRadius: 140,
    backgroundColor: 'rgba(74,222,128,0.08)',
    top: -60,
    right: -60,
  },
  orb2: {
    position: 'absolute',
    width: 180,
    height: 180,
    borderRadius: 90,
    backgroundColor: 'rgba(34,197,94,0.06)',
    bottom: 120,
    left: -50,
  },
  orb3: {
    position: 'absolute',
    width: 100,
    height: 100,
    borderRadius: 50,
    backgroundColor: 'rgba(134,239,172,0.05)',
    bottom: 300,
    right: 30,
  },

  scroll: {
    flexGrow: 1,
    paddingHorizontal: 24,
    paddingTop: 56,
    paddingBottom: 40,
  },

  // Header row with back + dots
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 28,
  },
  backBtn: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.glass,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    justifyContent: 'center',
    alignItems: 'center',
  },
  dotsRow: {
    flexDirection: 'row',
    gap: 6,
    alignItems: 'center',
  },
  dotInactive: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: 'rgba(255,255,255,0.2)',
  },
  dotActive: {
    width: 24,
    height: 8,
    borderRadius: 4,
    backgroundColor: colors.accent,
  },

  // Hero
  heroSection: {
    alignItems: 'center',
    marginBottom: 28,
  },
  logoContainer: {
    borderRadius: 26,
    overflow: 'hidden',
    ...shadows.glow,
    marginBottom: 16,
  },
  logoGradient: {
    width: 84,
    height: 84,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 8,
  },
  logoImg: { width: '100%', height: '100%', borderRadius: 16 },
  title: {
    fontSize: 26,
    fontWeight: '800',
    color: colors.text,
    letterSpacing: -0.4,
    marginBottom: 6,
  },
  subtitle: {
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
    padding: 22,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    ...shadows.card,
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
    marginBottom: 12,
    height: 54,
  },
  inputIconWrap: {
    width: 30,
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
    marginTop: 8,
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
