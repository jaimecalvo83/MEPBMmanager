# Arquitectura MEPBMmanager (nota para el humano que viene detrás)

## Mapa en 30 segundos

- `backend/MEPBMmanager.Api/Controllers/` — solo HTTP: autentica, resuelve
  ámbito (jugador/staff), delega y mapea errores a status. **Sin reglas.**
- `backend/MEPBMmanager.Api/Orders/` — todo lo de órdenes (una clase
  `partial` por fichero, una responsabilidad por fichero):
  - `OrderEstimateService.cs` — orquestación: `EstimateAsync` /
    `EligibleAsync` + constantes compartidas. Sin reglas.
  - `.Eligibility.cs` — quién puede dar qué orden.
  - `.Forms.cs` — qué campos pide cada orden (`RequiresFor`).
  - `.Validation.cs` — validación: un método pequeño por familia.
  - `.Costs.cs` — costes en vivo: un método por familia, devuelve
    `CostEstimate` (sin `out`).
  - `EstimateScope.cs` — todo lo que una regla necesita en un objeto
    (mundo, parámetros con nombre, errores). Las reglas son
    `Regla(código, scope)`: 2 parámetros, sin locales de una letra.
  - `OrderTexts.cs` — **único sitio** con textos ES/EN de órdenes (claves
    `err.*`, `reason.*`, `label.*`, `opt.*`, `warn.*`, `cost.*` + nombres).
    ¿Cambiar un texto? Aquí. ¿Añadir idioma? Añade columna aquí y `Norm`.
  - `OrderModels.cs` — DTOs, `EstimateCtx`, `PendingUsage`, `CostEstimate`,
    `OrderRequestException` (el servicio falla con status; el controlador
    lo traduce a HTTP).
- `backend/MEPBMmanager.Api/Services/TurnProcessor*.cs` — resolución del
  turno en 16 `partial` por dominio (núcleo + Economy/Market/Troops/
  Transfers/Combat/Movement/Forces/Hostages/Ships/Artifacts/Magic/
  Agents/Places/Diplomacy/Characters; ninguno pasa de ~630 líneas).
  `CombatResolver.cs` es puro y testeable. Los mensajes que guarda en
  `Order.Result` son histórico en inglés a conciencia (no traducir sin
  cambiar el modelo).
- `backend/MEPBMmanager.Domain/Constants/` — catálogos fuente de verdad
  (hechizos, artefactos + tablas `*Es`, habilidades, órdenes).
- `frontend/packages/web/src/i18n/` — `dict-en.ts` (manda: `DictKey`
  sale de aquí; si al ES le falta una clave **no compila**), `dict-es.ts`,
  `helpers.ts` (etiquetas de códigos de datos), `LangContext.tsx`
  (provider/hook/selector), `lang.tsx` (compat).
- `frontend/.../GameView.tsx` (~1600 líneas, pestañas inline) y
  `TurnProcessor.cs` son los dos próximos a trocear; mientras tanto,
  no añadas más casos ahí sin mirar esta nota.

## Convenciones

1. Nada de literales ES fuera de `OrderTexts` / `dict-*.ts` / catálogos `*Es`.
2. El controlador no decide reglas; el servicio no sabe de HTTP.
3. Funciones pequeñas con nombres que explican la intención; nada de
   locales de una letra (`t`, `n`, `pars`); las reglas reciben `(código,
   scope)`; sin `out` (devolver records); sin código muerto ni duplicados
   (si lo ves, extráelo o bórralo).
4. `dotnet test backend/MEPBMmanager.Tests` (106+ verdes: combate, economía,
   catálogos, duelos, integración de turno con InMemory, servicio de
   órdenes) antes de subir.
5. Español en comentarios, inglés en identificadores.

## Añadir una orden nueva

1. `OrderDefinitions` (Domain) + dispatcher en `TurnProcessor`.
2. Formulario en `OrderEstimateService.RequiresFor` (+ textos en `OrderTexts`).
3. Validación en `ValidateEstimateParams` (mismo mensaje que el resolve).
4. Coste en `EstimateCosts`. 5. Test de integración de turno.
