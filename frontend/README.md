# MEPBMmanager

Middle-earth Play By Mail game management system. Web-based platform for playing MEPBM games with automated turn processing.

## Tech Stack

- **Frontend**: React 18 + TypeScript + Vite + Tailwind CSS + Leaflet
- **Backend**: Node.js + Express + TypeScript
- **Database**: PostgreSQL 16 + Prisma ORM
- **Queue**: Redis + BullMQ
- **Auth**: JWT + bcrypt
- **Deploy**: Docker Compose

## Quick Start

### Local Development

```bash
# 1. Start PostgreSQL and Redis
docker-compose up -d postgres redis

# 2. Install dependencies
npm install

# 3. Generate Prisma client and push schema
npm run db:generate
npm run db:push

# 4. Start backend (terminal 1)
cd packages/server
npm run dev

# 5. Start frontend (terminal 2)
cd packages/web
npm run dev
```

### Production Deployment (VPS)

```bash
# Run deployment script
chmod +x deploy.sh
./deploy.sh
```

## Project Structure

```
MEPBMmanager/
├── packages/
│   ├── shared/          # Shared types and constants
│   ├── server/          # Backend API
│   └── web/             # Frontend React app
├── docker-compose.yml
└── docs/
    └── ARCHITECTURE.md
```

## Features

- **Game Management**: Create, join, and manage MEPBM games
- **25 Nations**: Play as Free Peoples, Dark Servants, or Neutrals
- **100+ Orders**: Full MEPBM order system
- **71 Spells**: Healing, Defensive, Offensive, Movement, Lore, Conjuring
- **Combat System**: Army combat with terrain and tactics
- **Economy**: Tax, market, production, maintenance
- **Diplomacy**: Nation relations and allegiance changes
- **Espionage**: Assassination, kidnapping, sabotage
- **Hex Map**: Interactive map with Leaflet
- **Automated Turns**: Background job processing

## API Endpoints

### Auth
- `POST /api/auth/register` - Register
- `POST /api/auth/login` - Login
- `GET /api/auth/me` - Current user

### Games
- `GET /api/games` - List games
- `POST /api/games` - Create game
- `GET /api/games/:id` - Game details
- `POST /api/games/:id/join` - Join game
- `GET /api/games/:id/state` - Full game state

### Orders
- `GET /api/games/:id/orders` - List orders
- `POST /api/games/:id/orders` - Submit order
- `DELETE /api/games/:id/orders/:orderId` - Cancel order

### Messages
- `GET /api/games/:id/messages` - List messages
- `POST /api/games/:id/messages` - Send message

## Game Rules

Based on MEPBM Rulebook v1.90. See `docs/GAME_RULES.md` for full mechanics.

### Turn Processing (28 Steps)

1. Natural healing + spells
2. Relation changes
3. Personal challenges
4. Combat
5. Encounters
6. Tax changes
7. Production + revenue
8-9. Market transactions
10. Transfers
11. Food consumption
12. Maintenance
13. Tax check
14-19. Character orders
20. Army orders
21. Pickup
22. Movement
23. Scouting
24. PC hiding
25. Transport
26. PC transfer
27. Market update
28. One Ring

## Testing

```bash
cd packages/server
npm test
```

## License

Private - For personal use only
