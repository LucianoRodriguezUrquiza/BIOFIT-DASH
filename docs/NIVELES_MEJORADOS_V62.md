# Stereo Madness V6.2 — revisión geométrica
Base: rama niveles-mejorados, V6 (2851 registros). Main V5 permanece sin cambios. DIPL/ICC/RFD/BRO vigentes.
Referencia: las tres parejas de capturas del usuario al 15%, 25%, 49% y las imágenes completas de Ball y Spider. Ajuste a cuadrícula; no equivalencia pixel exacta.
- 15%: eliminar plataforma intermedia, cuatro planks a y158 desde x4275. 25%: ampliar banco a tres planks (6795,6825,6855) y mover siguiente a x6945. 49%: duplicar columna baja y añadir dos bloques bajos.
- Ball: 11 filas / 66 pinchos; plataformas interiores completas de 7+7 bloques.
- Spider: 12 filas / 63 pinchos; plataformas completas de 8+14 bloques; techo +30 GD, suelo final +30 GD; moneda 3 +30 GD vertical.
- 2941 registros activos; SHA256 JSON compacto e31ed489afde8fdcd51dc963d947e6cd0cc702bbb83906175a20df7ecd2b990a.
- Datos locales: solo Assets/BIOFIT/Resources/BiofitStereoLocal/CampaignV5/1/Active.json. Sin cambios al C#, al menú V6.1 local, firmware, BLE, cooldown, otros niveles, música ni reglas de exclusión.
- Pruebas host: transformación con V6 real reconstruida, preflight, instalación, repetición y restauración probadas en proyecto simulado con 26 sprites reales; centinelas menú/nivel 2 intactos. Modelo geométrico simplificado (velocidad 6.5, salto 13.5, gravedad 4, escala temporal 1.5) encontró rutas locales a 400ms para las cinco zonas; no para todas a 500ms o más. No prueba Unity ni latencia BLE. No se garantiza el recorrido completo.
## Instalación
Cerrar Unity, en raíz del proyecto local y rama niveles-mejorados:
```powershell
git fetch origin
git merge --ff-only origin/niveles-mejorados
py -3 .\tools\campaign_v62\instalar_v62.py --proyecto "." --verificar
py -3 .\tools\campaign_v62\instalar_v62.py --proyecto "."
```
No forzar si Git detecta cambios incompatibles. Validar en Unity (2941 registros activos) y probar a CD 400ms. Para restaurar geometría V6 ejecutar el instalador con --restaurar. Menú V6.1 sigue local, publicación separada pendiente.
