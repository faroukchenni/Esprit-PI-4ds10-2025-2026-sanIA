import api from './api';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Asset } from 'expo-asset';
import * as ImageManipulator from 'expo-image-manipulator';
import { Buffer } from 'buffer';
import { loadTensorflowModel } from 'react-native-fast-tflite';
import jpeg from 'jpeg-js';

const CACHE_KEY = 'sania_scan_history';
const MAX_CACHE = 10;

// Severity thresholds (must match legacy + v3 training)
const SEVERITY_LABELS = [
  { max: 5,   label: 'Healthy',  color: '#27ae60' },
  { max: 20,  label: 'Low',      color: '#f1c40f' },
  { max: 40,  label: 'Moderate', color: '#e67e22' },
  { max: 60,  label: 'High',     color: '#e74c3c' },
  { max: 100, label: 'Critical', color: '#8e44ad' },
];

function getSeverityCategory(pct) {
  for (const s of SEVERITY_LABELS) {
    if (pct < s.max) return { label: s.label, color: s.color };
  }
  return { label: 'Critical', color: '#8e44ad' };
}

// Disease class names — MUST match training order (alphabetical)
const CLASSES = [
  'Apple___Apple_scab',
  'Apple___Black_rot',
  'Apple___Cedar_apple_rust',
  'Apple___healthy',
  'Background_without_leaves',
  'Grape___Black_rot',
  'Grape___Esca_(Black_Measles)',
  'Grape___Leaf_blight_(Isariopsis_Leaf_Spot)',
  'Grape___healthy',
  'Potato___Early_blight',
  'Potato___Late_blight',
  'Potato___healthy',
  'Tomato___Bacterial_spot',
  'Tomato___Early_blight',
  'Tomato___Late_blight',
  'Tomato___healthy',
];

// ─── Cache helpers ────────────────────────────────────────────────────────────

async function loadCache() {
  try {
    const raw = await AsyncStorage.getItem(CACHE_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

async function saveToCache(scanResult) {
  try {
    const existing = await loadCache();
    const updated = [scanResult, ...existing].slice(0, MAX_CACHE);
    await AsyncStorage.setItem(CACHE_KEY, JSON.stringify(updated));
  } catch {
    // cache write failure is non-fatal
  }
}

// ─── On-device TFLite inference ───────────────────────────────────────────────

let _model = null;
let _severityModel = null;

/**
 * Load the disease classification TFLite model from bundled assets.
 */
async function getModel() {
  if (_model) return _model;

  const [asset] = await Asset.loadAsync(
    require('../../assets/models/best_model.tflite')
  );
  if (!asset?.localUri) {
    throw new Error('Disease model could not be resolved to a local file URI.');
  }
  _model = await loadTensorflowModel({ url: asset.localUri });
  return _model;
}

/**
 * Load the severity segmentation TFLite model (Attention U-Net, exported from TDSP_Severity_Model_Final).
 * Input:  [1, 224, 224, 3] float32 — RGB normalized to [0, 1]
 * Output: [1, 224, 224, 1] float32 — pixel-wise disease mask
 */
async function getSeverityModel() {
  if (_severityModel) return _severityModel;

  const [asset] = await Asset.loadAsync(
    require('../../assets/models/severity_model_v3.tflite')
  );
  if (!asset?.localUri) {
    throw new Error('Severity model could not be resolved to a local file URI.');
  }
  _severityModel = await loadTensorflowModel({ url: asset.localUri });
  return _severityModel;
}

/**
 * Resize image to 224×224 and return a Float32Array normalized to [0, 1].
 * v3 model expects values in [0,1].
 */
async function preprocessForSeverity(imageUri) {
  const { base64 } = await ImageManipulator.manipulateAsync(
    imageUri,
    [{ resize: { width: 224, height: 224 } }],
    { format: 'jpeg', base64: true }
  );

  const { data } = jpeg.decode(Buffer.from(base64, 'base64'), { useTArray: true });

  const float32 = new Float32Array(224 * 224 * 3);
  let j = 0;
  for (let i = 0; i < data.length; i += 4) {
    float32[j++] = data[i]     / 255.0;  // R → [0,1]
    float32[j++] = data[i + 1] / 255.0;  // G → [0,1]
    float32[j++] = data[i + 2] / 255.0;  // B → [0,1]
  }
  return float32;
}

/**
 * Run severity analysis using v3 Attention U-Net with color fallback.
 */
async function runSeverityAnalysis(imageUri, diseaseConfidence = 0, isHealthy = false) {
  try {
    const model  = await getSeverityModel();
    const pixels = await preprocessForSeverity(imageUri);
    const outputs = await model.run([pixels]);
    const mask    = outputs?.[0];

    let severityPct = 0;
    let usedFallback = false;

    if (mask && mask.length > 0) {
      // Use whatever the model returns — flexible size
      const MASK_THRESHOLD = 0.35;
      let diseased = 0, softDiseased = 0;
      for (let i = 0; i < mask.length; i++) {
        if (mask[i] > MASK_THRESHOLD) diseased++;
        else if (mask[i] > 0.15) softDiseased++;
      }
      severityPct = parseFloat(((diseased / mask.length) * 100).toFixed(1));
      const softPct = parseFloat(((softDiseased / mask.length) * 100).toFixed(1));

      // Blend in soft pixels when model is under-confident
      if (!isHealthy && diseaseConfidence > 0.60 && severityPct < 5) {
        severityPct = Math.min(parseFloat((severityPct + softPct * 0.5).toFixed(1)), 50);
      }
    } else {
      usedFallback = true;
    }

    // --- COLOR-BASED FALLBACK (Option B) ---
    // If model failed OR result is suspiciously low for a diseased plant,
    // use pixel color analysis: count brown/yellow/dark pixels as disease indicator
    if ((usedFallback || severityPct < 1) && !isHealthy && diseaseConfidence > 0.60) {
      let brownYellowPixels = 0;
      const total = pixels.length / 3;
      for (let i = 0; i < pixels.length; i += 3) {
        const r = pixels[i], g = pixels[i+1], b = pixels[i+2];
        // Brown pixels: R high, G medium-low, B low
        const isBrown = r > 0.35 && g < 0.55 && b < 0.40 && r > g && r > b;
        // Yellow pixels: R & G high, B low
        const isYellow = r > 0.50 && g > 0.45 && b < 0.35 && (r + g) > b * 2.5;
        // Dark necrotic pixels: all channels low (dead tissue)
        const isNecrotic = r < 0.25 && g < 0.25 && b < 0.25;
        if (isBrown || isYellow || isNecrotic) brownYellowPixels++;
      }
      // Cap at 70% to avoid counting background objects
      const colorSeverity = Math.min(parseFloat(((brownYellowPixels / total) * 100).toFixed(1)), 70);
      // Weight: 60% color signal + confidence adjustment
      severityPct = Math.max(severityPct, parseFloat((colorSeverity * 0.6).toFixed(1)));
    }

    // --- ABSOLUTE FLOOR (Option C) ---
    // If disease is confirmed but severity is still < 5%, apply minimum
    if (!isHealthy && diseaseConfidence > 0.60 && severityPct < 5) {
      severityPct = parseFloat((5 + (diseaseConfidence - 0.60) * 25).toFixed(1));
    }

    // Classifier said "healthy" but seg model still fires on veins/texture — never show high %
    if (isHealthy) {
      severityPct = Math.min(severityPct, 4.9);
    }

    const { label: severityLabel, color: severityColor } = getSeverityCategory(severityPct);
    return { severityPct, severityLabel, severityColor };
  } catch (error) {
    // --- EMERGENCY FALLBACK: if everything fails, estimate from confidence ---
    console.error('Severity analysis error:', error);
    if (!isHealthy && diseaseConfidence > 0.60) {
      const fallbackPct = parseFloat((diseaseConfidence * 20).toFixed(1)); // e.g. 79% conf → ~15.8%
      const { label: severityLabel, color: severityColor } = getSeverityCategory(fallbackPct);
      return { severityPct: fallbackPct, severityLabel, severityColor };
    }
    return null;
  }
}

/**
 * Resize the image to 224×224 and decode JPEG into a flat Float32Array [0–255].
 * The model includes MobileNetV3 preprocessing internally, so raw [0,255] is correct.
 */
async function preprocessToFloat32(imageUri) {
  // Step 1: resize to 224×224, get base64 JPEG
  const { base64 } = await ImageManipulator.manipulateAsync(
    imageUri,
    [{ resize: { width: 224, height: 224 } }],
    { format: 'jpeg', base64: true }
  );

  // Step 2: decode JPEG → raw RGBA bytes using jpeg-js
  const jpegBuffer = Buffer.from(base64, 'base64');
  const { data } = jpeg.decode(jpegBuffer, { useTArray: true }); // RGBA uint8

  // Step 3: RGBA → RGB Float32 [0, 255]
  const float32 = new Float32Array(224 * 224 * 3);
  let j = 0;
  for (let i = 0; i < data.length; i += 4) {
    float32[j++] = data[i];       // R
    float32[j++] = data[i + 1];   // G
    float32[j++] = data[i + 2];   // B
    // skip alpha
  }
  return float32;
}

// Match backend/app/routers/scans.py _check_image_quality (64×64, real std dev, same thresholds)
const Q_BRIGHTNESS_MIN = 30;
const Q_STD_MIN = 8;
const Q_GREEN_RATIO_MIN = 0.28;

/**
 * Same guards as server-side offline: random / non-leaf photos are rejected before TFLite.
 */
async function checkImageQualityMatchServer(imageUri) {
  try {
    const { base64 } = await ImageManipulator.manipulateAsync(
      imageUri,
      [{ resize: { width: 64, height: 64 } }],
      { format: 'jpeg', base64: true }
    );
    const { data } = jpeg.decode(Buffer.from(base64, 'base64'), { useTArray: true });
    const rgb = [];
    for (let i = 0; i < data.length; i += 4) {
      rgb.push(data[i], data[i + 1], data[i + 2]);
    }
    if (rgb.length === 0) return null;

    let sum = 0;
    for (let i = 0; i < rgb.length; i++) sum += rgb[i];
    const meanAll = sum / rgb.length;
    let varSum = 0;
    for (let i = 0; i < rgb.length; i++) {
      const d = rgb[i] - meanAll;
      varSum += d * d;
    }
    const stdDev = Math.sqrt(varSum / rgb.length);

    let rSum = 0;
    let gSum = 0;
    let bSum = 0;
    const nPix = rgb.length / 3;
    for (let i = 0; i < rgb.length; i += 3) {
      rSum += rgb[i];
      gSum += rgb[i + 1];
      bSum += rgb[i + 2];
    }
    const rMean = rSum / nPix;
    const gMean = gSum / nPix;
    const bMean = bSum / nPix;
    const chTotal = rMean + gMean + bMean;
    const greenRatio = chTotal > 0 ? gMean / chTotal : 0;

    if (meanAll < Q_BRIGHTNESS_MIN) {
      return `Image is too dark (brightness ${Math.round(meanAll)}/255). Please take the photo in good lighting.`;
    }
    if (stdDev < Q_STD_MIN) {
      return 'Image appears to be blank or a solid color. Please take a clear photo of a plant leaf.';
    }
    if (chTotal > 0 && greenRatio < Q_GREEN_RATIO_MIN) {
      return 'No plant leaf detected in this image. Please take a close-up photo of a leaf.';
    }
    return null;
  } catch {
    return null;
  }
}

/**
 * Run TFLite inference on-device.
 */
async function runOfflineDetection(imageUri) {
  const qualityError = await checkImageQualityMatchServer(imageUri);
  if (qualityError) {
    throw { response: { data: { detail: qualityError } } };
  }

  const model = await getModel();
  const pixels = await preprocessToFloat32(imageUri);

  const outputs = await model.run([pixels]);
  const probabilities = outputs?.[0]; 
  if (!probabilities || probabilities.length !== CLASSES.length) {
    throw new Error(`Model returned unexpected output.`);
  }

  const indexed = Array.from(probabilities).map((p, i) => ({ disease: CLASSES[i], confidence: parseFloat(p.toFixed(4)) }));
  indexed.sort((a, b) => b.confidence - a.confidence);
  const top5 = indexed.slice(0, 5);

  let best = top5[0];
  if (best.disease === 'Background_without_leaves') {
    const nonBg = top5.filter(t => t.disease !== 'Background_without_leaves');
    if (nonBg.length > 0) {
      best = nonBg[0];
    } else {
      throw { response: { data: { detail: 'No plant leaf detected.' } } };
    }
  }

  if (best.confidence < 0.40) {
    throw { response: { data: { detail: 'Low confidence — try a clearer photo.' } } };
  }

  return {
    predicted_disease: best.disease,
    confidence: best.confidence,
    top5,
    offline: true,
  };
}

async function attachSeverity(result, imageUri) {
  if (result.severity_pct != null) return result;

  const confidence = result.confidence ?? 0;
  // Treat prediction as healthy if the disease name contains 'healthy'
  const isHealthy = (result.predicted_disease ?? '').toLowerCase().includes('healthy');

  // Segmentation often false-positives on healthy leaves (texture, veins). If the classifier
  // already says "healthy" with decent confidence, trust it and skip the mask (or we'd show ~30% "disease").
  if (isHealthy && confidence >= 0.45) {
    const { label: severityLabel, color: severityColor } = getSeverityCategory(0);
    return {
      ...result,
      severity_pct: 0,
      severity_label: severityLabel,
      severity_color: severityColor,
    };
  }

  const severity = await runSeverityAnalysis(imageUri, confidence, isHealthy);
  if (severity) {
    result.severity_pct   = severity.severityPct;
    result.severity_label = severity.severityLabel;
    result.severity_color = severity.severityColor;
  }
  return result;
}

export const scanService = {
  async detectDisease(imageUri, options = {}) {
    const formData = new FormData();
    formData.append('image', { uri: imageUri, name: 'plant.jpg', type: 'image/jpeg' });
    if (options.fieldId) formData.append('field_id', options.fieldId);
    if (options.cropType) formData.append('crop_type', options.cropType);
    formData.append('save_scan', options.saveScan ? 'true' : 'false');

    const response = await api.post('/scans/detect', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    });

    let result = { ...response.data, offline: false };
    if (options.awaitSeverity === true) {
      result = await attachSeverity(result, imageUri);
    }
    await saveToCache({
      id: Date.now().toString(),
      predicted_disease: result.predicted_disease,
      confidence: result.confidence,
      severity_pct: result.severity_pct ?? null,
      severity_label: result.severity_label ?? null,
      crop_type: options.cropType || 'Unknown',
      created_at: new Date().toISOString(),
      offline: false,
      synced: true,
    });
    return result;
  },

  async detectDiseaseOffline(imageUri, options = {}) {
    let result = await runOfflineDetection(imageUri);
    if (options.awaitSeverity === true) {
      result = await attachSeverity(result, imageUri);
    }
    await saveToCache({
      id: Date.now().toString(),
      predicted_disease: result.predicted_disease,
      confidence: result.confidence,
      severity_pct: result.severity_pct ?? null,
      severity_label: result.severity_label ?? null,
      crop_type: options.cropType || 'Unknown',
      created_at: new Date().toISOString(),
      offline: true,
      synced: false,
    });
    return result;
  },

  async getHistory() {
    try {
      const response = await api.get('/scans/');
      return response.data;
    } catch {
      return this.getCachedHistory();
    }
  },

  async getCachedHistory() {
    return loadCache();
  },

  async syncOfflineScans() {
    const cache = await loadCache();
    const unsynced = cache.filter((s) => !s.synced && s.offline);
    if (unsynced.length === 0) return;

    for (const scan of unsynced) {
      try {
        await api.post('/scans/', {
          field_id: null,
          crop_type: scan.crop_type,
          image_url: 'offline_scan',
          predicted_disease: scan.predicted_disease,
          confidence: scan.confidence,
        });
        scan.synced = true;
      } catch { }
    }

    const updated = cache.map((s) => unsynced.find((u) => u.id === s.id) || s);
    await AsyncStorage.setItem(CACHE_KEY, JSON.stringify(updated));
  },

  async computeSeverity(imageUri, predictedDisease, confidence = 0) {
    return attachSeverity({ predicted_disease: predictedDisease, confidence }, imageUri);
  },
};
