# MEPBMmanager - Architecture Document

## Overview

Web application to manage Middle-earth Play By Mail (MEPBM) games. Allows multiple players to play MEPBM through a web interface instead of email, with automated turn processing.

## Tech Stack

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| **Frontend** | React 18 + TypeScript + Vite | Fast dev, JS ecosystem, user familiarity |
| **UI Library** | Tailwind CSS + shadcn/ui | Rapid UI development, consistent design |
| **Map** | Leaflet.js + custom hex grid | Free, proven, hex grid support |
| **State** | Zustand + React Query | Lightweight state + server state management |
| **Backend** | Node.js + Express + TypeScript | User knows JS, fast to develop |
| **Database** | PostgreSQL 16 | Robust, free, handles complex queries |
| **ORM** | Prisma | Type-safe, great DX, migration system |
| **Auth** | JWT + bcrypt | Simple, stateless, proven |
| **Jobs** | BullMQ + Redis | Reliable job queues for turn processing |
| **Validation** | Zod | Runtime type validation, shared schemas |
| **Testing** | Vitest + Supertest | Fast, modern, good DX |
| **Deploy** | Docker Compose | VPS deployment, isolated services |

## Project Structure

```
MEPBMmanager/
├── packages/
│   ├── shared/                    # Shared types & validation
│   │   ├── src/
│   │   │   ├── types/             # TypeScript interfaces
│   │   │   ├── validation/        # Zod schemas
│   │   │   └── constants/         # Game constants, order codes
│   │   └── package.json
│   ├── server/                    # Backend API
│   │   ├── src/
│   │   │   ├── api/               # Express routes
│   │   │   │   ├── auth/          # Login, register, refresh
│   │   │   │   ├── games/         # Game CRUD, join, start
│   │   │   │   ├── orders/        # Submit, validate orders
│   │   │   │   ├── messages/      # Inter-player messaging
│   │   │   │   └── admin/         # Game master controls
│   │   │   ├── engine/            # Game engine
│   │   │   │   ├── combat/        # Combat resolution
│   │   │   │   ├── economy/       # Tax, market, production
│   │   │   │   ├── magic/         # Spell casting
│   │   │   │   ├── diplomacy/     # Relations, allegiance
│   │   │   │   ├── espionage/     # Agents, sabotage
│   │   │   │   ├── movement/      # Hex movement costs
│   │   │   │   └── orders/        # Order execution
│   │   │   ├── jobs/              # BullMQ workers
│   │   │   │   ├── turnProcessor/ # Main turn processor
│   │   │   │   └── scheduler/     # Cron-like scheduling
│   │   │   ├── db/                # Prisma client, migrations
│   │   │   └── utils/             # Helpers, dice rolls, etc
│   │   ├── prisma/
│   │   │   └── schema.prisma      # Database schema
│   │   └── package.json
│   └── web/                       # Frontend React app
│       ├── src/
│       │   ├── components/
│       │   │   ├── Map/           # Hex map with Leaflet
│       │   │   ├── Dashboard/     # Kingdom overview
│       │   │   ├── Orders/        # Order submission UI
│       │   │   ├── Messages/      # Player messaging
│       │   │   ├── History/       # Event log
│       │   │   └── Auth/          # Login/Register
│       │   ├── hooks/             # Custom React hooks
│       │   ├── stores/            # Zustand stores
│       │   └── api/               # API client functions
│       └── package.json
├── docker-compose.yml
├── package.json                   # Monorepo root
└── docs/
    ├── ARCHITECTURE.md
    └── GAME_RULES.md
```

## Database Schema (Core Entities)

### Game Management
- **Game** - Game instance (module, status, current turn, settings)
- **Player** - User account linked to a game
- **Nation** - Playable faction (25 nations per module)
- **Turn** - Turn record (number, status, deadlines, processing state)

### Characters & Units
- **Character** - Named character (skills, health, stealth, location)
- **CharacterSkill** - Skill values (command, agent, emissary, mage)
- **Company** - Small character group
- **Army** - Military unit (troops, weapons, armour, morale, food)
- **ArmyTroops** - Troop composition per army
- **Navy** - Naval unit (warships, transports)

### Geography
- **HexTile** - Map hex (terrain, owner, coordinates)
- **PopulationCentre** - City/town/village (size, loyalty, production, siege)
- **Fortification** - Building in PC (type, level)

### Economy & Resources
- **Resource** - Nation resources (gold, food, timber, materials)
- **MarketPrice** - Current market prices per good
- **CaravanOrder** - Market transactions

### Orders & Processing
- **Order** - Submitted order (character, code, parameters, status)
- **OrderTemplate** - Order definition (code, name, restrictions)
- **TurnResult** - Processed result per player per turn

### Diplomacy & Relations
- **NationRelation** - Relation between two nations (level)
- **Alliance** - Active alliances between nations
- **Message** - Player-to-player messages

### Magic
- **Spell** - Known spells per mage
- **SpellEffect** - Active spell effects

### Events & History
- **GameEvent** - Logged event (combat, diplomacy, economy, etc)
- **CombatResult** - Detailed combat outcome

## Game Engine Flow (Per Turn)

```
┌─────────────────────────────────────────────┐
│              TURN PROCESSING                 │
├─────────────────────────────────────────────┤
│ 1.  Natural healing + healing spells        │
│ 2.  Allegiance/relation changes             │
│ 3.  Personal challenges                     │
│ 4.  Combat (navy → army → PC)              │
│ 5.  Encounter reactions                     │
│ 6.  Voluntary tax changes                   │
│ 7.  PC production + tax revenue            │
│ 8.  Market buy (bid/purchase)              │
│ 9.  Market sell                             │
│ 10. Transfers (food, items)                │
│ 11. Food consumption + morale              │
│ 12. Maintenance charges                     │
│ 13. Tax rate check (elimination if >100%)  │
│ 14. Command orders                          │
│ 15. Emissary orders                         │
│ 16. Agent orders                            │
│ 17. Hostage escape attempts                 │
│ 18. Other mage orders                       │
│ 19. New characters created                  │
│ 20. Army/company non-combat orders          │
│ 21. Ship/artifact pickup                    │
│ 22. Character + army movement               │
│ 23. Scouting + lore spells                  │
│ 24. PC hiding                               │
│ 25. Goods transport                         │
│ 26. PC transfer + capital relocation        │
│ 27. New market prices + costs               │
│ 28. One Ring attempts                       │
└─────────────────────────────────────────────┘
```

## API Endpoints

### Auth
- `POST /api/auth/register` - Register new player
- `POST /api/auth/login` - Login
- `POST /api/auth/refresh` - Refresh JWT
- `GET /api/auth/me` - Current user

### Games
- `GET /api/games` - List games
- `POST /api/games` - Create game (admin)
- `GET /api/games/:id` - Game details
- `POST /api/games/:id/join` - Join game
- `POST /api/games/:id/start` - Start game (admin)

### Orders
- `GET /api/games/:id/orders` - List my orders for turn
- `POST /api/games/:id/orders` - Submit order
- `DELETE /api/games/:id/orders/:orderId` - Cancel order
- `POST /api/games/:id/orders/validate` - Validate orders

### Map & State
- `GET /api/games/:id/map` - Get hex map
- `GET /api/games/:id/nations/:nationId` - Nation state
- `GET /api/games/:id/turns/current` - Current turn info
- `GET /api/games/:id/turns/:turnId/results` - Turn results

### Messages
- `GET /api/games/:id/messages` - List messages
- `POST /api/games/:id/messages` - Send message

### Admin
- `POST /api/admin/games/:id/process` - Force process turn
- `POST /api/admin/games/:id/pause` - Pause game
- `GET /api/admin/games/:id/stats` - Game statistics

## Key Game Constants

### Troop Types
```typescript
enum TroopType {
  HEAVY_CAVALRY = 'hc',    // Cost: 3000/100
  LIGHT_CAVALRY = 'lc',    // Cost: 1500/100
  HEAVY_INFANTRY = 'hi',   // Cost: 2000/100
  LIGHT_INFANTRY = 'li',   // Cost: 1000/100
  ARCHERS = 'ar',           // Cost: 1000/100
  MEN_AT_ARMS = 'ma',       // Cost: 500/100
}
```

### Weapon/Armour Ranks
```typescript
enum MaterialRank {
  NONE = 0,
  WOOD = 10,
  LEATHER = 20,
  BRONZE = 40,
  STEEL = 60,
  MITHRIL = 100,
}
```

### Relation Levels
```typescript
enum RelationLevel {
  HATED = -2,
  DISLIKED = -1,
  UNFRIENDLY = 0, // (between neutral positions)
  NEUTRAL = 0,
  TOLERANT = 1,
  FRIENDLY = 2,
}
```

### Spell Categories
```typescript
enum SpellCategory {
  HEALING = 'healing',       // Spells 2,4,6,8
  DEFENSIVE = 'defensive',   // Spells 102-116
  OFFENSIVE = 'offensive',   // Spells 202-248
  MOVEMENT = 'movement',     // Spells 302-314
  LORE = 'lore',             // Spells 402-436
  CONJURING = 'conjuring',   // Spells 502-512
}
```

## Deployment Architecture

```
┌─────────────────────────────────────────┐
│              VPS (Docker)                │
├─────────────────────────────────────────┤
│  ┌─────────┐  ┌─────────┐  ┌────────┐ │
│  │  Nginx   │  │  Node   │  │ Redis  │ │
│  │ (proxy)  │→ │ (API)   │  │ (jobs) │ │
│  └─────────┘  └────┬────┘  └────────┘ │
│                     │                   │
│              ┌──────┴──────┐            │
│              │ PostgreSQL  │            │
│              │   (data)    │            │
│              └─────────────┘            │
└─────────────────────────────────────────┘
```

## Cost Estimate

| Item | Cost |
|------|------|
| VPS (4GB RAM, 2 vCPU) | ~5-10€/month |
| PostgreSQL | Free (on VPS) |
| Redis | Free (on VPS) |
| Domain | ~10€/year |
| **Total** | **~6-11€/month** |

## AI Tool Recommendations

For development with AI assistance:

1. **GitHub Copilot** (10$/month) - Best for inline code completion
2. **Cursor** (20$/month) - Best IDE integration with AI
3. **Claude Pro** (20$/month) - Best for architecture discussions
4. **This session (opencode)** - Already working, no extra cost

**Recommendation**: Start with this session + Copilot free tier. Upgrade if needed.

## Development Phases

### Phase 1: Foundation (Days 1-2)
- Project scaffolding (monorepo)
- Database schema + Prisma setup
- Auth system (JWT)
- Basic API structure

### Phase 2: Game Engine Core (Days 3-5)
- Order system (~100 orders)
- Character management
- Army management
- Population centre management

### Phase 3: Game Systems (Days 6-8)
- Combat resolution
- Economy (tax, market, production)
- Magic system
- Diplomacy & relations

### Phase 4: Turn Processing (Day 9)
- Background job system
- 28-step turn processor
- Event logging

### Phase 5: Frontend (Days 10-14)
- Dashboard
- Hex map (Leaflet)
- Order submission
- Messaging
- Event history

### Phase 6: Polish & Deploy (Days 15-17)
- Testing
- Docker setup
- VPS deployment
- Admin panel
