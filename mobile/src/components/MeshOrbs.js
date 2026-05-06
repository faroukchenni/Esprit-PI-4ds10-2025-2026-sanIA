import { View, StyleSheet } from 'react-native';

/**
 * Soft glowing orbs for dark forest green backgrounds — atmospheric depth.
 */
export default function MeshOrbs({ variant = 'hero' }) {
  if (variant === 'compact') {
    return (
      <View style={StyleSheet.absoluteFill} pointerEvents="none">
        <View style={[styles.orb, styles.c1s]} />
        <View style={[styles.orb, styles.c2s]} />
      </View>
    );
  }
  return (
    <View style={StyleSheet.absoluteFill} pointerEvents="none">
      <View style={[styles.orb, styles.c1]} />
      <View style={[styles.orb, styles.c2]} />
      <View style={[styles.orb, styles.c3]} />
    </View>
  );
}

const styles = StyleSheet.create({
  orb: { position: 'absolute', borderRadius: 999 },
  c1: {
    width: 220,
    height: 220,
    right: -70,
    top: -60,
    backgroundColor: 'rgba(74,222,128,0.14)',
  },
  c2: {
    width: 140,
    height: 140,
    left: -50,
    bottom: -30,
    backgroundColor: 'rgba(34,197,94,0.1)',
  },
  c3: {
    width: 80,
    height: 80,
    right: 60,
    bottom: 20,
    backgroundColor: 'rgba(134,239,172,0.08)',
  },
  c1s: {
    width: 120,
    height: 120,
    right: -30,
    top: -30,
    backgroundColor: 'rgba(74,222,128,0.16)',
  },
  c2s: {
    width: 70,
    height: 70,
    left: 10,
    bottom: 4,
    backgroundColor: 'rgba(34,197,94,0.09)',
  },
});
