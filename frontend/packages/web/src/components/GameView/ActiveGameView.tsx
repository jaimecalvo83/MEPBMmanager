import { useState } from 'react';
import { useProcessTurn } from '../../hooks/useGameState';
import HexMap from '../Map/HexMap';
import OrdersPanel from '../Orders/OrdersPanel';
import MessagesPanel from '../Messages/MessagesPanel';
import NationPicker from '../NationPicker/NationPicker';
import RelationsTab from './tabs/RelationsTab';
import ReportsTab from './tabs/ReportsTab';
import StandingsTab from './tabs/StandingsTab';
import NationTab from './tabs/NationTab';
import CitiesTab from './tabs/CitiesTab';
import ArmiesTab from './tabs/ArmiesTab';
import CharactersTab from './tabs/CharactersTab';
import { useLang, LanguageSwitcher, sideLabel, nationName as trNation, seasonLabel, terrainLabel } from '../../i18n/lang';
import type { TabType } from './types';


// ═══════════════════════════════════════════
// ACTIVE GAME VIEW
// ═══════════════════════════════════════════
export default function ActiveGameView({ gameState, isTestAdmin, selectedNationId, setSelectedNationId, selectedHex, setSelectedHex, activeTab, setActiveTab, navigate }: any) {
  const { t, lang } = useLang();
  const nation = gameState?.nation;
  const nations = gameState?.nations || [];
  const characters = gameState?.characters || [];
  const armies = gameState?.armies || [];
  const populationCentres = gameState?.populationCentres || [];
  const hexTiles = gameState?.hexTiles || [];
  const currentTurn = gameState?.currentTurn;

  // Only admins see the nation sidebar
  const canSwitchNations = isTestAdmin || nations.length > 1;

  const gameId = gameState?.game?.id as string;
  const turns = gameState?.turns || [];
  const canProcess = gameState?.isGameAdmin === true || isTestAdmin;
  const needsNation = !!gameState?.player && !gameState.player.nationId;
  const [showNationPicker, setShowNationPicker] = useState(false);
  const processTurnMutation = useProcessTurn(gameId);

  const tabs: { key: TabType; label: string }[] = [
    { key: 'nation', label: t('game.tabNation') },
    { key: 'map', label: t('game.tabMap') },
    { key: 'cities', label: `${t('game.tabCities')} (${populationCentres.length})` },
    { key: 'armies', label: `${t('game.tabArmies')} (${armies.length})` },
    { key: 'characters', label: `${t('game.tabCharacters')} (${characters.length})` },
    { key: 'orders', label: t('game.tabOrders') },
    { key: 'messages', label: t('game.tabMessages') },
    { key: 'relations', label: t('game.tabRelations') },
    { key: 'reports', label: `${t('game.tabReports')} (${turns.length})` },
    { key: 'standings', label: t('game.tabStandings') },
  ];

  // Group nations by allegiance for sidebar
  const freeNations = nations.filter((n: any) => n.allegiance === 'free_peoples');
  const darkNations = nations.filter((n: any) => n.allegiance === 'dark_servants');
  const neutralNations = nations.filter((n: any) => n.allegiance === 'neutral');

  return (
    <div className="min-h-screen bg-gray-900 flex">
      {/* ── Nation sidebar (only for admins) ── */}
      {canSwitchNations && (
        <div className="w-56 bg-gray-800 border-r border-gray-700 flex-shrink-0 overflow-y-auto">
          <div className="p-3 border-b border-gray-700">
            <h3 className="text-xs font-bold text-mepbm-gold uppercase tracking-wider">{t('game.nations')}</h3>
          </div>
          <div className="p-2 space-y-1">
            {freeNations.length > 0 && (
              <div className="mb-2">
                <div className="px-2 py-1 text-[10px] font-bold text-green-400 uppercase tracking-wider">{t('game.freeP')}</div>
                {freeNations.map((n: any) => (
                  <button
                    key={n.id}
                    onClick={() => setSelectedNationId(n.id)}
                    className={`w-full text-left px-3 py-2 rounded text-sm flex items-center gap-2 transition ${
                      n.id === (selectedNationId || nation?.id)
                        ? 'bg-gray-700 border border-mepbm-gold text-white'
                        : 'text-gray-300 hover:bg-gray-750 hover:text-white'
                    }`}
                  >
                    <div className="w-3 h-3 rounded-full flex-shrink-0" style={{ backgroundColor: n.color }} />
                    <span className="truncate">{trNation(n.name, lang)}</span>
                  </button>
                ))}
              </div>
            )}
            {darkNations.length > 0 && (
              <div className="mb-2">
                <div className="px-2 py-1 text-[10px] font-bold text-red-400 uppercase tracking-wider">{t('game.darkP')}</div>
                {darkNations.map((n: any) => (
                  <button
                    key={n.id}
                    onClick={() => setSelectedNationId(n.id)}
                    className={`w-full text-left px-3 py-2 rounded text-sm flex items-center gap-2 transition ${
                      n.id === (selectedNationId || nation?.id)
                        ? 'bg-gray-700 border border-mepbm-gold text-white'
                        : 'text-gray-300 hover:bg-gray-750 hover:text-white'
                    }`}
                  >
                    <div className="w-3 h-3 rounded-full flex-shrink-0" style={{ backgroundColor: n.color }} />
                    <span className="truncate">{trNation(n.name, lang)}</span>
                  </button>
                ))}
              </div>
            )}
            {neutralNations.length > 0 && (
              <div className="mb-2">
                <div className="px-2 py-1 text-[10px] font-bold text-gray-400 uppercase tracking-wider">{t('game.neut')}</div>
                {neutralNations.map((n: any) => (
                  <button
                    key={n.id}
                    onClick={() => setSelectedNationId(n.id)}
                    className={`w-full text-left px-3 py-2 rounded text-sm flex items-center gap-2 transition ${
                      n.id === (selectedNationId || nation?.id)
                        ? 'bg-gray-700 border border-mepbm-gold text-white'
                        : 'text-gray-300 hover:bg-gray-750 hover:text-white'
                    }`}
                  >
                    <div className="w-3 h-3 rounded-full flex-shrink-0" style={{ backgroundColor: n.color }} />
                    <span className="truncate">{trNation(n.name, lang)}</span>
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* ── Main content ── */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top bar */}
        <div className="bg-gray-800 border-b border-gray-700 px-6 py-3 flex items-center justify-between">
          <div className="flex items-center gap-4">
            <button onClick={() => navigate('/')} className="text-mepbm-gold hover:text-yellow-400 text-sm font-semibold">
              {t('game.games')}
            </button>
            <h1 className="text-lg font-bold text-white">{gameState?.game?.name}</h1>
            {currentTurn && (
              <span className="text-sm text-gray-400">
                {t('game.turnLine', { n: currentTurn.number, s: seasonLabel(currentTurn.season, t), d: new Date(currentTurn.deadline).toLocaleDateString(lang === 'es' ? 'es-ES' : 'en-US') })}
              </span>
            )}
          </div>
          <div className="flex items-center gap-3">
            <LanguageSwitcher small />
            {canProcess && (
              <div className="flex items-center gap-2">
                <button
                  onClick={() => processTurnMutation.mutate()}
                  disabled={processTurnMutation.isLoading}
                  className="px-4 py-2 bg-red-600 text-white text-sm font-bold rounded hover:bg-red-500 transition disabled:opacity-50"
                >
                  {processTurnMutation.isLoading ? t('game.processing') : t('game.process')}
                </button>
                {processTurnMutation.isError && (
                  <span className="text-red-400 text-xs">
                    {(processTurnMutation.error as any)?.response?.data?.error || t('game.procFailed')}
                  </span>
                )}
                {processTurnMutation.isSuccess && (
                  <span className="text-green-400 text-xs">{t('game.procDone')}</span>
                )}
              </div>
            )}
            {nation && (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 rounded" style={{ backgroundColor: nation.color }} />
                <span className="font-bold text-white">{trNation(nation.name, lang)}</span>
                <span className="text-sm text-gray-400">({sideLabel(nation.allegiance, t)})</span>
              </div>
            )}
          </div>
        </div>

        {needsNation && (
          <div className="bg-gray-800 border-b border-yellow-600 px-6 py-3 flex items-center gap-4">
            <span className="text-sm text-yellow-300">{t('game.noNation')}</span>
            <button
              onClick={() => setShowNationPicker(true)}
              className="px-4 py-2 bg-blue-600 text-white text-sm font-bold rounded hover:bg-blue-500 transition"
            >
              {t('game.chooseNation')}
            </button>
          </div>
        )}
        {showNationPicker && (
          <NationPicker
            gameId={gameId}
            mode="update"
            onClose={() => setShowNationPicker(false)}
            onJoined={() => setShowNationPicker(false)}
          />
        )}

        {/* Nation resources bar */}
        {nation && (
          <div className="bg-gray-800 border-b border-gray-700 px-6 py-2 flex gap-6 text-sm">
            <span className="text-yellow-400">{t('game.resGold')}: {nation.gold}</span>
            <span className="text-green-400">{t('game.resFood')}: {nation.food}</span>
            <span className="text-amber-600">{t('game.resTimber')}: {nation.timber}</span>
            <span className="text-orange-400">{t('game.resLeather')}: {nation.leather}</span>
            <span className="text-gray-300">{t('game.resBronze')}: {nation.bronze}</span>
            <span className="text-blue-300">{t('game.resSteel')}: {nation.steel}</span>
            {nation.mithril > 0 && <span className="text-purple-400">{t('game.resMithril')}: {nation.mithril}</span>}
            <span className="text-emerald-400">{t('game.resMounts')}: {nation.mounts}</span>
            <span className="text-gray-500">{t('game.resTax')}: {nation.taxRate}%</span>
          </div>
        )}

        {/* Tab bar */}
        <div className="bg-gray-800 border-b border-gray-700 px-6 flex gap-1 overflow-x-auto">
          {tabs.map(({ key, label }) => (
            <button
              key={key}
              onClick={() => setActiveTab(key)}
              className={`px-4 py-2 text-sm font-semibold rounded-t transition whitespace-nowrap ${
                activeTab === key
                  ? 'bg-gray-900 text-mepbm-gold border-t border-x border-gray-700'
                  : 'text-gray-400 hover:text-white'
              }`}
            >
              {label}
            </button>
          ))}
        </div>

        {/* Tab content */}
        <div className="p-6 flex-1 overflow-y-auto">
          {activeTab === 'nation' && (
            <NationTab
              nation={nation}
              populationCentres={populationCentres}
              armies={armies}
              characters={characters}
              currentTurn={currentTurn}
            />
          )}
          {activeTab === 'map' && (
            <div className="space-y-4">
              <HexMap
                hexes={hexTiles}
                armies={armies}
                characters={characters}
                populationCentres={populationCentres}
                selectedHex={selectedHex}
                onHexClick={(q: number, r: number) => setSelectedHex({ q, r })}
              />

              {selectedHex && (
                <div className="bg-gray-800 rounded-lg p-4 border border-gray-700">
                  <h3 className="text-sm font-bold text-mepbm-gold mb-2">
                    {t('map.hex', { q: selectedHex.q, r: selectedHex.r })}
                  </h3>
                  <div className="text-sm text-gray-400">
                    {(() => {
                      const hex = hexTiles.find((h: any) => h.q === selectedHex.q && h.r === selectedHex.r);
                      if (!hex) return <span>{t('map.noData')}</span>;
                      const feats = [
                        hex.hasMajorRiver ? t('map.majorRiver') : null,
                        hex.hasMinorRiver ? t('map.minorRiver') : null,
                        hex.hasRoad ? t('map.road') : null,
                        hex.hasFord ? t('map.ford') : null,
                        hex.hasBridge ? t('map.bridge') : null,
                      ].filter(Boolean);
                      return <span>{t('map.terrainLine', { t: terrainLabel(hex.terrain, t), f: feats.length > 0 ? ` (${feats.join(' · ')})` : '' })}</span>;
                    })()}
                    {(() => {
                      const pc = populationCentres.find((p: any) => p.locationHex === `${selectedHex.q},${selectedHex.r}`);
                      return pc ? <span className="ml-4 text-mepbm-gold">★ {pc.name}{pc.isCapital ? ` ${t('map.capitalSuffix')}` : ''}</span> : null;
                    })()}
                    {(() => {
                      const army = armies.find((a: any) => a.locationHex === `${selectedHex.q},${selectedHex.r}`);
                      return army ? <span className="ml-4 text-red-400">⚔ {army.name}</span> : null;
                    })()}
                  </div>
                </div>
              )}
            </div>
          )}

          {activeTab === 'cities' && (
            <CitiesTab populationCentres={populationCentres} hexTiles={hexTiles} taxRate={nation?.taxRate} />
          )}

          {activeTab === 'armies' && (
            <ArmiesTab armies={armies} characters={characters} populationCentres={populationCentres} hexTiles={hexTiles} nationName={nation?.name} />
          )}

          {activeTab === 'characters' && (
            <CharactersTab characters={characters} armies={armies} populationCentres={populationCentres} nationName={nation?.name} nations={nations} />
          )}

          {activeTab === 'orders' && (
            <OrdersPanel gameId={gameState?.game?.id} characters={characters} />
          )}

          {activeTab === 'messages' && (
            <MessagesPanel gameId={gameState?.game?.id} />
          )}

          {activeTab === 'relations' && (
            <RelationsTab
              nationId={selectedNationId || nation?.id}
              nationName={nation?.name}
              allNations={gameState?.allNations || []}
              relations={gameState?.relations || []}
            />
          )}

          {activeTab === 'reports' && (
            <ReportsTab
              gameId={gameState?.game?.id}
              turns={gameState?.turns || []}
            />
          )}

          {activeTab === 'standings' && (
            <StandingsTab
              allNations={gameState?.allNations || []}
            />
          )}
        </div>
      </div>
    </div>
  );
}
