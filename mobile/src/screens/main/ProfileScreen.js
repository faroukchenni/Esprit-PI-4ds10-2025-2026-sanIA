import { useTranslation } from 'react-i18next';
import {
  View, Text, StyleSheet, TouchableOpacity, Alert, ScrollView,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../context/AuthContext';
import { colors, gradients, radius, shadows } from '../../theme/theme';
import MeshOrbs from '../../components/MeshOrbs';
import { setAppLanguage } from '../../i18n';
import i18n from '../../i18n';

const LANG_OPTIONS = [
  { code: 'en', labelKey: 'profile.languageEn' },
  { code: 'fr', labelKey: 'profile.languageFr' },
  { code: 'tn', labelKey: 'profile.languageTn' },
];

export default function ProfileScreen() {
  const { t } = useTranslation();
  const { user, logout } = useAuth();

  const handleLogout = () => {
    Alert.alert(t('profile.signOutTitle'), t('profile.signOutMsg'), [
      { text: t('profile.cancel'), style: 'cancel' },
      {
        text: t('profile.signOut'), style: 'destructive',
        onPress: async () => { await logout(); },
      },
    ]);
  };

  return (
    <LinearGradient colors={gradients.hero} style={styles.gradient}>
      <SafeAreaView style={styles.safeArea} edges={['top']}>
        <ScrollView showsVerticalScrollIndicator={false} contentContainerStyle={styles.scrollContent}>

          {/* ── Avatar Hero ─── */}
          <View style={styles.heroSection}>
            <MeshOrbs variant="compact" />
            <LinearGradient colors={gradients.cta} style={styles.avatar}>
              <Text style={styles.avatarText}>{(user?.name ?? 'U')[0].toUpperCase()}</Text>
            </LinearGradient>
            <Text style={styles.name}>{user?.name ?? '—'}</Text>
            <Text style={styles.email}>{user?.email ?? '—'}</Text>
            <View style={styles.roleBadge}>
              <Ionicons name="shield-checkmark-outline" size={13} color={colors.accent} />
              <Text style={styles.roleText}>
                {user?.role === 'COOPERATIVE_ADMIN' ? t('profile.roleAdmin') : t('profile.roleFarmer')}
              </Text>
            </View>
          </View>

          {/* ── Account ─── */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>{t('profile.account')}</Text>
            <View style={styles.glassCard}>
              <MenuItem icon="person-outline" label={t('profile.fullName')} value={user?.name} accent={colors.accent} />
              <View style={styles.divider} />
              <MenuItem icon="mail-outline" label={t('profile.email')} value={user?.email} accent={colors.accent} />
              <View style={styles.divider} />
              <MenuItem
                icon="shield-checkmark-outline"
                label={t('profile.role')}
                value={user?.role === 'COOPERATIVE_ADMIN' ? t('profile.roleAdmin') : t('profile.roleFarmer')}
                accent={colors.accentMid}
              />
            </View>
          </View>

          {/* ── Language ─── */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>{t('profile.language')}</Text>
            <Text style={styles.sectionHint}>{t('profile.languageHint')}</Text>
            <View style={styles.langRow}>
              {LANG_OPTIONS.map(({ code, labelKey }) => {
                const active = i18n.language === code;
                return (
                  <TouchableOpacity
                    key={code}
                    style={[styles.langChip, active && styles.langChipActive]}
                    onPress={() => setAppLanguage(code)}
                    activeOpacity={0.85}
                  >
                    {active ? (
                      <LinearGradient colors={gradients.cta} style={styles.langChipGrad}>
                        <Text style={styles.langChipTextActive}>{t(labelKey)}</Text>
                      </LinearGradient>
                    ) : (
                      <Text style={styles.langChipText}>{t(labelKey)}</Text>
                    )}
                  </TouchableOpacity>
                );
              })}
            </View>
          </View>

          {/* ── About us ─── */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>{t('profile.aboutUsTitle')}</Text>
            <View style={styles.aboutCard}>
              <Ionicons name="information-circle-outline" size={22} color={colors.accent} style={styles.aboutIcon} />
              <Text style={styles.aboutText}>{t('profile.aboutUsBody')}</Text>
            </View>
          </View>

          {/* ── Supported Crops ─── */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>{t('profile.supportedCrops')}</Text>
            <View style={styles.cropRow}>
              {[
                { emoji: '🍎', name: t('profile.cropApple') },
                { emoji: '🍇', name: t('profile.cropGrape') },
                { emoji: '🥔', name: t('profile.cropPotato') },
                { emoji: '🍅', name: t('profile.cropTomato') },
              ].map((c) => (
                <View key={c.name} style={styles.cropChip}>
                  <Text style={styles.cropEmoji}>{c.emoji}</Text>
                  <Text style={styles.cropName}>{c.name}</Text>
                </View>
              ))}
            </View>
          </View>

          {/* ── Logout ─── */}
          <TouchableOpacity style={styles.logoutBtn} onPress={handleLogout} activeOpacity={0.88}>
            <Ionicons name="log-out-outline" size={20} color={colors.danger} />
            <Text style={styles.logoutText}>{t('profile.signOut')}</Text>
          </TouchableOpacity>

          <View style={{ height: 32 }} />
        </ScrollView>
      </SafeAreaView>
    </LinearGradient>
  );
}

function MenuItem({ icon, label, value, accent }) {
  return (
    <View style={styles.menuItem}>
      <View style={[styles.menuIcon, { backgroundColor: accent + '1A' }]}>
        <Ionicons name={icon} size={18} color={accent} />
      </View>
      <View style={styles.menuContent}>
        <Text style={styles.menuLabel}>{label}</Text>
        {value && <Text style={styles.menuValue}>{value}</Text>}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  gradient: { flex: 1 },
  safeArea: { flex: 1 },
  scrollContent: { paddingBottom: 20 },

  // Hero
  heroSection: {
    alignItems: 'center',
    paddingTop: 16,
    paddingBottom: 32,
    paddingHorizontal: 20,
    overflow: 'hidden',
  },
  avatar: {
    width: 88,
    height: 88,
    borderRadius: 44,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 16,
    borderWidth: 3,
    borderColor: 'rgba(255,255,255,0.3)',
    ...shadows.glow,
  },
  avatarText: { fontSize: 34, fontWeight: '800', color: '#fff' },
  name: { fontSize: 24, fontWeight: '800', color: colors.text, letterSpacing: -0.3, marginBottom: 4 },
  email: { fontSize: 14, color: colors.textMuted, marginBottom: 12 },
  roleBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: colors.glassGreen,
    paddingHorizontal: 14,
    paddingVertical: 6,
    borderRadius: radius.pill,
    borderWidth: 1,
    borderColor: colors.glassGreenBorder,
  },
  roleText: { fontSize: 13, color: colors.accent, fontWeight: '700' },

  // Sections
  section: { marginHorizontal: 16, marginBottom: 20 },
  sectionTitle: {
    fontSize: 11,
    fontWeight: '800',
    color: colors.textMuted,
    marginBottom: 8,
    textTransform: 'uppercase',
    letterSpacing: 1.2,
  },
  sectionHint: { fontSize: 12, color: colors.textMuted, marginBottom: 10, lineHeight: 18 },

  aboutCard: {
    backgroundColor: colors.glass,
    borderRadius: radius.lg,
    padding: 16,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    ...shadows.soft,
  },
  aboutIcon: { marginBottom: 10 },
  aboutText: {
    fontSize: 14,
    color: colors.textSecondary,
    lineHeight: 22,
    letterSpacing: 0.2,
  },

  // Glass card
  glassCard: {
    backgroundColor: colors.glass,
    borderRadius: radius.lg,
    overflow: 'hidden',
    borderWidth: 1,
    borderColor: colors.glassBorder,
    ...shadows.soft,
  },
  divider: { height: 1, backgroundColor: colors.glassBorder, marginHorizontal: 14 },

  menuItem: { flexDirection: 'row', alignItems: 'center', padding: 14 },
  menuIcon: {
    width: 40,
    height: 40,
    borderRadius: 12,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },
  menuContent: { flex: 1 },
  menuLabel: { fontSize: 11, color: colors.textMuted, fontWeight: '700', letterSpacing: 0.3 },
  menuValue: { fontSize: 14, color: colors.text, fontWeight: '600', marginTop: 2 },

  // Language
  langRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  langChip: {
    borderRadius: radius.pill,
    borderWidth: 1.5,
    borderColor: colors.glassBorder,
    overflow: 'hidden',
  },
  langChipActive: { borderColor: 'transparent' },
  langChipGrad: { paddingHorizontal: 18, paddingVertical: 10 },
  langChipText: {
    fontSize: 14,
    fontWeight: '700',
    color: colors.textSecondary,
    paddingHorizontal: 18,
    paddingVertical: 10,
  },
  langChipTextActive: { fontSize: 14, fontWeight: '800', color: colors.primaryMid },

  // Crops
  cropRow: { flexDirection: 'row', gap: 10 },
  cropChip: {
    flex: 1,
    backgroundColor: colors.glass,
    borderRadius: radius.md,
    padding: 12,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.glassBorder,
    ...shadows.soft,
  },
  cropEmoji: { fontSize: 26 },
  cropName: { fontSize: 11, color: colors.textSecondary, fontWeight: '700', marginTop: 4 },

  // Logout
  logoutBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    marginHorizontal: 16,
    backgroundColor: colors.dangerBg,
    borderRadius: radius.lg,
    padding: 16,
    gap: 8,
    borderWidth: 1,
    borderColor: 'rgba(248,113,113,0.2)',
  },
  logoutText: { fontSize: 16, fontWeight: '800', color: colors.danger },
});
