import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View, Text, StyleSheet, Image, ScrollView,
  TouchableOpacity,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { getDiseaseInfo } from '../../utils/diseaseInfo';
import { scanService } from '../../services/scanService';
import { colors, gradients, radius, shadows } from '../../theme/theme';

export default function ResultScreen({ route, navigation }) {
  const { t } = useTranslation();
  const { result, imageUri } = route.params;
  const {
    predicted_disease, confidence, top5, offline = false,
    severity_pct, severity_label, severity_color,
  } = result;

  const [severityState, setSeverityState] = useState({
    pct: severity_pct ?? null,
    label: severity_label ?? null,
    color: severity_color ?? null,
    loading: severity_pct == null,
  });

  useEffect(() => {
    let mounted = true;
    if (severity_pct != null) return undefined;
    scanService.computeSeverity(imageUri, predicted_disease, confidence)
      .then((r) => {
        if (!mounted) return;
        setSeverityState({
          pct: r.severity_pct ?? null,
          label: r.severity_label ?? null,
          color: r.severity_color ?? null,
          loading: false,
        });
      })
      .catch(() => {
        if (!mounted) return;
        setSeverityState((prev) => ({ ...prev, loading: false }));
      });
    return () => { mounted = false; };
  }, [severity_pct, imageUri, predicted_disease, confidence]);

  const info = getDiseaseInfo(predicted_disease, t);
  const confidencePct = Math.round(confidence * 100);
  const diseaseStatus = predicted_disease?.toLowerCase().includes('healthy')
    ? t('result.statusHealthy')
    : t('result.statusDiseased');

  const goBack = () => navigation.navigate('ScanCamera');

  return (
    <LinearGradient colors={gradients.hero} style={styles.gradient}>
      <SafeAreaView style={styles.safeArea} edges={['top']}>
        {/* Header */}
        <View style={styles.header}>
          <TouchableOpacity onPress={goBack} style={styles.backBtn}>
            <Ionicons name="arrow-back" size={20} color={colors.text} />
          </TouchableOpacity>
          <Text style={styles.headerTitle}>{t('result.title')}</Text>
          {offline
            ? <View style={styles.offlinePill}><Text style={styles.offlinePillText}>{t('result.offlinePill')}</Text></View>
            : <View style={{ width: 40 }} />
          }
        </View>

        <ScrollView showsVerticalScrollIndicator={false} contentContainerStyle={styles.scroll}>

          {/* ── Image + Result Banner ─── */}
          <View style={styles.resultCard}>
            {imageUri && (
              <Image source={{ uri: imageUri }} style={styles.leafImage} resizeMode="cover" />
            )}
            {/* Disease banner */}
            <LinearGradient
              colors={[info.color + 'DD', info.color]}
              start={{ x: 0, y: 0 }} end={{ x: 1, y: 0 }}
              style={styles.diseaseBanner}
            >
              <Text style={styles.diseaseEmoji}>{info.icon}</Text>
              <View style={styles.diseaseBannerText}>
                <Text style={styles.diseaseName}>{info.displayName}</Text>
                <Text style={styles.cropLabel}>{info.crop}</Text>
              </View>
              <View style={styles.statusPill}>
                <Text style={styles.statusPillText}>{diseaseStatus}</Text>
              </View>
            </LinearGradient>

            {/* Confidence */}
            <View style={styles.confidenceSection}>
              <View style={styles.confidenceHeader}>
                <Text style={styles.confidenceLabel}>{t('result.confidence')}</Text>
                <Text style={[styles.confidenceValue, { color: info.color }]}>{confidencePct}%</Text>
              </View>
              <View style={styles.confidenceTrack}>
                <LinearGradient
                  colors={[info.color, info.color + 'AA']}
                  start={{ x: 0, y: 0 }} end={{ x: 1, y: 0 }}
                  style={[styles.confidenceFill, { width: `${confidencePct}%` }]}
                />
              </View>
            </View>
          </View>

          {/* ── Severity ─── */}
          {severityState.pct != null && (
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('result.coverageTitle')}</Text>
              <View style={styles.glassCard}>
                <View style={styles.severityRow}>
                  <View style={[styles.severityDot, { backgroundColor: severityState.color }]} />
                  <Text style={[styles.severityLabelText, { color: severityState.color }]}>
                    {severityState.label
                      ? t(`result.severityLabels.${severityState.label}`, { defaultValue: severityState.label })
                      : ''}
                  </Text>
                  <Text style={styles.severityPct}>{t('result.affected', { pct: severityState.pct })}</Text>
                </View>
                <View style={styles.severityTrack}>
                  <View style={[
                    styles.severityFill,
                    { width: `${Math.min(severityState.pct, 100)}%`, backgroundColor: severityState.color }
                  ]} />
                </View>
                <Text style={styles.hint}>{t('result.coverageHint')}</Text>
              </View>
            </View>
          )}

          {severityState.loading && (
            <View style={styles.section}>
              <View style={styles.glassCard}>
                <Text style={styles.hint}>{t('result.computingSeverity')}</Text>
              </View>
            </View>
          )}

          {/* ── About ─── */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>{t('result.aboutTitle')}</Text>
            <View style={styles.glassCard}>
              <Text style={styles.description}>{info.description}</Text>
            </View>
          </View>

          {/* ── Recommendations ─── */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>
              {info.status === 'healthy' ? t('result.maintenanceTips') : t('result.treatmentTips')}
            </Text>
            <View style={styles.glassCard}>
              {info.recommendations.map((rec, i) => (
                <View key={i} style={[styles.recRow, i > 0 && styles.recDivider]}>
                  <View style={[styles.recNumber, { backgroundColor: info.color + '28' }]}>
                    <Text style={[styles.recNumberText, { color: info.color }]}>{i + 1}</Text>
                  </View>
                  <Text style={styles.recText}>{rec}</Text>
                </View>
              ))}
            </View>
          </View>

          {/* ── Top 5 Predictions ─── */}
          {top5 && top5.length > 1 && (
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('result.topPredictions')}</Text>
              <View style={styles.glassCard}>
                {top5.map((pred, i) => {
                  const pInfo = getDiseaseInfo(pred.disease, t);
                  const pct = Math.round(pred.confidence * 100);
                  return (
                    <View key={i} style={[styles.predRow, i > 0 && styles.recDivider]}>
                      <View style={styles.predLeft}>
                        <Text style={styles.predRank}>#{i + 1}</Text>
                        <Text style={styles.predName}>{pInfo.displayName}</Text>
                      </View>
                      <View style={styles.predRight}>
                        <View style={styles.predTrack}>
                          <View style={[styles.predFill, { width: `${pct}%`, backgroundColor: i === 0 ? info.color : colors.glassBorder }]} />
                        </View>
                        <Text style={[styles.predPct, { color: i === 0 ? info.color : colors.textMuted }]}>{pct}%</Text>
                      </View>
                    </View>
                  );
                })}
              </View>
            </View>
          )}

          {/* ── Critical warning ─── */}
          {severityState.label === 'Critical' && (
            <View style={styles.warningBox}>
              <Ionicons name="alert-circle" size={22} color={colors.danger} />
              <Text style={styles.warningText}>{t('result.warningCritical')}</Text>
            </View>
          )}

          {/* ── Scan again ─── */}
          <TouchableOpacity style={styles.scanAgainBtn} onPress={goBack} activeOpacity={0.88}>
            <Text style={styles.sparkle}>✦</Text>
            <Ionicons name="scan-circle-outline" size={20} color={colors.primaryMid} />
            <Text style={styles.scanAgainText}>{t('result.scanAnother')}</Text>
          </TouchableOpacity>

          <View style={{ height: 32 }} />
        </ScrollView>
      </SafeAreaView>
    </LinearGradient>
  );
}

const styles = StyleSheet.create({
  gradient: { flex: 1 },
  safeArea: { flex: 1 },

  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: colors.glassBorder,
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
  headerTitle: { fontSize: 17, fontWeight: '800', color: colors.text },
  offlinePill: {
    backgroundColor: colors.dangerBg,
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: 'rgba(248,113,113,0.25)',
  },
  offlinePillText: { fontSize: 12, color: colors.danger, fontWeight: '700' },

  scroll: { padding: 16 },

  // Result card
  resultCard: {
    backgroundColor: colors.glass,
    borderRadius: radius.xl,
    overflow: 'hidden',
    marginBottom: 16,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    ...shadows.card,
  },
  leafImage: { width: '100%', height: 220 },
  diseaseBanner: { flexDirection: 'row', alignItems: 'center', padding: 16, gap: 12 },
  diseaseEmoji: { fontSize: 28 },
  diseaseBannerText: { flex: 1 },
  diseaseName: { fontSize: 18, fontWeight: '800', color: '#fff' },
  cropLabel: { fontSize: 13, color: 'rgba(255,255,255,0.75)', marginTop: 2 },
  statusPill: {
    backgroundColor: 'rgba(255,255,255,0.2)',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radius.pill,
  },
  statusPillText: { fontSize: 12, color: '#fff', fontWeight: '700' },
  confidenceSection: { padding: 16 },
  confidenceHeader: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 10 },
  confidenceLabel: { fontSize: 13, color: colors.textMuted, fontWeight: '600' },
  confidenceValue: { fontSize: 18, fontWeight: '800' },
  confidenceTrack: {
    height: 8,
    backgroundColor: 'rgba(255,255,255,0.1)',
    borderRadius: 4,
    overflow: 'hidden',
  },
  confidenceFill: { height: '100%', borderRadius: 4 },

  // Sections
  section: { marginBottom: 16 },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.accent,
    marginBottom: 8,
    letterSpacing: 0.5,
    textTransform: 'uppercase',
  },
  glassCard: {
    backgroundColor: colors.glass,
    borderRadius: radius.lg,
    padding: 16,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    ...shadows.soft,
  },
  description: { fontSize: 14, color: colors.textSecondary, lineHeight: 22 },

  // Recommendations
  recRow: { flexDirection: 'row', alignItems: 'flex-start', paddingVertical: 10 },
  recDivider: { borderTopWidth: 1, borderTopColor: colors.glassBorder },
  recNumber: {
    width: 28,
    height: 28,
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
    marginTop: 1,
  },
  recNumberText: { fontSize: 13, fontWeight: '800' },
  recText: { flex: 1, fontSize: 14, color: colors.textSecondary, lineHeight: 20 },

  // Predictions
  predRow: { flexDirection: 'row', alignItems: 'center', paddingVertical: 8 },
  predLeft: { flexDirection: 'row', alignItems: 'center', width: 170 },
  predRank: { fontSize: 12, color: colors.textMuted, fontWeight: '700', width: 28 },
  predName: { fontSize: 13, color: colors.text, fontWeight: '600', flex: 1 },
  predRight: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: 8 },
  predTrack: {
    flex: 1,
    height: 6,
    backgroundColor: 'rgba(255,255,255,0.08)',
    borderRadius: 3,
    overflow: 'hidden',
  },
  predFill: { height: '100%', borderRadius: 3 },
  predPct: { width: 36, fontSize: 12, fontWeight: '700', textAlign: 'right' },

  // Severity
  severityRow: { flexDirection: 'row', alignItems: 'center', marginBottom: 10, gap: 8 },
  severityDot: { width: 12, height: 12, borderRadius: 6 },
  severityLabelText: { fontSize: 15, fontWeight: '800' },
  severityPct: { fontSize: 13, color: colors.textMuted, marginLeft: 'auto' },
  severityTrack: {
    height: 10,
    backgroundColor: 'rgba(255,255,255,0.1)',
    borderRadius: 5,
    overflow: 'hidden',
    marginBottom: 8,
  },
  severityFill: { height: '100%', borderRadius: 5 },
  hint: { fontSize: 12, color: colors.textMuted, marginTop: 2 },

  // Warning
  warningBox: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    backgroundColor: colors.dangerBg,
    borderRadius: radius.lg,
    padding: 14,
    marginBottom: 16,
    gap: 10,
    borderWidth: 1,
    borderColor: 'rgba(248,113,113,0.2)',
  },
  warningText: { flex: 1, fontSize: 13, color: colors.danger, lineHeight: 20, fontWeight: '600' },

  // Scan again
  scanAgainBtn: {
    backgroundColor: colors.white,
    borderRadius: radius.pill,
    height: 56,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    ...shadows.cta,
  },
  sparkle: { fontSize: 16, color: colors.primaryMid },
  scanAgainText: { fontSize: 16, fontWeight: '800', color: colors.primaryMid },
});
