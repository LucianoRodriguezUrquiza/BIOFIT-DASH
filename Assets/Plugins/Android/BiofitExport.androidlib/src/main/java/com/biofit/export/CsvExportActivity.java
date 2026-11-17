package com.biofit.export;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.widget.Toast;
import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.security.MessageDigest;
import java.util.Arrays;

/**
 * Android SAF export: private app CSV -> user-chosen document.
 * status is polled by Unity, because calling startActivity is NOT proof of a save.
 * No broad storage permissions required. Original remains private and intact.
 */
public final class CsvExportActivity extends Activity {
    private static final int SAVE_CSV = 804;
    public static volatile String status = "SIN_SOLICITUD";

    public static boolean start(Activity activity, String path) {
        if (activity == null || path == null || path.length() == 0) {
            status = "ERROR: actividad o archivo inexistente";
            return false;
        }
        status = "ABRIENDO_SELECTOR";
        try {
            Intent intent = new Intent(activity, CsvExportActivity.class);
            intent.putExtra("biofit_path", path);
            activity.startActivity(intent);
            return true;
        } catch (RuntimeException ex) {
            status = "ERROR: no se pudo abrir exportador: " + ex.getMessage();
            return false;
        }
    }

    private File source() throws Exception {
        String path = getIntent().getStringExtra("biofit_path");
        if (path == null) throw new Exception("Archivo ausente");
        File file = new File(path).getCanonicalFile();
        File root = getExternalFilesDir(null);
        boolean external = root != null
            && file.getPath().startsWith(root.getCanonicalPath() + File.separator);
        boolean internal = file.getPath().startsWith(
            getFilesDir().getCanonicalPath() + File.separator);
        if ((!external && !internal) || !file.isFile()
            || !file.getName().endsWith(".csv"))
            throw new Exception("CSV privado no accesible");
        return file;
    }

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (state != null) return; // keep pending SAF picker after recreation
        try {
            File file = source();
            Intent pick = new Intent(Intent.ACTION_CREATE_DOCUMENT);
            pick.addCategory(Intent.CATEGORY_OPENABLE);
            pick.setType("text/csv");
            pick.putExtra(Intent.EXTRA_TITLE, file.getName());
            startActivityForResult(pick, SAVE_CSV);
            status = "SELECCIONAR_DESTINO";
        } catch (Exception ex) {
            done("ERROR: selector: " + ex.getMessage());
        }
    }

    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != SAVE_CSV) return;
        if (result != RESULT_OK || data == null || data.getData() == null) {
            done("CANCELADO: CSV original conservado");
            return;
        }
        try {
            status = "GUARDANDO";
            File file = source();
            Uri uri = data.getData();
            MessageDigest copied = MessageDigest.getInstance("SHA-256");
            long size = 0;
            try (FileInputStream in = new FileInputStream(file);
                 OutputStream out = getContentResolver().openOutputStream(uri, "wt")) {
                if (out == null) throw new Exception("Destino no disponible");
                byte[] buffer = new byte[8192];
                int n;
                while ((n = in.read(buffer)) != -1) {
                    copied.update(buffer, 0, n);
                    out.write(buffer, 0, n);
                    size += n;
                }
                out.flush();
            }
            if (size != file.length()) throw new Exception("Cantidad de bytes diferente");
            MessageDigest readBack = MessageDigest.getInstance("SHA-256");
            long readBytes = 0;
            try (InputStream verify = getContentResolver().openInputStream(uri)) {
                if (verify == null) throw new Exception("Destino no verificable");
                byte[] buffer = new byte[8192];
                int n;
                while ((n = verify.read(buffer)) != -1) {
                    readBack.update(buffer, 0, n);
                    readBytes += n;
                }
            }
            if (size != readBytes || !Arrays.equals(copied.digest(), readBack.digest()))
                throw new Exception("Verificacion SHA256 del destino fallida");
            done("GUARDADO: CSV verificado (" + size + " bytes)");
        } catch (Exception ex) {
            done("ERROR: CSV no verificado: " + ex.getMessage());
        }
    }

    private void done(String message) {
        status = message;
        Toast.makeText(this, message, Toast.LENGTH_LONG).show();
        finish();
    }
}
