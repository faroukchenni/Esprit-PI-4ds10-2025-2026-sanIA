import React, { useState, useEffect } from 'react';
import { Map, Plus, ChevronRight, ChevronDown, Thermometer, Droplets, X, Leaf, Activity, Clock, Waves, BarChart3, RefreshCw, Satellite, Edit, Trash2 } from 'lucide-react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, AreaChart, Area } from 'recharts';
import { fieldService, sensorService, ndviService } from '../services/api';
import FieldMap from '../components/FieldMap';
import ScrollReveal from '../components/ScrollReveal';

const cropEmojis = { Tomato: '🍅', Grape: '🍇', Potato: '🥔', Apple: '🍎', Wheat: '🌾', Olive: '🫒', default: '🌱' };

const CROP_OPTIONS = [
  { value: 'Tomato',  label: 'Tomate',         emoji: '🍅' },
  { value: 'Grape',   label: 'Vigne',           emoji: '🍇' },
  { value: 'Potato',  label: 'Pomme de terre',  emoji: '🥔' },
  { value: 'Apple',   label: 'Pommier',         emoji: '🍎' },
  { value: 'Wheat',   label: 'Blé',             emoji: '🌾' },
  { value: 'Olive',   label: 'Olivier',         emoji: '🫒' },
];

/* ── Custom crop type picker (replaces native <select>) ── */
const CropPicker = ({ value, onChange }) => (
  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: '0.45rem' }}>
    {CROP_OPTIONS.map(opt => {
      const active = value === opt.value;
      return (
        <button
          key={opt.value}
          type="button"
          onClick={() => onChange(opt.value)}
          style={{
            display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.25rem',
            padding: '0.65rem 0.4rem', borderRadius: '12px', cursor: 'pointer', border: 'none',
            background: active ? 'rgba(139,195,74,0.18)' : 'rgba(255,255,255,0.04)',
            outline: active ? '1.5px solid rgba(139,195,74,0.55)' : '1px solid rgba(255,255,255,0.08)',
            transition: 'all 0.15s',
          }}
        >
          <span style={{ fontSize: '1.35rem', lineHeight: 1 }}>{opt.emoji}</span>
          <span style={{ fontSize: '0.62rem', fontWeight: active ? '700' : '500', color: active ? '#A5D96A' : 'rgba(255,255,255,0.45)', letterSpacing: '0.2px' }}>
            {opt.label}
          </span>
        </button>
      );
    })}
  </div>
);

/* ── Custom chart tooltip ── */
const ChartTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null;
  return (
    <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--outline-variant)', borderRadius: 'var(--radius-sm)', padding: '0.6rem 0.8rem', boxShadow: '0 8px 24px rgba(0,0,0,0.06)', fontSize: '0.75rem' }}>
      <p style={{ color: 'var(--text-dim)', marginBottom: '0.2rem' }}>{label}</p>
      {payload.map((p, i) => (
        <p key={i} style={{ color: p.color, fontWeight: '600' }}>{p.name}: {p.value}</p>
      ))}
    </div>
  );
};

/* ── NDVI colour helpers ── */
const ndviColor = v => { if (v==null) return '#888'; if (v>=0.6) return '#4CAF50'; if (v>=0.4) return '#8BC34A'; if (v>=0.2) return '#FFC107'; return '#F44336'; };
const ndviLabel = v => { if (v==null) return 'N/A'; if (v>=0.6) return 'Excellent'; if (v>=0.4) return 'Bon'; if (v>=0.2) return 'Modéré'; return 'Faible'; };

/* ── Satellite Diagnostic Panel ── */
const SatDiagPanel = ({ diag, loading }) => {
  if (loading) return (
    <div style={{ padding: '1.2rem', display: 'flex', alignItems: 'center', gap: '0.7rem', background: 'rgba(78,173,213,0.06)', border: '1px solid rgba(78,173,213,0.15)', borderRadius: '12px' }}>
      <RefreshCw size={14} color="#4EADD5" style={{ animation: 'spin 1s linear infinite' }} />
      <span style={{ fontSize: '0.78rem', color: 'var(--text-dim)' }}>Synchronisation satellite…</span>
    </div>
  );
  if (!diag?.summary) return null;
  const s   = diag.summary;
  const v   = s.avg_ndvi;
  const col = ndviColor(v);
  const pct = v != null ? Math.max(0, Math.min(100, (v + 0.1) / 1.1 * 100)) : 0;

  return (
    <div style={{ background: 'rgba(0,0,0,0.28)', border: '1px solid rgba(255,255,255,0.09)', borderRadius: '14px', overflow: 'hidden' }}>
      {/* Header */}
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '0.75rem 1rem',
        background: `linear-gradient(90deg, ${col}12 0%, transparent 70%)`,
        borderBottom: '1px solid rgba(255,255,255,0.06)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <Satellite size={14} color={col} />
          <span style={{ fontSize: '0.78rem', fontWeight: '700', color: 'var(--text-bright)' }}>Rapport Satellite</span>
        </div>
        <span style={{ padding: '0.16rem 0.6rem', borderRadius: '99px', fontSize: '0.65rem', fontWeight: '700', background: `${col}22`, color: col, border: `1px solid ${col}35` }}>
          {ndviLabel(v)}
        </span>
      </div>

      <div style={{ padding: '0.9rem 1rem', display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
        {/* NDVI value + bar */}
        <div style={{ display: 'flex', alignItems: 'flex-end', gap: '1.2rem' }}>
          <div>
            <div style={{ fontSize: '0.55rem', color: 'rgba(255,255,255,0.4)', textTransform: 'uppercase', letterSpacing: '1.5px' }}>Moy. NDVI</div>
            <div style={{ fontSize: '2rem', fontWeight: '900', color: '#fff', fontFamily: "'Newsreader', serif", lineHeight: 1 }}>
              {v != null ? v.toFixed(3) : '—'}
            </div>
          </div>
          <div style={{ flex: 1, paddingBottom: '4px' }}>
            <div style={{ height: '8px', borderRadius: '99px', background: 'linear-gradient(90deg, #F44336, #FF9800, #FFC107, #8BC34A, #4CAF50)', marginBottom: '4px', position: 'relative' }}>
              <div style={{ position: 'absolute', top: '-4px', left: `${pct}%`, transform: 'translateX(-50%)', width: '16px', height: '16px', borderRadius: '50%', background: col, border: '3px solid #fff', boxShadow: `0 0 8px ${col}88`, transition: 'left 0.8s ease' }} />
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.55rem', color: 'rgba(255,255,255,0.35)' }}>
              <span>−0.1 Sol nu</span><span>0.5</span><span>1.0 Max</span>
            </div>
          </div>
        </div>

        {/* Diagnostic label */}
        {s.health_label && (
          <div style={{ fontSize: '0.8rem', fontWeight: '700', color: col }}>{s.health_label}</div>
        )}

        {/* Stats row */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: '0.5rem' }}>
          {[
            { label: 'Min', val: s.min_ndvi?.toFixed(3) },
            { label: 'Max', val: s.max_ndvi?.toFixed(3) },
            { label: 'EVI', val: s.avg_evi?.toFixed(3) },
          ].filter(x => x.val != null).map(({ label, val }) => (
            <div key={label} style={{ padding: '0.45rem 0.6rem', borderRadius: '9px', background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.07)', textAlign: 'center' }}>
              <div style={{ fontSize: '0.55rem', color: 'rgba(255,255,255,0.35)', textTransform: 'uppercase', letterSpacing: '1px' }}>{label}</div>
              <div style={{ fontSize: '0.88rem', fontWeight: '700', color: 'var(--text-bright)' }}>{val}</div>
            </div>
          ))}
        </div>

        {/* Source footer */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', paddingTop: '0.5rem', borderTop: '1px solid rgba(255,255,255,0.05)', fontSize: '0.65rem', color: 'rgba(255,255,255,0.35)' }}>
          <span>📅 {s.date}</span>
          {s.clouds != null && <span>· ☁️ {s.clouds?.toFixed(1)}%</span>}
          {s.source && <span>· {s.source}</span>}
        </div>
      </div>
    </div>
  );
};

/* ── Expanded Field Detail Panel ── */
const FieldDetail = ({ field }) => {
  const [sensorData,    setSensorData]    = useState([]);
  const [ndviData,      setNdviData]      = useState([]);
  const [irrigationLogs,setIrrigationLogs]= useState([]);
  const [loadingSensors,setLoadingSensors]= useState(true);
  const [viewMode,      setViewMode]      = useState('charts');
  const [showNdviOverlay,setShowNdviOverlay] = useState(false);
  const [diagnosticData, setDiagnosticData] = useState(null);
  const [diagLoading,   setDiagLoading]   = useState(false);
  const [liveNdvi,      setLiveNdvi]      = useState(null);

  useEffect(() => {
    if (!field.polygon_geojson || field.polygon_geojson === '[]') return;
    setDiagLoading(true);
    ndviService.getSpatialDiagnostic(field.id)
      .then(res => {
        const diag = res.data;
        setDiagnosticData(diag);
        const avg = diag?.summary?.avg_ndvi;
        if (typeof avg === 'number') setLiveNdvi(avg);
      })
      .catch(() => {})
      .finally(() => setDiagLoading(false));
  }, [field.id]); // eslint-disable-line

  useEffect(() => {
    const fetchData = async () => {
      setLoadingSensors(true);
      const results = await Promise.allSettled([
        sensorService.getHistory(field.id, 7),
        ndviService.getHistory(field.id, 8),
        fieldService.getIrrigationLogs(field.id),
      ]);
      if (results[0].status === 'fulfilled') {
        setSensorData(results[0].value.data.map(d => ({
          time: new Date(d.created_at).toLocaleDateString('fr', { day: '2-digit', month: 'short' }),
          humidity: Math.round(d.soil_moisture),
          temp: Math.round(d.temperature_c * 10) / 10,
          airHumidity: Math.round(d.humidity_pct),
        })).reverse());
      }
      if (results[1].status === 'fulfilled') {
        setNdviData(results[1].value.data.map(d => ({
          date: new Date(d.captured_at).toLocaleDateString('fr', { day: '2-digit', month: 'short' }),
          ndvi: d.ndvi_value,
        })).reverse());
      }
      if (results[2].status === 'fulfilled') setIrrigationLogs(results[2].value.data);
      setLoadingSensors(false);
    };
    fetchData();
  }, [field.id]);

  if (loadingSensors) return (
    <div style={{ padding: '2rem', textAlign: 'center' }}>
      <div style={{ display: 'flex', justifyContent: 'center', gap: '0.4rem', marginBottom: '0.75rem' }}>
        {[0,1,2].map(i => <div key={i} style={{ width: '8px', height: '8px', borderRadius: '50%', background: '#8BC34A', animation: `bounce 1.2s ease-in-out ${i*0.2}s infinite` }} />)}
      </div>
      <p style={{ color: 'var(--text-dim)', fontSize: '0.8rem' }}>Chargement des données…</p>
    </div>
  );

  const polygon = field.polygon_geojson ? JSON.parse(field.polygon_geojson) : [];
  const lastSensor = sensorData[sensorData.length - 1];

  return (
    <div style={{ padding: '1rem 0', display: 'flex', flexDirection: 'column', gap: '1.2rem' }}>
      {/* Toggle */}
      <div style={{ display: 'flex', gap: '0.4rem', background: 'rgba(255,255,255,0.04)', padding: '0.25rem', borderRadius: '12px', width: 'fit-content', border: '1px solid rgba(255,255,255,0.08)' }}>
        {[
          { id: 'charts', icon: BarChart3, label: 'Graphiques' },
          { id: 'map',    icon: Map,       label: 'Carte NDVI' },
        ].map(({ id, icon: Icon, label }) => (
          <button key={id} onClick={() => setViewMode(id)} style={{
            display: 'flex', alignItems: 'center', gap: '0.4rem',
            padding: '0.38rem 0.9rem', borderRadius: '9px', border: 'none', cursor: 'pointer',
            fontSize: '0.72rem', fontWeight: viewMode === id ? '700' : '500',
            background: viewMode === id ? (id === 'map' ? 'rgba(255,152,0,0.15)' : 'rgba(139,195,74,0.15)') : 'transparent',
            color: viewMode === id ? (id === 'map' ? '#FF9800' : '#8BC34A') : 'var(--text-muted)',
            borderRight: viewMode === id ? `1px solid ${id === 'map' ? 'rgba(255,152,0,0.3)' : 'rgba(139,195,74,0.3)'}` : 'none',
            transition: 'all 0.18s',
          }}>
            <Icon size={13} /> {label}
          </button>
        ))}
      </div>

      {viewMode === 'map' ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
          {/* Map */}
          <div style={{ height: '320px', borderRadius: '14px', overflow: 'hidden', border: '1px solid rgba(255,255,255,0.1)', position: 'relative' }}>
            <FieldMap initialPolygon={polygon} showNdvi={showNdviOverlay} diagnosticData={diagnosticData} />
            <div style={{ position: 'absolute', top: '0.65rem', left: '50%', transform: 'translateX(-50%)', zIndex: 1000 }}>
              <button
                onClick={() => setShowNdviOverlay(!showNdviOverlay)}
                style={{
                  display: 'flex', alignItems: 'center', gap: '0.45rem',
                  padding: '0.45rem 1rem', borderRadius: '99px', border: 'none', cursor: 'pointer',
                  fontSize: '0.72rem', fontWeight: '700',
                  background: showNdviOverlay ? 'rgba(139,195,74,0.9)' : 'rgba(0,0,0,0.65)',
                  color: showNdviOverlay ? '#0a1a00' : '#fff',
                  backdropFilter: 'blur(8px)',
                  boxShadow: '0 2px 12px rgba(0,0,0,0.3)',
                  transition: 'all 0.2s',
                }}
              >
                <Satellite size={13} />
                {showNdviOverlay ? 'Désactiver Overlay NDVI' : 'Activer Overlay NDVI'}
              </button>
            </div>
          </div>
          {/* Diagnostic panel */}
          <SatDiagPanel diag={diagnosticData} loading={diagLoading} />
        </div>
      ) : (
        <>
          {/* Sensor stat tiles */}
          {lastSensor && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: '0.7rem' }}>
              {[
                { icon: Droplets,    label: 'Humidité Sol',  value: `${lastSensor.humidity ?? '--'}%`,    color: '#4EADD5' },
                { icon: Thermometer, label: 'Température',   value: `${lastSensor.temp ?? '--'}°C`,       color: '#C75B39' },
                { icon: Waves,       label: 'Hum. Air',      value: `${lastSensor.airHumidity ?? '--'}%`, color: '#8BC34A' },
                {
                  icon: Satellite, label: 'NDVI',
                  value: liveNdvi != null ? liveNdvi.toFixed(3) : (ndviData[ndviData.length - 1]?.ndvi?.toFixed(3) ?? '--'),
                  color: ndviColor(liveNdvi ?? ndviData[ndviData.length - 1]?.ndvi),
                  badge: liveNdvi != null ? '🛰' : null,
                },
              ].map((s, i) => (
                <div key={i} style={{
                  padding: '0.8rem 0.9rem', borderRadius: '12px',
                  background: `${s.color}0d`, border: `1px solid ${s.color}22`,
                  display: 'flex', flexDirection: 'column', gap: '0.25rem',
                }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <s.icon size={14} color={s.color} />
                    {s.badge && <span style={{ fontSize: '0.65rem' }}>{s.badge}</span>}
                  </div>
                  <div style={{ fontSize: '0.58rem', color: 'rgba(255,255,255,0.4)', textTransform: 'uppercase', letterSpacing: '1px' }}>{s.label}</div>
                  <div style={{ fontSize: '1.15rem', fontWeight: '800', color: '#fff', fontFamily: "'Newsreader', serif", lineHeight: 1 }}>{s.value}</div>
                </div>
              ))}
            </div>
          )}

          {/* Charts */}
          {sensorData.length > 0 && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '0.9rem' }}>
              {[
                { id: 'humidity', label: '💧 Humidité Sol (7j)', key: 'humidity', color: '#4EADD5' },
                { id: 'temp',     label: '🌡️ Température (7j)',  key: 'temp',     color: '#C75B39' },
              ].map(({ id, label, key, color }) => (
                <div key={id} style={{ background: 'rgba(255,255,255,0.03)', borderRadius: '12px', padding: '1rem', border: '1px solid rgba(255,255,255,0.07)' }}>
                  <div style={{ fontSize: '0.75rem', fontWeight: '600', color: 'var(--text-light)', marginBottom: '0.7rem' }}>{label}</div>
                  <ResponsiveContainer width="100%" height={150}>
                    <AreaChart data={sensorData}>
                      <defs>
                        <linearGradient id={`${id}-${field.id}`} x1="0" y1="0" x2="0" y2="1">
                          <stop offset="5%" stopColor={color} stopOpacity={0.25} />
                          <stop offset="95%" stopColor={color} stopOpacity={0} />
                        </linearGradient>
                      </defs>
                      <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.04)" vertical={false} />
                      <XAxis dataKey="time" tick={{ fill: 'rgba(255,255,255,0.35)', fontSize: 9 }} axisLine={false} tickLine={false} interval="preserveStartEnd" />
                      <YAxis tick={{ fill: 'rgba(255,255,255,0.35)', fontSize: 9 }} axisLine={false} tickLine={false} />
                      <Tooltip content={<ChartTooltip />} />
                      <Area type="monotone" dataKey={key} stroke={color} fill={`url(#${id}-${field.id})`} strokeWidth={2} dot={false} />
                    </AreaChart>
                  </ResponsiveContainer>
                </div>
              ))}
            </div>
          )}

          {/* NDVI chart */}
          {ndviData.length > 0 && (
            <div style={{ background: 'rgba(255,255,255,0.03)', borderRadius: '12px', padding: '1rem', border: '1px solid rgba(107,142,35,0.18)' }}>
              <div style={{ fontSize: '0.75rem', fontWeight: '600', color: 'var(--text-light)', marginBottom: '0.7rem', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                <Satellite size={13} color="#8BC34A" /> Indice NDVI — Santé végétale (8 semaines)
              </div>
              <ResponsiveContainer width="100%" height={140}>
                <AreaChart data={ndviData}>
                  <defs>
                    <linearGradient id={`ndvi-${field.id}`} x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%"  stopColor="#6B8E23" stopOpacity={0.35} />
                      <stop offset="95%" stopColor="#6B8E23" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.04)" vertical={false} />
                  <XAxis dataKey="date" tick={{ fill: 'rgba(255,255,255,0.35)', fontSize: 9 }} axisLine={false} tickLine={false} />
                  <YAxis domain={[0, 1]} tick={{ fill: 'rgba(255,255,255,0.35)', fontSize: 9 }} axisLine={false} tickLine={false} />
                  <Tooltip content={<ChartTooltip />} />
                  <Area type="monotone" dataKey="ndvi" name="NDVI" stroke="#8BC34A" fill={`url(#ndvi-${field.id})`} strokeWidth={2} dot={{ r: 3, fill: '#8BC34A', strokeWidth: 0 }} />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}

          {/* Irrigation logs */}
          {irrigationLogs.length > 0 && (
            <div>
              <div style={{ fontSize: '0.72rem', fontWeight: '600', color: 'var(--text-light)', marginBottom: '0.6rem', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                <Droplets size={13} color="#4EADD5" /> Logs d'Irrigation ({irrigationLogs.length})
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
                {irrigationLogs.slice(0, 5).map((log, i) => (
                  <div key={log.id || i} style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0.55rem 0.9rem', borderRadius: '10px', background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.07)', fontSize: '0.76rem' }}>
                    <Clock size={12} color="#4EADD5" />
                    <span style={{ color: 'var(--text-dim)', minWidth: '50px' }}>{new Date(log.created_at).toLocaleDateString('fr-TN', { day: '2-digit', month: 'short' })}</span>
                    <span style={{ color: 'var(--text-light)', fontWeight: '600' }}>{log.recommended_minutes} min</span>
                    {log.executed_minutes != null && <span style={{ color: '#8BC34A' }}>→ {log.executed_minutes} min</span>}
                    <span style={{ marginLeft: 'auto', color: '#4EADD5', fontWeight: '700' }}>{log.water_estimate_m3} m³</span>
                    <span style={{ padding: '0.12rem 0.45rem', borderRadius: '6px', fontSize: '0.6rem', fontWeight: '700', background: log.status === 'done' ? 'rgba(139,195,74,0.15)' : 'rgba(212,168,67,0.15)', color: log.status === 'done' ? '#8BC34A' : '#D4A843' }}>
                      {log.status}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {sensorData.length === 0 && ndviData.length === 0 && irrigationLogs.length === 0 && (
            <div style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-dim)', fontSize: '0.8rem' }}>
              Aucune donnée disponible. Connectez des capteurs IoT pour commencer.
            </div>
          )}
        </>
      )}
      <style>{`
        @keyframes bounce { 0%,80%,100%{transform:scale(0.55);opacity:.35} 40%{transform:scale(1);opacity:1} }
        @keyframes spin { from{transform:rotate(0deg)} to{transform:rotate(360deg)} }
      `}</style>
    </div>
  );
};

/* ── Zone Card with expand ── */
const ZoneCard = ({ zone, delay, onEdit, onDelete }) => {
  const [expanded, setExpanded] = useState(false);
  const createdDate = new Date(zone.created_at).toLocaleDateString('fr-TN', { day: 'numeric', month: 'short', year: 'numeric' });

  return (
    <div
      className={`animate-slide-up delay-${delay}`}
      style={{
        background: 'rgba(255,255,255,0.03)',
        border: '1px solid rgba(255,255,255,0.08)',
        borderRadius: '18px',
        overflow: 'hidden',
        backdropFilter: 'blur(16px)',
        boxShadow: '0 4px 28px rgba(0,0,0,0.18)',
        display: 'flex', flexDirection: 'column',
        transition: 'box-shadow 0.2s, transform 0.2s',
      }}
    >
      {/* Card header bar */}
      <div style={{
        padding: '1.1rem 1.2rem 0.9rem',
        background: 'linear-gradient(135deg, rgba(139,195,74,0.08) 0%, transparent 60%)',
        borderBottom: expanded ? '1px solid rgba(255,255,255,0.06)' : 'none',
        position: 'relative',
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          {/* Crop icon + name */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.7rem' }}>
            <div style={{
              width: '40px', height: '40px', borderRadius: '12px', display: 'flex',
              alignItems: 'center', justifyContent: 'center', fontSize: '1.2rem',
              background: 'rgba(139,195,74,0.12)', border: '1px solid rgba(139,195,74,0.2)',
            }}>
              {cropEmojis[zone.crop_type] || cropEmojis.default}
            </div>
            <div>
              <h3 style={{ fontSize: '1rem', fontWeight: '800', color: 'var(--text-bright)', fontFamily: "'Newsreader', serif", margin: 0, letterSpacing: '-0.2px' }}>
                {zone.name}
              </h3>
              <p style={{ fontSize: '0.7rem', color: 'rgba(255,255,255,0.4)', margin: 0, marginTop: '2px' }}>
                Créé le {createdDate}
              </p>
            </div>
          </div>
          {/* Actions */}
          <div style={{ display: 'flex', gap: '0.3rem' }}>
            <button onClick={() => onEdit(zone)} style={{ width: '30px', height: '30px', display: 'flex', alignItems: 'center', justifyContent: 'center', borderRadius: '8px', background: 'rgba(78,173,213,0.1)', border: '1px solid rgba(78,173,213,0.2)', cursor: 'pointer', transition: 'background 0.15s' }}>
              <Edit size={13} color="#4EADD5" />
            </button>
            <button onClick={() => { if(window.confirm("Supprimer cette parcelle ?")) onDelete(zone.id); }} style={{ width: '30px', height: '30px', display: 'flex', alignItems: 'center', justifyContent: 'center', borderRadius: '8px', background: 'rgba(199,91,57,0.1)', border: '1px solid rgba(199,91,57,0.2)', cursor: 'pointer', transition: 'background 0.15s' }}>
              <Trash2 size={13} color="#C75B39" />
            </button>
          </div>
        </div>

        {/* Badges row */}
        <div style={{ display: 'flex', gap: '0.4rem', marginTop: '0.75rem', flexWrap: 'wrap' }}>
          <span style={{ padding: '0.2rem 0.65rem', borderRadius: '99px', fontSize: '0.65rem', fontWeight: '700', background: 'rgba(139,195,74,0.14)', color: '#8BC34A', border: '1px solid rgba(139,195,74,0.25)' }}>
            {cropEmojis[zone.crop_type] || '🌱'} {zone.crop_type?.toUpperCase()}
          </span>
          <span style={{ padding: '0.2rem 0.65rem', borderRadius: '99px', fontSize: '0.65rem', fontWeight: '700', background: 'rgba(212,168,67,0.12)', color: '#D4A843', border: '1px solid rgba(212,168,67,0.22)' }}>
            📐 {zone.area_ha} ha
          </span>
        </div>
      </div>

      {/* Expand toggle */}
      <button
        onClick={() => setExpanded(!expanded)}
        style={{
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.45rem',
          padding: '0.6rem', border: 'none', cursor: 'pointer',
          background: expanded ? 'rgba(139,195,74,0.08)' : 'transparent',
          color: expanded ? '#8BC34A' : 'rgba(255,255,255,0.4)',
          fontSize: '0.72rem', fontWeight: '600',
          transition: 'background 0.2s, color 0.2s',
          borderTop: expanded ? '1px solid rgba(255,255,255,0.05)' : 'none',
        }}
      >
        {expanded ? <><ChevronDown size={13} /> Masquer les détails</> : <><ChevronRight size={13} /> Voir les données en détail</>}
      </button>

      {/* Expanded content */}
      {expanded && (
        <div style={{ padding: '0 1.2rem 1.2rem', borderTop: '1px solid rgba(255,255,255,0.05)' }}>
          <FieldDetail field={zone} />
        </div>
      )}
    </div>
  );
};

/* ═══════════════════════════════════════
   MAIN FIELDS DASHBOARD 
   ═══════════════════════════════════════ */

const calculateAreaHa = (latLngs) => {
  if (!latLngs || latLngs.length < 3) return 0;
  
  // High-precision spherical area formula (using 6378137m WGS84 radius)
  const radius = 6378137;
  let area = 0;
  
  // Convert to radians and calc
  const coords = latLngs.map(p => ({
    lat: p[0] * Math.PI / 180,
    lng: p[1] * Math.PI / 180
  }));

  for (let i = 0; i < coords.length; i++) {
    const p1 = coords[i];
    const p2 = coords[(i + 1) % coords.length];
    area += (p2.lng - p1.lng) * (2 + Math.sin(p1.lat) + Math.sin(p2.lat));
  }
  
  const finalArea = Math.abs(area * radius * radius / 2.0);
  return (finalArea / 10000).toFixed(2); // m2 to ha
};

const FieldsDashboard = () => {
  const [view, setView] = useState('grid'); // 'grid' or 'list'
  const isDark = true; 
  const [zones, setZones] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingFieldId, setEditingFieldId] = useState(null);
  const [newField, setNewField] = useState({ name: '', crop_type: 'Tomato', area_ha: '', polygon: [] });
  const [searchTerm, setSearchTerm] = useState('');
  const [filterCrop, setFilterCrop] = useState('all');

  const fetchFields = () => {
    setLoading(true);
    fieldService.getFields()
      .then(res => { setZones(res.data); setLoading(false); })
      .catch(err => { console.error("Error fetching fields:", err); setLoading(false); });
  };

  useEffect(() => { fetchFields(); }, []);

  const openCreateModal = () => {
    setEditingFieldId(null);
    setNewField({ name: '', crop_type: 'Tomato', area_ha: '', polygon: [] });
    setIsModalOpen(true);
  };

  const openEditModal = (zone) => {
    setEditingFieldId(zone.id);
    let poly = [];
    try { poly = JSON.parse(zone.polygon_geojson || '[]'); } catch { poly = []; }
    setNewField({ name: zone.name, crop_type: zone.crop_type, area_ha: zone.area_ha?.toString() || '', polygon: poly });
    setIsModalOpen(true);
  };

  const handleDeleteField = (id) => {
    fieldService.deleteField(id).then(() => fetchFields()).catch(err => alert("Erreur: " + err.message));
  };

  const handleSaveField = (e) => {
    e.preventDefault();
    const payload = {
      name: newField.name,
      crop_type: newField.crop_type,
      area_ha: parseFloat(newField.area_ha),
      polygon_geojson: JSON.stringify(newField.polygon)
    };
    
    if (editingFieldId) {
      fieldService.updateField(editingFieldId, payload)
        .then(() => { setIsModalOpen(false); fetchFields(); })
        .catch(err => alert("Erreur: " + err.message));
    } else {
      payload.farm_id = '88888888-4444-4444-4444-121212121212';
      fieldService.createField(payload)
        .then(() => { setIsModalOpen(false); fetchFields(); })
        .catch(err => alert("Erreur: " + err.message));
    }
  };

  // Filtering
  const cropTypes = [...new Set(zones.map(z => z.crop_type))];
  const filtered = zones.filter(z => {
    const matchSearch = z.name.toLowerCase().includes(searchTerm.toLowerCase()) || z.crop_type.toLowerCase().includes(searchTerm.toLowerCase());
    const matchCrop = filterCrop === 'all' || z.crop_type === filterCrop;
    return matchSearch && matchCrop;
  });
  const totalArea = zones.reduce((s, z) => s + (z.area_ha || 0), 0);

  const inputStyle = {
    background: 'rgba(255,255,255,0.04)', border: '1px solid var(--glass-border)',
    padding: '0.8rem 1rem', borderRadius: 'var(--radius-md)', color: 'var(--text-light)',
    fontSize: '0.9rem', width: '100%', outline: 'none', transition: 'border-color 0.3s, box-shadow 0.3s', fontFamily: "'Manrope', sans-serif",
  };

  if (loading && zones.length === 0) {
    return (
      <div style={{ padding: '4rem', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '1rem' }}>
        <div className="floating"><Leaf size={40} color="var(--primary)" /></div>
        <p style={{ color: 'var(--text-muted)' }}>Chargement des parcelles...</p>
      </div>
    );
  }

  return (
    <div style={{ padding: '2rem 0' }}>
      {/* Header */}
      <ScrollReveal>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.3rem' }}>
            <span style={{ fontSize: '1.2rem' }}>🌾</span>
            <span style={{ fontSize: '0.7rem', color: 'var(--olive)', textTransform: 'uppercase', letterSpacing: '2px', fontWeight: '600' }}>Gestion foncière</span>
          </div>
          <h2 style={{ fontFamily: "'Newsreader', serif", fontSize: '1.8rem', fontWeight: '700', color: 'var(--text-bright)' }}>
            Parcelles & <span className="gradient-text-warm">Exploitations</span>
          </h2>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem', marginTop: '0.2rem' }}>
            {zones.length} parcelle{zones.length !== 1 ? 's' : ''} • {totalArea.toFixed(1)} ha total • {cropTypes.length} culture{cropTypes.length !== 1 ? 's' : ''} différente{cropTypes.length !== 1 ? 's' : ''}
          </p>
        </div>
        <button className="btn btn-warm" onClick={openCreateModal}>
          <Plus size={16} /> Nouvelle Parcelle
        </button>
      </div>
      </ScrollReveal>

      {/* Filters */}
      <ScrollReveal delay={0.05}>
      <div style={{ display: 'flex', gap: '0.8rem', marginBottom: '2rem', flexWrap: 'wrap', alignItems: 'center' }}>
        <input
          type="text" placeholder="🔍 Rechercher une parcelle..."
          value={searchTerm} onChange={e => setSearchTerm(e.target.value)}
          style={{ ...inputStyle, maxWidth: '300px', padding: '0.6rem 1rem', fontSize: '0.85rem' }}
        />
        <div style={{ display: 'flex', gap: '0.3rem', background: 'var(--glass)', padding: '0.25rem', borderRadius: 'var(--radius-full)', border: '1px solid var(--glass-border)' }}>
          <button
            onClick={() => setFilterCrop('all')}
            className={filterCrop === 'all' ? 'btn btn-primary' : 'btn'}
            style={{ padding: '0.35rem 0.8rem', fontSize: '0.72rem', borderRadius: 'var(--radius-full)', background: filterCrop === 'all' ? undefined : 'transparent', color: filterCrop === 'all' ? undefined : 'var(--text-muted)' }}
          >
            Tous
          </button>
          {cropTypes.map(ct => (
            <button key={ct}
              onClick={() => setFilterCrop(ct)}
              className={filterCrop === ct ? 'btn btn-primary' : 'btn'}
              style={{ padding: '0.35rem 0.8rem', fontSize: '0.72rem', borderRadius: 'var(--radius-full)', background: filterCrop === ct ? undefined : 'transparent', color: filterCrop === ct ? undefined : 'var(--text-muted)' }}
            >
              {cropEmojis[ct] || '🌱'} {ct}
            </button>
          ))}
        </div>
      </div>
      </ScrollReveal>

      {/* Modal */}
      {isModalOpen && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.82)', backdropFilter: 'blur(10px)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem' }}>
          <div className="animate-scale-in" style={{ width: '920px', maxWidth: '95vw', background: '#0f1a0f', borderRadius: '20px', border: '1px solid rgba(255,255,255,0.1)', overflow: 'hidden', boxShadow: '0 32px 80px rgba(0,0,0,0.6)' }}>

            {/* Modal header */}
            <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid rgba(255,255,255,0.07)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: 'linear-gradient(90deg, rgba(139,195,74,0.08) 0%, transparent 60%)' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.65rem' }}>
                <div style={{ width: '34px', height: '34px', borderRadius: '10px', background: 'rgba(139,195,74,0.15)', border: '1px solid rgba(139,195,74,0.25)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1.1rem' }}>🌿</div>
                <div>
                  <div style={{ fontSize: '1rem', fontWeight: '800', color: 'var(--text-bright)', fontFamily: "'Newsreader', serif" }}>
                    {editingFieldId ? 'Modifier la Parcelle' : 'Nouvelle Parcelle'}
                  </div>
                  <div style={{ fontSize: '0.65rem', color: 'rgba(255,255,255,0.35)', marginTop: '1px' }}>
                    {editingFieldId ? 'Mise à jour des informations et du polygone' : 'Délimitez la parcelle sur la carte pour créer'}
                  </div>
                </div>
              </div>
              <button onClick={() => setIsModalOpen(false)} style={{ width: '32px', height: '32px', display: 'flex', alignItems: 'center', justifyContent: 'center', borderRadius: '8px', background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer', color: 'rgba(255,255,255,0.5)' }}>
                <X size={15} />
              </button>
            </div>

            <form onSubmit={handleSaveField} style={{ display: 'grid', gridTemplateColumns: '300px 1fr', gap: 0 }}>
              {/* Left panel */}
              <div style={{ padding: '1.4rem', display: 'flex', flexDirection: 'column', gap: '1.1rem', borderRight: '1px solid rgba(255,255,255,0.07)', background: 'rgba(0,0,0,0.2)' }}>

                {/* Field name */}
                <div>
                  <label style={{ fontSize: '0.62rem', color: 'rgba(255,255,255,0.4)', textTransform: 'uppercase', letterSpacing: '1.5px', fontWeight: '700', display: 'block', marginBottom: '0.45rem' }}>
                    Nom de la parcelle
                  </label>
                  <input
                    type="text" required
                    placeholder="ex: Oliveraie Nord"
                    value={newField.name}
                    onChange={e => setNewField({...newField, name: e.target.value})}
                    style={{
                      width: '100%', padding: '0.7rem 0.9rem', borderRadius: '10px',
                      background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.1)',
                      color: 'var(--text-bright)', fontSize: '0.88rem', outline: 'none',
                      fontFamily: "'Manrope', sans-serif", boxSizing: 'border-box',
                      transition: 'border-color 0.2s',
                    }}
                  />
                </div>

                {/* Crop picker */}
                <div>
                  <label style={{ fontSize: '0.62rem', color: 'rgba(255,255,255,0.4)', textTransform: 'uppercase', letterSpacing: '1.5px', fontWeight: '700', display: 'block', marginBottom: '0.45rem' }}>
                    Type de culture
                  </label>
                  <CropPicker value={newField.crop_type} onChange={v => setNewField({...newField, crop_type: v})} />
                </div>

                {/* Area */}
                <div>
                  <label style={{ fontSize: '0.62rem', color: 'rgba(255,255,255,0.4)', textTransform: 'uppercase', letterSpacing: '1.5px', fontWeight: '700', display: 'block', marginBottom: '0.45rem' }}>
                    Surface (hectares) <span style={{ color: 'rgba(139,195,74,0.6)', fontStyle: 'italic', textTransform: 'none', letterSpacing: 0 }}>— auto depuis la carte</span>
                  </label>
                  <input
                    type="number" step="0.01" required
                    placeholder="ex: 5.20"
                    value={newField.area_ha}
                    onChange={e => setNewField({...newField, area_ha: e.target.value})}
                    style={{
                      width: '100%', padding: '0.7rem 0.9rem', borderRadius: '10px',
                      background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.1)',
                      color: 'var(--text-bright)', fontSize: '0.88rem', outline: 'none',
                      fontFamily: "'Manrope', sans-serif", boxSizing: 'border-box',
                    }}
                  />
                </div>

                {/* Polygon hint */}
                <div style={{ padding: '0.7rem 0.9rem', borderRadius: '10px', background: newField.polygon.length >= 3 ? 'rgba(139,195,74,0.08)' : 'rgba(255,152,0,0.07)', border: `1px solid ${newField.polygon.length >= 3 ? 'rgba(139,195,74,0.2)' : 'rgba(255,152,0,0.2)'}`, display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                  <span style={{ fontSize: '1rem' }}>{newField.polygon.length >= 3 ? '✅' : '📍'}</span>
                  <span style={{ fontSize: '0.68rem', color: newField.polygon.length >= 3 ? '#8BC34A' : '#FF9800', lineHeight: 1.4 }}>
                    {newField.polygon.length >= 3
                      ? `${newField.polygon.length} points tracés — parcelle valide`
                      : 'Cliquez sur la carte pour tracer le périmètre (min. 3 points)'}
                  </span>
                </div>

                {/* Actions */}
                <div style={{ display: 'flex', gap: '0.6rem', marginTop: 'auto' }}>
                  <button type="button" onClick={() => setIsModalOpen(false)} style={{ flex: 1, padding: '0.7rem', borderRadius: '99px', background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.12)', color: 'rgba(255,255,255,0.6)', fontSize: '0.8rem', fontWeight: '600', cursor: 'pointer' }}>
                    Annuler
                  </button>
                  <button type="submit" disabled={newField.polygon.length < 3} style={{ flex: 1, padding: '0.7rem', borderRadius: '99px', background: newField.polygon.length < 3 ? 'rgba(139,195,74,0.1)' : 'linear-gradient(135deg,#C75B39,#D4A843)', border: 'none', color: newField.polygon.length < 3 ? 'rgba(139,195,74,0.4)' : '#fff', fontSize: '0.8rem', fontWeight: '700', cursor: newField.polygon.length < 3 ? 'not-allowed' : 'pointer', transition: 'opacity 0.2s' }}>
                    {editingFieldId ? 'Enregistrer' : 'Créer'}
                  </button>
                </div>
              </div>

              {/* Map panel */}
              <div style={{ height: '480px', position: 'relative' }}>
                <FieldMap
                  isEditable={true}
                  initialPolygon={newField.polygon}
                  onPolygonChange={(poly) => {
                    const exactArea = calculateAreaHa(poly);
                    setNewField({...newField, polygon: poly, area_ha: exactArea > 0 ? exactArea : newField.area_ha});
                  }}
                />
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Zone Cards */}
      <ScrollReveal delay={0.08}>
      <div className="grid-cols-3">
        {filtered.map((z, i) => <ZoneCard key={z.id} zone={z} delay={Math.min(i + 1, 6)} onEdit={openEditModal} onDelete={handleDeleteField} />)}
      </div>
      </ScrollReveal>

      {filtered.length === 0 && zones.length > 0 && (
        <div style={{ textAlign: 'center', padding: '3rem', color: 'var(--text-muted)' }}>
          <p>Aucune parcelle ne correspond à "{searchTerm || filterCrop}"</p>
        </div>
      )}

      {zones.length === 0 && !loading && (
        <div style={{ textAlign: 'center', padding: '4rem 2rem', color: 'var(--text-muted)' }}>
          <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>🌾</div>
          <h3 style={{ fontFamily: "'Newsreader', serif", marginBottom: '0.5rem', color: 'var(--text-light)' }}>Aucune parcelle</h3>
          <p style={{ fontSize: '0.9rem', marginBottom: '1.5rem' }}>Commencez par ajouter votre première zone d'exploitation</p>
          <button className="btn btn-warm" onClick={openCreateModal}><Plus size={16} /> Créer ma première parcelle</button>
        </div>
      )}
    </div>
  );
};

export default FieldsDashboard;
