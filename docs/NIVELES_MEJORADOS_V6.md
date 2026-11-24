# V6 experimental — Niveles mejorados / Stereo Madness
Base: V5 Campaña Clásica, main ab1d29b848fd32d393b5aad26a1072c2e5e6183f. DIPL / ICC / RFD / BRO vigentes.

Referencia: MAPA completo Stereo Madness.png, 2048 × 44 px, editado por el usuario; comparación con Full Level Map V5. La escala limita la identificación exacta de cada bloque. Se interpretaron las superposiciones y se ajustaron los obstáculos a la cuadrícula original de 30 GD.
- Cube: ampliar dos plataformas (x 4095 y 4335 GD) con cinco piezas adicionales; rebajar tres columnas cerca del 50% (x 13215, 13335 y 13455). Mantener las monedas.
- Ball (7995–12555 GD): ocho grupos alternantes de obstáculos, dos plataformas interiores; conservar suelo, techo y transición.
- Spider (22935–26565 GD): ocho grupos alternantes y dos plataformas interiores; conservar suelo, techo, portal y meta.
- Total: 2785 -> 2851 registros activos; 44 retirados y 110 añadidos. Pinchos de los corredores: 38 -> 78. Tres monedas intactas.
- Modelo geométrico nominal: 9.75 unidades/s, 1.23 s mínimo entre acciones centrales estimadas de Ball/Spider, con 150 GD de anticipación Ball y 60 GD Spider. No constituye validación física ni garantía con 900 ms.
- Instalador modifica solo Assets/BIOFIT/Resources/BiofitStereoLocal/CampaignV5/1/Active.json. No modifica código C#, firmware, BLE, cooldown, física, otros niveles, reglas de exclusión ni archivos fuente originales.
- SHA256 JSON compacto V5: b92994dcd97d2adf37754b6ceb7b82784d7895ede36b8f39a077133e9c6ef58b.
- SHA256 JSON compacto V6: 7c1b2f7dbe15cd88627ae27d52d05ad64ebf36dd92c62f8e22577f6b782f502a.
- Pruebas: reconstrucción con datos V5 reales de nivel 1; preflight, instalación y restauración probadas en estructura simulada con sprites de prueba; conservación de niveles 2–5, monedas, portales y registros no seleccionados. Unity y BLE físico pendientes.

## Instalar sobre el mismo proyecto
Cerrar Unity, ubicarse en la raíz del proyecto local, git fetch origin, git switch --track origin/niveles-mejorados (solo si aún no existe rama local).
```powershell
py -3 .\tools\campaign_v6\instalar_v6.py --proyecto "." --verificar
py -3 .\tools\campaign_v6\instalar_v6.py --proyecto "."
```
El script guarda solo el JSON anterior en Builds/BiofitV6/Active_V5.json. Para volver a V5, cerrar Unity y ejecutar:
```powershell
py -3 .\tools\campaign_v6\instalar_v6.py --proyecto "." --restaurar
```
Cambiar de rama Git por sí solo no restaura los datos locales ignorados. No fusionar con main hasta validar el recorrido y el BLE.
