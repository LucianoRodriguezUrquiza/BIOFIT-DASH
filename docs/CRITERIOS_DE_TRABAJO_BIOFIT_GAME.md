# BIOFIT GAME — Criterios permanentes de desarrollo y validación

## Prioridad operativa establecida por el usuario (2026-11-14)

La actualización local del repositorio, la importación de Unity, la compilación
de Android, la transferencia e instalación de la APK y la colocación de las
Waveshare suponen mucho tiempo y esfuerzo. Un ciclo de prueba puede requerir
aproximadamente 20 minutos, y el almacenamiento del equipo compila lentamente.

Por lo tanto:

1. **Agrupar cambios relacionados en lotes significativos.** Evitar una compilación
   o APK nueva por cada script, contador, parámetro o ajuste menor.
2. **Diseñar sesiones de validación multifunción.** Preparar una sola APK de
   diagnóstico que permita observar varias métricas y funciones a la vez
   (BLE dual, estados, eventos, alternancia, candidatos de pasos, cadencia,
   movimientos y, cuando corresponda, saltos).
3. **No pedir repetición de ensayos ya suficientemente informativos.** Reutilizar
   capturas, registros y resultados anteriores. Solo solicitar nuevos ensayos
   cuando resuelvan dudas concretas.
4. **Reducir las acciones manuales del usuario.** Realizar los cambios de código
   directamente en GitHub cuando sea posible y ofrecer indicaciones mínimas,
   explícitas y paso a paso para Unity, GitHub Desktop y Android.
5. **No confundir desarrollo rápido con validación superficial.** Agrupar
   funciones relacionadas en entregas progresivas; conservar diagnósticos y
   comprobaciones técnicas relevantes; distinguir candidatos experimentales
   de detecciones validadas.
6. **No solicitar recompilación inmediatamente tras cambios menores.** Acumular
   modificaciones útiles y consolidarlas antes de la siguiente APK.
7. **Evitar solicitar nuevamente que el usuario se coloque los trackers** para
   pruebas que puedan postergarse, simularse o combinarse con pruebas futuras.

## Estado de referencia al establecer este criterio

- Unity 6.3 LTS para Android y repositorio GitHub `BIOFIT-GAME-UNITY`.
- Dos trackers Waveshare QMI8658, izquierda y derecha, ya calibrados.
- Recepción de eventos BLE de ambas placas confirmada en APK de diagnóstico.
- `BiofitInputEngine` experimental añadido al repositorio; sus estimaciones de
  pasos/cadencia/actividad todavía no están validadas.
- Preferencia: planificar el siguiente lote funcional completo antes de pedir
  la próxima compilación o colocación de sensores.

Estos criterios deben consultarse al planificar las próximas actualizaciones.
