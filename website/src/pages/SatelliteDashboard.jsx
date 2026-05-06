import React, { useState, useEffect } from 'react';
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
} from 'recharts';
import {
  Satellite, Leaf, RefreshCw, ChevronRight, AlertTriangle, CheckCircle,
  Clock, Map, Activity, Sparkles, Droplets, FlaskConical, Zap, TrendingUp,
  CloudSun, BookOpen,
} from 'lucide-react';
import { fieldService, ndviService, vraService } from '../services/api';
import FieldMap from '../components/FieldMap';
import ScrollReveal from '../components/ScrollReveal';

/* ── colour helpers ── */
const ndviColor = (v) => {
  if (v == null) return '#888';
  if (v >= 0.6) return '#4CAF50';
  if (v >= 0.4) return '#8BC34A';
  if (v >= 0.2) return '#FFC107';
  return '#F44336';
};
const ndviLabel = (v) => {
  if (v == null) return 'N/A';
  if (v >= 0.6) return 'Excellent';
  if (v >= 0.4) return 'Bon';
  if (v >= 0.2) return 'Modéré';
  return 'Faible';
};
const zoneColor  = { A: '#F44336', B: '#FF9800', C: '#4CAF50' };
const zoneIcon   = { A: '🔴', B: '🟠', C: '🟢' };
const stageStatusColor = { completed: '#6B8E23', current: '#8BC34A', upcoming: '#444' };

/* ── shared primitives ── */
const SCard = ({ children, style = {} }) => (
  <div style={{
    background: 'rgba(255,255,255,0.03)',
    border: '1px solid rgba(255,255,255,0.08)',
    borderRadius: '18px',
    overflow: 'hidden',
    backdropFilter: 'blur(16px)',
    boxShadow: '0 4px 32px rgba(0,0,0,0.18)',
    ...style,
  }}>
    {children}
  </div>
);

const SCardHeader = ({ icon: Icon, title, color = '#8BC34A', badge, right }) => (
  <div style={{
    display: 'flex', alignItems: 'center', justifyContent: 'space-between',
    padding: '1.1rem 1.4rem',
    borderBottom: '1px solid rgba(255,255,255,0.06)',
    background: `linear-gradient(90deg, ${color}0d 0%, transparent 60%)`,
  }}>
    <div style={{ display: 'flex', alignItems: 'center', gap: '0.65rem' }}>
      <div style={{
        width: '32px', height: '32px', borderRadius: '9px', display: 'flex',
        alignItems: 'center', justifyContent: 'center',
        background: `linear-gradient(135deg, ${color}30, ${color}12)`,
        border: `1px solid ${color}35`,
      }}>
        <Icon size={16} color={color} />
      </div>
      <span style={{ fontWeight: '700', fontSize: '0.9rem', color: 'var(--text-bright)', letterSpacing: '0.01em' }}>
        {title}
      </span>
      {badge}
    </div>
    {right}
  </div>
);

const SPill = ({ label, color, size = 'sm' }) => (
  <span style={{
    padding: size === 'lg' ? '0.3rem 0.9rem' : '0.18rem 0.65rem',
    borderRadius: '99px',
    fontSize: size === 'lg' ? '0.76rem' : '0.67rem',
    fontWeight: '700',
    background: `${color}1e`,
    color,
    border: `1px solid ${color}35`,
    whiteSpace: 'nowrap',
  }}>
    {label}
  </span>
);

const Divider = () => (
  <div style={{ height: '1px', background: 'rgba(255,255,255,0.05)', margin: '1rem 0' }} />
);

/* ── RAG text renderer: converts plain text / markdown-like to JSX ── */
const RagText = ({ text }) => {
  if (!text) return null;
  const lines = text.split('\n');
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem' }}>
      {lines.map((line, i) => {
        const trimmed = line.trim();
        if (!trimmed) return <div key={i} style={{ height: '0.4rem' }} />;
        // Bold heading: **text** or starts with # or ##
        if (/^#{1,3} /.test(trimmed)) {
          return (
            <div key={i} style={{
              fontSize: '0.86rem', fontWeight: '700', color: '#C5E1A5',
              marginTop: '0.6rem', letterSpacing: '0.02em',
            }}>
              {trimmed.replace(/^#{1,3} /, '')}
            </div>
          );
        }
        // Bullet: -, *, •
        if (/^[-*•] /.test(trimmed)) {
          return (
            <div key={i} style={{ display: 'flex', gap: '0.6rem', alignItems: 'flex-start' }}>
              <span style={{ color: '#8BC34A', fontSize: '0.8rem', marginTop: '2px', flexShrink: 0 }}>▸</span>
              <span style={{ fontSize: '0.82rem', color: 'var(--text-light)', lineHeight: 1.6 }}>
                {renderInline(trimmed.replace(/^[-*•] /, ''))}
              </span>
            </div>
          );
        }
        // Numbered list
        if (/^\d+\. /.test(trimmed)) {
          const num = trimmed.match(/^(\d+)\./)[1];
          return (
            <div key={i} style={{ display: 'flex', gap: '0.6rem', alignItems: 'flex-start' }}>
              <span style={{
                minWidth: '20px', height: '20px', borderRadius: '50%',
                background: 'rgba(139,195,74,0.2)', color: '#8BC34A',
                fontSize: '0.65rem', fontWeight: '700',
                display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
              }}>{num}</span>
              <span style={{ fontSize: '0.82rem', color: 'var(--text-light)', lineHeight: 1.6 }}>
                {renderInline(trimmed.replace(/^\d+\. /, ''))}
              </span>
            </div>
          );
        }
        // Normal paragraph
        return (
          <p key={i} style={{ fontSize: '0.82rem', color: 'var(--text-light)', lineHeight: 1.65, margin: 0 }}>
            {renderInline(trimmed)}
          </p>
        );
      })}
    </div>
  );
};

/* inline bold/italic */
function renderInline(text) {
  const parts = text.split(/(\*\*[^*]+\*\*|\*[^*]+\*)/g);
  return parts.map((part, i) => {
    if (/^\*\*[^*]+\*\*$/.test(part))
      return <strong key={i} style={{ color: '#D4E6A5', fontWeight: '700' }}>{part.slice(2, -2)}</strong>;
    if (/^\*[^*]+\*$/.test(part))
      return <em key={i} style={{ color: '#C8E6C9', fontStyle: 'italic' }}>{part.slice(1, -1)}</em>;
    return part;
  });
}

/* ══════════════════════════════════════════════════════
   NDVI CARD
══════════════════════════════════════════════════════ */
const NdviCard = ({ fieldId, liveVal, liveDate, liveClouds, liveSource }) => {
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!fieldId) return;
    setLoading(true);
    ndviService.getHistory(fieldId, 12)
      .then(r => {
        const raw = r.data;
        const arr = Array.isArray(raw) ? raw : (raw?.history || []);
        setHistory(arr.map(h => ({
          ndvi: h.ndvi ?? h.ndvi_value ?? null,
          label: h.week_label ?? new Date(h.captured_at || h.date).toLocaleDateString('fr', { day: '2-digit', month: 'short' }),
        })));
      })
      .catch(() => setHistory([]))
      .finally(() => setLoading(false));
  }, [fieldId, liveVal]);

  const val   = liveVal ?? history[history.length - 1]?.ndvi ?? null;
  const color = ndviColor(val);
  const pct   = val != null ? Math.max(0, Math.min(100, (val + 0.1) / 1.1 * 100)) : 0;

  return (
    <SCard>
      <SCardHeader
        icon={Leaf} title="Indice NDVI — Santé Végétale" color="#4CAF50"
        badge={val != null ? <SPill label={ndviLabel(val)} color={color} size="lg" /> : null}
      />
      <div style={{ padding: '1.4rem' }}>
        {loading ? (
          <div style={{ textAlign: 'center', color: 'var(--text-dim)', padding: '2rem' }}>Chargement…</div>
        ) : (
          <>
            {/* Value row */}
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: '1.5rem', marginBottom: '1.2rem' }}>
              <div>
                <div style={{ fontSize: '0.6rem', color: 'rgba(255,255,255,0.45)', textTransform: 'uppercase', letterSpacing: '1.5px', marginBottom: '0.1rem' }}>
                  NDVI ACTUEL
                </div>
                <div style={{ fontSize: '3rem', fontWeight: '900', color: '#fff', fontFamily: "'Newsreader', serif", lineHeight: 1, letterSpacing: '-1px' }}>
                  {val != null ? val.toFixed(3) : '—'}
                </div>
                <div style={{ fontSize: '0.62rem', marginTop: '0.3rem', display: 'flex', alignItems: 'center', gap: '5px', color: liveVal != null ? '#66BB6A' : 'var(--text-dim)' }}>
                  {liveVal != null ? (
                    <><RefreshCw size={10} />🛰 Synchronisé Satellite {liveDate}</>
                  ) : (
                    <><Clock size={10} /> Dernière lecture DB</>
                  )}
                </div>
              </div>

              {/* Gauge */}
              <div style={{ flex: 1, paddingBottom: '0.5rem' }}>
                <div style={{ position: 'relative', height: '10px', borderRadius: '99px', background: 'linear-gradient(90deg, #F44336 0%, #FF9800 30%, #FFC107 55%, #8BC34A 75%, #4CAF50 100%)', marginBottom: '0.45rem' }}>
                  <div style={{
                    position: 'absolute', top: '-4px',
                    left: `${pct}%`, transform: 'translateX(-50%)',
                    width: '18px', height: '18px', borderRadius: '50%',
                    background: color, border: '3px solid #fff',
                    boxShadow: `0 0 10px ${color}88`,
                    transition: 'left 0.8s ease',
                  }} />
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.58rem', color: 'rgba(255,255,255,0.4)' }}>
                  <span>−0.1 Sol nu</span><span>0.5 Dense</span><span>1.0 Max</span>
                </div>
                {liveClouds != null && (
                  <div style={{ marginTop: '0.4rem', fontSize: '0.62rem', color: 'rgba(255,255,255,0.4)', display: 'flex', alignItems: 'center', gap: '4px' }}>
                    <CloudSun size={10} /> Nuages : {liveClouds.toFixed(1)}%
                  </div>
                )}
              </div>
            </div>

            {/* Chart */}
            {history.length > 1 ? (
              <div style={{ marginTop: '0.5rem' }}>
                <div style={{ fontSize: '0.62rem', color: 'rgba(255,255,255,0.35)', textTransform: 'uppercase', letterSpacing: '1px', marginBottom: '0.5rem' }}>
                  Historique
                </div>
                <ResponsiveContainer width="100%" height={130}>
                  <AreaChart data={history} margin={{ top: 4, right: 0, bottom: 0, left: -28 }}>
                    <defs>
                      <linearGradient id="ng" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="10%" stopColor="#4CAF50" stopOpacity={0.35} />
                        <stop offset="95%" stopColor="#4CAF50" stopOpacity={0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.04)" vertical={false} />
                    <XAxis dataKey="label" tick={{ fill: 'rgba(255,255,255,0.4)', fontSize: 9 }} axisLine={false} tickLine={false} />
                    <YAxis domain={[-0.1, 1]} tick={{ fill: 'rgba(255,255,255,0.4)', fontSize: 9 }} axisLine={false} tickLine={false} />
                    <Tooltip
                      contentStyle={{ background: '#1a2a1a', border: '1px solid rgba(139,195,74,0.3)', borderRadius: '10px', fontSize: '0.75rem' }}
                      labelStyle={{ color: 'rgba(255,255,255,0.6)' }}
                    />
                    <Area type="monotone" dataKey="ndvi" stroke="#4CAF50" strokeWidth={2} fill="url(#ng)" name="NDVI" dot={{ r: 3, fill: '#4CAF50', strokeWidth: 0 }} />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            ) : (
              <div style={{
                height: '120px', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
                border: '1px dashed rgba(255,255,255,0.1)', borderRadius: '12px', gap: '0.5rem',
              }}>
                <Activity size={22} color="rgba(255,255,255,0.2)" />
                <span style={{ fontSize: '0.73rem', color: 'var(--text-dim)', textAlign: 'center' }}>
                  Historique en cours de constitution<br/>
                  <span style={{ opacity: 0.6 }}>Cliquez Actualiser pour synchroniser</span>
                </span>
              </div>
            )}
          </>
        )}
      </div>
    </SCard>
  );
};

/* ══════════════════════════════════════════════════════
   SOIL HEALTH CARD
══════════════════════════════════════════════════════ */
const SoilHealthCard = ({ data }) => {
  if (!data) return null;
  const score = data.health_score ?? 0;
  const scoreColor = score >= 70 ? '#4CAF50' : score >= 45 ? '#FF9800' : '#F44336';
  const ind = data.indicators || {};
  const stressColor = { Low: '#4CAF50', Moderate: '#FF9800', High: '#F44336' };

  const indItems = [
    { key: 'NDVI',  val: ind.ndvi  ?? ind.NDVI,  color: '#4CAF50' },
    { key: 'SAVI',  val: ind.savi  ?? ind.SAVI,  color: '#8BC34A' },
    { key: 'MSAVI', val: ind.msavi ?? ind.MSAVI, color: '#AED581' },
  ].filter(x => x.val != null);

  return (
    <SCard style={{ marginTop: '1.2rem' }}>
      <SCardHeader
        icon={FlaskConical} title="Santé du Sol" color="#8BC34A"
        badge={<SPill label={data.health_label || 'N/A'} color={scoreColor} size="lg" />}
      />
      <div style={{ padding: '1.4rem' }}>
        <div style={{ display: 'flex', gap: '1.5rem', alignItems: 'center', marginBottom: '1.2rem' }}>
          {/* Score ring */}
          <div style={{ position: 'relative', width: '80px', height: '80px', flexShrink: 0 }}>
            <svg width="80" height="80" style={{ transform: 'rotate(-90deg)' }}>
              <circle cx="40" cy="40" r="33" fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="7" />
              <circle cx="40" cy="40" r="33" fill="none" stroke={scoreColor} strokeWidth="7"
                strokeDasharray={`${(score / 100) * 207} 207`}
                strokeLinecap="round"
                style={{ transition: 'stroke-dasharray 1s ease' }}
              />
            </svg>
            <div style={{
              position: 'absolute', inset: 0, display: 'flex', flexDirection: 'column',
              alignItems: 'center', justifyContent: 'center',
            }}>
              <span style={{ fontSize: '1.3rem', fontWeight: '900', color: scoreColor, lineHeight: 1 }}>{score}</span>
              <span style={{ fontSize: '0.5rem', color: 'var(--text-dim)', letterSpacing: '1px' }}>/100</span>
            </div>
          </div>

          {/* Index pills */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.45rem', flex: 1 }}>
            {indItems.map(({ key, val, color }) => (
              <div key={key} style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                <span style={{ fontSize: '0.65rem', color: 'var(--text-dim)', width: '42px' }}>{key}</span>
                <div style={{ flex: 1, height: '5px', borderRadius: '99px', background: 'rgba(255,255,255,0.06)', overflow: 'hidden' }}>
                  <div style={{ height: '100%', width: `${Math.max(0, Math.min(100, (val + 0.1) / 1.1 * 100))}%`, background: color, borderRadius: '99px', transition: 'width 0.8s ease' }} />
                </div>
                <span style={{ fontSize: '0.72rem', fontWeight: '700', color, width: '40px', textAlign: 'right' }}>{val?.toFixed(3)}</span>
              </div>
            ))}
          </div>
        </div>

        {/* Tags row */}
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '0.9rem' }}>
          {data.moisture_stress && (
            <div style={{
              padding: '0.28rem 0.75rem', borderRadius: '99px', fontSize: '0.7rem', fontWeight: '600',
              background: `${stressColor[data.moisture_stress] || '#888'}18`,
              border: `1px solid ${stressColor[data.moisture_stress] || '#888'}30`,
            }}>
              <span style={{ color: 'var(--text-dim)' }}>Stress hydrique : </span>
              <span style={{ color: stressColor[data.moisture_stress] || '#888' }}>{data.moisture_stress}</span>
            </div>
          )}
          {data.soil_type_classification && (
            <div style={{ padding: '0.28rem 0.75rem', borderRadius: '99px', fontSize: '0.7rem', fontWeight: '600', background: 'rgba(255,152,0,0.12)', border: '1px solid rgba(255,152,0,0.25)' }}>
              <span style={{ color: 'var(--text-dim)' }}>Sol : </span>
              <span style={{ color: '#FF9800' }}>{data.soil_type_classification}</span>
            </div>
          )}
        </div>

        {data.fertility_desc && (
          <div style={{
            padding: '0.65rem 0.9rem', borderRadius: '10px', fontSize: '0.77rem', color: 'var(--text-dim)',
            background: 'rgba(255,255,255,0.03)', border: '1px solid rgba(255,255,255,0.06)',
            fontStyle: 'italic', lineHeight: 1.55, marginBottom: '0.9rem',
          }}>
            {data.fertility_desc}
          </div>
        )}

        {data.recommendations?.length > 0 && (
          <>
            <div style={{ fontSize: '0.6rem', textTransform: 'uppercase', letterSpacing: '1.5px', color: 'rgba(255,255,255,0.35)', marginBottom: '0.6rem' }}>
              Recommandations
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
              {data.recommendations.map((r, i) => (
                <div key={i} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-start', fontSize: '0.78rem', color: 'var(--text-light)', lineHeight: 1.5 }}>
                  <span style={{ color: '#8BC34A', flexShrink: 0, marginTop: '3px' }}>▸</span>
                  {r}
                </div>
              ))}
            </div>
          </>
        )}
      </div>
    </SCard>
  );
};

/* ══════════════════════════════════════════════════════
   VRA CARD
══════════════════════════════════════════════════════ */
const VraCard = ({ data }) => {
  if (!data) return null;
  const zones   = data.zones || [];
  const savings = data.savings_vs_uniform_pct;
  const noData  = data.note && zones.length === 0;

  return (
    <SCard>
      <SCardHeader icon={Map} title="Carte de Prescription VRA" color="#FF9800"
        right={data.avg_ndvi != null ? <SPill label={`NDVI moy. ${data.avg_ndvi?.toFixed(3)}`} color="#FF9800" /> : null}
      />
      <div style={{ padding: '1.4rem' }}>

        {noData ? (
          <div style={{ textAlign: 'center', padding: '2rem 1rem', color: 'var(--text-dim)', fontSize: '0.82rem', lineHeight: 1.6 }}>
            <Map size={32} color="rgba(255,152,0,0.3)" style={{ marginBottom: '0.75rem' }} />
            <p>{data.note}</p>
          </div>
        ) : (
          <>
            {/* Savings banner */}
            {savings != null && (
              <div style={{
                display: 'flex', alignItems: 'center', gap: '1.2rem',
                padding: '0.9rem 1.1rem', borderRadius: '12px', marginBottom: '1.2rem',
                background: 'linear-gradient(135deg, rgba(255,152,0,0.1), rgba(255,193,7,0.06))',
                border: '1px solid rgba(255,152,0,0.25)',
              }}>
                <div>
                  <div style={{ fontSize: '0.58rem', color: 'rgba(255,255,255,0.4)', textTransform: 'uppercase', letterSpacing: '1.5px' }}>Économies intrants</div>
                  <div style={{ fontSize: '2.2rem', fontWeight: '900', color: '#FF9800', fontFamily: "'Newsreader', serif", lineHeight: 1 }}>
                    ~{savings?.toFixed(0)}%
                  </div>
                </div>
                <div style={{ fontSize: '0.75rem', color: 'var(--text-dim)', lineHeight: 1.5, flex: 1 }}>
                  {data.savings_message || 'vs application uniforme sur toute la parcelle'}
                </div>
              </div>
            )}

            {/* Map */}
            {data.spatial_overlay?.length > 0 && (
              <div style={{ height: '200px', borderRadius: '12px', overflow: 'hidden', border: '1px solid rgba(255,152,0,0.2)', marginBottom: '1.2rem' }}>
                <FieldMap showNdvi center={data.spatial_overlay[0].polygon[0]} zoom={15} vraData={data} />
              </div>
            )}

            {/* Zones */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              {zones.map(z => {
                const c    = zoneColor[z.id] || '#888';
                const p    = z.prescription || {};
                const pct  = z.area_pct ?? 0;
                return (
                  <div key={z.id} style={{
                    borderRadius: '12px', overflow: 'hidden',
                    border: `1px solid ${c}30`,
                    background: `${c}09`,
                  }}>
                    {/* Zone header */}
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0.7rem 1rem', borderBottom: `1px solid ${c}18` }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                        <div style={{ width: '10px', height: '10px', borderRadius: '50%', background: c, boxShadow: `0 0 6px ${c}88` }} />
                        <span style={{ fontWeight: '700', fontSize: '0.83rem', color: 'var(--text-bright)' }}>
                          Zone {z.label || z.id}
                        </span>
                      </div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                        {/* Progress bar */}
                        <div style={{ width: '60px', height: '5px', borderRadius: '99px', background: 'rgba(255,255,255,0.08)', overflow: 'hidden' }}>
                          <div style={{ height: '100%', width: `${pct}%`, background: c, borderRadius: '99px' }} />
                        </div>
                        <SPill label={`${pct?.toFixed(0)}% du champ`} color={c} />
                      </div>
                    </div>
                    {/* Interpretation */}
                    {z.interpretation && (
                      <div style={{ padding: '0.4rem 1rem', fontSize: '0.72rem', color: 'var(--text-dim)', fontStyle: 'italic', borderBottom: `1px solid ${c}10` }}>
                        {z.interpretation}
                      </div>
                    )}
                    {/* Prescription pills */}
                    <div style={{ padding: '0.6rem 1rem', display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
                      {p.N_kg    != null && <PrescPill icon="N" val={`${p.N_kg} kg`}    color="#66BB6A" />}
                      {p.P_kg    != null && <PrescPill icon="P" val={`${p.P_kg} kg`}    color="#29B6F6" />}
                      {p.K_kg    != null && <PrescPill icon="K" val={`${p.K_kg} kg`}    color="#FFA726" />}
                      {p.water_m3 != null && <PrescPill icon="💧" val={`${p.water_m3} m³`} color="#4FC3F7" />}
                      <PrescPill icon="Taux" val={`${z.application_rate_pct}%`} color={c} />
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Totals */}
            {data.total_prescription && (
              <div style={{
                marginTop: '1rem', padding: '0.7rem 1rem', borderRadius: '10px',
                background: 'rgba(255,152,0,0.06)', border: '1px solid rgba(255,152,0,0.15)',
                display: 'flex', flexWrap: 'wrap', gap: '1rem', alignItems: 'center',
              }}>
                <span style={{ fontSize: '0.65rem', color: 'rgba(255,255,255,0.35)', textTransform: 'uppercase', letterSpacing: '1px' }}>Total parcelle</span>
                {[
                  { k: 'N', v: data.total_prescription.N_kg,    u: 'kg',  c: '#66BB6A' },
                  { k: 'P', v: data.total_prescription.P_kg,    u: 'kg',  c: '#29B6F6' },
                  { k: 'K', v: data.total_prescription.K_kg,    u: 'kg',  c: '#FFA726' },
                  { k: '💧', v: data.total_prescription.water_m3, u: 'm³', c: '#4FC3F7' },
                ].map(({ k, v, u, c }) => v != null && (
                  <span key={k} style={{ fontSize: '0.78rem' }}>
                    <span style={{ color: 'rgba(255,255,255,0.4)' }}>{k} </span>
                    <b style={{ color: c }}>{v} {u}</b>
                  </span>
                ))}
              </div>
            )}
          </>
        )}
      </div>
    </SCard>
  );
};

const PrescPill = ({ icon, val, color }) => (
  <div style={{
    display: 'flex', alignItems: 'center', gap: '0.3rem',
    padding: '0.22rem 0.65rem', borderRadius: '99px',
    background: `${color}18`, border: `1px solid ${color}30`,
    fontSize: '0.7rem',
  }}>
    <span style={{ color: 'rgba(255,255,255,0.5)', fontSize: '0.62rem' }}>{icon}</span>
    <span style={{ color, fontWeight: '700' }}>{val}</span>
  </div>
);

/* ══════════════════════════════════════════════════════
   CROP CALENDAR CARD
══════════════════════════════════════════════════════ */
const CropCalendarCard = ({ data, liveNdvi }) => {
  if (!data) return null;
  const timeline     = data.timeline || [];
  const upcoming     = data.upcoming_actions || [];
  const cur          = data.current_stage || {};
  const progress     = data.season_progress_pct ?? 0;
  const ndviDeviation = (liveNdvi != null && cur.ndvi_expected != null) ? liveNdvi - cur.ndvi_expected : null;
  const stressAlert   = ndviDeviation != null && ndviDeviation < -0.15;

  return (
    <SCard style={{ marginTop: '1.2rem' }}>
      <SCardHeader icon={Clock} title="Calendrier Cultural" color="#9C27B0" />
      <div style={{ padding: '1.4rem' }}>

        {stressAlert && (
          <div style={{
            display: 'flex', gap: '0.7rem', alignItems: 'flex-start',
            padding: '0.75rem 1rem', borderRadius: '12px', marginBottom: '1.2rem',
            background: 'rgba(244,67,54,0.08)', border: '1px solid rgba(244,67,54,0.25)',
          }}>
            <AlertTriangle size={17} color="#F44336" style={{ flexShrink: 0, marginTop: '2px' }} />
            <div>
              <div style={{ fontSize: '0.8rem', fontWeight: '700', color: '#F44336', marginBottom: '0.2rem' }}>
                Alerte stress végétal détecté
              </div>
              <div style={{ fontSize: '0.72rem', color: 'var(--text-dim)', lineHeight: 1.5 }}>
                NDVI satellite ({liveNdvi?.toFixed(3)}) est {Math.abs(ndviDeviation).toFixed(2)} en dessous du seuil attendu ({cur.ndvi_expected?.toFixed(2)}) pour «{cur.name_fr || cur.name}».
              </div>
            </div>
          </div>
        )}

        {/* Progress */}
        <div style={{ marginBottom: '1.1rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.85rem', fontWeight: '700', color: 'var(--text-bright)' }}>
              {cur.name_fr || cur.name || '—'}
            </span>
            <span style={{ fontSize: '0.75rem', fontWeight: '700', color: '#CE93D8' }}>{progress?.toFixed(0)}%</span>
          </div>
          <div style={{ height: '6px', borderRadius: '99px', background: 'rgba(255,255,255,0.07)', overflow: 'hidden' }}>
            <div style={{ height: '100%', width: `${progress}%`, background: 'linear-gradient(90deg, #7B1FA2, #CE93D8)', borderRadius: '99px', transition: 'width 0.8s ease' }} />
          </div>
          {cur.action && (
            <div style={{
              marginTop: '0.7rem', padding: '0.55rem 0.9rem', borderRadius: '9px',
              background: 'rgba(156,39,176,0.08)', border: '1px solid rgba(156,39,176,0.2)',
              fontSize: '0.76rem', color: 'var(--text-light)', lineHeight: 1.5,
            }}>
              <b style={{ color: '#CE93D8' }}>Action : </b>{cur.action}
            </div>
          )}
        </div>

        {/* Timeline */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem', marginBottom: '1rem' }}>
          {timeline.map((s, i) => {
            const c   = stageStatusColor[s.status] || '#444';
            const cur = s.status === 'current';
            return (
              <div key={i} style={{
                display: 'flex', alignItems: 'center', gap: '0.75rem',
                padding: cur ? '0.5rem 0.8rem' : '0.35rem 0.8rem',
                borderRadius: '9px',
                background: cur ? 'rgba(139,195,74,0.09)' : 'transparent',
                border: cur ? '1px solid rgba(139,195,74,0.2)' : '1px solid transparent',
              }}>
                <div style={{ width: '8px', height: '8px', borderRadius: '50%', background: c, boxShadow: cur ? `0 0 6px ${c}99` : 'none', flexShrink: 0 }} />
                <span style={{ flex: 1, fontSize: '0.78rem', fontWeight: cur ? '700' : '400', color: cur ? 'var(--text-bright)' : 'var(--text-muted)' }}>
                  {s.name_fr || s.name}
                  {s.period && <span style={{ color: 'var(--text-dim)', fontWeight: '400', marginLeft: '0.4rem', fontSize: '0.65rem' }}>({s.period})</span>}
                </span>
                {s.status === 'completed' && <CheckCircle size={12} color="#6B8E23" />}
                {cur && <SPill label="En cours" color="#8BC34A" />}
              </div>
            );
          })}
        </div>

        {upcoming.length > 0 && (
          <>
            <div style={{ fontSize: '0.6rem', textTransform: 'uppercase', letterSpacing: '1.5px', color: 'rgba(255,255,255,0.3)', marginBottom: '0.6rem' }}>Actions à venir</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.45rem' }}>
              {upcoming.map((a, i) => (
                <div key={i} style={{ display: 'flex', gap: '0.55rem', alignItems: 'flex-start', fontSize: '0.77rem', color: 'var(--text-light)', lineHeight: 1.5 }}>
                  <AlertTriangle size={12} color={i === 0 ? '#F44336' : '#FF9800'} style={{ marginTop: '3px', flexShrink: 0 }} />
                  <span>
                    <b style={{ color: i === 0 ? '#F44336' : '#FF9800' }}>{a.label === 'NOW' ? 'MAINTENANT' : a.label} — {a.stage}: </b>
                    {a.action}
                  </span>
                </div>
              ))}
            </div>
          </>
        )}
      </div>
    </SCard>
  );
};

/* ══════════════════════════════════════════════════════
   RAG RECOMMENDATIONS CARD (full-width)
══════════════════════════════════════════════════════ */
const RagCard = ({ fieldId, analysis }) => {
  const [data, setData]       = useState(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!fieldId || !analysis) return;
    let cancelled = false;
    setLoading(true);
    setData(null);
    vraService.getRecommendations(fieldId, analysis)
      .then(r => { if (!cancelled) setData(r.data); })
      .catch(err => {
        if (!cancelled) {
          const msg = err?.code === 'ECONNABORTED'
            ? 'Délai dépassé — le LLM peut prendre plusieurs minutes. Réessayez.'
            : (err?.response?.data?.detail || err?.message || 'Requête échouée');
          setData({ ok: false, error: typeof msg === 'string' ? msg : 'Erreur inconnue' });
        }
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [fieldId, analysis]);

  return (
    <SCard>
      <SCardHeader
        icon={BookOpen} title="Recommandations IA — Documentation + Satellite" color="#7C4DFF"
        badge={loading ? <SPill label="Analyse en cours…" color="#7C4DFF" /> : (data?.ok ? <SPill label={`${data.sources?.length || 0} sources`} color="#4CAF50" /> : null)}
      />
      <div style={{ padding: '1.4rem' }}>
        {loading && (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.75rem', padding: '2rem', color: 'var(--text-dim)' }}>
            <div style={{ display: 'flex', gap: '0.4rem' }}>
              {[0, 1, 2].map(i => (
                <div key={i} style={{
                  width: '8px', height: '8px', borderRadius: '50%', background: '#7C4DFF',
                  animation: `bounce 1.2s ease-in-out ${i * 0.2}s infinite`,
                }} />
              ))}
            </div>
            <p style={{ fontSize: '0.82rem', margin: 0 }}>Génération des conseils en cours…</p>
            <p style={{ fontSize: '0.72rem', opacity: 0.7, margin: 0, textAlign: 'center' }}>
              Première analyse : 1–3 min (embeddings locaux + LLM ESPRIT). Ne fermez pas l'onglet.
            </p>
          </div>
        )}

        {!loading && data && !data.ok && (
          <div style={{
            padding: '1rem 1.2rem', borderRadius: '12px',
            background: 'rgba(255,152,0,0.07)', border: '1px solid rgba(255,152,0,0.2)',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', marginBottom: '0.7rem' }}>
              <AlertTriangle size={16} color="#FF9800" />
              <span style={{ fontSize: '0.82rem', fontWeight: '700', color: '#FFB74D' }}>Recommandations indisponibles</span>
            </div>
            <p style={{ fontSize: '0.78rem', color: 'var(--text-dim)', lineHeight: 1.6, margin: 0 }}>{data.error}</p>
            {data.indexed_chunks != null && (
              <div style={{ marginTop: '0.6rem', fontSize: '0.7rem', color: 'rgba(255,255,255,0.35)' }}>
                Documents indexés : {data.indexed_chunks}
              </div>
            )}
            {Array.isArray(data.fix_steps) && data.fix_steps.length > 0 && (
              <div style={{ marginTop: '0.75rem', display: 'flex', flexDirection: 'column', gap: '0.35rem' }}>
                {data.fix_steps.map((s, i) => (
                  <div key={i} style={{ display: 'flex', gap: '0.5rem', fontSize: '0.74rem', color: 'var(--text-light)' }}>
                    <span style={{ color: '#FF9800', fontWeight: '700', flexShrink: 0 }}>{i + 1}.</span>{s}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {!loading && data?.ok && (
          <div>
            <RagText text={data.text} />
            {data.sources?.length > 0 && (
              <>
                <Divider />
                <details>
                  <summary style={{ fontSize: '0.7rem', color: 'rgba(255,255,255,0.35)', cursor: 'pointer', userSelect: 'none', letterSpacing: '0.5px' }}>
                    📚 Sources documentaires ({data.sources.length})
                  </summary>
                  <div style={{ marginTop: '0.6rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                    {data.sources.map((s, i) => (
                      <div key={i} style={{
                        padding: '0.55rem 0.8rem', borderRadius: '9px',
                        background: 'rgba(124,77,255,0.06)', border: '1px solid rgba(124,77,255,0.15)',
                        fontSize: '0.7rem',
                      }}>
                        <div style={{ fontWeight: '700', color: '#B39DDB', marginBottom: '0.2rem' }}>{s.source_path} · §{s.chunk_index}</div>
                        <div style={{ color: 'var(--text-dim)', lineHeight: 1.5, opacity: 0.85 }}>{s.preview}</div>
                      </div>
                    ))}
                  </div>
                </details>
              </>
            )}
            {data.disclaimer && (
              <p style={{ fontSize: '0.62rem', color: 'rgba(255,255,255,0.25)', marginTop: '1rem', fontStyle: 'italic' }}>
                {data.disclaimer}
              </p>
            )}
          </div>
        )}
      </div>
      <style>{`
        @keyframes bounce {
          0%, 80%, 100% { transform: scale(0.6); opacity: 0.4; }
          40% { transform: scale(1); opacity: 1; }
        }
      `}</style>
    </SCard>
  );
};

/* ══════════════════════════════════════════════════════
   SATELLITE DATA SOURCE BANNER
══════════════════════════════════════════════════════ */
const SourceBanner = ({ src }) => {
  if (!src || src === 'error') return null;
  const cfg = {
    planetary_stac:   { bg: 'rgba(33,150,243,0.08)', border: 'rgba(33,150,243,0.25)', color: '#64B5F6', icon: '🛰', label: 'Sentinel-2 L2A via Planetary Computer (open data) — NDVI réel, carte VRA modélisée.' },
    eosda:            { bg: 'rgba(76,175,80,0.08)',  border: 'rgba(76,175,80,0.25)',  color: '#81C784', icon: '🌍', label: 'Sentinel-2 L2A via EOSDA API — NDVI/EVI réels, carte VRA modélisée.' },
    agromonitoring:   { bg: 'rgba(139,195,74,0.08)', border: 'rgba(139,195,74,0.25)', color: '#AED581', icon: '📡', label: 'Sentinel-2 L2A via Agromonitoring — NDVI/EVI réels, carte VRA modélisée.' },
    simulated_quota:  { bg: 'rgba(255,152,0,0.09)',  border: 'rgba(255,152,0,0.3)',   color: '#FFB74D', icon: '⚠️', label: 'Mode secours (quota Agromonitoring) — NDVI/VRA simulés de façon stable, pas satellite réel.' },
    api_quota:        { bg: 'rgba(183,28,28,0.1)',   border: 'rgba(244,67,54,0.3)',   color: '#EF9A9A', icon: '🚫', label: 'Quota Agromonitoring atteint — aucun NDVI affiché. Libérez des polygones sur agromonitoring.com.' },
  };
  const c = cfg[src];
  if (!c) return null;
  return (
    <div style={{
      padding: '0.6rem 1rem', borderRadius: '10px', marginBottom: '1.2rem',
      background: c.bg, border: `1px solid ${c.border}`,
      fontSize: '0.74rem', color: c.color, display: 'flex', alignItems: 'center', gap: '0.6rem',
    }}>
      <span>{c.icon}</span>
      <span>{c.label}</span>
    </div>
  );
};

/* ══════════════════════════════════════════════════════
   MAIN PAGE
══════════════════════════════════════════════════════ */
const SatelliteDashboard = () => {
  const [fields, setFields]         = useState([]);
  const [selectedId, setSelectedId] = useState(null);
  const [analysis, setAnalysis]     = useState(null);
  const [loading, setLoading]       = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError]           = useState(false);

  useEffect(() => {
    fieldService.getFields()
      .then(r => {
        const f = r.data || [];
        setFields(f);
        if (f.length > 0) setSelectedId(f[0].id);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!selectedId) return;
    setRefreshing(true);
    setAnalysis(null);
    setError(false);
    vraService.getFullAnalysis(selectedId)
      .then(r => { setAnalysis(r.data); setError(false); })
      .catch(() => setError(true))
      .finally(() => setRefreshing(false));
  }, [selectedId]);

  const handleRefresh = () => {
    if (!selectedId || refreshing) return;
    setRefreshing(true);
    setError(false);
    vraService.getFullAnalysis(selectedId)
      .then(r => { setAnalysis(r.data); setError(false); })
      .catch(() => setError(true))
      .finally(() => setRefreshing(false));
  };

  const selectedField = fields.find(f => f.id === selectedId);

  /* Derive live NDVI */
  const ndviSum  = analysis?.ndvi_summary || {};
  const diagSum  = analysis?.ndvi_diagnostic?.summary || {};
  const liveVal  = typeof ndviSum.avg_ndvi === 'number' ? ndviSum.avg_ndvi
                 : typeof diagSum.avg_ndvi === 'number' ? diagSum.avg_ndvi
                 : typeof analysis?.vra_map?.avg_ndvi === 'number' ? analysis.vra_map.avg_ndvi
                 : null;
  const liveDate   = ndviSum.date    || diagSum.date;
  const liveClouds = ndviSum.clouds  ?? diagSum.clouds;
  const liveSource = ndviSum.source  || diagSum.source;
  const satSrc     = ndviSum.satellite_data_source;

  return (
    <div style={{ paddingTop: '1.5rem', paddingBottom: '5rem', maxWidth: '1400px', margin: '0 auto' }}>

      {/* ── Header ── */}
      <ScrollReveal>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.8rem', flexWrap: 'wrap', gap: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
            <div style={{
              width: '48px', height: '48px', borderRadius: '14px', display: 'flex', alignItems: 'center', justifyContent: 'center',
              background: 'linear-gradient(135deg, #1a3a5c, #0d2137)',
              border: '1px solid rgba(33,150,243,0.3)',
              boxShadow: '0 4px 16px rgba(33,150,243,0.15)',
            }}>
              <Satellite size={24} color="#4EADD5" />
            </div>
            <div>
              <h2 style={{ fontFamily: "'Newsreader', serif", fontSize: '1.65rem', fontWeight: '800', color: 'var(--text-bright)', margin: 0, lineHeight: 1.1 }}>
                Analyse Satellite
              </h2>
              <p style={{ color: 'var(--text-dim)', fontSize: '0.75rem', margin: 0, letterSpacing: '0.5px' }}>
                NDVI · VRA · Santé du Sol · Calendrier Cultural
              </p>
            </div>
          </div>
          <button onClick={handleRefresh} disabled={refreshing} style={{
            display: 'flex', alignItems: 'center', gap: '0.5rem',
            padding: '0.6rem 1.3rem', borderRadius: '99px',
            background: refreshing ? 'rgba(139,195,74,0.05)' : 'rgba(139,195,74,0.1)',
            border: '1px solid rgba(139,195,74,0.3)',
            color: '#8BC34A', fontSize: '0.8rem', fontWeight: '700',
            cursor: refreshing ? 'not-allowed' : 'pointer',
            transition: 'all 0.2s', opacity: refreshing ? 0.6 : 1,
          }}>
            <RefreshCw size={14} style={{ animation: refreshing ? 'spin 1s linear infinite' : 'none' }} />
            {refreshing ? 'Analyse…' : 'Actualiser'}
          </button>
        </div>
      </ScrollReveal>

      {loading ? (
        <div style={{ textAlign: 'center', color: 'var(--text-dim)', padding: '5rem' }}>Chargement des parcelles…</div>
      ) : fields.length === 0 ? (
        <SCard>
          <div style={{ textAlign: 'center', padding: '4rem', color: 'var(--text-dim)' }}>
            <Satellite size={48} style={{ marginBottom: '1rem', opacity: 0.3 }} />
            <p>Aucune parcelle enregistrée. Créez d'abord une parcelle dans l'onglet Parcelles.</p>
          </div>
        </SCard>
      ) : (
        <ScrollReveal delay={0.05}>
          <>
            {/* ── Field selector ── */}
            <div style={{ display: 'flex', gap: '0.55rem', flexWrap: 'wrap', marginBottom: '1.2rem' }}>
              {fields.map(f => {
                const active = f.id === selectedId;
                return (
                  <button key={f.id} onClick={() => setSelectedId(f.id)} style={{
                    padding: '0.45rem 1.1rem', borderRadius: '99px', cursor: 'pointer',
                    fontSize: '0.8rem', fontWeight: active ? '700' : '500',
                    background: active ? 'rgba(139,195,74,0.14)' : 'rgba(255,255,255,0.04)',
                    border: active ? '1px solid rgba(139,195,74,0.45)' : '1px solid rgba(255,255,255,0.08)',
                    color: active ? '#8BC34A' : 'var(--text-muted)',
                    transition: 'all 0.18s',
                  }}>
                    🌾 {f.name}
                  </button>
                );
              })}
            </div>

            {/* ── Field info bar ── */}
            {selectedField && (
              <div style={{
                display: 'flex', flexWrap: 'wrap', gap: '1.5rem',
                padding: '0.75rem 1.2rem', marginBottom: '1.2rem',
                background: 'rgba(139,195,74,0.05)', border: '1px solid rgba(139,195,74,0.12)',
                borderRadius: '12px', fontSize: '0.78rem', color: 'var(--text-muted)',
                alignItems: 'center',
              }}>
                <span style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                  <span>📍</span>
                  <b style={{ color: 'var(--text-light)' }}>{selectedField.name}</b>
                </span>
                {selectedField.crop_type && (
                  <span style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                    <Leaf size={13} color="#8BC34A" />
                    <span>Culture : <b style={{ color: 'var(--text-light)' }}>{selectedField.crop_type}</b></span>
                  </span>
                )}
                {selectedField.area_ha && (
                  <span style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                    <span>📐</span>
                    <span>Surface : <b style={{ color: 'var(--text-light)' }}>{selectedField.area_ha} ha</b></span>
                  </span>
                )}
                {liveVal != null && (
                  <span style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                    <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: ndviColor(liveVal), display: 'inline-block', boxShadow: `0 0 6px ${ndviColor(liveVal)}` }} />
                    <b style={{ color: ndviColor(liveVal) }}>NDVI {liveVal.toFixed(3)}</b>
                    {liveDate && <span style={{ fontSize: '0.7rem', opacity: 0.6 }}>· {liveDate}</span>}
                  </span>
                )}
              </div>
            )}

            <SourceBanner src={satSrc} />

            {/* ── Loading ── */}
            {refreshing && (
              <SCard>
                <div style={{ textAlign: 'center', padding: '3.5rem', color: 'var(--text-dim)' }}>
                  <div style={{ display: 'flex', justifyContent: 'center', gap: '0.5rem', marginBottom: '1.5rem' }}>
                    {[0,1,2,3].map(i => (
                      <div key={i} style={{
                        width: '10px', height: '10px', borderRadius: '50%',
                        background: '#4EADD5',
                        animation: `bounce 1.2s ease-in-out ${i * 0.15}s infinite`,
                      }} />
                    ))}
                  </div>
                  <p style={{ margin: 0, fontSize: '0.85rem' }}>Analyse satellite en cours…</p>
                  <p style={{ margin: '0.5rem 0 0', fontSize: '0.72rem', opacity: 0.6 }}>Requête Sentinel-2 · Planetary Computer</p>
                </div>
              </SCard>
            )}

            {/* ── Error ── */}
            {!refreshing && error && (
              <SCard>
                <div style={{ textAlign: 'center', padding: '3rem', color: 'var(--text-dim)' }}>
                  <AlertTriangle size={36} color="#FF9800" style={{ marginBottom: '0.8rem' }} />
                  <p style={{ margin: 0 }}>Impossible de charger l'analyse. Vérifiez que le backend tourne sur le port 8000.</p>
                </div>
              </SCard>
            )}

            {/* ── Main content ── */}
            {!refreshing && !error && analysis && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '1.2rem' }}>
                {/* 2-column grid */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(400px, 1fr))', gap: '1.2rem', alignItems: 'start' }}>
                  {/* Left col */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '1.2rem' }}>
                    <NdviCard fieldId={selectedId} liveVal={liveVal} liveDate={liveDate} liveClouds={liveClouds} liveSource={liveSource} />
                    <SoilHealthCard data={analysis.soil_health} />
                  </div>
                  {/* Right col */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '1.2rem' }}>
                    <VraCard data={analysis.vra_map} />
                    <CropCalendarCard data={analysis.crop_calendar} liveNdvi={liveVal} />
                  </div>
                </div>

                {/* RAG full-width below */}
                <RagCard fieldId={selectedId} analysis={analysis} />
              </div>
            )}
          </>
        </ScrollReveal>
      )}

      <style>{`
        @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
        @keyframes bounce {
          0%, 80%, 100% { transform: scale(0.55); opacity: 0.35; }
          40% { transform: scale(1); opacity: 1; }
        }
      `}</style>
    </div>
  );
};

export default SatelliteDashboard;
