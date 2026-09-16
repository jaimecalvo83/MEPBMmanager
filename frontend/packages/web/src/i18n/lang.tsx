import { createContext, useContext, useState, type ReactNode } from 'react';

export type Lang = 'en' | 'es';

// ── English (default) ──
const en = {
  // common
  'common.cancel': 'Cancel',
  'common.remove': 'Remove',
  'common.add': 'Add',
  'common.loading': 'Loading...',
  // auth
  'auth.subtitle': 'Middle-earth Play By Mail',
  'auth.email': 'Email',
  'auth.password': 'Password',
  'auth.passwordMin': 'Password (min 6 chars)',
  'auth.username': 'Username',
  'auth.login': 'Login',
  'auth.loggingIn': 'Logging in...',
  'auth.loginFailed': 'Login failed',
  'auth.noAccount': "Don't have an account?",
  'auth.register': 'Register',
  'auth.createAccount': 'Create Account',
  'auth.regFailed': 'Registration failed',
  'auth.creating': 'Creating account...',
  'auth.haveAccount': 'Already have an account?',
  'auth.logout': 'Logout',
  // dashboard
  'dash.myGames': 'My Games',
  'dash.createGame': 'Create Game',
  'dash.gameName': 'Game name',
  'dash.addPlayers': 'Add players by email (required)',
  'dash.admin': 'Admin',
  'dash.counts': '{n} player(s) added, {m}/{max} admin(s) selected',
  'dash.maxReached': '(max reached)',
  'dash.admins': 'Admins:',
  'dash.creating': 'Creating...',
  'dash.loading': 'Loading games...',
  'dash.noGames': 'No games yet',
  'dash.noGamesSub': 'Create a new game to get started',
  'dash.scenario': 'Scenario:',
  'dash.status': 'Status:',
  'dash.turn': 'Turn:',
  'dash.players': 'Players:',
  'dash.joinGame': 'Join Game',
  'dash.enterGame': 'Enter Game',
  'dash.deleteGame': 'Delete game',
  'dash.confirmDelete': 'Are you sure you want to delete this game?',
  'dash.adding': 'Adding...',
  // nation picker
  'picker.selectNation': 'Select Your Nation',
  'picker.chooseNation': 'Choose Your Nation',
  'picker.loading': 'Loading nations...',
  'picker.none': 'No nations available',
  'picker.failed': 'Failed to join game',
  'picker.joining': 'Joining...',
  'picker.selectBtn': 'Select Nation',
  'picker.joinBtn': 'Join Game',
  // messages
  'msg.title': 'Messages',
  'msg.compose': 'Compose',
  'msg.subject': 'Subject',
  'msg.contentPh': 'Message content...',
  'msg.sending': 'Sending...',
  'msg.send': 'Send',
  'msg.loading': 'Loading messages...',
  'msg.none': 'No messages yet',
  // map legend + hex
  'map.terrain': 'Terrain',
  'map.features': 'Features',
  'map.majorRiver': 'Major river',
  'map.minorRiver': 'Minor river',
  'map.road': 'Road',
  'map.ford': 'Ford',
  'map.bridge': 'Bridge',
  'map.units': 'Units',
  'map.capital': 'Capital',
  'map.town': 'Town',
  'map.army': 'Army',
  'map.character': 'Character',
  'map.loose': 'Loose chars',
  'map.hex': 'Hex: {q}, {r}',
  'map.noData': 'No data',
  'map.terrainLine': 'Terrain: {t}{f}',
  'map.capitalSuffix': '(Capital)',
  // game setup
  'setup.loading': 'Loading game...',
  'setup.gameTitle': 'Game: ',
  'setup.statusWaiting': 'Status: {s} | Waiting for players...',
  'setup.title': 'Game Setup',
  'setup.adminsPending': '{n} admin(s) pending. ',
  'setup.playersPending': '{n} player(s) pending. ',
  'setup.allConfirmed': 'All confirmed! ',
  'setup.needOne': 'Need at least 1 participant. ',
  'setup.ready': 'Ready to start!',
  'setup.notReady': 'Not ready yet.',
  'setup.brackets': '2950 brackets (free + dark + neutral): 6-10 → 4+4+2 · 11-15 → 6+6+3 · 16-20 → 8+8+4 · 21-25 → 10+10+5. Surplus nations play as NPCs (max 4, neutrals first) on a cropped map and can join later.',
  'setup.start': 'Start Game',
  'setup.starting': 'Starting...',
  'setup.startFailed': 'Failed to start game',
  'setup.addPlayer': 'Add Player',
  'setup.addFailed': 'Failed to add player',
  'setup.invited': 'You are invited to this game',
  'setup.chooseWith': 'Choose a player you want to play with (optional):',
  'setup.noPref': 'No preference',
  'setup.confirming': 'Confirming...',
  'setup.confirmAtt': 'Confirm Attendance',
  'setup.confirmedAtt': 'You have confirmed your attendance',
  'setup.playingWith': 'Playing with: {x}',
  'setup.invitedAdmin': 'You are invited as game admin',
  'setup.adminMsg': 'Accept the admin role so the game can be started. Nations are assigned when the game starts.',
  'setup.accepting': 'Accepting...',
  'setup.acceptAdmin': 'Accept Admin Role',
  'setup.acceptFailed': 'Failed to accept admin role',
  'setup.participants': 'Participants ({c} confirmed / {t} total)',
  'setup.thName': 'Name',
  'setup.thRole': 'Role',
  'setup.thStatus': 'Status',
  'setup.thPlayingWith': 'Playing With',
  'setup.thActions': 'Actions',
  'setup.roleBoth': 'Player + Admin',
  'setup.roleAdmin': 'Admin',
  'setup.rolePlayer': 'Player',
  'setup.confirmed': 'Confirmed',
  'setup.pending': 'Pending',
  'setup.confirmRemove': 'Remove {x}?',
  'setup.back': 'Back to Games',
  // active game
  'game.nations': 'Nations',
  'game.freeP': 'Free Peoples',
  'game.darkP': 'Dark Servants',
  'game.neut': 'Neutral',
  'game.games': '← Games',
  'game.turnLine': 'Turn {n} · {s} · Deadline: {d}',
  'game.turnShort': 'Turn {n}{s}',
  'game.processing': 'Processing...',
  'game.process': 'Process Turn',
  'game.procFailed': 'Failed',
  'game.procDone': 'Turn processed',
  'game.noNation': 'You have no nation yet. Claim one of the free nations:',
  'game.chooseNation': 'Choose Your Nation',
  'game.resGold': 'Gold',
  'game.resFood': 'Food',
  'game.resTimber': 'Timber',
  'game.resLeather': 'Leather',
  'game.resBronze': 'Bronze',
  'game.resSteel': 'Steel',
  'game.resMithril': 'Mithril',
  'game.resMounts': 'Mounts',
  'game.resTax': 'Tax',
  'game.tabNation': 'Nation',
  'game.tabMap': 'Map',
  'game.tabCities': 'Cities',
  'game.tabArmies': 'Armies',
  'game.tabCharacters': 'Characters',
  'game.tabOrders': 'Orders',
  'game.tabMessages': 'Messages',
  'game.tabRelations': 'Relations',
  'game.tabReports': 'Reports',
  'game.tabStandings': 'Standings',
  // relations
  'rel.ally': 'Ally',
  'rel.tolerant': 'Tolerant',
  'rel.neutral': 'Neutral',
  'rel.hostile': 'Hostile',
  'rel.enemy': 'Enemy',
  'rel.selectNation': 'Select a nation to view its relations.',
  'rel.introA': 'Relations of ',
  'rel.introB': '. Level > 0 (tolerant or ally) lets your armies pass; 0 or less blocks them.',
  'rel.thNation': 'Nation',
  'rel.thSide': 'Side',
  'rel.thRelation': 'Relation',
  'rel.thChange': 'Change to',
  'rel.saving': 'Saving…',
  // reports
  'rep.noTurns': 'No turns yet. Reports appear after the first turn is processed.',
  'rep.turnBtn': 'Turn {n} · {s} ({st})',
  'rep.loading': 'Loading report...',
  'rep.loadFail': 'Failed to load the turn report.',
  'rep.noResults': 'No results recorded for this turn yet.',
  // standings
  'stand.none': 'No nations in this game.',
  'stand.intro': 'Turn victory points per nation (recalculated each turn from areas of play, not cumulative).',
  'stand.elimNote': 'Eliminated nations are ranked but cannot win.',
  'stand.eliminated': 'eliminated',
  'stand.vp': 'VP',
  // nation tab
  'nation.none': 'No nation selected.',
  'nation.cities': 'Cities',
  'nation.armies': 'Armies',
  'nation.characters': 'Characters',
  'nation.taxRate': 'Tax rate',
  'nation.vp': 'Victory points',
  'nation.ws': 'Warship strength',
  'nation.capitalLine': '★ Capital: {x} @ {h}',
  'nation.abilities': 'Special Nation Abilities',
  // cities
  'city.none': 'No population centres visible.',
  'city.loc': 'Location: @ {h} in {t}',
  'city.size': 'Size:',
  'city.fort': 'Fortifications:',
  'city.loyalty': 'Loyalty:',
  'city.docks': 'Docks:',
  'city.port': 'Port',
  'city.harbour': 'Harbour',
  'city.none2': 'None',
  'city.hidden': 'Hidden?:',
  'city.yes': 'Yes',
  'city.no': 'No',
  'city.sieged': 'Sieged?:',
  'city.tax': 'Tax:',
  'city.mined': 'Mined gold:',
  'city.resTitle': 'Resources (turn)',
  'city.prod': 'Prod:',
  'city.stores': 'Stores:',
  // armies
  'army.none': 'No armies visible.',
  'army.troopsWord': '{n} troops',
  'army.cmdUpkeep': 'Command & Upkeep',
  'army.morale': 'Morale',
  'army.trAvg': 'Training (avg)',
  'army.foodTurn': 'Food/turn',
  'army.goldTurn': 'Gold/turn',
  'army.troopsSuffix': '({n} troops)',
  'army.train': 'Baggage Train',
  'army.food': 'Food',
  'army.turnsSuffix': '({n} turns)',
  'army.wm': 'War Machines',
  'army.sw': 'Spare Weapons',
  'army.sa': 'Spare Armour',
  'army.troopsTitle': 'Troops by type, weapon & armour',
  'army.thType': 'Type',
  'army.thWeapon': 'Weapon',
  'army.thArmour': 'Armour',
  'army.thTraining': 'Training',
  'army.charsTitle': 'Characters',
  'army.noCommander': 'No commander assigned.',
  'army.commanderMark': ' (commander)',
  'army.cmdSkills': 'Command {c} · Agent {a} · Emissary {e} · Mage {m}',
  'army.health': 'Health {h}',
  'army.trHC': 'Heavy Cavalry',
  'army.trLC': 'Light Cavalry',
  'army.trHI': 'Heavy Infantry',
  'army.trLI': 'Light Infantry',
  'army.trAR': 'Archers',
  'army.trMA': 'Men-at-Arms',
  'army.matMithril': 'mithril',
  'army.matSteel': 'steel',
  'army.matBronze': 'bronze',
  'army.matLeather': 'leather',
  'army.matWood': 'wood',
  'army.matNone': 'none',
  'army.tipStr': 'Strength {a} · Constitution {b}',
  'army.tipUpkeep': 'Upkeep: {g} gold + {f} food/turn ({pg}g + {pf}f each)',
  'army.tipTactic': 'Best tactic {b} · worst {w}',
  'army.tipTerrain': 'Terrain: {t}',
  'army.tipWeapons': 'Weapons: {m} ({r})',
  'army.tipArmour': 'Armour: {m} ({r})',
  'army.tipTraining': 'Training: {t}',
  'army.terrPlains': 'plains',
  'army.terrDesert': 'desert',
  'army.terrCoast': 'coast',
  'army.terrHills': 'hills',
  'army.terrMountains': 'mountains',
  'army.terrForest': 'forest',
  // characters
  'char.none': 'No characters visible.',
  'char.champion': 'Champion',
  'char.dead': 'Dead',
  'char.kidnapped': 'Kidnapped',
  'char.skills': 'Skills',
  'char.command': 'Command',
  'char.agent': 'Agent',
  'char.emissary': 'Emissary',
  'char.mage': 'Mage',
  'char.status': 'Status',
  'char.health': 'Health',
  'char.stealth': 'Stealth',
  'char.challenge': 'Challenge',
  'char.artifacts': 'Artifacts',
  'char.spells': 'Spells',
  'char.pcSentence': 'The {size}{fort} of {name} flying the flag of {owner} is here.',
  'char.artBonus': 'Bonus +{x}',
  'char.artAlignment': 'Alignment: {x}',
  'char.artLocation': 'Location: {x}',
  'char.artNation': 'Nation: {x}',
  'char.artHolder': 'Held by: {x}',
  'char.spMinRank': 'min rank {x}',
  'char.spRank': 'rank {x}',
  'char.spPrereq': 'Prerequisites: {x}',
  'char.spReqInfo': 'Required info: {x}',
  'char.cmdArmy': '{n} commands an army at {h}.',
  'char.atHex': '{n} is currently at {h}.',
  // orders
  'ord.title': 'Orders',
  'ord.validate': 'Validate All',
  'ord.validating': 'Validating…',
  'ord.none': 'No characters visible.',
  'ord.available': '{n} available',
  'ord.selectOrder': 'Select order…',
  'ord.loading': 'Loading order info…',
  'ord.cost': 'Cost: ',
  'ord.expected': 'expected +{x} gold',
  'ord.max': '(max {x})',
  'ord.moved': 'After 1st order → {x}',
  'ord.submit': 'Submit {slot}',
  'ord.submitting': 'Submitting…',
  'ord.submitFail': 'Failed to submit order',
  'ord.slot1': '1st order',
  'ord.slot2': '2nd order (conditioned by the 1st)',
  'ord.ord1': '1st:',
  'ord.ord2': '2nd:',
  'ord.twoSubmitted': 'Two orders submitted. Cancel one to change it.',
  'ord.ordersCount': '{n}/2 orders',
  'ord.selectPh': 'Select…',
  'ord.maxBtn': 'max {x}',
  'ord.maxTitle': 'Use maximum possible',
  'ord.noMatches': 'No matches',
  'ord.searchPh': 'Type to search…',
  'ord.clearTitle': 'Clear',
  'ord.eligFail': 'Could not load eligible orders',
  'ord.yes': 'yes',
  'ord.no': 'no',
  'ord.tipType': 'Type',
  'ord.tipDifficulty': 'Difficulty',
  'ord.tipPrereq': 'Prerequisites',
  'ord.tipReqInfo': 'Required information',
  'ord.tipDesc': 'Description',
  'ord.typeGeneral': 'General',
  'ord.typeCommand': 'Command',
  'ord.typeAgent': 'Agent',
  'ord.typeEmissary': 'Emissary',
  'ord.typeMage': 'Mage',
  'ord.rForceCommander': 'Force commander',
  'ord.rCompany': 'In a company',
  'ord.rFourthAge': 'Fourth Age only',
  'ord.rKingdom': 'Kingdom',
  'ord.rCapital': 'At capital',
  'ord.pCapital': 'At your capital',
  'ord.pCurrentCapital': 'At your current capital',
  'ord.pArmy': 'In an army',
  'ord.pNavy': 'Command a navy',
  'ord.pNavyNation': 'Nation owns a navy',
  'ord.pCompany': 'In a company',
  'ord.pOwnPc': 'At one of your population centres',
  'ord.pLand': 'On land',
  'ord.pCombatArt': 'Held combat artifact',
  'ord.pHeldArt': 'Held artifact',
  'ord.pHeal': 'Known healing spell',
  'ord.pCombatSpell': 'Known combat spell',
  'ord.pConjuring': 'Known conjuring spell',
  'ord.pMovement': 'Known movement spell',
  'ord.pLore': 'Known lore spell',
  'ord.skillPlus': ' +skill',
  'ord.diffAutomatic': 'automatic',
  'ord.diffEasy': 'easy',
  'ord.diffAverage': 'average',
  'ord.diffHard': 'hard',
  'ord.diffVaries': 'varies',
  'data.typeCommander': 'Commander',
  'data.typeAgent': 'Agent',
  'data.typeEmissary': 'Emissary',
  'data.typeMage': 'Mage',
  'data.sizeCamp': 'Camp',
  'data.sizeVillage': 'Village',
  'data.sizeTown': 'Town',
  'data.sizeMajorTown': 'Major town',
  'data.sizeCity': 'City',
  'data.sizeFortress': 'Fortress',
  'data.sizeCitadel': 'Citadel',
  'data.fortTower': 'Tower',
  'data.fortFort': 'Fort',
  'data.fortCastle': 'Castle',
  'data.fortKeep': 'Keep',
  'data.fortCitadel': 'Citadel',
  'data.fortPalisade': 'Palisade',
  'data.fortStoneWalls': 'Stone walls',
  'data.fortWalls': 'Walls',
  'data.fortFortress': 'Fortress',
  'data.fortCitadelWalls': 'Citadel walls',
  'data.seasonSpring': 'Spring',
  'data.seasonSummer': 'Summer',
  'data.seasonAutumn': 'Autumn',
  'data.seasonWinter': 'Winter',
  'data.stActive': 'Active',
  'data.stSetup': 'Setup',
  'data.stOrdersOpen': 'Orders open',
  'data.stProcessing': 'Processing',
  'data.stCompleted': 'Completed',
  'data.terrPlains': 'Plains',
  'data.terrForest': 'Forest',
  'data.terrMountains': 'Mountains',
  'data.terrRough': 'Rough',
  'data.terrDesert': 'Desert',
  'data.terrSwamp': 'Swamp',
  'data.terrShore': 'Shore',
  'data.terrCoastal': 'Coastal',
  'data.terrWater': 'Water',
  'data.terrOcean': 'Ocean',
  'data.terrHills': 'Hills',
  'data.terrRiver': 'River',
  'data.terrCoast': 'Coast',
  'data.terrMarsh': 'Marsh',
  'data.terrSea': 'Sea',
  'data.roleTestAdmin': 'Test Admin',
  'data.alNone': 'none',
  'data.alNeutral': 'Neutral',
  'data.alGood': 'Good',
  'data.alEvil': 'Evil',
};

export type DictKey = keyof typeof en;

// ── Spanish ──
const es: Record<DictKey, string> = {
  'common.cancel': 'Cancelar',
  'common.remove': 'Quitar',
  'common.add': 'Añadir',
  'common.loading': 'Cargando...',
  'auth.subtitle': 'Middle-earth Play By Mail',
  'auth.email': 'Correo',
  'auth.password': 'Contraseña',
  'auth.passwordMin': 'Contraseña (mín 6 caracteres)',
  'auth.username': 'Usuario',
  'auth.login': 'Entrar',
  'auth.loggingIn': 'Entrando...',
  'auth.loginFailed': 'Error al entrar',
  'auth.noAccount': '¿No tienes cuenta?',
  'auth.register': 'Registrarse',
  'auth.createAccount': 'Crear cuenta',
  'auth.regFailed': 'Error al registrar',
  'auth.creating': 'Creando cuenta...',
  'auth.haveAccount': '¿Ya tienes cuenta?',
  'auth.logout': 'Salir',
  'dash.myGames': 'Mis partidas',
  'dash.createGame': 'Crear partida',
  'dash.gameName': 'Nombre de la partida',
  'dash.addPlayers': 'Añadir jugadores por correo (obligatorio)',
  'dash.admin': 'Admin',
  'dash.counts': '{n} jugador(es), {m}/{max} admin(s)',
  'dash.maxReached': '(máximo alcanzado)',
  'dash.admins': 'Admins:',
  'dash.creating': 'Creando...',
  'dash.loading': 'Cargando partidas...',
  'dash.noGames': 'Sin partidas',
  'dash.noGamesSub': 'Crea una partida para empezar',
  'dash.scenario': 'Escenario:',
  'dash.status': 'Estado:',
  'dash.turn': 'Turno:',
  'dash.players': 'Jugadores:',
  'dash.joinGame': 'Unirse',
  'dash.enterGame': 'Entrar',
  'dash.deleteGame': 'Borrar partida',
  'dash.confirmDelete': '¿Seguro que quieres borrar la partida?',
  'dash.adding': 'Añadiendo...',
  'picker.selectNation': 'Elige tu nación',
  'picker.chooseNation': 'Elige tu nación',
  'picker.loading': 'Cargando naciones...',
  'picker.none': 'No hay naciones disponibles',
  'picker.failed': 'No se pudo unir a la partida',
  'picker.joining': 'Uniendo...',
  'picker.selectBtn': 'Elegir nación',
  'picker.joinBtn': 'Unirse',
  'msg.title': 'Mensajes',
  'msg.compose': 'Redactar',
  'msg.subject': 'Asunto',
  'msg.contentPh': 'Contenido...',
  'msg.sending': 'Enviando...',
  'msg.send': 'Enviar',
  'msg.loading': 'Cargando mensajes...',
  'msg.none': 'Sin mensajes',
  'map.terrain': 'Terreno',
  'map.features': 'Elementos',
  'map.majorRiver': 'Río mayor',
  'map.minorRiver': 'Río menor',
  'map.road': 'Camino',
  'map.ford': 'Vado',
  'map.bridge': 'Puente',
  'map.units': 'Unidades',
  'map.capital': 'Capital',
  'map.town': 'Ciudad',
  'map.army': 'Ejército',
  'map.character': 'Personaje',
  'map.loose': 'Sueltos',
  'map.hex': 'Hex: {q}, {r}',
  'map.noData': 'Sin datos',
  'map.terrainLine': 'Terreno: {t}{f}',
  'map.capitalSuffix': '(Capital)',
  'setup.loading': 'Cargando partida...',
  'setup.gameTitle': 'Partida: ',
  'setup.statusWaiting': 'Estado: {s} | Esperando jugadores...',
  'setup.title': 'Preparar partida',
  'setup.adminsPending': '{n} admin(s) pendientes. ',
  'setup.playersPending': '{n} jugador(es) pendientes. ',
  'setup.allConfirmed': '¡Todos confirmados! ',
  'setup.needOne': 'Hace falta 1 participante como mínimo. ',
  'setup.ready': '¡Listo para empezar!',
  'setup.notReady': 'Aún no listo.',
  'setup.brackets': 'Grupos 2950 (libres + oscuros + neutrales): 6-10 → 4+4+2 · 11-15 → 6+6+3 · 16-20 → 8+8+4 · 21-25 → 10+10+5. Las naciones sobrantes juegan como PNJ (máx 4, neutrales primero) en mapa recortado y pueden unirse luego.',
  'setup.start': 'Empezar',
  'setup.starting': 'Empezando...',
  'setup.startFailed': 'No se pudo empezar',
  'setup.addPlayer': 'Añadir jugador',
  'setup.addFailed': 'No se pudo añadir',
  'setup.invited': 'Estás invitado a esta partida',
  'setup.chooseWith': 'Elige con quién quieres jugar (opcional):',
  'setup.noPref': 'Sin preferencia',
  'setup.confirming': 'Confirmando...',
  'setup.confirmAtt': 'Confirmar asistencia',
  'setup.confirmedAtt': 'Has confirmado tu asistencia',
  'setup.playingWith': 'Juegas con: {x}',
  'setup.invitedAdmin': 'Estás invitado como admin',
  'setup.adminMsg': 'Acepta el rol de admin para poder empezar. Las naciones se asignan al empezar.',
  'setup.accepting': 'Aceptando...',
  'setup.acceptAdmin': 'Aceptar rol de admin',
  'setup.acceptFailed': 'No se pudo aceptar',
  'setup.participants': 'Participantes ({c} confirmados / {t} en total)',
  'setup.thName': 'Nombre',
  'setup.thRole': 'Rol',
  'setup.thStatus': 'Estado',
  'setup.thPlayingWith': 'Juega con',
  'setup.thActions': 'Acciones',
  'setup.roleBoth': 'Jugador + Admin',
  'setup.roleAdmin': 'Admin',
  'setup.rolePlayer': 'Jugador',
  'setup.confirmed': 'Confirmado',
  'setup.pending': 'Pendiente',
  'setup.confirmRemove': '¿Quitar a {x}?',
  'setup.back': 'Volver',
  'game.nations': 'Naciones',
  'game.freeP': 'Pueblos Libres',
  'game.darkP': 'Sirvientes Oscuros',
  'game.neut': 'Neutral',
  'game.games': '← Partidas',
  'game.turnLine': 'Turno {n} · {s} · Límite: {d}',
  'game.turnShort': 'Turno {n}{s}',
  'game.processing': 'Procesando...',
  'game.process': 'Procesar turno',
  'game.procFailed': 'Falló',
  'game.procDone': 'Turno procesado',
  'game.noNation': 'Aún no tienes nación. Reclama una libre:',
  'game.chooseNation': 'Elige tu nación',
  'game.resGold': 'Oro',
  'game.resFood': 'Comida',
  'game.resTimber': 'Madera',
  'game.resLeather': 'Cuero',
  'game.resBronze': 'Bronce',
  'game.resSteel': 'Acero',
  'game.resMithril': 'Mitril',
  'game.resMounts': 'Monturas',
  'game.resTax': 'Tasa',
  'game.tabNation': 'Nación',
  'game.tabMap': 'Mapa',
  'game.tabCities': 'Ciudades',
  'game.tabArmies': 'Ejércitos',
  'game.tabCharacters': 'Personajes',
  'game.tabOrders': 'Órdenes',
  'game.tabMessages': 'Mensajes',
  'game.tabRelations': 'Relaciones',
  'game.tabReports': 'Informes',
  'game.tabStandings': 'Clasificación',
  'rel.ally': 'Aliado',
  'rel.tolerant': 'Tolerante',
  'rel.neutral': 'Neutral',
  'rel.hostile': 'Hostil',
  'rel.enemy': 'Enemigo',
  'rel.selectNation': 'Selecciona una nación para ver sus relaciones.',
  'rel.introA': 'Relaciones de ',
  'rel.introB': '. Nivel > 0 (tolerante o aliado) deja pasar a tus ejércitos; 0 o menos lo bloquea.',
  'rel.thNation': 'Nación',
  'rel.thSide': 'Bando',
  'rel.thRelation': 'Relación',
  'rel.thChange': 'Cambiar a',
  'rel.saving': 'Guardando…',
  'rep.noTurns': 'Sin turnos. Los informes salen tras procesar el primero.',
  'rep.turnBtn': 'Turno {n} · {s} ({st})',
  'rep.loading': 'Cargando informe...',
  'rep.loadFail': 'No se pudo cargar el informe.',
  'rep.noResults': 'Sin resultados para este turno.',
  'stand.none': 'Sin naciones en la partida.',
  'stand.intro': 'Puntos de victoria por nación (recalculados cada turno, no acumulativos).',
  'stand.elimNote': 'Las eliminadas salen pero no pueden ganar.',
  'stand.eliminated': 'eliminada',
  'stand.vp': 'PV',
  'nation.none': 'Sin nación.',
  'nation.cities': 'Ciudades',
  'nation.armies': 'Ejércitos',
  'nation.characters': 'Personajes',
  'nation.taxRate': 'Tasa',
  'nation.vp': 'Puntos de victoria',
  'nation.ws': 'Fuerza naval',
  'nation.capitalLine': '★ Capital: {x} @ {h}',
  'nation.abilities': 'Habilidades especiales',
  'city.none': 'Sin centros de población.',
  'city.loc': 'Lugar: @ {h} en {t}',
  'city.size': 'Tamaño:',
  'city.fort': 'Fortificaciones:',
  'city.loyalty': 'Lealtad:',
  'city.docks': 'Diques:',
  'city.port': 'Puerto',
  'city.harbour': 'Dársena',
  'city.none2': 'Ninguno',
  'city.hidden': '¿Oculto?:',
  'city.yes': 'Sí',
  'city.no': 'No',
  'city.sieged': '¿Asediado?:',
  'city.tax': 'Tasa:',
  'city.mined': 'Oro minado:',
  'city.resTitle': 'Recursos (turno)',
  'city.prod': 'Prod.:',
  'city.stores': 'Almacén:',
  'army.none': 'Sin ejércitos.',
  'army.troopsWord': '{n} tropas',
  'army.cmdUpkeep': 'Mando y consumo',
  'army.morale': 'Moral',
  'army.trAvg': 'Instrucción (media)',
  'army.foodTurn': 'Comida/turno',
  'army.goldTurn': 'Oro/turno',
  'army.troopsSuffix': '({n} tropas)',
  'army.train': 'Tren',
  'army.food': 'Comida',
  'army.turnsSuffix': '({n} turnos)',
  'army.wm': 'Máquinas',
  'army.sw': 'Armas repuesto',
  'army.sa': 'Armaduras repuesto',
  'army.troopsTitle': 'Tropas por tipo, arma y armadura',
  'army.thType': 'Tipo',
  'army.thWeapon': 'Arma',
  'army.thArmour': 'Armadura',
  'army.thTraining': 'Instrucción',
  'army.charsTitle': 'Personajes',
  'army.noCommander': 'Sin comandante.',
  'army.commanderMark': ' (comandante)',
  'army.cmdSkills': 'Mando {c} · Agente {a} · Emisario {e} · Mago {m}',
  'army.health': 'Salud {h}',
  'army.trHC': 'Caballería pesada',
  'army.trLC': 'Caballería ligera',
  'army.trHI': 'Infantería pesada',
  'army.trLI': 'Infantería ligera',
  'army.trAR': 'Arqueros',
  'army.trMA': 'Hombres de armas',
  'army.matMithril': 'mitril',
  'army.matSteel': 'acero',
  'army.matBronze': 'bronce',
  'army.matLeather': 'cuero',
  'army.matWood': 'madera',
  'army.matNone': 'ninguno',
  'army.tipStr': 'Fuerza {a} · Constitución {b}',
  'army.tipUpkeep': 'Coste: {g} oro + {f} comida/turno ({pg}o + {pf}c c/u)',
  'army.tipTactic': 'Mejor táctica {b} · peor {w}',
  'army.tipTerrain': 'Terreno: {t}',
  'army.tipWeapons': 'Armas: {m} ({r})',
  'army.tipArmour': 'Armadura: {m} ({r})',
  'army.tipTraining': 'Instrucción: {t}',
  'army.terrPlains': 'llanos',
  'army.terrDesert': 'desierto',
  'army.terrCoast': 'costa',
  'army.terrHills': 'colinas',
  'army.terrMountains': 'montañas',
  'army.terrForest': 'bosque',
  'char.none': 'Sin personajes.',
  'char.champion': 'Campeón',
  'char.dead': 'Muerto',
  'char.kidnapped': 'Secuestrado',
  'char.skills': 'Habilidades',
  'char.command': 'Mando',
  'char.agent': 'Agente',
  'char.emissary': 'Emisario',
  'char.mage': 'Mago',
  'char.status': 'Estado',
  'char.health': 'Salud',
  'char.stealth': 'Sigilo',
  'char.challenge': 'Desafío',
  'char.artifacts': 'Artefactos',
  'char.spells': 'Hechizos',
  'char.pcSentence': 'El {size}{fort} de {name} ondea la bandera de {owner}.',
  'char.artBonus': 'Bonus +{x}',
  'char.artAlignment': 'Alineamiento: {x}',
  'char.artLocation': 'Lugar: {x}',
  'char.artNation': 'Nación: {x}',
  'char.artHolder': 'En manos de: {x}',
  'char.spMinRank': 'rango mín {x}',
  'char.spRank': 'rango {x}',
  'char.spPrereq': 'Prerequisitos: {x}',
  'char.spReqInfo': 'Info requerida: {x}',
  'char.cmdArmy': '{n} manda un ejército en {h}.',
  'char.atHex': '{n} está en {h}.',
  'ord.title': 'Órdenes',
  'ord.validate': 'Validar todo',
  'ord.validating': 'Validando…',
  'ord.none': 'Sin personajes.',
  'ord.available': '{n} disponibles',
  'ord.selectOrder': 'Elige orden…',
  'ord.loading': 'Cargando info…',
  'ord.cost': 'Coste: ',
  'ord.expected': 'esperados +{x} oro',
  'ord.max': '(máx {x})',
  'ord.moved': 'Tras 1ª orden → {x}',
  'ord.submit': 'Enviar {slot}',
  'ord.submitting': 'Enviando…',
  'ord.submitFail': 'No se pudo enviar',
  'ord.slot1': '1ª orden',
  'ord.slot2': '2ª orden (condicionada a la 1ª)',
  'ord.ord1': '1ª:',
  'ord.ord2': '2ª:',
  'ord.twoSubmitted': 'Dos órdenes enviadas. Cancela una para cambiar.',
  'ord.ordersCount': '{n}/2 órdenes',
  'ord.selectPh': 'Elige…',
  'ord.maxBtn': 'máx {x}',
  'ord.maxTitle': 'Usar máximo posible',
  'ord.noMatches': 'Sin resultados',
  'ord.searchPh': 'Escribe para buscar…',
  'ord.clearTitle': 'Limpiar',
  'ord.eligFail': 'No se pudieron cargar las órdenes',
  'ord.yes': 'sí',
  'ord.no': 'no',
  'ord.tipType': 'Tipo',
  'ord.tipDifficulty': 'Dificultad',
  'ord.tipPrereq': 'Prerequisitos',
  'ord.tipReqInfo': 'Info requerida',
  'ord.tipDesc': 'Descripción',
  'ord.typeGeneral': 'General',
  'ord.typeCommand': 'Mando',
  'ord.typeAgent': 'Agente',
  'ord.typeEmissary': 'Emisario',
  'ord.typeMage': 'Mago',
  'ord.rForceCommander': 'Comandante de fuerza',
  'ord.rCompany': 'En compañía',
  'ord.rFourthAge': 'Solo Cuarta Edad',
  'ord.rKingdom': 'Reino',
  'ord.rCapital': 'En capital',
  'ord.pCapital': 'En tu capital',
  'ord.pCurrentCapital': 'En tu capital actual',
  'ord.pArmy': 'En ejército',
  'ord.pNavy': 'Mandar armada',
  'ord.pNavyNation': 'La nación tiene armada',
  'ord.pCompany': 'En compañía',
  'ord.pOwnPc': 'En un centro propio',
  'ord.pLand': 'En tierra',
  'ord.pCombatArt': 'Artefacto de combate en mano',
  'ord.pHeldArt': 'Artefacto en mano',
  'ord.pHeal': 'Hechizo de curación conocido',
  'ord.pCombatSpell': 'Hechizo de combate conocido',
  'ord.pConjuring': 'Hechizo de conjuración conocido',
  'ord.pMovement': 'Hechizo de movimiento conocido',
  'ord.pLore': 'Hechizo de saber conocido',
  'ord.skillPlus': ' +hab.',
  'ord.diffAutomatic': 'automática',
  'ord.diffEasy': 'fácil',
  'ord.diffAverage': 'media',
  'ord.diffHard': 'difícil',
  'ord.diffVaries': 'variable',
  'data.typeCommander': 'Comandante',
  'data.typeAgent': 'Agente',
  'data.typeEmissary': 'Emisario',
  'data.typeMage': 'Mago',
  'data.sizeCamp': 'Campamento',
  'data.sizeVillage': 'Aldea',
  'data.sizeTown': 'Villa',
  'data.sizeMajorTown': 'Villa grande',
  'data.sizeCity': 'Ciudad',
  'data.sizeFortress': 'Fortaleza',
  'data.sizeCitadel': 'Ciudadela',
  'data.fortTower': 'Torre',
  'data.fortFort': 'Fuerte',
  'data.fortCastle': 'Castillo',
  'data.fortKeep': 'Torreón',
  'data.fortCitadel': 'Ciudadela',
  'data.fortPalisade': 'Empalizada',
  'data.fortStoneWalls': 'Muros de piedra',
  'data.fortWalls': 'Muros',
  'data.fortFortress': 'Fortaleza',
  'data.fortCitadelWalls': 'Muros de ciudadela',
  'data.seasonSpring': 'Primavera',
  'data.seasonSummer': 'Verano',
  'data.seasonAutumn': 'Otoño',
  'data.seasonWinter': 'Invierno',
  'data.stActive': 'Activa',
  'data.stSetup': 'Preparación',
  'data.stOrdersOpen': 'Órdenes abiertas',
  'data.stProcessing': 'Procesando',
  'data.stCompleted': 'Completada',
  'data.terrPlains': 'Llanos',
  'data.terrForest': 'Bosque',
  'data.terrMountains': 'Montañas',
  'data.terrRough': 'Abrupto',
  'data.terrDesert': 'Desierto',
  'data.terrSwamp': 'Pantano',
  'data.terrShore': 'Orilla',
  'data.terrCoastal': 'Costa',
  'data.terrWater': 'Agua',
  'data.terrOcean': 'Océano',
  'data.terrHills': 'Colinas',
  'data.terrRiver': 'Río',
  'data.terrCoast': 'Costa',
  'data.terrMarsh': 'Marisma',
  'data.terrSea': 'Mar',
  'data.roleTestAdmin': 'Admin de pruebas',
  'data.alNone': 'ninguno',
  'data.alNeutral': 'Neutral',
  'data.alGood': 'Bueno',
  'data.alEvil': 'Maligno',
};

const dicts: Record<Lang, Record<DictKey, string>> = { en, es };

export type TFunc = (key: DictKey, vars?: Record<string, string | number>) => string;

const LangCtx = createContext<{ lang: Lang; setLang: (l: Lang) => void; t: TFunc }>({
  lang: 'en',
  setLang: () => {},
  t: (key) => en[key] ?? key,
});

export function LangProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(() => {
    try {
      return (localStorage.getItem('mepbm-lang') as Lang) || 'en';
    } catch {
      return 'en';
    }
  });
  const setLang = (l: Lang) => {
    setLangState(l);
    try {
      localStorage.setItem('mepbm-lang', l);
    } catch { /* ignore */ }
  };
  const t: TFunc = (key, vars) => {
    let s: string = dicts[lang][key] ?? en[key] ?? key;
    if (vars) {
      for (const k of Object.keys(vars)) {
        s = s.split(`{${k}}`).join(String(vars[k]));
      }
    }
    return s;
  };
  return <LangCtx.Provider value={{ lang, setLang, t }}>{children}</LangCtx.Provider>;
}

export function useLang() {
  return useContext(LangCtx);
}

export function sideLabel(code: string, t: TFunc): string {
  if (code === 'free_peoples') return t('game.freeP');
  if (code === 'dark_servants') return t('game.darkP');
  if (code === 'neutral') return t('game.neut');
  return code;
}

export function charTypeLabel(code: string | undefined, t: TFunc): string {
  switch ((code ?? '').toLowerCase()) {
    case 'commander': return t('data.typeCommander');
    case 'agent': return t('data.typeAgent');
    case 'emissary': return t('data.typeEmissary');
    case 'mage': return t('data.typeMage');
    default: return code ?? '';
  }
}

export function pcSizeLabel(size: string | undefined, t: TFunc): string {
  switch ((size ?? '').toLowerCase()) {
    case 'camp': return t('data.sizeCamp');
    case 'village': return t('data.sizeVillage');
    case 'town': return t('data.sizeTown');
    case 'major town': return t('data.sizeMajorTown');
    case 'city': return t('data.sizeCity');
    case 'fortress': return t('data.sizeFortress');
    case 'citadel': return t('data.sizeCitadel');
    default: return size ?? '';
  }
}

export function fortLabel(fort: string | undefined, t: TFunc): string {
  switch ((fort ?? '').toLowerCase()) {
    case 'tower': return t('data.fortTower');
    case 'fort': return t('data.fortFort');
    case 'castle': return t('data.fortCastle');
    case 'keep': return t('data.fortKeep');
    case 'citadel': return t('data.fortCitadel');
    case 'palisade': return t('data.fortPalisade');
    case 'stone walls': return t('data.fortStoneWalls');
    case 'walls': return t('data.fortWalls');
    case 'fortress': return t('data.fortFortress');
    case 'citadel walls': return t('data.fortCitadelWalls');
    default: return fort ?? '';
  }
}

export function seasonLabel(s: string | undefined, t: TFunc): string {
  switch ((s ?? '').toLowerCase()) {
    case 'spring': return t('data.seasonSpring');
    case 'summer': return t('data.seasonSummer');
    case 'autumn': case 'fall': return t('data.seasonAutumn');
    case 'winter': return t('data.seasonWinter');
    default: return s ?? '';
  }
}

export function statusLabel(s: string | undefined, t: TFunc): string {
  switch ((s ?? '').toLowerCase()) {
    case 'active': return t('data.stActive');
    case 'setup': return t('data.stSetup');
    case 'orders_open': return t('data.stOrdersOpen');
    case 'processing': return t('data.stProcessing');
    case 'completed': return t('data.stCompleted');
    default: return s ?? '';
  }
}

export function terrainLabel(s: string | undefined, t: TFunc): string {
  switch ((s ?? '').toLowerCase()) {
    case 'plains': return t('data.terrPlains');
    case 'forest': return t('data.terrForest');
    case 'mountains': return t('data.terrMountains');
    case 'rough': return t('data.terrRough');
    case 'desert': return t('data.terrDesert');
    case 'swamp': return t('data.terrSwamp');
    case 'shore': return t('data.terrShore');
    case 'coastal': return t('data.terrCoastal');
    case 'water': return t('data.terrWater');
    case 'ocean': return t('data.terrOcean');
    case 'hills': return t('data.terrHills');
    case 'river': return t('data.terrRiver');
    case 'coast': return t('data.terrCoast');
    case 'marsh': return t('data.terrMarsh');
    case 'sea': return t('data.terrSea');
    default: return s ?? '';
  }
}

export function difficultyLabel(v: string | undefined, t: TFunc): string {
  switch ((v ?? '').toLowerCase()) {
    case 'automatic': return t('ord.diffAutomatic');
    case 'easy': return t('ord.diffEasy');
    case 'average': return t('ord.diffAverage');
    case 'hard': return t('ord.diffHard');
    case 'varies': return t('ord.diffVaries');
    default: return v ?? '';
  }
}

export function alignmentValue(v: string | undefined, t: TFunc): string {
  switch ((v ?? '').toLowerCase()) {
    case '': case 'none': return t('data.alNone');
    case 'neutral': return t('data.alNeutral');
    case 'good': return t('data.alGood');
    case 'evil': return t('data.alEvil');
    default: return v ?? '';
  }
}

export function roleLabel(role: string | undefined, t: TFunc): string {
  const r = (role ?? '').toLowerCase();
  if (r === 'test_admin' || r === 'test admin') return t('data.roleTestAdmin');
  return role ?? '';
}

export function LanguageSwitcher({ small }: { small?: boolean }) {
  const { lang, setLang } = useLang();
  const cls = (active: boolean) =>
    `${small ? 'px-2 py-0.5 text-xs' : 'px-2 py-1 text-sm'} rounded font-bold transition ${
      active ? 'bg-mepbm-gold text-gray-900' : 'bg-gray-700 text-gray-400 hover:text-white'
    }`;
  return (
    <span className="inline-flex gap-1">
      <button type="button" onClick={() => setLang('en')} className={cls(lang === 'en')}>EN</button>
      <button type="button" onClick={() => setLang('es')} className={cls(lang === 'es')}>ES</button>
    </span>
  );
}
