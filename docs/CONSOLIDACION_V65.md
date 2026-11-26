# Consolidación V6.5 — registro RCA (26/11/2026)

## Aceptación y alcance
El usuario informa APK reciente compilada y probada satisfactoriamente, y autoriza corrección de CI, documentación y consolidación posterior. No es certificación exhaustiva de Unity, Android o sensores. Captura PowerShell: HEAD local y remoto ffa907d6baa7974b8152b9622026679f2981a52a, rama v6.5-desarrollo, diff versionado vacío. BiofitUi y Songs (carpetas y .meta) no versionados: mantener local, no publicar ni borrar sin revisar procedencia.

## Mapas activos
| Nivel | Registros | SHA-256 observado en captura y referencia remota |
|---|---:|---|
| Stereo Madness | 2991 | `bd2d104cfc88b79751e9cde421f097d6f8413da80847a7ee38c7583379cbfc43` |
| Back on Track | 1920 | `39e8beeb17c3a549a6eb452587da86b0655b9e81b0e650ffddc0e9c4cd151651` |
| Polargeist | 2074 | `41cb08ba05e8b2de64522e54d0ed1aaa29bf5551ce2b966e47e232fd038fe78b` |
| Can't Let Go | 1871 | `cc53076263427535a026c7e0d84c6eeb52e717a2627ef9017390afa548bf0d80` |
| Jumper | 3351 | `87f253f933656a0da853ccd1ddeb139c653e3ec4c5f75e034998e4f9f4b3c756` |
Total: 12207. Niveles 1–3 mediante deltas; 4–5 coinciden con manifiesto V6.3. No se incluyen mapas completos ni arte original. La reconstrucción usa las bases locales y [deltas](../tools/level_editor/changes/INDEX.md).

## Avances V6.5
Editor: vista real, prueba desde X/Y, catálogo por categorías, portapapeles, paso GD configurable, transformaciones grupales y funciones múltiples, guías de trayectorias. Mecánicas ya publicadas: Mini Cube, Red Pad, orbes adicionales y extensión autorizada de pads/orbes a Ball/Spider. Detalles, decisiones y limitaciones en [guía](LEVEL_EDITOR_V65.md) y contexto histórico. Polargeist: 163 ediciones, 728 altas y 765 bajas.

## Corrección de CI
[Run fallido](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/actions/runs/45893627968): 4/5, regression se detuvo en tests/check_geometry_ux.py línea 525, porque buscar o.frames= también coincide con o.frames==null (lectura). Se reemplaza por detección de escrituras que distingue igualdad y cubre asignaciones simples, compuestas e incrementos/decrementos, con casos positivos y negativos. Se conserva el contrato de preservar geometría y sprites; no se altera código C#, niveles, interfaz, BLE, firmware, física, recursos ni exclusiones. El resto de la regresión quedaba sin ejecutar en el run fallido. Nuevo CI pendiente antes de promover main.

## Pendiente técnico
Investigar fallo reportado de saltos corporales repetidos, con recuperación tras reposo; causa no demostrada. Localizar etapa mediante contadores UP, rearme, recepción BLE, emparejamiento y acción del personaje. No modificar ICC sin diagnóstico y autorización. No se ha hecho prueba física independiente.

## Instalación
No se requiere ZIP ni recompilar APK por esta corrección de pruebas/documentación. Con Unity cerrado, sincronizar mediante git fetch y git pull --ff-only en rama actual; tras consolidar, pasar a main solo cuando el estado local lo permita. No usar git add . ni reset destructivo.
