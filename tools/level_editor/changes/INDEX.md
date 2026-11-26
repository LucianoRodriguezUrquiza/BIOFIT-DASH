# Deltas experimentales de niveles

V6.3 en main conserva sus cinco mapas. La rama experimental registra los deltas del usuario con ranura, huellas, altas/bajas/ediciones y nota de validación. Consultar docs/LEVEL_EDITOR_V64.md.

- [BIOFIT_level_1_20261126_001459.json](BIOFIT_level_1_20261126_001459.json) — ranura 1; 69 ediciones, 86 altas, 103 bajas; base e64c3a5a6ef2e7e3234468189bfe38c944b36984b5c6c3818d029f411fff839b; objetivo 8c7adbe0f89959dcf86b89e70c10ed62f084ebbb2407420a56a4ffb159798f0f. Edición experimental desde BIOFIT Level Editor; validación de jugabilidad pendiente.

## Verificación del primer delta (26/11/2026)

- Commit de publicación: `9f1dba739338759ef27f6dfc5b2ab5e169e3586e`. El archivo remoto y el índice coinciden con la salida de PowerShell; 2998 índices únicos, 69 ediciones, 86 altas, 103 bajas. Las altas son 52 sólidos, 31 peligros y 3 decoraciones. La base reconstruida V6.3 tiene SHA-256 coincidente. El publicador local informó reconstrucción exacta del objetivo; no se ha vuelto a calcular el SHA objetivo independientemente en este registro.
- Se eliminaron las tres monedas originales (índices 1005, 1404, 3714) sin nuevas monedas. Cuatro grupos de sólidos nuevos superpuestos en x=24495,24525,24555,24585 GD, y=165 GD: 11 registros en total (7 redundancias potenciales). Revisar si son intencionales; no corregir automáticamente.
- El editor abrió y el usuario exportó/publicó correctamente; alcance de jugabilidad, APK y BLE pendientes. No afirmar que el delta está aplicado localmente sin comprobar Active.json. No modificar main ni la geometría del usuario. El delta requiere base local V6.3 coincidente.
