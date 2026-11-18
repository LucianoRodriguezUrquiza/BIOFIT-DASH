# v0.9.6 — recuperación de sesiones y exportación dual

**Base:** feat/raw-session-v095 (PR #10). **Implementación:** fix/export-recovery-ui-v096 (PR #11, borrador). Firmware v2.2.7, calibración, NVS, umbrales iniciales 0,25/0,25/0,12 g y ventana 180 ms sin modificar.

## Incidencia física y diagnóstico

Tras una sesión de unos 10 minutos con decenas de miles de paquetes RAW, la interfaz mostró a la vez «Cerrando CSV y RAW...» y «CSV guardado SHA256 OK + RAW», pero el usuario no encontró archivos en Descargas. Una sesión corta posterior sí se exportó. Esto **no demuestra la pérdida del archivo privado**. La v0.9.5 conservaba un mensaje de FINALIZAR sin actualizar y exportaba desde una operación sincrónica que procesaba archivos completos. La pantalla no permitía elegir capturas antiguas.

## Cambios implementados

- Escritura privada progresiva en .partial, con SHA256 incremental y verificación por streaming; renombrar a CSV únicamente después de comprobar la integridad. Sin reconstruir el archivo entero en memoria. Sesiones antiguas conservadas y nuevas con prefijo BIOFIT_v096_.
- Botón SESIONES: muestra hasta 80 CSV principales privados y parciales, por nombre, tamaño, SHA y presencia/estado del RAW correspondiente. Se puede elegir la sesión larga v0.9.5 aunque haya habido otra más reciente.
- Método 1, 3. ARCHIVO: exportación asíncrona MediaStore a Descargas, bloques de 64 KiB, SHA256 origen/destino y publicación solo cuando se verifica. Conserva originales privados ante cualquier error.
- Método 2, 4. RESCATE: selector Android Storage Access Framework (ACTION_CREATE_DOCUMENT), para elegir el destino del CSV principal y, si existe, luego el destino del CSV RAW. Exportación por bloques y verificación de bytes + SHA256; independiente de MediaStore.
- Botones EXP y RAW adyacentes en la misma fila. Estado grande y explícito arriba: SIN GRABACION, GRABANDO: ETAPA, FINALIZANDO, GUARDADO PRIVADO o EXPORTANDO. Mensajes de error y progreso en el panel inferior; se evita interpretar GUARDADO PRIVADO como exportado a Descargas.
- No hay modificaciones deliberadas en el detector, QMI8658, BLE de eventos, calibración, filtros, umbrales ni NVS.

## Interpretación de estados

| Estado | Significado |
|---|---|
| GRABANDO | Datos hacia archivos privados progresivos |
| FINALIZANDO | Colas, SHA y cierre del archivo privado |
| GUARDADO PRIVADO / SHA OK | Archivo interno íntegro; todavía NO implica exportación |
| EXPORTANDO | Copia a MediaStore o SAF, no desconectar ni cerrar |
| EXPORTADO Y VERIFICADO | Destino reabierto, tamaño y SHA coincidentes |
| ERROR / CANCEL | Conservar originales internos, intentar el método alternativo |

## Recuperar la prueba larga sin repetir movimientos

1. Mantener instalada v0.9.5 y sus datos hasta poder instalar la APK 0.9.6 como actualización con mismo package ID y firma. No desinstalar.
2. Abrir BIOFIT DASH y tocar SESIONES, sin INICIAR grabación nueva.
3. Ubicar BIOFIT_v095_ de fecha de la prueba larga y tamaño mayor que las pruebas breves. Seleccionar la fila que indique SHA OK.
4. Tocar 3. ARCHIVO. Verificar CSV principal y RAW del mismo ID en Descargas y el mensaje EXPORTADO Y VERIFICADO.
5. Si falla, tocar 4. RESCATE; elegir dónde guardar cada uno de los archivos desde el selector del sistema (dos elecciones si hay RAW). Conservar ambos CSV originales.
6. Si únicamente aparecen .partial o SHA incorrecto, no presentarlos como CSV completos. Guardar nombre/estado y no borrar esos originales.
7. Antes de repetir 40 movimientos, hacer una única sesión corta de humo RAW con dos sensores y comprobar ambas exportaciones en Android real.

## Observaciones aportadas por el usuario (sin CSV ni vídeo)

| Bloque | Resultado reportado |
|---|---|
| 1 DER 0,25 RAW OFF | Una lenta y otra rápida: ninguna respuesta |
| A DER 0,25 RAW ON | Lentas: un salto durante descenso; rápidas: tres saltos durante descenso |
| B DER 0,15 EXP | Lentas: dos respuestas (una ascenso y otra descenso); rápidas: cinco saltos para tres movimientos, entre ascenso y descenso |
| A' DER 0,25 | Lentas: ninguna; rápidas: respuestas durante descenso |
| C IZQ 0,25 | Lentas: ninguna; rápidas: ascenso y descenso (no el primer ascenso) |
| D BILATERAL 0,12 | Lentas: ninguna; rápidas: ascenso y descenso |
| E BILATERAL 0,10 | Lentas: solo ascenso; rápidas: ascenso y descenso |
| 8 DER 0,25 RAW OFF | Lenta y rápida: respuesta al bajar; primera tentativa previa a SYNC sin respuesta |

Estas observaciones apuntan a disparos durante frenado/descenso y posibles dobles al reducir umbral, pero no permiten medir tiempos reales ni sensibilidad sin registros RAW. No cambiar valores de producción basándose en estas notas.

## Validación y límites

Pruebas host: regresiones originales, nuevo test de 48.000 filas principales y 31.000 RAW con hash streaming, compilación Java contra SDK Android y stub de UnityPlayer. **No constituyen una compilación Unity ni prueba Android física**. Requiere Play, compilación APK con misma firma y prueba MediaStore + SAF con tamaños crecientes y fallos/cancelaciones. Los .partial se conservan, pero no se exportan como completos. No fusionar PR hasta validar en dispositivo.

Referencias: https://developer.android.com/training/data-storage/shared/documents-files y https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/developing/plugins-for/plugin-types/java-source-plugins