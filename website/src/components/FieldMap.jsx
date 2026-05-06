import React, { useState, useEffect, useCallback } from 'react';
import { MapContainer, TileLayer, Polygon, useMapEvents, Popup, useMap } from 'react-leaflet';
import L from 'leaflet';
import { Map as MapIcon, Pencil, X, RefreshCw } from 'lucide-react';

// Fix for default marker icons
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon-2x.png',
  iconUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon.png',
  shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png',
});

/* ── Separate component: handles click events only ── */
const ClickHandler = ({ active, onMapClick }) => {
  useMapEvents({
    click: (e) => {
      if (active) onMapClick(e.latlng);
    },
  });
  return null;
};

/* ── Separate component: reads map instance and fits bounds ── */
const BoundsController = ({ setMap, points }) => {
  const map = useMap();
  useEffect(() => {
    if (map) {
      setMap(map);
      if (points && points.length > 1) {
        try {
          const bounds = L.latLngBounds(points);
          map.fitBounds(bounds, { padding: [30, 30] });
        } catch (e) {
          // silently ignore
        }
      }
    }
  }, [map]); // eslint-disable-line
  return null;
};

/* ── Main FieldMap component ── */
const FieldMap = ({
  initialPolygon = [],
  onPolygonChange,
  isEditable = false,
  center = [34.74, 10.76],
  zoom = 13,
  showNdvi = false,
  diagnosticData = null,
  vraData = null,
}) => {
  const [points, setPoints] = useState(initialPolygon);
  const [map, setMap] = useState(null);
  const [baseLayer, setBaseLayer] = useState('satellite');

  useEffect(() => {
    if (initialPolygon?.length > 0) setPoints(initialPolygon);
  }, [initialPolygon]);

  const handleMapClick = (latlng) => {
    const newPoints = [...points, [latlng.lat, latlng.lng]];
    setPoints(newPoints);
    if (onPolygonChange) onPolygonChange(newPoints);
  };

  const clearPolygon = () => {
    setPoints([]);
    if (onPolygonChange) onPolygonChange([]);
  };

  const undoLastPoint = () => {
    const newPoints = points.slice(0, -1);
    setPoints(newPoints);
    if (onPolygonChange) onPolygonChange(newPoints);
  };

  const fitBounds = useCallback(() => {
    if (map && points.length > 0) {
      try {
        map.fitBounds(L.latLngBounds(points), { padding: [20, 20] });
      } catch (e) { /* ignore */ }
    }
  }, [map, points]);

  const tileUrl = baseLayer === 'satellite'
    ? 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}'
    : 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';

  return (
    <div style={{ position: 'relative', height: '100%', width: '100%', borderRadius: 'inherit', overflow: 'hidden' }}>
      <MapContainer
        center={center}
        zoom={zoom}
        style={{ height: '100%', width: '100%', zIndex: 0 }}
        zoomControl={false}
      >
        {/* Tile layer */}
        <TileLayer url={tileUrl} />

        {/* Map initializer */}
        <BoundsController setMap={setMap} points={points} />

        {/* Click handler (only when editable) */}
        {isEditable && <ClickHandler active={isEditable} onMapClick={handleMapClick} />}

        {/* Field boundary */}
        {points.length > 0 && (
          <Polygon
            positions={points}
            pathOptions={{
              color: showNdvi ? 'rgba(255,255,255,0.4)' : '#8BC34A',
              fillColor: showNdvi ? 'transparent' : '#8BC34A',
              fillOpacity: 0.2,
              weight: 2,
            }}
          />
        )}

        {/* NDVI diagnostic zones */}
        {showNdvi && diagnosticData?.zones && diagnosticData.zones.map((zone, idx) => (
          <Polygon
            key={idx}
            positions={zone.polygon}
            pathOptions={{
              fillColor: zone.color,
              fillOpacity: 0.72,
              stroke: true,
              color: 'rgba(255,255,255,0.15)',
              weight: 1,
            }}
          />
        ))}

        {/* VRA Map Overlay (Grid Slices) */}
        {showNdvi && vraData?.spatial_overlay && vraData.spatial_overlay.map((zone, idx) => (
          <Polygon
            key={`vra-${idx}`}
            positions={zone.polygon}
            pathOptions={{
              fillColor: zone.color,
              fillOpacity: 0.65,
              stroke: true,
              color: 'rgba(0,0,0,0.1)',
              weight: 1,
            }}
          >
            <Popup>
              <div style={{ padding: '0.2rem', fontSize: '0.75rem', fontWeight: 'bold', color: zone.color }}>
                {zone.label}
              </div>
            </Popup>
          </Polygon>
        ))}

        {/* Fallback green when NDVI enabled but no zones yet */}
        {showNdvi && !diagnosticData?.zones && points.length >= 3 && (
          <Polygon
            positions={points}
            pathOptions={{ fillColor: '#66bd63', fillOpacity: 0.4, stroke: false }}
          />
        )}
      </MapContainer>

      {/* Layer switcher (outside map) */}
      <div style={{ position: 'absolute', top: '0.6rem', right: '0.6rem', zIndex: 900, display: 'flex', gap: '0.3rem' }}>
        <button onClick={() => setBaseLayer('satellite')} title="Satellite" style={{ padding: '0.35rem 0.65rem', fontSize: '0.62rem', cursor: 'pointer', borderRadius: '6px', background: baseLayer === 'satellite' ? 'rgba(139,195,74,0.88)' : 'rgba(0,0,0,0.55)', color: '#fff', border: '1px solid rgba(255,255,255,0.2)' }}>
          🛰️ Satellite
        </button>
        <button onClick={() => setBaseLayer('osm')} title="OpenStreetMap" style={{ padding: '0.35rem 0.65rem', fontSize: '0.62rem', cursor: 'pointer', borderRadius: '6px', background: baseLayer === 'osm' ? 'rgba(78,173,213,0.88)' : 'rgba(0,0,0,0.55)', color: '#fff', border: '1px solid rgba(255,255,255,0.2)' }}>
          🗺️ Carte
        </button>
      </div>

      {/* Drawing toolbar */}
      {isEditable && (
        <div style={{ position: 'absolute', top: '0.6rem', left: '0.6rem', zIndex: 900, display: 'flex', gap: '0.3rem', background: 'rgba(0,0,0,0.6)', padding: '0.35rem', borderRadius: '8px', border: '1px solid rgba(255,255,255,0.15)' }}>
          <button onClick={undoLastPoint} title="Annuler" style={{ padding: '0.3rem', background: 'rgba(255,255,255,0.1)', border: 'none', borderRadius: '5px', color: '#fff', cursor: 'pointer', display: 'flex' }}>
            <RefreshCw size={12} />
          </button>
          <button onClick={clearPolygon} title="Effacer" style={{ padding: '0.3rem', background: 'rgba(199,91,57,0.5)', border: 'none', borderRadius: '5px', color: '#fff', cursor: 'pointer', display: 'flex' }}>
            <X size={12} />
          </button>
          <span style={{ fontSize: '0.6rem', color: '#ccc', alignSelf: 'center', padding: '0 0.2rem' }}>{points.length} pts</span>
        </div>
      )}

      {/* Fit-bounds button */}
      <div style={{ position: 'absolute', bottom: '3.5rem', left: '0.6rem', zIndex: 900 }}>
        <button onClick={fitBounds} title="Recentrer" style={{ padding: '0.35rem', background: 'rgba(0,0,0,0.6)', border: '1px solid rgba(255,255,255,0.2)', borderRadius: '6px', color: '#fff', cursor: 'pointer', display: 'flex' }}>
          <MapIcon size={14} />
        </button>
      </div>

      {/* NDVI legend */}
      {showNdvi && (
        <div style={{ position: 'absolute', bottom: '1.5rem', right: '0.6rem', zIndex: 900, background: 'rgba(0,0,0,0.72)', padding: '0.65rem', borderRadius: '8px', border: '1px solid rgba(255,255,255,0.15)', width: '165px' }}>
          <div style={{ fontSize: '0.58rem', color: '#aaa', marginBottom: '0.35rem', textTransform: 'uppercase', letterSpacing: '1px' }}>Légende NDVI</div>
          <div style={{ height: '7px', background: 'linear-gradient(to right, #d73027, #f46d43, #fdae61, #fee08b, #d9ef8b, #a6d96a, #66bd63, #1a9850)', borderRadius: '4px', marginBottom: '0.3rem' }} />
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.56rem', color: '#ccc' }}>
            <span>Stress</span><span>0.5</span><span>Vigueur</span>
          </div>
        </div>
      )}

      {/* Draw hint */}
      {isEditable && points.length === 0 && (
        <div style={{ position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%,-50%)', zIndex: 900, pointerEvents: 'none', textAlign: 'center' }}>
          <div style={{ background: 'rgba(139,195,74,0.12)', border: '1px solid rgba(139,195,74,0.3)', borderRadius: '10px', padding: '0.9rem', backdropFilter: 'blur(6px)' }}>
            <Pencil size={20} style={{ color: '#8BC34A', marginBottom: '0.35rem' }} />
            <p style={{ fontSize: '0.75rem', color: '#ccc', margin: 0 }}>Cliquez pour délimiter la parcelle</p>
          </div>
        </div>
      )}
    </div>
  );
};

export default FieldMap;
