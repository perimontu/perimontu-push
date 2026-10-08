# CoberPush.Api

Servicio sin estado que envía notificaciones push por FCM y reenvía al backend de cada proyecto los avisos de lectura. Atiende a **varios proyectos de Firebase** desde una sola instancia. Plan en el artifact `plan_multiproyecto.md` de la conversación de diseño; requerimientos `REQ-009`, `REQ-010` y `REQ-011` en `CoberApp/.agent/memorias/requerimientos.md`. Contrato de la API: `CoberApp/.agent/memorias/Push/API/PushAPI.md`.

## Endpoints

`{projectId}` es el id alfanumérico del proyecto en `appsettings` (no distingue mayúsculas).

| Método | Ruta | Protección |
|---|---|---|
| `POST` | `/api/{projectId}/push/send` | API key del proyecto + IP permitida + rate limit de la key |
| `POST` | `/api/{projectId}/push/topic/{topic}` | Ídem |
| `POST` | `/api/{projectId}/receipts/read` | Recibo firmado + rate limit por IP (sin filtro de IP) |
| `GET` | `/api/{projectId}/health` | API key del proyecto (sin filtro de IP). Comprueba Firebase |
| `GET` | `/health` | Pública (el proceso vive) |

Ejemplos en [`CoberPush.Api.http`](CoberPush.Api.http).

## Configuración por proyecto

Todo vive en el array `Projects` de `appsettings`. Cada proyecto reúne su backend de Firebase, sus consumidores (API keys), su backend de destino y sus límites:

```json
"Projects": [{
  "Id": "cober",
  "Name": "Cober Touch",
  "Enabled": true,
  "Firebase": { "ProjectId": "cober-touch-64940", "CredentialsEnvVar": "GOOGLE_APPLICATION_CREDENTIALS" },
  "AllowedIps": [ "203.0.113.10", "198.51.100.0/24" ],
  "ApiKeys": [
    { "Name": "backend-php", "Enabled": true, "Key": "<32+ caracteres>",
      "ExpiresAt": "2027-01-31T23:59:59-03:00",
      "AllowedIps": [ "203.0.113.20" ],
      "RateLimit": { "PermitLimit": 60, "WindowSeconds": 60 } }
  ],
  "DefaultRateLimit":  { "PermitLimit": 30,  "WindowSeconds": 60 },
  "ReceiptsRateLimit": { "PermitLimit": 120, "WindowSeconds": 60 },
  "Receipts": { "HmacSecret": "<32+ caracteres>", "MaxAgeDays": 30 },
  "UrlApi":   { "BaseUrl": "https://www.cober.com.ar/api/push", "ReadPath": "/read", "BearerToken": "<token>", "TimeoutSeconds": 10, "RetryCount": 2 },
  "UrlPolicy": { "AllowedHosts": [ "www.cober.com.ar", "cober.com.ar" ], "CanonicalHost": "www.cober.com.ar", "PathPrefix": "/app" }
}]
```

| Campo | Detalle |
|---|---|
| `Firebase.CredentialsEnvVar` | **Nombre** de la variable de entorno cuyo valor es la ruta al JSON de la cuenta de servicio (no `google-services.json`). El JSON debe ser del `Firebase.ProjectId` indicado; si no, el envío responde `503` |
| `AllowedIps` | IP suelta o CIDR. Nivel proyecto; una key que define las suyas **sobrescribe** estas. Vacío en ambos niveles = nadie puede enviar |
| `ApiKeys[].Key` | En texto plano, ≥ 32 caracteres, única en todo el archivo. Para rotar: agregar otra key y poner `Enabled: false` a la vieja |
| `ApiKeys[].ExpiresAt` | Fecha y hora con zona; pasada, la key da `401`. Vacío = no vence |
| `ApiKeys[].RateLimit` | Si falta, se usa `DefaultRateLimit` del proyecto |
| `Receipts.HmacSecret` | ≥ 32 caracteres. El `projectId` entra en la firma: un recibo de un proyecto no vale en otro |

Valores globales: `Api:KeyHeader`, `Api:KnownProxies` (proxies de confianza, p. ej. nginx de Plesk, para leer la IP real de `X-Forwarded-For`), `Api:UnauthenticatedRateLimit` (límite por IP para quien no presenta una key válida) y `Push:*`.

**Las keys y los secretos están en los `appsettings`, no hay user-secrets.** Por eso:

- `appsettings.json` (versionado) deja `Key`, `HmacSecret` y `BearerToken` **vacíos**: la API no arranca hasta completarlos y dice cuál falta.
- Los valores reales de desarrollo van en `appsettings.Development.json`, que está en `.gitignore`. En el servidor, en `appsettings.Production.json` (también ignorado) con permisos restrictivos.
- Si el archivo ya estaba en git: `git rm --cached appsettings.Development.json`.

Si algo de un proyecto es inválido (id, key corta o repetida, IP/CIDR, URL, falta la variable de credencial…), la API **no arranca** y lista todos los errores con su proyecto y campo.

## Desarrollo local

```powershell
# Ruta (fuera del repo y de carpetas sincronizadas) al JSON de la cuenta de servicio, una variable por proyecto
$env:GOOGLE_APPLICATION_CREDENTIALS = "C:\ruta\fuera\del\repo\cober-touch-adminsdk.json"
dotnet run --launch-profile http
```

Una variable de entorno de **usuario** nueva en Windows solo la ven los procesos iniciados después (reiniciar Visual Studio).

## Pruebas

```powershell
dotnet test tests\CoberPush.Api.Tests
```

Las pruebas usan un Firebase y un backend de destino simulados; la entrega real por FCM solo se verifica con un dispositivo y la credencial real.

## Notas

- `success: true` en un envío significa que **FCM aceptó el mensaje**, no que llegó al dispositivo.
- `SENDER_ID_MISMATCH` (el token es de otro proyecto de Firebase) se informa como `NO_EXISTE` y **no** pide borrar el token.
- FirebaseAdmin 3.7 marca `MulticastMessage.Tokens` como obsoleto a favor de `Fids`; hoy la app usa tokens de registro, por eso se sigue usando `Tokens`.
