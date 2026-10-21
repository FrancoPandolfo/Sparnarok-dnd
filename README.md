# Sparnarok: The Architect's Codex ⚔️🎲

**Sparnarok** es un motor de simulación matemática, análisis táctico y orquestación de mundos para **D&D 5e (Reglas 2024 / SRD)**. Permite a Directores de Juego (DMs), diseñadores de sistemas e investigadores evaluar el balance de encuentros mediante simulaciones de Monte Carlo masivas ejecutadas de forma concurrente en microsegundos.

---

## 📋 Requisitos Previos

Antes de ejecutar la aplicación, asegúrate de tener instalado en tu sistema:

1. **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):** Para compilar y ejecutar la Web API de backend.
2. **[Node.js](https://nodejs.org/) (versión 20+ recomendada) y npm:** Para compilar y ejecutar el frontend en React (Vite).
3. **[PostgreSQL](https://www.postgresql.org/) (versión 16 o 17):** Motor de base de datos relacional para persistencia.

---

## ⚙️ Configuración Inicial de la Base de Datos

La aplicación utiliza PostgreSQL en el puerto `5432` con la siguiente cadena de conexión por defecto (definida en `src/backend/Sparnarok.Api/appsettings.json`):

```
Host=localhost;Port=5432;Database=sparnarok_dev_db;Username=sparnarok;Password=sparnarok_secret
```

### 1. Crear el usuario y la base de datos (si aún no existen)
En la consola de PostgreSQL (`psql`):

```sql
CREATE USER sparnarok WITH PASSWORD 'sparnarok_secret';
CREATE DATABASE sparnarok_dev_db OWNER sparnarok;
GRANT ALL PRIVILEGES ON DATABASE sparnarok_dev_db TO sparnarok;
```

### 2. Aplicar las migraciones de Entity Framework Core
Desde la raíz del repositorio:

```powershell
dotnet ef database update --project src/backend/Sparnarok.Api
```

*(Esto creará las tablas `Creatures`, `CreatureActions`, `WeaponMasteries`, `Weapons`, `Armors` y columnas de líneas base de DPR con sus maestrías de armas canónicas presembradas).*

---

## 🚀 Paso a Paso: Cómo Iniciar la Aplicación

Para utilizar Sparnarok necesitas levantar dos procesos: el **Backend (.NET 10)** y el **Frontend (Vite + React)**.

### Paso 1: Iniciar el Backend (API REST)
Abre una terminal en la raíz del proyecto y ejecuta:

```powershell
dotnet run --project src/backend/Sparnarok.Api --launch-profile http
```

- La API iniciará en: **`http://localhost:5000`**
- La documentación interactiva de Swagger/OpenAPI estará disponible en: **`http://localhost:5000/swagger`**

### Paso 2: Iniciar el Frontend (Web UI)
Abre una segunda terminal y ejecuta:

```powershell
cd src/frontend/sparnarok-web
npm install
npm run dev
```

- La aplicación web estará disponible en tu navegador en: **`http://localhost:5173`**
- El servidor de desarrollo de Vite redirige automáticamente todas las peticiones que comiencen con `/api` hacia `http://localhost:5000`.

---

## 🎮 Guía de Uso de la Aplicación

Una vez que ingreses a `http://localhost:5173`, encontrarás la siguiente experiencia:

### 1. Cargar Datos Semilla Iniciales (Opcional pero Recomendado)

Sparnarok incluye catálogos oficiales listos para poblar la base de datos:

- **Ingesta de Armas (CSV):**
  - En la pestaña **"Bestiario"**, haz clic en el botón superior **"📁 Ingesta de Armas (CSV)"**.
  - Arrastra o selecciona el archivo `data/seeds/weapons.csv`.
  - Haz clic en **"Iniciar Ingesta"**. Se poblarán 37 armas canónicas (Damage, DamageType, Properties, etc.).
- **Sincronización del Bestiario (Open5e):**
  - Puedes poblar más de 50 monstruos oficiales del SRD ejecutando una petición HTTP POST al endpoint de sincronización:
    ```powershell
    Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/ingestion/open5e/monsters/sync?limit=50"
    ```
  - Al recargar la web verás monstruos reales como *Aboleth*, *Adult Red Dragon*, *Bugbear*, etc.

---

### 2. Exploración del Bestiario Táctico
- En la pestaña **"Bestiario"**:
  - Utiliza el buscador en tiempo real para filtrar criaturas por nombre.
  - Haz clic en **"Ver Ficha Táctica"** en cualquier tarjeta para abrir la ficha 2024 de la criatura:
    - **Clase de Armadura Dinámica:** Verás el desglose exacto (armadura equipada, bono de escudo y contribución de Destreza).
    - **Acciones y Ataques:** Visualiza los bonos de impacto calculados y las fórmulas de daño.
    - **Maestrías de Armas:** Consulta las propiedades tácticas como *Topple*, *Vex*, *Graze*, etc.

---

### 3. Crear un Héroe / Personaje Jugador con DPR de Arquetipo
Sparnarok utiliza **Líneas Base de DPR Esperado** (*Expected DPR Baselines*) para simular personajes jugadores con rigor estadístico sin obligarte a configurar micro-reglas de dados:

1. Ve a la pestaña **"Simulador de Combate"**.
2. Haz clic en el botón **"🛡️ Añadir Héroe Custom"**.
3. En el modal interactivo:
   - Ingresa el **Nombre del Héroe** (ej. *Sir Galahad*, *Valeria*).
   - Ajusta los **Puntos de Golpe (HP)** y la **Clase de Armadura (CA)**.
   - Selecciona la **Clase / Arquetipo** deseado:
     - `Fighter 2H` (Espadón con Great Weapon Fighting)
     - `Ranger Ranged` (Arco largo con Archery y Hunter's Mark)
     - `Paladin 2H` (Espadón con Divine Smite prorrateado)
     - `Barbarian 2H` (Greataxe con Furia y Reckless Attack)
     - `Rogue Sneak Attack` (Estoque con escalado de Sneak Attack)
     - `Warlock Eldritch Blast` (Línea base canónica de Treantmonk)
   - Selecciona el **Nivel (1 a 20)**.
   - Observa cómo el panel **"DPR Estimado"** calcula al instante el daño por ronda esperado (ej. nivel 5 Fighter = **19.4 DPR**).
4. Haz clic en **"Añadir al Simulador"**. El héroe quedará seleccionado automáticamente como el Combatiente A.

---

### 4. Ejecutar Simulaciones de Monte Carlo (1v1)
1. En la pestaña **"Simulador de Combate"**:
   - **Combatiente A (Iniciador):** Selecciona tu héroe o cualquier criatura.
     - *Nota:* Si seleccionas un héroe con DPR configurado, el selector de arma mostrará automáticamente `⚡ DPR de Arquetipo (<valor> DPR) [Sin arma requerida]`.
   - **Combatiente B (Defensor):** Selecciona el monstruo rival (ej. *Gorgon Vanguard*, *Bugbear Stalker*) y su arma equipada.
   - **Número de Iteraciones:** Elige entre `100`, `1,000`, `5,000`, `10,000 (Recomendado)` o `20,000 (Stress test)` duelos concurrentes.
2. Haz clic en **"🎲 Simular Combate (Montecarlo)"**.
3. En menos de 25 milisegundos se calcularán los miles de combates en paralelo y se presentará el panel de resultados:
   - **Win Rate:** Barra visual de probabilidad de victoria de cada bando y porcentaje de empates.
   - **Tiempo de Cómputo:** Latencia del cálculo en CPU multihilo.
   - **Promedio de Rondas:** Duración media del encuentro antes de que un contrincante caiga a 0 HP.
   - **DPR Promedio Real:** Daño medio por ronda infligido por cada combatiente a lo largo de la simulación.

---

### 5. Alternar Idioma (i18n)
En la esquina superior derecha del encabezado, haz clic en **"Cambiar a Inglés"** / **"Cambiar a Español"** para alternar de forma fluida toda la terminología táctica de D&D 2024 entre ambos idiomas.

---

## 🧪 Ejecución de Pruebas Automatizadas

Sparnarok cuenta con una suite completa de pruebas unitarias, de integración y end-to-end:

### Pruebas de Backend (.NET)
```powershell
dotnet test
```
*(Ejecuta 179 pruebas: 156 unitarias de dominio y cálculo estocástico + 23 de integración con PostgreSQL/SQLite).*

### Pruebas Unitarias de Frontend (Vitest)
```powershell
cd src/frontend/sparnarok-web
npm test -- --run
```
*(Ejecuta 23 pruebas de componentes con Testing Library y jsdom).*

### Pruebas End-to-End (Playwright)
```powershell
cd tests/e2e
npx playwright test
```
*(Ejecuta 8 pruebas E2E en Chromium levantando automáticamente el servidor web y validando los flujos de navegación, simulación e ingesta).*

---

## 🏛️ Decisiones de Arquitectura (ADRs)

Para comprender el diseño matemático y técnico del sistema, consulta el directorio [`docs/adrs/`](file:///docs/adrs/README.md):
- [ADR-0001: Monorepo y Stack Tecnológico](file:///docs/adrs/0001-monorepo-initial-architecture.md)
- [ADR-0002: Modelado Central D&D 5e (2024)](file:///docs/adrs/0002-dnd-5e-2024-core-domain-modeling.md)
- [ADR-0003: Persistencia con EF Core y PostgreSQL](file:///docs/adrs/0003-efcore-postgresql-persistence-mapping.md)
- [ADR-0004: Capa de Aplicación y Endpoints CRUD](file:///docs/adrs/0004-application-cqrs-and-crud-endpoints.md)
- [ADR-0005: Arquitectura Frontend y Cliente de Consumo](file:///docs/adrs/0005-frontend-creatures-tactical-ui.md)
- [ADR-0006: Pipeline de Ingesta Masiva CSV](file:///docs/adrs/0006-csv-data-ingestion-pipeline.md)
- [ADR-0007: Generación Pseudoaleatoria (RNG) y Evaluación de Dados](file:///docs/adrs/0007-combat-engine-rng-and-dice-evaluation.md)
- [ADR-0008: Concurrencia en Monte Carlo y Métricas de Combate](file:///docs/adrs/0008-montecarlo-simulation-concurrency-and-metrics.md)
- [ADR-0009: Sistema de Armaduras y Cálculo Dinámico de CA](file:///docs/adrs/0009-armor-system-and-dynamic-ac-calculation.md)
- [ADR-0010: Integración Open5e y Perfiles de Héroes Custom](file:///docs/adrs/0010-open5e-integration-and-custom-combatant-profiles.md)
- [ADR-0011: Abstracción de DPR Esperado por Arquetipo](file:///docs/adrs/0011-dpr-baselines-and-archetype-abstraction.md)
