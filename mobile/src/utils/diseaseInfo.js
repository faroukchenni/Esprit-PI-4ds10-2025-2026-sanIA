/**
 * Disease metadata for all 16 classes the model was trained on.
 * Includes display names, severity, description, and actionable recommendations.
 * UI translations (FR / Tunisian Derja) live in src/locales — use getDiseaseInfo(..., t).
 */

import i18n from '../i18n';

export const DISEASE_INFO = {
  Apple___Apple_scab: {
    displayName: 'Apple Scab',
    crop: 'Apple',
    status: 'diseased',
    severity: 'moderate',
    color: '#E76F51',
    icon: '🍎',
    description:
      'Fungal disease (Venturia inaequalis) causing dark, velvety or scabby spots on leaves, fruit, and stems.',
    recommendations: [
      'Apply fungicide (captan or myclobutanil) during bud-break and early leaf stage',
      'Remove and destroy infected fallen leaves to break the disease cycle',
      'Prune for better air circulation inside the canopy',
      'Avoid overhead irrigation — water at the base of the tree',
      'Plant scab-resistant apple varieties for new plantings',
    ],
  },

  Apple___Black_rot: {
    displayName: 'Apple Black Rot',
    crop: 'Apple',
    status: 'diseased',
    severity: 'high',
    color: '#C1121F',
    icon: '🍎',
    description:
      'Fungal disease (Botryosphaeria obtusa) causing circular lesions on leaves, mummified fruit, and cankers on branches.',
    recommendations: [
      'Prune out all dead or cankered wood and burn or bury it',
      'Apply captan or thiophanate-methyl fungicide from pink stage through harvest',
      'Remove mummified fruits from the tree and ground',
      'Maintain tree vigor with balanced fertilization',
      'Ensure good drainage around tree roots',
    ],
  },

  Apple___Cedar_apple_rust: {
    displayName: 'Cedar Apple Rust',
    crop: 'Apple',
    status: 'diseased',
    severity: 'moderate',
    color: '#F4A261',
    icon: '🍎',
    description:
      'Fungal disease (Gymnosporangium juniperi-virginianae) creating bright orange-yellow spots on upper leaf surface.',
    recommendations: [
      'Apply myclobutanil or sulfur-based fungicide from pink bud through first cover spray',
      'Remove nearby eastern red cedar or juniper trees if possible (alternate host)',
      'Plant rust-resistant apple varieties',
      'Scout fields regularly during spring wet periods',
    ],
  },

  Apple___healthy: {
    displayName: 'Healthy Apple',
    crop: 'Apple',
    status: 'healthy',
    severity: 'none',
    color: '#52B788',
    icon: '✅',
    description: 'The apple plant appears healthy with no visible disease symptoms.',
    recommendations: [
      'Continue current management practices',
      'Apply preventive fungicide sprays during high-risk wet periods',
      'Monitor weekly for early symptoms of scab or rust',
      'Maintain proper pruning and nutrition schedule',
    ],
  },

  Background_without_leaves: {
    displayName: 'No Plant Detected',
    crop: 'Unknown',
    status: 'invalid',
    severity: 'none',
    color: '#ADB5BD',
    icon: '⚠️',
    description: 'The image does not contain a clear plant leaf. Please retake the photo.',
    recommendations: [
      'Take a close-up photo of a single leaf',
      'Ensure good lighting — avoid shadows or glare',
      'Fill most of the frame with the leaf',
      'Keep the camera steady to avoid blurring',
    ],
  },

  Grape___Black_rot: {
    displayName: 'Grape Black Rot',
    crop: 'Grape',
    status: 'diseased',
    severity: 'high',
    color: '#C1121F',
    icon: '🍇',
    description:
      'Fungal disease (Guignardia bidwellii) causing brown leaf spots with black pycnidia, and shriveled mummified berries.',
    recommendations: [
      'Apply mancozeb or myclobutanil from bloom through 4–6 weeks after',
      'Remove all mummified berries and infected canes during dormant pruning',
      'Destroy infected debris — do not compost',
      'Improve canopy airflow with proper trellising and leaf removal',
      'Spray preventively before forecast rain events',
    ],
  },

  'Grape___Esca_(Black_Measles)': {
    displayName: 'Grape Esca (Black Measles)',
    crop: 'Grape',
    status: 'diseased',
    severity: 'high',
    color: '#9B2226',
    icon: '🍇',
    description:
      'Complex fungal trunk disease causing "tiger stripe" leaf discoloration, berry spotting, and wood decay.',
    recommendations: [
      'No curative treatment exists — focus on prevention',
      'Make pruning cuts during dry weather to prevent spore infection',
      'Apply wound sealant (Bordeaux paste) on pruning wounds',
      'Remove severely affected vines to avoid spreading',
      'Avoid water stress which increases susceptibility',
    ],
  },

  'Grape___Leaf_blight_(Isariopsis_Leaf_Spot)': {
    displayName: 'Grape Leaf Blight',
    crop: 'Grape',
    status: 'diseased',
    severity: 'moderate',
    color: '#E76F51',
    icon: '🍇',
    description:
      'Fungal disease (Isariopsis clavispora) causing angular brown spots on leaves, leading to premature defoliation.',
    recommendations: [
      'Apply copper-based fungicide or mancozeb at first signs',
      'Remove and destroy heavily infected leaves',
      'Avoid dense canopies — thin leaves to improve air circulation',
      'Water at the base, not overhead',
    ],
  },

  Grape___healthy: {
    displayName: 'Healthy Grape',
    crop: 'Grape',
    status: 'healthy',
    severity: 'none',
    color: '#52B788',
    icon: '✅',
    description: 'The grape vine appears healthy with no visible disease symptoms.',
    recommendations: [
      'Continue preventive copper or mancozeb spray program',
      'Monitor for early signs of black rot after rainy periods',
      'Maintain canopy openness with regular leaf removal',
      'Keep records of spray schedule for disease traceability',
    ],
  },

  Potato___Early_blight: {
    displayName: 'Potato Early Blight',
    crop: 'Potato',
    status: 'diseased',
    severity: 'moderate',
    color: '#E9C46A',
    icon: '🥔',
    description:
      'Fungal disease (Alternaria solani) causing concentric ring "target-board" lesions on older leaves.',
    recommendations: [
      'Apply chlorothalonil, mancozeb, or azoxystrobin at first symptom',
      'Scout fields regularly starting at canopy closure',
      'Remove and destroy heavily infected foliage',
      'Avoid excessive nitrogen which increases susceptibility',
      'Use certified, disease-free seed potatoes',
    ],
  },

  Potato___Late_blight: {
    displayName: 'Potato Late Blight',
    crop: 'Potato',
    status: 'diseased',
    severity: 'critical',
    color: '#9B2226',
    icon: '🥔',
    description:
      'Devastating oomycete disease (Phytophthora infestans) — the pathogen that caused the Irish Famine. Water-soaked lesions turn brown-black rapidly.',
    recommendations: [
      'Apply metalaxyl + mancozeb or cymoxanil IMMEDIATELY',
      'Destroy infected plants — bag and bury or burn, do not compost',
      'Harvest unaffected tubers immediately if infection is severe',
      'Avoid overhead irrigation and working in wet fields',
      'Plant resistant varieties (e.g., Sarpo Mira) in future seasons',
      'Alert neighboring farms — this disease spreads very rapidly',
    ],
  },

  Potato___healthy: {
    displayName: 'Healthy Potato',
    crop: 'Potato',
    status: 'healthy',
    severity: 'none',
    color: '#52B788',
    icon: '✅',
    description: 'The potato plant appears healthy with no visible disease symptoms.',
    recommendations: [
      'Continue preventive fungicide program during humid periods',
      'Scout weekly for early blight and late blight symptoms',
      'Ensure proper hilling to protect tubers from light exposure',
      'Maintain balanced fertilization — avoid excess nitrogen',
    ],
  },

  Tomato___Bacterial_spot: {
    displayName: 'Tomato Bacterial Spot',
    crop: 'Tomato',
    status: 'diseased',
    severity: 'moderate',
    color: '#F4A261',
    icon: '🍅',
    description:
      'Bacterial disease (Xanthomonas spp.) causing small, water-soaked spots with yellow halos on leaves and fruit.',
    recommendations: [
      'Apply copper-based bactericide (copper hydroxide) preventively',
      'Avoid overhead irrigation — use drip irrigation',
      'Remove and destroy infected plant material',
      'Rotate crops — do not plant tomatoes in the same location for 2 years',
      'Use disease-free certified seeds or transplants',
    ],
  },

  Tomato___Early_blight: {
    displayName: 'Tomato Early Blight',
    crop: 'Tomato',
    status: 'diseased',
    severity: 'moderate',
    color: '#E76F51',
    icon: '🍅',
    description:
      'Fungal disease (Alternaria solani) causing concentric ring lesions starting on lower/older leaves.',
    recommendations: [
      'Apply chlorothalonil or mancozeb at first symptom appearance',
      'Remove affected lower leaves and destroy them',
      'Mulch around base to prevent soil splash onto leaves',
      'Stake or cage plants to keep foliage off the ground',
      'Rotate with non-solanaceous crops annually',
    ],
  },

  Tomato___Late_blight: {
    displayName: 'Tomato Late Blight',
    crop: 'Tomato',
    status: 'diseased',
    severity: 'critical',
    color: '#9B2226',
    icon: '🍅',
    description:
      'Oomycete disease (Phytophthora infestans) causing large, greasy-green to brown lesions rapidly destroying foliage and fruit.',
    recommendations: [
      'Apply metalaxyl-M or cymoxanil + mancozeb IMMEDIATELY',
      'Remove and destroy infected plants completely — do not compost',
      'Avoid working in the field when foliage is wet',
      'Eliminate volunteer tomato/potato plants in surrounding areas',
      'Alert neighboring farms due to rapid airborne spread',
      'Consider harvest of green tomatoes if infection is severe',
    ],
  },

  Tomato___healthy: {
    displayName: 'Healthy Tomato',
    crop: 'Tomato',
    status: 'healthy',
    severity: 'none',
    color: '#52B788',
    icon: '✅',
    description: 'The tomato plant appears healthy with no visible disease symptoms.',
    recommendations: [
      'Maintain preventive fungicide and bactericide spray schedule',
      'Scout weekly — especially after wet, humid weather',
      'Keep foliage dry — use drip irrigation or water early in the day',
      'Remove suckers and stake plants for good air circulation',
    ],
  },
};

export const SEVERITY_CONFIG = {
  none: { label: 'Healthy', color: '#52B788', bg: '#D8F3DC' },
  low: { label: 'Low Risk', color: '#D4A017', bg: '#FFF8DC' },
  moderate: { label: 'Moderate', color: '#E9C46A', bg: '#FFF3CD' },
  high: { label: 'High Risk', color: '#E76F51', bg: '#FFE8E0' },
  critical: { label: 'Critical', color: '#9B2226', bg: '#FFE0E0' },
  invalid: { label: 'Invalid Image', color: '#ADB5BD', bg: '#F0F0F0' },
};

export function getDiseaseInfo(className, t) {
  const rawFallback = {
    displayName: (className || '').replace(/___/g, ' - ').replace(/_/g, ' '),
    crop: 'Unknown',
    status: 'unknown',
    severity: 'moderate',
    color: '#6C757D',
    icon: '🌿',
    description: 'Disease information not available.',
    recommendations: ['Consult a local agronomist for advice.'],
  };

  const base = DISEASE_INFO[className] ?? rawFallback;

  if (!t || i18n.language === 'en') {
    if (t && !DISEASE_INFO[className]) {
      return { ...base, crop: t('common.unknown') };
    }
    return base;
  }

  const prefix = `diseases.${className}`;
  if (!i18n.exists(`${prefix}.displayName`)) {
    return {
      ...base,
      crop: base.crop === 'Unknown' ? t('common.unknown') : base.crop,
    };
  }

  const recs = t(`${prefix}.recommendations`, { returnObjects: true });
  return {
    ...base,
    displayName: t(`${prefix}.displayName`),
    crop: t(`${prefix}.crop`),
    description: t(`${prefix}.description`),
    recommendations: Array.isArray(recs) && recs.length ? recs : base.recommendations,
  };
}
