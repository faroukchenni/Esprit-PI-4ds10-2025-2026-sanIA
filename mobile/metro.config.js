const { getDefaultConfig } = require('expo/metro-config');

const config = getDefaultConfig(__dirname);

// Allow Metro to bundle .tflite binary model files as assets
if (!config.resolver.assetExts.includes('tflite')) {
  config.resolver.assetExts.push('tflite');
}

module.exports = config;
