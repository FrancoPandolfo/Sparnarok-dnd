# Guía de Despliegue en Producción: Render + Neon.tech

Esta guía describe el procedimiento paso a paso para desplegar la API backend de **Sparnarok (.NET 10)** en **Render** (alojamiento gratuito mediante contenedores Docker) conectado a **Neon.tech** (PostgreSQL Serverless gratuito).

---

## 1. Paso 1: Configurar la Base de Datos en Neon.tech

1. Accede a [Neon.tech](https://neon.tech/) e inicia sesión o crea una cuenta gratuita.
2. Haz clic en **"New Project"**.
3. Asigna un nombre al proyecto (ej. `sparnarok-db`) y selecciona la región **US East (Ohio / N. Virginia)** o la región más cercana a tu servicio de Render.
4. Una vez creado el proyecto, ve al **Dashboard** principal del proyecto.
5. En la sección **"Connection Details"**, asegúrate de que esté seleccionada la base de datos `neondb` y copia la **Connection String**.
   - El formato obtenido será similar a:
     ```text
     postgresql://neondb_owner:npg_AbCd123xyz@ep-fancy-pooler-123456.us-east-2.aws.neon.tech/neondb?sslmode=require
     ```
   > **Nota de compatibilidad:** El backend de Sparnarok cuenta con un parser automático (`PostgresConnectionHelper`) que acepta tanto la URI estándar `postgresql://...` de Neon como el formato tradicional ADO.NET (`Host=...;Database=...`). No necesitas convertirla manualmente.

---

## 2. Paso 2: Crear el Servicio Web en Render.com

1. Accede a tu panel en [Render.com](https://dashboard.render.com/).
2. Haz clic en el botón **"New +"** (arriba a la derecha) y selecciona **"Web Service"**.
3. Conecta tu cuenta de GitHub o GitLab y selecciona el repositorio de **Sparnarok**.
4. Configura los parámetros principales del servicio:
   - **Name:** `sparnarok-api` (o el nombre de tu preferencia).
   - **Region:** Selecciona la misma región o la más cercana a tu base de datos de Neon (ej. `Ohio (US East)`).
   - **Branch:** `main` (o la rama que desees desplegar).
   - **Root Directory:** Déjalo vacío si usas el `Dockerfile` de la raíz, o especifica `src/backend/Sparnarok.Api` si prefieres compilar únicamente la carpeta del backend.
   - **Language / Runtime:** Asegúrate de que esté seleccionado **Docker**.
   - **Plan Type:** Selecciona el plan **Free**.

---

## 3. Paso 3: Configurar Variables de Entorno en Render

1. En la misma pantalla de configuración (o en la pestaña **Environment** del servicio una vez creado), busca la sección **"Environment Variables"**.
2. Añade la variable de entorno obligatoria para la base de datos:
   - **Key:** `ConnectionStrings__DefaultConnection`
   - **Value:** Pega la Connection String completa copiada en el Paso 1 desde Neon.tech:
     ```text
     postgresql://neondb_owner:npg_AbCd123xyz@ep-fancy-pooler-123456.us-east-2.aws.neon.tech/neondb?sslmode=require
     ```
3. (Opcional) Variables adicionales recomendadas:
   - **Key:** `ENABLE_SWAGGER` | **Value:** `true` (habilita la interfaz interactiva de Swagger en producción).
   - **Key:** `ASPNETCORE_ENVIRONMENT` | **Value:** `Production`.

---

## 4. Paso 4: Desplegar y Verificar

1. Haz clic en **"Create Web Service"** (o **"Manual Deploy" -> "Deploy latest commit"**).
2. Render iniciará la construcción del contenedor Docker multi-etapa:
   - Restaurará las dependencias NuGet desde la capa en caché de .NET 10.
   - Compilará la aplicación en modo `Release`.
   - Empaquetará los binarios en la imagen runtime ultraligera `mcr.microsoft.com/dotnet/aspnet:10.0`.
   - Expondrá el puerto `8080` vía `ENV ASPNETCORE_URLS=http://+:8080`.
3. **Migración automática en el primer arranque:**
   - Durante el inicio del contenedor, Entity Framework Core ejecutará automáticamente `db.Database.Migrate()`, creando todas las tablas relacionales (`Creatures`, `WeaponMasteries`, `Parties`, `PartyMembers`, `StashItems`, etc.) en tu base de datos de Neon.
   - Se sembrarán los datos iniciales y el catálogo de monstruos y armas canónicas.
4. **Verificación:**
   - Una vez que los logs indiquen `Application started. Press Ctrl+C to shut down`, abre la URL pública generada por Render (ej. `https://sparnarok-api.onrender.com/swagger`).
   - Deberás ver la documentación interactiva de OpenAPI / Swagger UI lista para ejecutar peticiones.

---

## 5. Método Alternativo: Despliegue con Blueprint (`render.yaml`)

Si prefieres usar Infraestructura como Código (IaC):

1. En Render, ve a **"New +" -> "Blueprint"**.
2. Selecciona el repositorio de **Sparnarok**.
3. Render detectará automáticamente el archivo [render.yaml](file:///c:/Users/franc/Desktop/Sparnarok-dnd/render.yaml) en la raíz.
4. Render te solicitará ingresar el valor de la variable `ConnectionStrings__DefaultConnection`. Pega allí la URI de Neon.tech.
5. Haz clic en **"Apply"** para crear y desplegar el servicio de forma automatizada.
