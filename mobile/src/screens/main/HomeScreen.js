import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View, Text, StyleSheet, ScrollView,
  TouchableOpacity, RefreshControl, ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../context/AuthContext';
import { scanService } from '../../services/scanService';
import { getDiseaseInfo } from '../../utils/diseaseInfo';
import { colors, gradients, radius, shadows } from '../../theme/theme';
import MeshOrbs from '../../components/MeshOrbs';

export default function HomeScreen({ navigation }) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [scans, setScans] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const fetchScans = async () => {
    try {
      const data = await scanService.getHistory();
      setScans(data.slice(0, 5));
    } catch (_) {
      // ignore
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => { fetchScans(); }, []);

  const stats = {
    total: scans.length,
    diseased: scans.filter(s => !s.predicted_disease?.includes('healthy')).length,
    healthy: scans.filter(s => s.predicted_disease?.includes('healthy')).length,
  };

  const firstName = user?.name?.split(' ')[0] ?? t('home.farmerDefault');

  return (
    <LinearGradient colors={gradients.hero} style={styles.gradient}>
      <SafeAreaView style={styles.safeArea} edges={['top']}>
        <ScrollView
          showsVerticalScrollIndicator={false}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={() => { setRefreshing(true); fetchScans(); }}
              tintColor={colors.accent}
            />
          }
          contentContainerStyle={styles.scrollContent}
        >
          {/* ── Hero Header ───────────────────────────── */}
          <View style={styles.hero}>
            <MeshOrbs />
            <View style={styles.heroRow}>
              <View style={{ flex: 1 }}>
                <View style={styles.eyebrowRow}>
                  <LinearGradient colors={gradients.aurora} style={styles.eyebrowDash} />
                  <Text style={styles.heroEyebrow}>{t('home.eyebrow')}</Text>
                </View>
                <Text style={styles.greeting}>{t('home.greeting', { name: firstName })}</Text>
                <Text style={styles.headerSub}>{t('home.headerSub')}</Text>
              </View>
              <LinearGradient colors={gradients.cta} style={styles.avatarCircle}>
                <Text style={styles.avatarText}>{(user?.name ?? 'U')[0].toUpperCase()}</Text>
              </LinearGradient>
            </View>
          </View>

          {/* ── Scan CTA ──────────────────────────────── */}
          <TouchableOpacity
            style={styles.scanCTAWrap}
            onPress={() => navigation.navigate('Scan')}
            activeOpacity={0.9}
          >
            <LinearGradient colors={gradients.cta} start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }} style={styles.scanCTA}>
              <LinearGradient colors={gradients.ctaShine} style={styles.ctaShine} start={{ x: 0, y: 0 }} end={{ x: 0, y: 1 }} />
              <View style={styles.ctaIconCircle}>
                <Ionicons name="scan-circle-outline" size={28} color={colors.accentDark} />
              </View>
              <View style={{ flex: 1 }}>
                <Text style={styles.ctaBadge}>{t('home.ctaBadge')}</Text>
                <Text style={styles.ctaTitle}>{t('home.ctaTitle')}</Text>
                <Text style={styles.ctaSub}>{t('home.ctaSub')}</Text>
              </View>
              <View style={styles.ctaArrow}>
                <Ionicons name="arrow-forward" size={18} color="#fff" />
              </View>
            </LinearGradient>
          </TouchableOpacity>

          {/* ── Stats Row ─────────────────────────────── */}
          <View style={styles.statsRow}>
            <StatCard label={t('home.statScans')} value={stats.total} icon="analytics-outline" accent={colors.accent} />
            <StatCard label={t('home.statAlerts')} value={stats.diseased} icon="warning-outline" accent="#f87171" />
            <StatCard label={t('home.statHealthy')} value={stats.healthy} icon="checkmark-circle-outline" accent="#4ade80" />
          </View>

          {/* ── Features ──────────────────────────────── */}
          <View style={styles.section}>
            <Text style={styles.sectionKicker}>{t('home.featuresKicker')}</Text>
            <Text style={styles.sectionTitle}>{t('home.featuresTitle')}</Text>
            <View style={styles.featureGrid}>
              <FeatureCard
                icon="scan-outline"
                label={t('home.featDiagTitle')}
                sub={t('home.featDiagSub')}
                active={false}
              />
              <FeatureCard
                icon="notifications-outline"
                label={t('home.featReminderTitle')}
                sub={t('home.featReminderSub')}
                active
              />
              <FeatureCard
                icon="checkmark-done-outline"
                label={t('home.featPlanTitle')}
                sub={t('home.featPlanSub')}
                active={false}
              />
            </View>

            <View style={styles.featureRow}>
              <View style={styles.featureRowIcon}>
                <Ionicons name="bulb-outline" size={22} color={colors.accent} />
              </View>
              <View style={{ flex: 1 }}>
                <Text style={styles.featureRowTitle}>{t('home.featSolutionsTitle')}</Text>
                <Text style={styles.featureRowSub}>{t('home.featSolutionsSub')}</Text>
              </View>
            </View>

            <View style={styles.featureRow}>
              <View style={styles.featureRowIcon}>
                <Ionicons name="bar-chart-outline" size={22} color={colors.accent} />
              </View>
              <View style={{ flex: 1 }}>
                <Text style={styles.featureRowTitle}>{t('home.featProgressTitle')}</Text>
                <Text style={styles.featureRowSub}>{t('home.featProgressSub')}</Text>
              </View>
            </View>
          </View>

          {/* ── Recent Scans ──────────────────────────── */}
          <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <View>
                <Text style={styles.sectionKicker}>{t('home.sectionTimeline')}</Text>
                <Text style={styles.sectionTitle}>{t('home.recentScans')}</Text>
              </View>
              <TouchableOpacity onPress={() => navigation.navigate('History')} hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}>
                <Text style={styles.seeAll}>{t('home.viewAll')}</Text>
              </TouchableOpacity>
            </View>

            {loading ? (
              <ActivityIndicator color={colors.accent} style={{ marginTop: 24 }} />
            ) : scans.length === 0 ? (
              <View style={styles.emptyState}>
                <View style={styles.emptyIconWrap}>
                  <Ionicons name="leaf-outline" size={36} color={colors.accent} />
                </View>
                <Text style={styles.emptyText}>{t('home.emptyTitle')}</Text>
                <Text style={styles.emptySub}>{t('home.emptySub')}</Text>
              </View>
            ) : (
              scans.map((scan) => <RecentScanItem key={scan.id} scan={scan} t={t} />)
            )}
          </View>

          {/* ── Tips Card ─────────────────────────────── */}
          <View style={[styles.section, styles.tipsCard]}>
            <LinearGradient colors={gradients.aurora} start={{ x: 0, y: 0 }} end={{ x: 1, y: 0 }} style={styles.tipsBar} />
            <View style={styles.tipsHeader}>
              <Ionicons name="sparkles-outline" size={18} color={colors.accent} />
              <Text style={styles.tipsTitle}>{t('home.tipTitle')}</Text>
            </View>
            {[t('home.tip1'), t('home.tip2'), t('home.tip3'), t('home.tip4')].map((tip, i) => (
              <View key={i} style={styles.tipRow}>
                <LinearGradient colors={gradients.aurora} style={styles.tipBullet} />
                <Text style={styles.tipText}>{tip}</Text>
              </View>
            ))}
          </View>

          <View style={{ height: 100 }} />
        </ScrollView>
      </SafeAreaView>
    </LinearGradient>
  );
}

function StatCard({ label, value, icon, accent }) {
  return (
    <View style={[styles.statCard, { borderColor: accent + '40' }]}>
      <Ionicons name={icon} size={20} color={accent} />
      <Text style={[styles.statValue, { color: accent }]}>{value}</Text>
      <Text style={styles.statLabel}>{label}</Text>
    </View>
  );
}

function FeatureCard({ icon, label, sub, active }) {
  return (
    <View style={[styles.featureCard, active && styles.featureCardActive]}>
      <View style={[styles.featureCardIconWrap, active && styles.featureCardIconActive]}>
        <Ionicons name={icon} size={22} color={active ? colors.primaryMid : colors.accent} />
      </View>
      <Text style={[styles.featureCardLabel, active && styles.featureCardLabelActive]}>{label}</Text>
      <Text style={styles.featureCardSub}>{sub}</Text>
    </View>
  );
}

function RecentScanItem({ scan, t }) {
  const info = getDiseaseInfo(scan.predicted_disease, t);
  const isHealthy = scan.predicted_disease?.toLowerCase().includes('healthy');
  const statusBadge = isHealthy
    ? { label: t('home.statusHealthy'), color: colors.accent, bg: colors.accentSoft }
    : { label: t('home.statusDiseased'), color: colors.danger, bg: colors.dangerBg };
  const cropLabel = !scan.crop_type || String(scan.crop_type).toLowerCase() === 'unknown'
    ? t('common.unknown')
    : scan.crop_type;
  return (
    <View style={styles.scanItem}>
      <View style={[styles.scanAccent, { backgroundColor: info.color }]} />
      <View style={[styles.scanIconWrap, { backgroundColor: info.color + '28' }]}>
        <Text style={{ fontSize: 20 }}>{info.icon}</Text>
      </View>
      <View style={styles.scanInfo}>
        <Text style={styles.scanDisease} numberOfLines={1}>{info.displayName}</Text>
        <Text style={styles.scanCrop}>{t('home.confidenceLine', { crop: cropLabel, pct: Math.round(scan.confidence * 100) })}</Text>
      </View>
      <View style={[styles.scanBadge, { backgroundColor: statusBadge.bg }]}>
        <Text style={[styles.scanBadgeText, { color: statusBadge.color }]}>{statusBadge.label}</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  gradient: { flex: 1 },
  safeArea: { flex: 1 },
  scrollContent: { paddingBottom: 8 },

  // Hero
  hero: { paddingHorizontal: 20, paddingTop: 8, paddingBottom: 28, overflow: 'hidden' },
  heroRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  eyebrowRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 10 },
  eyebrowDash: { width: 24, height: 3, borderRadius: 2 },
  heroEyebrow: { fontSize: 10, fontWeight: '800', color: colors.mintDark, letterSpacing: 2, textTransform: 'uppercase' },
  greeting: { fontSize: 28, fontWeight: '800', color: colors.text, letterSpacing: -0.8, marginBottom: 6 },
  headerSub: { fontSize: 13, color: colors.textSecondary, lineHeight: 19, maxWidth: 260 },
  avatarCircle: {
    width: 52,
    height: 52,
    borderRadius: 26,
    justifyContent: 'center',
    alignItems: 'center',
    ...shadows.glow,
  },
  avatarText: { fontSize: 22, fontWeight: '800', color: '#fff' },

  // Scan CTA
  scanCTAWrap: {
    marginHorizontal: 16,
    borderRadius: radius.xl,
    overflow: 'hidden',
    ...shadows.cta,
  },
  scanCTA: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 18,
    paddingHorizontal: 16,
    gap: 12,
    overflow: 'hidden',
  },
  ctaShine: { position: 'absolute', left: 0, right: 0, top: 0, height: '45%' },
  ctaIconCircle: {
    width: 52,
    height: 52,
    borderRadius: 26,
    backgroundColor: 'rgba(255,255,255,0.92)',
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.6)',
  },
  ctaBadge: {
    fontSize: 10,
    fontWeight: '800',
    color: 'rgba(255,255,255,0.9)',
    letterSpacing: 1.2,
    textTransform: 'uppercase',
    backgroundColor: 'rgba(0,0,0,0.15)',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
    alignSelf: 'flex-start',
    overflow: 'hidden',
    marginBottom: 4,
  },
  ctaTitle: { fontSize: 18, fontWeight: '800', color: '#fff', letterSpacing: -0.3 },
  ctaSub: { fontSize: 12, color: 'rgba(255,255,255,0.85)', marginTop: 2 },
  ctaArrow: {
    width: 38,
    height: 38,
    borderRadius: 19,
    backgroundColor: 'rgba(255,255,255,0.2)',
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.25)',
  },

  // Stats
  statsRow: { flexDirection: 'row', marginHorizontal: 16, marginTop: 18, gap: 10 },
  statCard: {
    flex: 1,
    backgroundColor: colors.glass,
    borderRadius: radius.md,
    padding: 12,
    alignItems: 'center',
    borderWidth: 1,
    ...shadows.soft,
  },
  statValue: { fontSize: 22, fontWeight: '800', marginTop: 6, letterSpacing: -0.5 },
  statLabel: { fontSize: 9, color: colors.textMuted, marginTop: 2, fontWeight: '700', letterSpacing: 0.8, textTransform: 'uppercase', textAlign: 'center' },

  // Sections
  section: { marginHorizontal: 16, marginTop: 24 },
  sectionHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-end', marginBottom: 14 },
  sectionKicker: { fontSize: 10, fontWeight: '800', color: colors.accent, letterSpacing: 1.4, textTransform: 'uppercase', marginBottom: 4 },
  sectionTitle: { fontSize: 20, fontWeight: '800', color: colors.text, letterSpacing: -0.3 },
  seeAll: { fontSize: 13, color: colors.accent, fontWeight: '700' },

  // Feature grid
  featureGrid: { flexDirection: 'row', gap: 10, marginBottom: 12 },
  featureCard: {
    flex: 1,
    backgroundColor: colors.glass,
    borderRadius: radius.lg,
    padding: 12,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    alignItems: 'center',
    ...shadows.soft,
  },
  featureCardActive: {
    backgroundColor: colors.accent,
    borderColor: colors.accent,
  },
  featureCardIconWrap: {
    width: 44,
    height: 44,
    borderRadius: 14,
    backgroundColor: colors.glassGreen,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 8,
  },
  featureCardIconActive: {
    backgroundColor: 'rgba(0,0,0,0.15)',
  },
  featureCardLabel: {
    fontSize: 11,
    fontWeight: '800',
    color: colors.text,
    textAlign: 'center',
    marginBottom: 2,
  },
  featureCardLabelActive: { color: colors.primaryMid },
  featureCardSub: { fontSize: 9, color: colors.textMuted, textAlign: 'center' },

  // Feature rows
  featureRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 14,
    backgroundColor: colors.glass,
    borderRadius: radius.lg,
    padding: 16,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    marginBottom: 10,
    ...shadows.soft,
  },
  featureRowIcon: {
    width: 48,
    height: 48,
    borderRadius: 14,
    backgroundColor: colors.glassGreen,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.glassGreenBorder,
  },
  featureRowTitle: { fontSize: 15, fontWeight: '800', color: colors.text, marginBottom: 3 },
  featureRowSub: { fontSize: 12, color: colors.textSecondary, lineHeight: 17 },

  // Empty state
  emptyState: {
    alignItems: 'center',
    paddingVertical: 32,
    paddingHorizontal: 18,
    backgroundColor: colors.glass,
    borderRadius: radius.xl,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    ...shadows.soft,
  },
  emptyIconWrap: {
    width: 80,
    height: 80,
    borderRadius: 40,
    backgroundColor: colors.glassGreen,
    borderWidth: 1,
    borderColor: colors.glassGreenBorder,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 14,
  },
  emptyText: { fontSize: 17, fontWeight: '800', color: colors.text },
  emptySub: { fontSize: 13, color: colors.textMuted, marginTop: 8, textAlign: 'center', lineHeight: 20 },

  // Scan items
  scanItem: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.glass,
    borderRadius: radius.lg,
    padding: 12,
    paddingLeft: 10,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    overflow: 'hidden',
    ...shadows.soft,
  },
  scanAccent: { width: 3, alignSelf: 'stretch', borderRadius: 2, marginRight: 10 },
  scanIconWrap: { width: 44, height: 44, borderRadius: 13, justifyContent: 'center', alignItems: 'center', marginRight: 12 },
  scanInfo: { flex: 1 },
  scanDisease: { fontSize: 14, fontWeight: '800', color: colors.text, letterSpacing: -0.2 },
  scanCrop: { fontSize: 12, color: colors.textMuted, marginTop: 3 },
  scanBadge: { paddingHorizontal: 10, paddingVertical: 5, borderRadius: radius.pill },
  scanBadgeText: { fontSize: 10, fontWeight: '800', letterSpacing: 0.3 },

  // Tips card
  tipsCard: {
    backgroundColor: colors.glass,
    borderRadius: radius.xl,
    padding: 18,
    paddingTop: 20,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    overflow: 'hidden',
    ...shadows.soft,
  },
  tipsBar: { position: 'absolute', top: 0, left: 0, right: 0, height: 3 },
  tipsHeader: { flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 14 },
  tipsTitle: { fontSize: 15, fontWeight: '800', color: colors.text, letterSpacing: -0.2 },
  tipRow: { flexDirection: 'row', alignItems: 'flex-start', marginBottom: 10, gap: 10 },
  tipBullet: { width: 7, height: 7, borderRadius: 4, marginTop: 6 },
  tipText: { fontSize: 13, color: colors.textSecondary, flex: 1, lineHeight: 20 },
});
