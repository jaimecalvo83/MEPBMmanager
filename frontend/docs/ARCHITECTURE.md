# MEPBMmanager - Architecture Document

## Overview

Sistema web para gestionar partidas de Middle-earth Play By Mail (MEPBM). Permite a varios jugadores jugar MEPBM a través de una interfaz web en lugar de por correo, con procesamiento de turnos automatizado.

## Tech Stack

| Capa | Tecnología | Detalle |
|------|-----------|---------|
| **Frontend** | React 18 + TypeScript + Vite | App SPA |
| **UI** | Tailwind CSS | Tema oscuro personalizado |
| **Mapa** | Leaflet.js | Grid de hexágonos |
| **Estado** | Zustand + React Query | Estado cliente + estado servidor |
| **Backend** | .NET 10 + ASP.NET Core Web API | REST API |
| **ORM** | EF Core + Npgsql | PostgreSQL |
| **Base de datos** | PostgreSQL 16 | Docker local |
| **Auth** | JWT Bearer + BCrypt | Stateless |
| **Validación** | Zod (frontend) | Schemas compartidos |
| **Deploy** | Docker Compose | Postgres + backend + nginx |

## Estructura del monorepo

```
MEPBMmanager/
├── backend/                        # Solución .NET 10 (MEPBMmanager.slnx)
│   ├── MEPBMmanager.Api/           # Capa de presentación (Web API)
│   │   ├── Controllers/            #   Auth, Games, Orders, Messages
│   │   ├── Services/               #   TurnProcessor, CombatResolver,
│   │   │                           #   MovementResolver, TurnReportService
│   │   ├── Program.cs              #   Composition root + DI
│   │   └── Dockerfile
│   ├── MEPBMmanager.Domain/        # Capa de dominio (pura, sin deps)
│   │   ├── Entities/               #   23 entidades POCO
│   │   ├── Enums/                  #   GameEnums, CharacterEnums, ArmyEnums
│   │   └── Constants/              #   OrderDefinitions, SpellDefinitions,
│   │                               #   TroopConstants, Nations
│   └── MEPBMmanager.Infrastructure/  # Acceso a datos
│       ├── Data/                   #   MepbmDbContext, DbInitializer
│       └── Migrations/             #   Migraciones EF Core
├── frontend/                       # Frontend React (npm workspaces)
│   ├── packages/shared/            #   Tipos + constantes compartidas
│   ├── packages/web/               #   App React (Vite + Tailwind + Leaflet)
│   └── docker-compose.yml
└── .gitignore
```

**Dirección de dependencias:** `Api → Infrastructure → Domain`. Domain no tiene dependencias externas.

## Backend (.NET)

### Capas

| Capa | Responsabilidad | Notas |
|------|----------------|-------|
| **Api** | Controllers + Services | Lógica de negocio del juego aquí |
| **Domain** | Entidades, enums, constantes | POCOs sin comportamiento |
| **Infrastructure** | EF Core, DbContext, seed | Acceso a PostgreSQL |

### DI Registrations (Program.cs)

| Servicio | Lifetime | Uso |
|----------|----------|-----|
| `MepbmDbContext` | Scoped | EF Core + Npgsql |
| `TurnProcessor` | Scoped | Motor de procesamiento de turnos |
| `CombatResolver` | Scoped | Resolución de combate |
| `TurnReportService` | Scoped | Generación de informes |
| `JwtBearer` | — | Auth por token |
| CORS `AllowFrontend` | — | `http://localhost:5173` |

### Servicios principales

- **TurnProcessor** — Motor del juego: economía, reclutamiento, combate, magia, mercado, rehenes, naves, artefactos, espionaje. Resume ~120 códigos de orden.
- **CombatResolver** — Combate de ejércitos/armadas/asaltos a PC con terreno y tácticas.
- **MovementResolver** — Pathfinding Dijkstra, costes por terreno, encuentros.
- **TurnReportService** — Informe narrativo por nación por turno.

### Modelo de datos (entidades clave)

```
User ──< Player >── Game
                   >── Nation
Game ──< Nation ──< Character / Army / Navy / PopulationCentre
                   < NationRelation (auto-ref)
                   < Order / Message / HexTile / MarketPrice / Encounter / GameEvent
     >── GameType ──< NationTemplate
Nation ──< Order >── Turn >── Game
Character ──< Spell / Artifact / Guard / Order / Encounter
Army ──< Character (via CommanderId)
Turn ──< Order / TurnResult / GameEvent
```

### Arquitectura de plantilla vs instancia

- **GameType** = escenario inmutable (Code 1650/2950/pruebas, NationTemplates, descripción).
- **Game** = instancia mutable (naciones, turnos, órdenes, hexes como copias).
- **NationTemplate** = plantilla enriquecida (recursos iniciales, composición de ejército, nombres de PC, reglas admin).
- Al crear un juego: las 20 naciones reciben PC (capital + pueblo fronterizo) y ejércitos desde la plantilla. Los personajes se crean al empezar la partida para las naciones con jugador.

## Turn Processing Flow

```
[Orders abiertos]
      │
      ▼
1. FASE ECONÓMICA  → Ingresos por PC, impuestos, consumo de comida, producción
      │
      ▼
2. AUTO-ÓRDENES    → Naciones sin órdenes reciben "Hold" (código 100)
      │
      ▼
3. DESAFÍOS        → Resolución de desafíos personales (orden 210)
      │
      ▼
4. ÓRDENES         → Procesa cada orden (combate, movimiento, reclutamiento…)
      │
      ▼
5. AVANCE          → Cierra el turno, crea el siguiente con deadline
      │
      ▼
[TurnResult] + [Informe narrativo]
```

## Frontend (React)

### Estructura

```
packages/web/src/
├── api/client.ts          # Cliente Axios + interceptores JWT
├── hooks/                 # Custom hooks React Query
│   ├── useGames.ts        #   Lista de juegos + crear
│   ├── useGameState.ts    #   Estado de un juego + procesar turno
│   ├── useOrders.ts       #   Órdenes (list/submit/cancel/validate)
│   ├── useMessages.ts     #   Mensajes (list/send/markRead)
│   └── useNations.ts      #   Naciones disponibles
├── stores/authStore.ts    # Zustand auth (persistencia localStorage)
├── types/                 # Interfaces TypeScript
└── components/
    ├── Auth/              # Login, Register
    ├── Dashboard/         # Lista de partidas + crear + unirse
    ├── GameView/          # Vista de partida (mapa/órdenes/mensajes)
    ├── Map/HexMap.tsx     # Mapa hex Leaflet
    ├── Orders/            # Panel de órdenes
    ├── Messages/          # Panel de mensajes
    └── NationPicker/      # Selector de nación al unirse
```

### Rutas

| Path | Componente |
|------|-----------|
| `/login`, `/register` | Auth |
| `/` | Dashboard |
| `/game/:id` | GameView |

### Data fetching

React Query gestiona el estado del servidor: caching, refetch, invalidación automática tras mutations.

## Deployment (Docker Compose)

```
frontend/docker-compose.yml
├── postgres   (puerto 5432)
├── backend    (.NET, puerto 5171, contexto ../backend)
└── web        (nginx, puerto 80, proxy /api → backend:5171)
```

El nginx de `packages/web/nginx.conf` redirige `/api` al servicio `backend`.
