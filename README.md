# CoberPush.Api

Servicio sin estado que envía notificaciones push por FCM y reenvía a una API PHP los avisos de lectura. Plan completo en el artifact `plan_coberpush_api.md` de la conversación de diseño; requerimientos `REQ-009` y `REQ-010` en `CoberApp/.agent/memorias/requerimientos.md`.

## Endpoints

| Método | Ruta | Protección |
|---|---|---|
| `POST` | `/api/push/send` | IP permitida + `X-Api-Key` + rate limit |
| `POST` | `/api/push/topic/{topic}` | IP permitida + `X-Api-Key` + rate limit |
| `POST` | `/api/receipts/read` | Recibo firmado + rate limit por IP (sin filtro de IP) |
| `GET` | `/health` | Pública |

Ejemplos en [`CoberPush.Api.http`](CoberPush.Api.http).

## Configuración

Todo está en `appsettings.json` (sin secretos). Secretos por variable de entorno o `user-secrets`:

| Secreto | Variable |
|---|---|
| API key de envío (≥ 32 caracteres) | `Api__Key` |
| Secreto HMAC de recibos (≥ 32 caracteres) | `Receipts__HmacSecret` |
| Bearer hacia la API PHP | `PhpApi__BearerToken` |
| Credencial de servicio de Firebase (archivo JSON, **no** `google-services.json`) | `GOOGLE_APPLICATION_CREDENTIALS` |

IPs permitidas para enviar (IP suelta o CIDR): `Api:AllowedSendIps`. Con la lista vacía **nadie** puede enviar.
Detrás de nginx/Plesk, poner el proxy en `Api:KnownProxies` para que la IP real se lea de `X-Forwarded-For`.

Desarrollo local:

```powershell
dotnet user-secrets set "Api:Key" "<32+ caracteres>"
dotnet user-secrets set "Receipts:HmacSecret" "<32+ caracteres>"
dotnet user-secrets set "PhpApi:BearerToken" "<token>"
dotnet user-secrets set "Api:AllowedSendIps:0" "127.0.0.1"
$env:GOOGLE_APPLICATION_CREDENTIALS = "C:\ruta\fuera\del\repo\cober-touch-adminsdk.json"
dotnet run --launch-profile http
```

Si falta un secreto o hay una IP/CIDR inválida, la API **no arranca** y lo indica.

## Pruebas

```powershell
dotnet test tests\CoberPush.Api.Tests
```

Las pruebas usan un Firebase y un PHP simulados; la entrega real por FCM solo se verifica con un dispositivo y la credencial real.

## Notas

- `success: true` en un envío significa que **FCM aceptó el mensaje**, no que llegó al dispositivo.
- FirebaseAdmin 3.7 marca `MulticastMessage.Tokens` como obsoleto a favor de `Fids`; hoy la app usa tokens de registro, por eso se sigue usando `Tokens`.
