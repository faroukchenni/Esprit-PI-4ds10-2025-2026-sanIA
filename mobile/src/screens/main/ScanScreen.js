import { useState, useRef, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View, Text, StyleSheet, TouchableOpacity, Image,
  Alert, ActivityIndicator, ScrollView, Platform,
} from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { CameraView, useCameraPermissions } from 'expo-camera';
import * as ImagePicker from 'expo-image-picker';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { scanService } from '../../services/scanService';
import useNetworkStatus from '../../hooks/useNetworkStatus';
import { colors, gradients, radius, shadows } from '../../theme/theme';
import MeshOrbs from '../../components/MeshOrbs';

export default function ScanScreen({ navigation }) {
  const { t } = useTranslation();
  const insets = useSafeAreaInsets();
  const [permission, requestPermission] = useCameraPermissions();
  const [mode, setMode] = useState('menu'); // 'menu' | 'camera' | 'preview'
  const [imageUri, setImageUri] = useState(null);
  const [analyzing, setAnalyzing] = useState(false);
  const cameraRef = useRef(null);
  const { isOnline } = useNetworkStatus();

  useEffect(() => {
    ImagePicker.requestMediaLibraryPermissionsAsync();
  }, []);

  const takePicture = async () => {
    if (!cameraRef.current) return;
    try {
      const photo = await cameraRef.current.takePictureAsync({ quality: 0.8 });
      setImageUri(photo.uri);
      setMode('preview');
    } catch {
      Alert.alert(t('scan.captureError'), t('scan.captureErrorMsg'));
    }
  };

  const pickFromGallery = async () => {
    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.8,
    });
    if (!result.canceled && result.assets.length > 0) {
      setImageUri(result.assets[0].uri);
      setMode('preview');
    }
  };

  const analyzeImage = async () => {
    if (!imageUri) return;
    setAnalyzing(true);
    try {
      let result;
      if (isOnline) {
        try {
          result = await scanService.detectDisease(imageUri);
        } catch (netErr) {
          if (!netErr.response) {
            result = await scanService.detectDiseaseOffline(imageUri);
          } else {
            throw netErr;
          }
        }
      } else {
        result = await scanService.detectDiseaseOffline(imageUri);
      }
      navigation.navigate('Result', { result, imageUri });
      setMode('menu');
      setImageUri(null);
    } catch (err) {
      const msg = err.response?.data?.detail || err.message || t('scan.analysisFailed');
      Alert.alert(t('scan.detectionError'), msg);
    } finally {
      setAnalyzing(false);
    }
  };

  // ── Menu ─────────────────────────────────────────────────────────────
  if (mode === 'menu') {
    return (
      <LinearGradient colors={gradients.hero} style={styles.gradient}>
        <SafeAreaView style={styles.safeArea} edges={['top']}>
          {/* Header */}
          <View style={styles.menuHeader}>
            <MeshOrbs variant="compact" />
            <View style={styles.menuHeaderRow}>
              <View style={{ flex: 1 }}>
                <Text style={styles.menuKicker}>{t('scan.kicker')}</Text>
                <Text style={styles.menuTitle}>{t('scan.title')}</Text>
                <Text style={styles.menuSub}>{t('scan.sub')}</Text>
              </View>
              <View style={[styles.statusRing, { borderColor: isOnline ? colors.accent + '80' : colors.danger + '80' }]}>
                <View style={[styles.statusDot, { backgroundColor: isOnline ? colors.accent : colors.danger }]} />
              </View>
            </View>
          </View>

          <ScrollView
            contentContainerStyle={[styles.menuBody, { paddingBottom: 32 + insets.bottom + 56 }]}
            showsVerticalScrollIndicator={false}
          >
            {/* Offline banner */}
            {!isOnline && (
              <View style={styles.offlineBanner}>
                <View style={styles.offlineIcon}>
                  <Ionicons name="hardware-chip-outline" size={18} color={colors.warning} />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.offlineTitle}>{t('scan.offlineIntelTitle')}</Text>
                  <Text style={styles.offlineSub}>{t('scan.offlineIntelSub')}</Text>
                </View>
              </View>
            )}

            {/* Option cards */}
            <TouchableOpacity
              style={styles.optionCard}
              onPress={async () => {
                if (!permission?.granted) {
                  const res = await requestPermission();
                  if (!res.granted) {
                    Alert.alert(t('scan.permTitle'), t('scan.permMsg'));
                    return;
                  }
                }
                setMode('camera');
              }}
              activeOpacity={0.85}
            >
              <LinearGradient colors={gradients.headerShort} style={styles.optionGradient}>
                <LinearGradient colors={gradients.ctaShine} style={styles.optionShine} start={{ x: 0, y: 0 }} end={{ x: 0, y: 1 }} />
                <View style={styles.optionIconWrap}>
                  <Ionicons name="camera" size={32} color={colors.accent} />
                </View>
                <Text style={styles.optionTitle}>{t('scan.takePhoto')}</Text>
                <Text style={styles.optionSub}>{t('scan.takePhotoSub')}</Text>
                <View style={styles.optionArrow}>
                  <Ionicons name="arrow-forward" size={16} color={colors.accent} />
                </View>
              </LinearGradient>
            </TouchableOpacity>

            <TouchableOpacity style={styles.optionCard} onPress={pickFromGallery} activeOpacity={0.85}>
              <LinearGradient colors={['#0f2e1a', '#1a4d2e', '#25663d']} style={styles.optionGradient}>
                <LinearGradient colors={gradients.ctaShine} style={styles.optionShine} start={{ x: 0, y: 0 }} end={{ x: 0, y: 1 }} />
                <View style={styles.optionIconWrap}>
                  <Ionicons name="images" size={32} color={colors.accent} />
                </View>
                <Text style={styles.optionTitle}>{t('scan.gallery')}</Text>
                <Text style={styles.optionSub}>{t('scan.gallerySub')}</Text>
                <View style={styles.optionArrow}>
                  <Ionicons name="arrow-forward" size={16} color={colors.accent} />
                </View>
              </LinearGradient>
            </TouchableOpacity>

            {/* Tips box */}
            <View style={styles.tipsBox}>
              <LinearGradient colors={gradients.aurora} start={{ x: 0, y: 0 }} end={{ x: 1, y: 0 }} style={styles.tipsTopLine} />
              <Text style={styles.tipsTitle}>{t('scan.tipsTitle')}</Text>
              {[t('scan.tip1'), t('scan.tip2'), t('scan.tip3'), t('scan.tip4')].map((tipLine, i) => (
                <View key={i} style={styles.tipRow}>
                  <Text style={styles.tipBullet}>✓</Text>
                  <Text style={styles.tipText}>{tipLine}</Text>
                </View>
              ))}
            </View>

            {/* Supported crops */}
            <View style={styles.cropsBox}>
              <Text style={styles.cropsTitle}>{t('scan.cropsTitle')}</Text>
              <View style={styles.cropsRow}>
                {[t('scan.cropApple'), t('scan.cropGrape'), t('scan.cropPotato'), t('scan.cropTomato')].map((c) => (
                  <View key={c} style={styles.cropPill}>
                    <Text style={styles.cropPillText}>{c}</Text>
                  </View>
                ))}
              </View>
            </View>
          </ScrollView>
        </SafeAreaView>
      </LinearGradient>
    );
  }

  // ── Camera ────────────────────────────────────────────────────────────
  if (mode === 'camera') {
    return (
      <View style={styles.cameraContainer}>
        <CameraView ref={cameraRef} style={styles.camera} facing="back">
          <View style={styles.cameraOverlay}>
            <LinearGradient colors={['rgba(0,0,0,0.7)', 'transparent']} style={styles.cameraTopBar}>
              <TouchableOpacity onPress={() => setMode('menu')} style={styles.cameraBackBtn}>
                <Ionicons name="arrow-back" size={22} color="#fff" />
              </TouchableOpacity>
              <Text style={styles.cameraHint}>{t('scan.cameraHint')}</Text>
              <View style={{ width: 40 }} />
            </LinearGradient>
            {/* Viewfinder frame */}
            <View style={styles.frameWrapper}>
              <View style={[styles.frameCorner, styles.cornerTL]} />
              <View style={[styles.frameCorner, styles.cornerTR]} />
              <View style={[styles.frameCorner, styles.cornerBL]} />
              <View style={[styles.frameCorner, styles.cornerBR]} />
            </View>
            <LinearGradient colors={['transparent', 'rgba(0,0,0,0.7)']} style={styles.cameraBottomBar}>
              <TouchableOpacity onPress={pickFromGallery} style={styles.galleryMiniBtn}>
                <Ionicons name="images-outline" size={26} color="#fff" />
              </TouchableOpacity>
              <TouchableOpacity onPress={takePicture} style={styles.captureBtn}>
                <LinearGradient colors={gradients.cta} style={styles.captureInner} />
              </TouchableOpacity>
              <View style={{ width: 48 }} />
            </LinearGradient>
          </View>
        </CameraView>
      </View>
    );
  }

  // ── Preview ───────────────────────────────────────────────────────────
  return (
    <LinearGradient colors={gradients.hero} style={styles.gradient}>
      <SafeAreaView style={styles.safeArea} edges={['top', 'bottom']}>
        <View style={styles.previewHeader}>
          <TouchableOpacity
            style={styles.previewBackBtn}
            onPress={() => { setMode('menu'); setImageUri(null); }}
          >
            <Ionicons name="arrow-back" size={20} color={colors.text} />
          </TouchableOpacity>
          <Text style={styles.previewTitle}>{t('scan.confirmPhoto')}</Text>
          {!isOnline
            ? <View style={styles.offlinePill}><Text style={styles.offlinePillText}>{t('scan.offlinePill')}</Text></View>
            : <View style={{ width: 60 }} />
          }
        </View>

        <Image source={{ uri: imageUri }} style={styles.previewImage} resizeMode="cover" />

        <View style={styles.previewActions}>
          <Text style={styles.previewHint}>
            {isOnline ? t('scan.previewHintOnline') : t('scan.previewHintOffline')}
          </Text>

          {/* Primary analyze button */}
          <TouchableOpacity
            style={[styles.analyzeBtn, analyzing && styles.analyzeBtnDisabled]}
            onPress={analyzeImage}
            disabled={analyzing}
            activeOpacity={0.88}
          >
            {analyzing ? (
              <View style={styles.analyzingRow}>
                <ActivityIndicator color={colors.primaryMid} />
                <Text style={styles.analyzeBtnText}>
                  {isOnline ? t('scan.analyzingOnline') : t('scan.analyzingOffline')}
                </Text>
              </View>
            ) : (
              <View style={styles.analyzingRow}>
                <Text style={styles.sparkle}>✦</Text>
                <Text style={styles.analyzeBtnText}>{t('scan.analyzeDisease')}</Text>
              </View>
            )}
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.retakeBtn}
            onPress={() => { setImageUri(null); setMode('camera'); }}
            disabled={analyzing}
            activeOpacity={0.85}
          >
            <Ionicons name="camera-outline" size={18} color={colors.accent} />
            <Text style={styles.retakeBtnText}>{t('scan.retake')}</Text>
          </TouchableOpacity>
        </View>
      </SafeAreaView>
    </LinearGradient>
  );
}

const styles = StyleSheet.create({
  gradient: { flex: 1 },
  safeArea: { flex: 1 },

  // Menu header
  menuHeader: { paddingHorizontal: 20, paddingTop: 6, paddingBottom: 22, overflow: 'hidden' },
  menuHeaderRow: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between' },
  menuKicker: { fontSize: 10, fontWeight: '800', color: colors.mintDark, letterSpacing: 2, textTransform: 'uppercase', marginBottom: 4 },
  menuTitle: { fontSize: 26, fontWeight: '800', color: colors.text, letterSpacing: -0.5, marginBottom: 6 },
  menuSub: { fontSize: 13, color: colors.textSecondary, lineHeight: 18, maxWidth: 260 },
  statusRing: {
    width: 44,
    height: 44,
    borderRadius: 22,
    borderWidth: 2,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: 'rgba(255,255,255,0.06)',
    marginTop: 4,
  },
  statusDot: { width: 14, height: 14, borderRadius: 7 },

  menuBody: { paddingHorizontal: 16 },

  offlineBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    backgroundColor: colors.warningBg,
    borderRadius: radius.lg,
    padding: 14,
    marginBottom: 14,
    borderWidth: 1,
    borderColor: 'rgba(251,191,36,0.2)',
  },
  offlineIcon: {
    width: 40,
    height: 40,
    borderRadius: 12,
    backgroundColor: 'rgba(251,191,36,0.1)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  offlineTitle: { fontSize: 13, fontWeight: '800', color: colors.warning },
  offlineSub: { fontSize: 11, color: colors.textMuted, marginTop: 2 },

  optionCard: {
    borderRadius: radius.xl,
    overflow: 'hidden',
    marginBottom: 14,
    ...shadows.cta,
  },
  optionGradient: { padding: 24, alignItems: 'center', overflow: 'hidden' },
  optionShine: { position: 'absolute', left: 0, right: 0, top: 0, height: '40%' },
  optionIconWrap: {
    width: 64,
    height: 64,
    borderRadius: 20,
    backgroundColor: colors.glassGreen,
    borderWidth: 1,
    borderColor: colors.glassGreenBorder,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 12,
  },
  optionTitle: { fontSize: 18, fontWeight: '800', color: colors.text, marginBottom: 4, letterSpacing: -0.3 },
  optionSub: { fontSize: 13, color: colors.textSecondary },
  optionArrow: {
    marginTop: 12,
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: colors.glassGreen,
    borderWidth: 1,
    borderColor: colors.glassGreenBorder,
    justifyContent: 'center',
    alignItems: 'center',
  },

  tipsBox: {
    backgroundColor: colors.glass,
    borderRadius: radius.xl,
    padding: 16,
    paddingTop: 18,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    overflow: 'hidden',
  },
  tipsTopLine: { position: 'absolute', top: 0, left: 0, right: 0, height: 3 },
  tipsTitle: { fontSize: 14, fontWeight: '800', color: colors.text, marginBottom: 12, letterSpacing: -0.2 },
  tipRow: { flexDirection: 'row', marginBottom: 6, gap: 8 },
  tipBullet: { color: colors.accent, fontWeight: '700', fontSize: 14 },
  tipText: { fontSize: 13, color: colors.textSecondary, flex: 1 },

  cropsBox: {
    backgroundColor: colors.glass,
    borderRadius: radius.lg,
    padding: 16,
    borderWidth: 1,
    borderColor: colors.glassBorder,
  },
  cropsTitle: { fontSize: 11, fontWeight: '800', color: colors.textMuted, marginBottom: 10, letterSpacing: 0.5, textTransform: 'uppercase' },
  cropsRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  cropPill: {
    backgroundColor: colors.glassGreen,
    paddingHorizontal: 14,
    paddingVertical: 7,
    borderRadius: radius.pill,
    borderWidth: 1,
    borderColor: colors.glassGreenBorder,
  },
  cropPillText: { fontSize: 13, color: colors.accent, fontWeight: '700' },

  // Camera
  cameraContainer: { flex: 1 },
  camera: { flex: 1 },
  cameraOverlay: { flex: 1, justifyContent: 'space-between' },
  cameraTopBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: 54,
    paddingBottom: 16,
    paddingHorizontal: 20,
  },
  cameraBackBtn: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(255,255,255,0.15)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  cameraHint: { fontSize: 14, color: '#fff', fontWeight: '600' },
  frameWrapper: {
    alignSelf: 'center',
    width: 260,
    height: 260,
  },
  frameCorner: {
    position: 'absolute',
    width: 28,
    height: 28,
    borderColor: colors.accent,
  },
  cornerTL: { top: 0, left: 0, borderTopWidth: 3, borderLeftWidth: 3, borderTopLeftRadius: 6 },
  cornerTR: { top: 0, right: 0, borderTopWidth: 3, borderRightWidth: 3, borderTopRightRadius: 6 },
  cornerBL: { bottom: 0, left: 0, borderBottomWidth: 3, borderLeftWidth: 3, borderBottomLeftRadius: 6 },
  cornerBR: { bottom: 0, right: 0, borderBottomWidth: 3, borderRightWidth: 3, borderBottomRightRadius: 6 },
  cameraBottomBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 40,
    paddingTop: 16,
    paddingBottom: 48,
  },
  galleryMiniBtn: {
    width: 48,
    height: 48,
    borderRadius: 24,
    backgroundColor: 'rgba(255,255,255,0.15)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  captureBtn: {
    width: 72,
    height: 72,
    borderRadius: 36,
    backgroundColor: 'rgba(255,255,255,0.2)',
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 3,
    borderColor: 'rgba(255,255,255,0.5)',
    padding: 5,
    ...shadows.glow,
  },
  captureInner: { flex: 1, borderRadius: 28 },

  // Preview
  previewHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: colors.glassBorder,
  },
  previewBackBtn: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.glass,
    borderWidth: 1,
    borderColor: colors.glassBorder,
    justifyContent: 'center',
    alignItems: 'center',
  },
  previewTitle: { fontSize: 17, fontWeight: '800', color: colors.text },
  offlinePill: {
    backgroundColor: colors.dangerBg,
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: 'rgba(248,113,113,0.25)',
  },
  offlinePillText: { fontSize: 12, color: colors.danger, fontWeight: '700' },
  previewImage: { flex: 1, width: '100%' },
  previewActions: {
    backgroundColor: colors.bgElevated,
    padding: 20,
    paddingBottom: Platform.OS === 'ios' ? 34 : 20,
    borderTopWidth: 1,
    borderTopColor: colors.glassBorder,
  },
  previewHint: { fontSize: 13, color: colors.textMuted, textAlign: 'center', marginBottom: 16 },
  analyzeBtn: {
    backgroundColor: colors.white,
    borderRadius: radius.pill,
    height: 56,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 12,
    ...shadows.cta,
  },
  analyzeBtnDisabled: { opacity: 0.75 },
  analyzingRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  sparkle: { fontSize: 16, color: colors.primaryMid },
  analyzeBtnText: { fontSize: 16, fontWeight: '800', color: colors.primaryMid },
  retakeBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    height: 50,
    borderWidth: 1.5,
    borderColor: colors.glassBorder,
    borderRadius: radius.pill,
    backgroundColor: colors.glass,
  },
  retakeBtnText: { fontSize: 15, fontWeight: '700', color: colors.accent },
});
