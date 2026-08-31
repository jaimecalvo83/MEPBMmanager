import { useEffect, useRef, useState } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

interface HexTile {
  q: number;
  r: number;
  terrain: string;
  ownerId?: string;
}

interface MapArmy {
  id: string;
  name: string;
  locationHex: string;
}

interface MapCharacter {
  id: string;
  name: string;
  type: string;
  locationHex: string;
}

interface MapCentre {
  id: string;
  name: string;
  locationHex: string;
  isCapital: boolean;
}

interface HexMapProps {
  hexes: HexTile[];
  armies?: MapArmy[];
  characters?: MapCharacter[];
  populationCentres?: MapCentre[];
  onHexClick?: (q: number, r: number) => void;
  selectedHex?: { q: number; r: number } | null;
}

const TERRAIN_COLORS: Record<string, string> = {
  plains: '#90EE90',
  forest: '#228B22',
  mountains: '#808080',
  rough: '#D2B48C',
  desert: '#F4A460',
  swamp: '#556B2F',
  shore: '#87CEEB',
  water: '#4169E1',
};

const HEX_SIZE = 30;

function hexToPixel(q: number, r: number): { x: number; y: number } {
  const x = HEX_SIZE * (Math.sqrt(3) * q + (Math.sqrt(3) / 2) * r);
  const y = HEX_SIZE * ((3 / 2) * r);
  return { x, y };
}

function getHexCorners(cx: number, cy: number): [number, number][] {
  const corners: [number, number][] = [];
  for (let i = 0; i < 6; i++) {
    const angle = (Math.PI / 180) * (60 * i - 30);
    corners.push([
      cx + HEX_SIZE * Math.cos(angle),
      cy + HEX_SIZE * Math.sin(angle),
    ]);
  }
  return corners;
}

export default function HexMap({ hexes, armies = [], characters = [], populationCentres = [], onHexClick, selectedHex }: HexMapProps) {
  const mapRef = useRef<HTMLDivElement>(null);
  const mapInstanceRef = useRef<L.Map | null>(null);
  const [hoveredHex, setHoveredHex] = useState<{ q: number; r: number } | null>(null);

  useEffect(() => {
    if (!mapRef.current || mapInstanceRef.current) return;

    const map = L.map(mapRef.current, {
      crs: L.CRS.Simple,
      minZoom: -2,
      maxZoom: 4,
      zoomSnap: 0.25,
    });

    mapInstanceRef.current = map;

    return () => {
      map.remove();
      mapInstanceRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapInstanceRef.current;
    if (!map) return;

    map.eachLayer((layer) => {
      if (layer instanceof L.Polygon) {
        map.removeLayer(layer);
      }
    });

    for (const hex of hexes) {
      const { x, y } = hexToPixel(hex.q, hex.r);
      const corners = getHexCorners(x, y);

      const isSelected = selectedHex?.q === hex.q && selectedHex?.r === hex.r;
      const isHovered = hoveredHex?.q === hex.q && hoveredHex?.r === hex.r;

      let color = TERRAIN_COLORS[hex.terrain] || '#90EE90';
      if (isSelected) color = '#FFD700';
      else if (isHovered) color = '#FFA500';

      const polygon = L.polygon(corners, {
        color: '#333',
        weight: 1,
        fillColor: color,
        fillOpacity: 0.8,
      }).addTo(map);

      polygon.on('click', () => {
        onHexClick?.(hex.q, hex.r);
      });

      polygon.on('mouseover', () => {
        setHoveredHex({ q: hex.q, r: hex.r });
      });

      polygon.on('mouseout', () => {
        setHoveredHex(null);
      });

      if (hex.ownerId) {
        polygon.bindTooltip(`Owner: ${hex.ownerId}`, { permanent: false });
      }
    }

    const parseHex = (loc?: string): { q: number; r: number } | null => {
      if (!loc) return null;
      const [q, r] = loc.split(',').map((v) => parseInt(v.trim(), 10));
      if (Number.isNaN(q) || Number.isNaN(r)) return null;
      return { q, r };
    };

    const addMarker = (loc: string, html: string, tooltip: string) => {
      const coord = parseHex(loc);
      if (!coord) return;
      const { x, y } = hexToPixel(coord.q, coord.r);
      const icon = L.divIcon({
        className: 'hex-marker',
        html,
        iconSize: [HEX_SIZE, HEX_SIZE],
        iconAnchor: [HEX_SIZE / 2, HEX_SIZE / 2],
      });
      L.marker([y, x], { icon, interactive: false }).addTo(map).bindTooltip(tooltip, { permanent: false });
    };

    for (const pc of populationCentres) {
      const isCap = pc.isCapital;
      addMarker(
        pc.locationHex,
        `<div style="width:${HEX_SIZE}px;height:${HEX_SIZE}px;display:flex;align-items:center;justify-content:center;font-size:18px;color:${isCap ? '#FFD700' : '#CCC'};text-shadow:0 0 3px #000;">${isCap ? '★' : '▢'}</div>`,
        `${pc.name}${isCap ? ' (Capital)' : ''}`
      );
    }

    for (const army of armies) {
      addMarker(
        army.locationHex,
        `<div style="width:${HEX_SIZE}px;height:${HEX_SIZE}px;display:flex;align-items:center;justify-content:center;font-size:16px;color:#FF5555;text-shadow:0 0 3px #000;">⚔</div>`,
        army.name
      );
    }

    const charColor: Record<string, string> = {
      commander: '#5DADE2',
      agent: '#AF7AC5',
      emissary: '#48C9B0',
      mage: '#F5B041',
    };
    for (const char of characters) {
      const color = charColor[char.type] || '#FFFFFF';
      addMarker(
        char.locationHex,
        `<div style="width:${HEX_SIZE}px;height:${HEX_SIZE}px;display:flex;align-items:center;justify-content:center;font-size:14px;color:${color};text-shadow:0 0 3px #000;">●</div>`,
        `${char.name} (${char.type})`
      );
    }

    if (hexes.length > 0) {
      const bounds = hexes.map((hex) => {
        const { x, y } = hexToPixel(hex.q, hex.r);
        return [y, x] as [number, number];
      });
      map.fitBounds(bounds);
    }
  }, [hexes, selectedHex, hoveredHex, onHexClick]);

  return (
    <div className="relative">
      <div ref={mapRef} className="w-full h-[600px] bg-gray-900 rounded-lg border border-gray-700" />
      <div className="absolute bottom-4 left-4 bg-gray-800 p-3 rounded-lg border border-gray-700 text-xs space-y-1">
        <p className="font-bold text-mepbm-gold mb-2">Terrain</p>
        {Object.entries(TERRAIN_COLORS).map(([terrain, color]) => (
          <div key={terrain} className="flex items-center gap-2">
            <div className="w-4 h-4 rounded" style={{ backgroundColor: color }} />
            <span className="capitalize">{terrain}</span>
          </div>
        ))}
        <div className="border-t border-gray-700 my-2 pt-2 space-y-1">
          <p className="font-bold text-mepbm-gold">Units</p>
          <div className="flex items-center gap-2"><span className="text-yellow-400">★</span><span>Capital</span></div>
          <div className="flex items-center gap-2"><span className="text-gray-300">▢</span><span>Town</span></div>
          <div className="flex items-center gap-2"><span className="text-red-400">⚔</span><span>Army</span></div>
          <div className="flex items-center gap-2"><span className="text-white">●</span><span>Character</span></div>
        </div>
      </div>
      {hoveredHex && (
        <div className="absolute top-4 right-4 bg-gray-800 px-3 py-2 rounded-lg border border-gray-700 text-sm">
          Hex: {hoveredHex.q}, {hoveredHex.r}
        </div>
      )}
    </div>
  );
}
