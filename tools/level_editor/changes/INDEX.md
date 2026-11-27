# Historial de deltas de niveles — V6.4

Base histórica V6.3: cinco mapas locales y manifiesto inalterados. Los deltas del usuario pasan con la consolidación a main; cada entrada conserva ranura, huellas, altas/bajas/ediciones y validación. Consultar [consolidación V6.4](../../../docs/CONSOLIDACION_V64.md). Las observaciones anteriores son históricas.

- [BIOFIT_level_1_20261126_001459.json](BIOFIT_level_1_20261126_001459.json) — ranura 1; 69 ediciones, 86 altas, 103 bajas; base e64c3a5a6ef2e7e3234468189bfe38c944b36984b5c6c3818d029f411fff839b; objetivo 8c7adbe0f89959dcf86b89e70c10ed62f084ebbb2407420a56a4ffb159798f0f. Edición experimental desde BIOFIT Level Editor; validación de jugabilidad pendiente.

## Verificación del primer delta (26/11/2026)

- Commit de publicación: `9f1dba739338759ef27f6dfc5b2ab5e169e3586e`. El archivo remoto y el índice coinciden con la salida de PowerShell; 2998 índices únicos, 69 ediciones, 86 altas, 103 bajas. Las altas son 52 sólidos, 31 peligros y 3 decoraciones. La base reconstruida V6.3 tiene SHA-256 coincidente. El publicador local informó reconstrucción exacta del objetivo; no se ha vuelto a calcular el SHA objetivo independientemente en este registro.
- Se eliminaron las tres monedas originales (índices 1005, 1404, 3714) sin nuevas monedas. Cuatro grupos de sólidos nuevos superpuestos en x=24495,24525,24555,24585 GD, y=165 GD: 11 registros en total (7 redundancias potenciales). Revisar si son intencionales; no corregir automáticamente.
- El editor abrió y el usuario exportó/publicó correctamente; alcance de jugabilidad, APK y BLE pendientes. No afirmar que el delta está aplicado localmente sin comprobar Active.json. No modificar main ni la geometría del usuario. El delta requiere base local V6.3 coincidente.

- [BIOFIT_level_1_20261126_004628.json](BIOFIT_level_1_20261126_004628.json) — ranura 1; 0 ediciones, 16 altas, 23 bajas; base 8c7adbe0f89959dcf86b89e70c10ed62f084ebbb2407420a56a4ffb159798f0f; objetivo bd2d104cfc88b79751e9cde421f097d6f8413da80847a7ee38c7583379cbfc43. Edición experimental desde BIOFIT Level Editor; validación de jugabilidad pendiente.

- [BIOFIT_level_2_20261126_114359.json](BIOFIT_level_2_20261126_114359.json) — ranura 2; 135 ediciones, 268 altas, 82 bajas; base db175d2b95a020ffcca792ea2b6d0b79dd103af3b8ec8b980ca02e2a7f04da5c; objetivo 39e8beeb17c3a549a6eb452587da86b0655b9e81b0e650ffddc0e9c4cd151651. Edición experimental desde BIOFIT Level Editor; validación de jugabilidad pendiente.

- [BIOFIT_level_3_20261126_174732.json](BIOFIT_level_3_20261126_174732.json) — ranura 3; 163 ediciones, 728 altas, 765 bajas; base 6313a59c0eac4bfdc281ac210226aa079f2ed4fcf5ae6b11e6b7838c078d2a33; objetivo 41cb08ba05e8b2de64522e54d0ed1aaa29bf5551ce2b966e47e232fd038fe78b. Edición experimental desde BIOFIT Level Editor; validación de jugabilidad pendiente.

## Verificación local V6.5 (26/11/2026)

El usuario aportó captura de PowerShell: HEAD local/remoto ffa907d, diff versionado vacío y los cinco hashes Active.json coinciden con referencias publicadas. Nivel 3: 2074 registros; total campaña 12207. Ver [consolidación V6.5](../../../docs/CONSOLIDACION_V65.md). No subir mapas completos ni recursos originales; conservar los deltas y bases locales.

- [BIOFIT_level_3_20261127_155739.json](BIOFIT_level_3_20261127_155739.json) — ranura 3; 163 ediciones, 723 altas, 767 bajas; base 6313a59c0eac4bfdc281ac210226aa079f2ed4fcf5ae6b11e6b7838c078d2a33; objetivo e124ad3a5fdf05e13c59ca8d2910a50e8df6253cf610341c9179a87bb4517b3e. Edición experimental desde BIOFIT Level Editor; validación de jugabilidad pendiente.
