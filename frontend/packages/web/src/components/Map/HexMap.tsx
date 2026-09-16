import { useEffect, useRef, useState } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import {
  MAP2950_MAJOR_RIVER_SIDES,
  MAP2950_MINOR_RIVER_SIDES,
  MAP2950_ROAD_SIDES,
  MAP2950_FORD_SIDES,
  MAP2950_BRIDGE_SIDES,
} from '@MEPBMmanager/shared';
import { useLang, terrainLabel, charTypeLabel, pcSizeLabel, fortLabel as fortName } from '../../i18n/lang';

interface HexTile {
  q: number;
  r: number;
  terrain: string;
  ownerId?: string;
  hasBridge?: boolean;
  hasFord?: boolean;
  hasMajorRiver?: boolean;
  hasMinorRiver?: boolean;
  hasRoad?: boolean;
}

interface MapArmy {
  id: string;
  name: string;
  locationHex: string;
  commanderId?: string;
  heavyCavalry?: number;
  lightCavalry?: number;
  heavyInfantry?: number;
  lightInfantry?: number;
  archers?: number;
  menAtArms?: number;
}

interface MapCharacter {
  id: string;
  name: string;
  type: string;
  locationHex: string;
  isChampion?: boolean;
  commandSkill?: number;
  agentSkill?: number;
  emissarySkill?: number;
  mageSkill?: number;
  armyId?: string;
  health?: number;
}

interface MapCentre {
  id: string;
  name: string;
  locationHex: string;
  isCapital: boolean;
  size?: string;
  fortification?: string;
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
  plains: '#E4E8C4',
  forest: '#30C000',
  mountains: '#705030',
  rough: '#B0A050',
  desert: '#F0E090',
  swamp: '#20C070',
  shore: '#B0F8B8',
  coastal: '#30E8D0',
  water: '#0070F0',
};

const HEX_SIZE = 30;

// Map: CRS.Simple with an explicit, non-flipping transformation so that our
// metric lattice (x grows right, y grows down) maps 1:1 onto the screen,
// exactly like the digitized source image (north-up, east-right).
const SimpleXY = L.Util.extend({}, L.CRS.Simple, {
  transformation: new L.Transformation(1, 0, 1, 0),
});

function hexToPixel(q: number, r: number): { x: number; y: number } {
  const x = HEX_SIZE * (Math.sqrt(3) * q + (Math.sqrt(3) / 2) * ((r + 1) % 2));
  const y = HEX_SIZE * ((3 / 2) * r);
  return { x, y };
}

// latlng = [y, x]: y grows down = south, x grows right = east.
function toLatLng(px: { x: number; y: number }): [number, number] {
  return [px.y, px.x];
}

function getHexCorners(cx: number, cy: number): [number, number][] {
  const corners: [number, number][] = [];
  for (let i = 0; i < 6; i++) {
    const angle = (Math.PI / 180) * (60 * i - 30);
    corners.push(toLatLng({
      x: cx + HEX_SIZE * Math.cos(angle),
      y: cy + HEX_SIZE * Math.sin(angle),
    }));
  }
  return corners;
}

export default function HexMap({ hexes, armies = [], characters = [], populationCentres = [], onHexClick, selectedHex }: HexMapProps) {
  const { t } = useLang();
  const mapRef = useRef<HTMLDivElement>(null);
  const mapInstanceRef = useRef<L.Map | null>(null);
  const [hoveredHex, setHoveredHex] = useState<{ q: number; r: number } | null>(null);

  useEffect(() => {
    if (!mapRef.current || mapInstanceRef.current) return;

    const map = L.map(mapRef.current, {
      crs: SimpleXY,
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
      if (layer instanceof L.Polygon || layer instanceof L.Marker) {
        map.removeLayer(layer);
      }
    });

    // ── Helpers ──
    const parseHex = (loc?: string): { q: number; r: number } | null => {
      if (!loc) return null;
      const [q, r] = loc.split(',').map((v) => parseInt(v.trim(), 10));
      if (Number.isNaN(q) || Number.isNaN(r)) return null;
      return { q, r };
    };

    // ── Group all entities by hex ──
    type HexEntity = {
      hex: string;
      type: 'pc' | 'army' | 'character';
      name: string;
      detail: string;
      data: any;
    };
    const allEntities: HexEntity[] = [];

    // Build commander lookup for army tooltips
    const charById = new Map<string, MapCharacter>();
    for (const c of characters) charById.set(c.id, c);

    for (const pc of populationCentres) {
      const sizeTxt = pcSizeLabel(pc.size, t);
      const fortTxt = pc.fortification ? ` [${fortName(pc.fortification, t)}]` : '';
      allEntities.push({
        hex: pc.locationHex,
        type: 'pc',
        name: pc.name,
        detail: `${sizeTxt}${pc.isCapital ? ` ${t('map.capitalSuffix')}` : ''}${fortTxt}`,
        data: pc,
      });
    }
    for (const army of armies) {
      const totalTroops = (army.heavyCavalry || 0) + (army.lightCavalry || 0) +
        (army.heavyInfantry || 0) + (army.lightInfantry || 0) +
        (army.archers || 0) + (army.menAtArms || 0);
      const commander = army.commanderId ? charById.get(army.commanderId) : null;
      const general = commander ? ` — ${t('map.commander')}: ${commander.name}` : '';
      allEntities.push({
        hex: army.locationHex,
        type: 'army',
        name: army.name,
        detail: `${t('army.troopsWord', { n: totalTroops })}${general}`,
        data: army,
      });
    }
    for (const char of characters) {
      const loose = !char.armyId;
      const skills = `Cmd:${char.commandSkill || 0} Agt:${char.agentSkill || 0} Emb:${char.emissarySkill || 0} Mge:${char.mageSkill || 0}`;
      const champ = char.isChampion ? ` [${t('char.champion')}]` : '';
      const looseTag = loose ? ` (${t('map.loose')})` : '';
      allEntities.push({
        hex: char.locationHex,
        type: 'character',
        name: char.name,
        detail: `${charTypeLabel(char.type, t)}${champ}${looseTag} — ${skills} HP:${char.health || 0}`,
        data: char,
      });
    }

    const byHex = new Map<string, HexEntity[]>();
    for (const e of allEntities) {
      if (!byHex.has(e.hex)) byHex.set(e.hex, []);
      byHex.get(e.hex)!.push(e);
    }

    const entityIcon = (type: string) => type === 'pc' ? '★' : type === 'army' ? '⚔' : '●';

    // ── Draw hex polygons ──
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

      polygon.on('click', () => onHexClick?.(hex.q, hex.r));
      polygon.on('mouseover', () => setHoveredHex({ q: hex.q, r: hex.r }));
      polygon.on('mouseout', () => setHoveredHex(null));

      // Tooltip with terrain + features + entities
      const hexKey = `${hex.q},${hex.r}`;
      const hexEntities = byHex.get(hexKey) || [];
      let tooltipText = `${t('map.hex', { q: hex.q, r: hex.r })} — ${terrainLabel(hex.terrain, t)}`;
      const feats: string[] = [];
      if (hex.hasMajorRiver) feats.push(t('map.majorRiver'));
      if (hex.hasMinorRiver) feats.push(t('map.minorRiver'));
      if (hex.hasRoad) feats.push(t('map.road'));
      if (hex.hasFord) feats.push(t('map.ford'));
      if (hex.hasBridge) feats.push(t('map.bridge'));
      if (feats.length > 0) tooltipText += `\n${feats.join(' · ')}`;
      for (const e of hexEntities) {
        tooltipText += `\n${entityIcon(e.type)} ${e.name} (${e.detail})`;
      }
      polygon.bindTooltip(tooltipText, { permanent: false, direction: 'top', className: 'hex-tooltip' });
    }

    // ── Draw 2950 rivers, roads, fords & bridges like the official JPG ──
    // Uses the digitized hex-side lists (map2950_features): rivers run ALONG
    // the shared hex edge; roads join the midpoints of a hex's road sides;
    // fords/bridges are ticks on the side midpoint. Only for the 2950 module.
    // On cropped maps (brackets 10/15/20) only sides with both hexes present.
    if (hexes.length > 0) {
      const present = new Set(
        hexes.map((h: any) => `${String(h.q).padStart(2, '0')}${String(h.r).padStart(2, '0')}`)
      );
      const live = (sides: string[][]) => sides.filter(([a, b]) => present.has(a) && present.has(b));
      const majorSides = live(MAP2950_MAJOR_RIVER_SIDES as unknown as string[][]);
      const minorSides = live(MAP2950_MINOR_RIVER_SIDES as unknown as string[][]);
      const roadSides = live(MAP2950_ROAD_SIDES as unknown as string[][]);
      const fordSides = live(MAP2950_FORD_SIDES as unknown as string[][]);
      const bridgeSides = live(MAP2950_BRIDGE_SIDES as unknown as string[][]);
      const EDGE_HALF = HEX_SIZE / 2; // regular hex: side length = circumradius
      const idToQR = (id: string) => ({ q: parseInt(id.slice(0, 2), 10), r: parseInt(id.slice(2, 4), 10) });
      const segLine = (x1: number, y1: number, x2: number, y2: number, opts: L.PolylineOptions) => {
        L.polyline([toLatLng({ x: x1, y: y1 }), toLatLng({ x: x2, y: y2 })], {
          interactive: false, lineCap: 'round', lineJoin: 'round', ...opts,
        }).addTo(map);
      };
      // Shared-edge geometry for side A-B: edge = perpendicular bisector
      // of the center-to-center segment, half-length EDGE_HALF.
      const edgeOf = (a: string, b: string) => {
        const A = idToQR(a), B = idToQR(b);
        const pa = hexToPixel(A.q, A.r), pb = hexToPixel(B.q, B.r);
        const mx = (pa.x + pb.x) / 2, my = (pa.y + pb.y) / 2;
        const dx = pb.x - pa.x, dy = pb.y - pa.y;
        const L = Math.hypot(dx, dy) || 1;
        return { mx, my, ex: -dy / L, ey: dx / L };
      };
      for (const [a, b] of majorSides) {
        const { mx, my, ex, ey } = edgeOf(a, b);
        segLine(mx - ex * EDGE_HALF, my - ey * EDGE_HALF, mx + ex * EDGE_HALF, my + ey * EDGE_HALF,
          { color: '#1E5CFF', weight: 4, opacity: 0.95 });
      }
      for (const [a, b] of minorSides) {
        const { mx, my, ex, ey } = edgeOf(a, b);
        segLine(mx - ex * EDGE_HALF, my - ey * EDGE_HALF, mx + ex * EDGE_HALF, my + ey * EDGE_HALF,
          { color: '#4DA6FF', weight: 2, opacity: 0.95 });
      }
      // Roads: join side midpoints inside each hex (JPG draws them through
      // the hex interior, crossing sides at their midpoints).
      const roadMids = new Map<string, { x: number; y: number }[]>();
      for (const [a, b] of roadSides) {
        const { mx, my } = edgeOf(a, b);
        if (!roadMids.has(a)) roadMids.set(a, []);
        if (!roadMids.has(b)) roadMids.set(b, []);
        roadMids.get(a)!.push({ x: mx, y: my });
        roadMids.get(b)!.push({ x: mx, y: my });
      }
      for (const [id, mids] of roadMids) {
        if (mids.length === 2) {
          segLine(mids[0].x, mids[0].y, mids[1].x, mids[1].y, { color: '#9AA0A6', weight: 3, opacity: 0.9 });
        } else {
          const { q, r } = idToQR(id);
          const c = hexToPixel(q, r);
          for (const m of mids) segLine(c.x, c.y, m.x, m.y, { color: '#9AA0A6', weight: 3, opacity: 0.9 });
        }
      }
      // Ford / bridge ticks across the side midpoint (like the JPG dashes).
      const tick = (a: string, b: string, color: string) => {
        const { mx, my, ex, ey } = edgeOf(a, b);
        const H = 7;
        segLine(mx - ex * H, my - ey * H, mx + ex * H, my + ey * H, { color: '#FFF', weight: 7, opacity: 0.9 });
        segLine(mx - ex * H, my - ey * H, mx + ex * H, my + ey * H, { color, weight: 4, opacity: 1 });
      };
      for (const [a, b] of fordSides) tick(a, b, '#3A3A3A');
      for (const [a, b] of bridgeSides) tick(a, b, '#000');
    }

    // ── Draw entity markers ──
    const offsets = [
      { dx: 0, dy: -8 },
      { dx: 8, dy: 4 },
      { dx: -8, dy: 4 },
      { dx: 0, dy: 10 },
      { dx: -10, dy: -4 },
      { dx: 10, dy: -4 },
    ];

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
      L.marker(toLatLng({ x, y }), { icon, interactive: false }).addTo(map)
        .bindTooltip(tooltip, { permanent: false, direction: 'top', offset: [0, -10] });
    };

    const pcHtml = (isCapital: boolean, off?: { dx: number; dy: number }) =>
      `<div style="width:${HEX_SIZE}px;height:${HEX_SIZE}px;display:flex;align-items:center;justify-content:center;font-size:18px;color:${isCapital ? '#FFD700' : '#CCC'};text-shadow:0 0 3px #000;${off ? `position:relative;left:${off.dx}px;top:${off.dy}px;` : ''}">${isCapital ? '★' : '▢'}</div>`;

    const armyHtml = (off?: { dx: number; dy: number }) =>
      `<div style="width:${HEX_SIZE}px;height:${HEX_SIZE}px;display:flex;align-items:center;justify-content:center;font-size:16px;color:#FF5555;text-shadow:0 0 3px #000;${off ? `position:relative;left:${off.dx}px;top:${off.dy}px;` : ''}">⚔</div>`;

    const charColor: Record<string, string> = {
      commander: '#5DADE2', agent: '#AF7AC5', emissary: '#48C9B0', mage: '#F5B041',
    };

    const charHtml = (type: string, off?: { dx: number; dy: number }) =>
      `<div style="width:${HEX_SIZE}px;height:${HEX_SIZE}px;display:flex;align-items:center;justify-content:center;font-size:14px;color:${charColor[type] || '#FFF'};text-shadow:0 0 3px #000;${off ? `position:relative;left:${off.dx}px;top:${off.dy}px;` : ''}">●</div>`;

    // Loose characters symbol (diamond) — shown when hex has loose chars
    const looseCharsHtml = (count: number, off?: { dx: number; dy: number }) =>
      `<div style="width:${HEX_SIZE}px;height:${HEX_SIZE}px;display:flex;align-items:center;justify-content:center;font-size:14px;color:#E0E0E0;text-shadow:0 0 3px #000;${off ? `position:relative;left:${off.dx}px;top:${off.dy}px;` : ''}">◆${count > 1 ? `<span style="font-size:9px;position:absolute;bottom:2px;right:2px;">${count}</span>` : ''}</div>`;

    // Ford / bridge badges at hex centers (below entity markers)

    for (const [hex, entities] of byHex) {
      // Separate loose characters from other entities
      const looseChars = entities.filter(e => e.type === 'character' && !e.data.armyId);
      const otherEntities = entities.filter(e => !(e.type === 'character' && !e.data.armyId));

      if (otherEntities.length === 0 && looseChars.length > 0) {
        // Only loose characters on this hex — show diamond with count
        const list = looseChars.map(e => `${e.name} (${e.detail})`).join(', ');
        addMarker(hex, looseCharsHtml(looseChars.length), `Loose characters: ${list}`);
      } else if (otherEntities.length === 1 && looseChars.length > 0) {
        // One main entity + loose chars — show entity + diamond
        const e = otherEntities[0];
        const mainTooltip = e.type === 'pc' ? `${e.name} (${e.detail})` : e.type === 'army' ? `${e.name} — ${e.detail}` : `${e.name} (${e.detail})`;
        const looseList = looseChars.map(c => c.name).join(', ');
        if (e.type === 'pc') addMarker(hex, pcHtml(e.data.isCapital), `${mainTooltip}\n◆ Loose: ${looseList}`);
        else if (e.type === 'army') addMarker(hex, armyHtml(), `${mainTooltip}\n◆ Loose: ${looseList}`);
        else addMarker(hex, charHtml(e.data.type), `${mainTooltip}\n◆ Loose: ${looseList}`);
      } else if (otherEntities.length > 0 && looseChars.length > 0) {
        // Multiple entities + loose chars — show with offsets + diamond
        otherEntities.forEach((e, i) => {
          const off = offsets[i % offsets.length];
          const tooltip = e.type === 'pc' ? `${e.name} (${e.detail})` : e.type === 'army' ? `${e.name} — ${e.detail}` : `${e.name} (${e.detail})`;
          if (e.type === 'pc') addMarker(hex, pcHtml(e.data.isCapital, off), tooltip);
          else if (e.type === 'army') addMarker(hex, armyHtml(off), tooltip);
          else addMarker(hex, charHtml(e.data.type, off), tooltip);
        });
        const looseList = looseChars.map(c => c.name).join(', ');
        const looseOff = offsets[otherEntities.length % offsets.length];
        addMarker(hex, looseCharsHtml(looseChars.length, looseOff), `Loose characters: ${looseList}`);
      } else {
        // No loose chars — original behavior
        if (entities.length === 1) {
          const e = entities[0];
          if (e.type === 'pc') addMarker(hex, pcHtml(e.data.isCapital), `${e.name} (${e.detail})`);
          else if (e.type === 'army') addMarker(hex, armyHtml(), `${e.name} — ${e.detail}`);
          else addMarker(hex, charHtml(e.data.type), `${e.name} (${e.detail})`);
        } else {
          entities.forEach((e, i) => {
            const off = offsets[i % offsets.length];
            if (e.type === 'pc') addMarker(hex, pcHtml(e.data.isCapital, off), `${e.name} (${e.detail})`);
            else if (e.type === 'army') addMarker(hex, armyHtml(off), `${e.name} — ${e.detail}`);
            else addMarker(hex, charHtml(e.data.type, off), `${e.name} (${e.detail})`);
          });
        }
      }
    }

    if (hexes.length > 0) {
      const bounds = hexes.map((hex) => toLatLng(hexToPixel(hex.q, hex.r)));
      map.fitBounds(bounds);
    }
  }, [hexes, selectedHex, hoveredHex, onHexClick, armies, characters, populationCentres, t]);

  return (
    <div className="relative">
      <div ref={mapRef} className="w-full h-[600px] bg-gray-900 rounded-lg border border-gray-700" />
      <div className="absolute bottom-4 left-4 bg-gray-800 p-3 rounded-lg border border-gray-700 text-xs space-y-1">
        <p className="font-bold text-mepbm-gold mb-2">{t('map.terrain')}</p>
        {Object.entries(TERRAIN_COLORS).map(([terrain, color]) => (
          <div key={terrain} className="flex items-center gap-2">
            <div className="w-4 h-4 rounded" style={{ backgroundColor: color }} />
            <span className="capitalize">{terrainLabel(terrain, t)}</span>
          </div>
        ))}
        <div className="border-t border-gray-700 my-2 pt-2 space-y-1">
          <p className="font-bold text-mepbm-gold">{t('map.features')}</p>
          <div className="flex items-center gap-2"><span style={{ color: '#1E5CFF' }}>━━</span><span>{t('map.majorRiver')}</span></div>
          <div className="flex items-center gap-2"><span style={{ color: '#4DA6FF' }}>──</span><span>{t('map.minorRiver')}</span></div>
          <div className="flex items-center gap-2"><span style={{ color: '#9AA0A6' }}>──</span><span>{t('map.road')}</span></div>
          <div className="flex items-center gap-2"><span className="text-gray-400">▪</span><span>{t('map.ford')}</span></div>
          <div className="flex items-center gap-2"><span className="text-black bg-white px-0.5 rounded">▬</span><span>{t('map.bridge')}</span></div>
        </div>
        <div className="border-t border-gray-700 my-2 pt-2 space-y-1">
          <p className="font-bold text-mepbm-gold">{t('map.units')}</p>
          <div className="flex items-center gap-2"><span className="text-yellow-400">★</span><span>{t('map.capital')}</span></div>
          <div className="flex items-center gap-2"><span className="text-gray-300">▢</span><span>{t('map.town')}</span></div>
          <div className="flex items-center gap-2"><span className="text-red-400">⚔</span><span>{t('map.army')}</span></div>
          <div className="flex items-center gap-2"><span className="text-white">●</span><span>{t('map.character')}</span></div>
          <div className="flex items-center gap-2"><span className="text-gray-300">◆</span><span>{t('map.loose')}</span></div>
        </div>
      </div>
      {hoveredHex && (
        <div className="absolute top-4 right-4 bg-gray-800 px-3 py-2 rounded-lg border border-gray-700 text-sm">
          {t('map.hex', { q: hoveredHex.q, r: hoveredHex.r })}
        </div>
      )}
    </div>
  );
}
