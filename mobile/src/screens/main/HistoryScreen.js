import { useState, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View, Text, StyleSheet, FlatList,
  TouchableOpacity, RefreshControl, ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { useFocusEffect } from '@react-navigation/native';
import { scanService } from '../../services/scanService';
import { getDiseaseInfo } from '../../utils/diseaseInfo';
import useNetworkStatus from '../../hooks/useNetworkStatus';
import { colors, gradients, radius, shadows } from '../../theme/theme';

export default function HistoryScreen() {
  const { t } = useTranslation();
  const [scans, setScans] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [filter, setFilter] = useState('all');
  const [fromCache, setFromCache] = useState(false);
  const { isOnline } = useNetworkStatus();

  const fetchScans = async () => {
    try {
      if (isOnline) {
        const data = await scanService.getHistory();
        setScans(data);
        setFromCache(false);
        scanService.syncOfflineScans().catch(() => {});
      } else {
        const cached = await scanService.getCachedHistory();
        setScans(cached);
        setFromCache(true);
      }
    } catch (_) {
      const cached = await scanService.getCachedHistory();
      setScans(cached);
      setFromCache(true);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useFocusEffect(useCallback(() => { fetchScans(); }, [isOnline]));

  const filtered = scans.filter((s) => {
    if (filter === 'healthy') return s.predicted_disease?.includes('healthy');
    if (filter === 'diseased') return !s.predicted_disease?.includes('healthy');
    return true;
  });

  const renderItem = ({ item }) => {
    const info = getDiseaseInfo(item.predicted_disease, t);
    const isHealthy = item.predicted_disease?.toLowerCase().includes('healthy');
    const statusBadge = isHealthy
      ? { label: t('history.statusHealthy'), color: colors.accent, bg: colors.accentSoft }
      : { label: t('history.statusDiseased'), color: colors.danger, bg: colors.dangerBg };
    const date = new Date(item.created_at).toLocaleDateString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
    });

    return (
      <View style={styles.card}>
        <View style={[styles.cardAccent, { backgroundColor: info.color }]} />
        <View style={[styles.iconBox, { backgroundColor: info.color + '28' }]}>
          <Text style={styles.iconEmoji}>{info.icon}</Text>
        </View>
        <View style={styles.cardBody}>
          <Text style={styles.diseaseName}>{info.displayName}</Text>
          <Text style={styles.cropText}>{item.crop_type}</Text>
          <View style={styles.cardFooter}>
            <View style={[styles.badge, { backgroundColor: statusBadge.bg, borderColor: isHealthy ? colors.glassGreenBorder : 'rgba(248,113,113,0.2)' }]}>
              <Text style={[styles.badgeText, { color: statusBadge.color }]}>{statusBadge.label}</Text>
            </View>
            <Text style={styles.confidence}>{Math.round(item.confidence * 100)}%</Text>
          </View>
        </View>
        <Text style={styles.date}>{date}</Text>
      </View>
    );
  };

  const FILTERS = [
    { key: 'all', label: t('history.filterAll') },
    { key: 'diseased', label: t('history.filterDiseased') },
    { key: 'healthy', label: t('history.filterHealthy') },
  ];

  return (
    <LinearGradient colors={gradients.hero} style={styles.gradient}>
      <SafeAreaView style={styles.safeArea} edges={['top']}>
        {/* Header */}
        <View style={styles.header}>
          <View>
            <Text style={styles.headerKicker}>SCAN TIMELINE</Text>
            <Text style={styles.headerTitle}>{t('history.title')}</Text>
          </View>
          <View style={styles.countBadge}>
            <Text style={styles.countText}>{scans.length}</Text>
          </View>
        </View>

        {/* Offline cache banner */}
        {fromCache && (
          <View style={styles.cacheBanner}>
            <Ionicons name="cloud-offline-outline" size={14} color={colors.warning} />
            <Text style={styles.cacheBannerText}>{t('history.cacheBanner')}</Text>
          </View>
        )}

        {/* Filter tabs */}
        <View style={styles.filterRow}>
          {FILTERS.map(({ key: f, label }) => (
            <TouchableOpacity
              key={f}
              style={[styles.filterBtn, filter === f && styles.filterBtnActive]}
              onPress={() => setFilter(f)}
              activeOpacity={0.8}
            >
              {filter === f ? (
                <LinearGradient colors={gradients.cta} style={styles.filterBtnGrad}>
                  <Text style={styles.filterTextActive}>{label}</Text>
                </LinearGradient>
              ) : (
                <Text style={styles.filterText}>{label}</Text>
              )}
            </TouchableOpacity>
          ))}
        </View>

        {loading ? (
          <ActivityIndicator color={colors.accent} style={{ marginTop: 40 }} />
        ) : (
          <FlatList
            data={filtered}
            keyExtractor={(item) => item.id}
            renderItem={renderItem}
            contentContainerStyle={styles.listContent}
            refreshControl={
              <RefreshControl
                refreshing={refreshing}
                onRefresh={() => { setRefreshing(true); fetchScans(); }}
                tintColor={colors.accent}
              />
            }
            ListEmptyComponent={
              <View style={styles.empty}>
                <View style={styles.emptyIconWrap}>
                  <Ionicons name="time-outline" size={36} color={colors.accent} />
                </View>
                <Text style={styles.emptyText}>{t('history.empty')}</Text>
                <Text style={styles.emptySubText}>{t('history.emptySub')}</Text>
              </View>
            }
          />
        )}
      </SafeAreaView>
    </LinearGradient>
  );
}

const styles = StyleSheet.create({
  gradient: { flex: 1 },
  safeArea: { flex: 1 },

  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 20,
    paddingTop: 8,
    paddingBottom: 18,
  },
  headerKicker: {
    fontSize: 10,
    fontWeight: '800',
    color: colors.mintDark,
    letterSpacing: 2,
    textTransform: 'uppercase',
    marginBottom: 4,
  },
  headerTitle: { fontSize: 26, fontWeight: '800', color: colors.text, letterSpacing: -0.5 },
  countBadge: {
    backgroundColor: colors.glassGreen,
    borderWidth: 1,
    borderColor: colors.glassGreenBorder,
    borderRadius: radius.pill,
    paddingHorizontal: 14,
    paddingVertical: 6,
  },
  countText: { fontSize: 16, fontWeight: '800', color: colors.accent },

  cacheBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.warningBg,
    paddingHorizontal: 16,
    paddingVertical: 8,
    gap: 6,
    marginHorizontal: 16,
    borderRadius: radius.md,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: 'rgba(251,191,36,0.2)',
  },
  cacheBannerText: { fontSize: 12, color: colors.warning, fontWeight: '600' },

  filterRow: {
    flexDirection: 'row',
    paddingHorizontal: 16,
    paddingBottom: 14,
    gap: 8,
  },
  filterBtn: {
    borderRadius: radius.pill,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    overflow: 'hidden',
  },
  filterBtnActive: { borderColor: 'transparent' },
  filterBtnGrad: { paddingHorizontal: 16, paddingVertical: 8 },
  filterText: { fontSize: 13, color: colors.textMuted, fontWeight: '600', paddingHorizontal: 16, paddingVertical: 8 },
  filterTextActive: { fontSize: 13, color: colors.primaryMid, fontWeight: '800' },

  listContent: { paddingHorizontal: 16, paddingBottom: 32 },

  card: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.glass,
    borderRadius: radius.lg,
    padding: 14,
    paddingLeft: 0,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    overflow: 'hidden',
    ...shadows.soft,
  },
  cardAccent: { width: 3, alignSelf: 'stretch', marginRight: 12 },
  iconBox: {
    width: 48,
    height: 48,
    borderRadius: 14,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },
  iconEmoji: { fontSize: 22 },
  cardBody: { flex: 1 },
  diseaseName: { fontSize: 14, fontWeight: '800', color: colors.text, letterSpacing: -0.2 },
  cropText: { fontSize: 12, color: colors.textMuted, marginTop: 2 },
  cardFooter: { flexDirection: 'row', alignItems: 'center', marginTop: 6, gap: 8 },
  badge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 8,
    borderWidth: 1,
  },
  badgeText: { fontSize: 11, fontWeight: '700' },
  confidence: { fontSize: 12, color: colors.textMuted, fontWeight: '700' },
  date: { fontSize: 11, color: colors.textDim, textAlign: 'right', marginLeft: 6 },

  empty: { alignItems: 'center', paddingTop: 60 },
  emptyIconWrap: {
    width: 80,
    height: 80,
    borderRadius: 40,
    backgroundColor: colors.glassGreen,
    borderWidth: 1,
    borderColor: colors.glassGreenBorder,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 16,
  },
  emptyText: { fontSize: 17, fontWeight: '800', color: colors.text, marginBottom: 6 },
  emptySubText: { fontSize: 13, color: colors.textMuted, textAlign: 'center', lineHeight: 20 },
});
