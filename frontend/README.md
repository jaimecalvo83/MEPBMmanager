# MEPBMmanager

Gestor de partidas de Middle-earth Play By Mail (MEPBM). Plataforma web para jugar MEPBM con procesamiento de turnos automatizado.

## Tech Stack

- **Frontend**: React 18 + TypeScript + Vite + Tailwind CSS + Leaflet
- **Backend**: .NET 10 + ASP.NET Core Web API + EF Core
- **Database**: PostgreSQL 16 (EF Core Migrations)
- **Auth**: JWT + BCrypt
- **Deploy**: Docker Compose

## Estructura del monorepo

```
MEPBMmanager/
├── backend/                  # Solución .NET 10
│   ├── MEPBMmanager.Api/         # Web API (controllers, services)
│   ├── MEPBMmanager.Domain/      # Entidades, enums, constantes
│   └── MEPBMmanager.Infrastructure/  # EF Core DbContext, migraciones, seed
├── frontend/                 # Frontend React (npm workspaces)
│   ├── packages/shared/          # Tipos y constantes compartidos
│   ├── packages/web/             # App React (Vite)
│   └── docker-compose.yml        # Postgres + backend + web (producción)
└── .gitignore
```

## Requisitos previos

- .NET SDK 10 (`dotnet --version` → 10.x)
- Node.js 20+ (`node --version`)
- Docker Desktop (para PostgreSQL) o un PostgreSQL local en `localhost:5432`

## Desarrollo local

### 1. Arrancar la base de datos (PostgreSQL)

Con Docker:

```bash
docker run --name mepbm-postgres -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=mepbm -p 5432:5432 -d postgres:16-alpine
```

> Nota: al arrancar, el backend ejecuta el seed de `GameTypes` y `NationTemplates` automáticamente.

### 2. Backend (.NET) — Terminal 1

```bash
cd backend
dotnet run --project MEPBMmanager.Api
```

- API en: `http://localhost:5171`
- Swagger/OpenAPI (desarrollo): `http://localhost:5171/openapi/v1.json`

### 3. Frontend (React) — Terminal 2

```bash
cd frontend/packages/web
npm install        # solo la primera vez
npm run dev
```

- Frontend en: `http://localhost:5173`
- El proxy de Vite reenvía `/api` → `http://localhost:5171` (configurado en `vite.config.ts`)

### Credenciales de prueba

- Email: `test@mepbm.com`
- Contraseña: `Password123!`

## Endpoints de la API

### Auth
- `POST /api/auth/register` - Registro
- `POST /api/auth/login` - Login
- `GET /api/auth/me` - Usuario actual

### Games
- `GET /api/games` - Listar partidas
- `POST /api/games` - Crear partida (el creador es admin)
- `GET /api/games/:id` - Detalles
- `POST /api/games/:id/join` - Unirse a una nación
- `POST /api/games/:id/leave` - Salir
- `POST /api/games/:id/start` - Empezar (solo admin)
- `POST /api/games/:id/process-turn` - Procesar turno (solo admin)
- `GET /api/games/:id/state` - Estado completo de la partida
- `GET /api/games/:id/nations` - Naciones disponibles

### Orders
- `GET /api/games/:id/orders` - Listar órdenes
- `POST /api/games/:id/orders` - Enviar orden
- `DELETE /api/games/:id/orders/:orderId` - Cancelar orden
- `POST /api/games/:id/orders/validate` - Validar órdenes

### Turnos
- `GET /api/games/:id/turns/:turnId/report` - Informe narrativo del turno

### Messages
- `GET /api/games/:id/messages` - Listar mensajes
- `POST /api/games/:id/messages` - Enviar mensaje

## Producción (Docker Compose)

```bash
cd frontend
docker-compose up -d --build
```

Levanta: `postgres` + `backend` (.NET, puerto 5171) + `web` (nginx, puerto 80).
El nginx redirige `/api` al servicio `backend`.

## Características

- Gestión de partidas (crear, unirse, empezar, procesar turnos)
- 20 naciones para el escenario 2950 (Free Peoples, Dark Servants, Neutrales)
- Sistema de ~120 órdenes (económicas, reclutamiento, combate, magia, mercado, espionaje)
- Combate de ejércitos/armadas con terreno y tácticas
- Economía: impuestos, mercado, producción, mantenimiento
- Diplomacia: relaciones entre naciones, cambio de alianzas
- Mapa hex interactivo con Leaflet
- Informes narrativos de turno por nación

## Reglas del juego

Basado en el MEPBM Rulebook v1.90 (ver `docs/`).
