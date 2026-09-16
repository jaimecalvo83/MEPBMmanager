# Arquitectura MEPBMmanager (nota para el humano que viene detrás)

## Mapa en 30 segundos

- `backend/MEPBMmanager.Api/Controllers/` — solo HTTP: autentica, resuelve
  ámbito (jugador/staff), delega y mapea errores a status. **Sin reglas.**
- `backend/MEPBMmanager.Api/Orders/` — todo lo de órdenes:
  - `OrderEstimateService.cs` — reglas: elegibilidad, formularios por orden
    (`RequiresFor`), validación, conflictos cruzados, costes. Punto de
    entrada: `EstimateAsync` / `EligibleAsync`.
  - `OrderTexts.cs` — **único sitio con textos ES/EN de órdenes** (claves
    `err.*`, `reason.*`, `label.*`, `opt.*`, `warn.*`, `cost.*` + nombres
    de naciones/materiales/etc). ¿Cambiar un texto? Aquí. ¿Añadir idioma?
    Añade columna aquí y `Norm`.
  - `OrderModels.cs` — DTOs, `EstimateCtx`, `PendingUsage`,
    `OrderRequestException` (el servicio falla con status; el controlador
    lo traduce a HTTP).
- `backend/MEPBMmanager.Api/Services/TurnProcessor.cs` — resolución del
  turno (god object histórico, ~4600 líneas: **siguiente candidato a
  partir en parciales por dominio**). `CombatResolver.cs` es puro y
  testeable. Los mensajes que guarda en `Order.Result` son histórico en
  inglés a conciencia (no traducir sin cambiar el modelo).
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
3. `dotnet test backend/MEPBMmanager.Tests` (84 verdes: combate, economía,
   catálogos, duelos, integración de turno con InMemory) antes de subir.
4. Español en comentarios, inglés en identificadores.

## Añadir una orden nueva

1. `OrderDefinitions` (Domain) + dispatcher en `TurnProcessor`.
2. Formulario en `OrderEstimateService.RequiresFor` (+ textos en `OrderTexts`).
3. Validación en `ValidateEstimateParams` (mismo mensaje que el resolve).
4. Coste en `EstimateCosts`. 5. Test de integración de turno.
