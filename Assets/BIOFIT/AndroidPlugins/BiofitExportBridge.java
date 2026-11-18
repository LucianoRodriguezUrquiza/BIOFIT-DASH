package com.biofit.game;

import android.app.Activity;
import android.app.Fragment;
import android.app.FragmentManager;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.MediaStore;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.security.MessageDigest;
import java.util.Arrays;

/**
 * BIOFIT v0.9.6. Two independent Android destinations:
 *  - DOWNLOADS via MediaStore with IS_PENDING until content verification;
 *  - SAF ACTION_CREATE_DOCUMENT selected by the user (one chooser per file).
 * Never deletes, renames or modifies the original private files.
 *
 * Invoked by Unity on the UI thread. All substantial file IO occurs on Java
 * worker threads. Every completion/error goes to the Unity MonoBehaviour.
 */
public final class BiofitExportBridge {
    private static final int REQUEST_SAF = 4926;
    private static final String FRAGMENT_TAG = "biofit.saf.export.v096";
    private static final int BUFFER = 65536;

    private BiofitExportBridge() {}

    private static void send(String receiver, String state) {
        UnityPlayer.UnitySendMessage(receiver, "OnBiofitExportResult",
                state.replace('\n', ' ').replace('\r', ' '));
    }

    private static void requireInput(String[] files) throws Exception {
        if (files == null || files.length == 0 || files.length > 2)
            throw new IllegalArgumentException("Se esperan uno o dos CSV");
        for (String path : files)
            if (path == null || !new File(path).isFile())
                throw new java.io.FileNotFoundException("Archivo privado no encontrado");
    }

    private static String safeName(String path) {
        return new File(path).getName().replace("|", "_");
    }

    /**
     * Copy in chunks, verify the same SHA-256 and number of bytes by reopening
     * the destination. Do NOT copy through a giant Java String or byte[].
     */
    private static long copyAndVerify(Activity activity, String path, Uri target,
                                      String receiver, String channel, int index,
                                      int total) throws Exception {
        File source = new File(path);
        ContentResolver resolver = activity.getContentResolver();
        MessageDigest sourceHash = MessageDigest.getInstance("SHA-256");
        byte[] buffer = new byte[BUFFER];
        long written = 0L;
        long progress = 0L;

        try (InputStream input = new FileInputStream(source);
             OutputStream output = resolver.openOutputStream(target, "w")) {
            if (output == null) throw new java.io.IOException("Destino no escribible");
            int count;
            while ((count = input.read(buffer)) != -1) {
                if (count == 0) continue;
                sourceHash.update(buffer, 0, count);
                output.write(buffer, 0, count);
                written += count;
                if (written - progress >= 1048576L) {
                    progress = written;
                    send(receiver, "PROGRESS|" + channel + "|" + index + "/" + total
                            + "|" + safeName(path) + "|" + written);
                }
            }
            output.flush();
        }

        MessageDigest targetHash = MessageDigest.getInstance("SHA-256");
        long readBack = 0L;
        try (InputStream check = resolver.openInputStream(target)) {
            if (check == null) throw new java.io.IOException("Destino no legible");
            int count;
            while ((count = check.read(buffer)) != -1) {
                if (count == 0) continue;
                targetHash.update(buffer, 0, count);
                readBack += count;
            }
        }
        if (written != source.length() || written != readBack
                || !Arrays.equals(sourceHash.digest(), targetHash.digest()))
            throw new java.io.IOException("No coincide SHA256/tamaño del CSV exportado");
        return written;
    }

    /** Export to Downloads with a private original retained on every error. */
    public static void startDownloads(final Activity activity, final String[] files,
                                      final String receiver) {
        new Thread(new Runnable() {
            @Override public void run() {
                try {
                    requireInput(files);
                    if (Build.VERSION.SDK_INT < 29)
                        throw new UnsupportedOperationException("Descargas requiere Android 10+");
                    final ContentResolver resolver = activity.getContentResolver();
                    for (int i = 0; i < files.length; i++) {
                        final int index = i + 1;
                        String path = files[i];
                        send(receiver, "PROGRESS|D|" + index + "/" + files.length
                                + "|" + safeName(path) + "|0");

                        ContentValues meta = new ContentValues();
                        meta.put(MediaStore.MediaColumns.DISPLAY_NAME, safeName(path));
                        meta.put(MediaStore.MediaColumns.MIME_TYPE, "text/csv");
                        meta.put(MediaStore.MediaColumns.RELATIVE_PATH,
                                Environment.DIRECTORY_DOWNLOADS + "/");
                        meta.put(MediaStore.MediaColumns.IS_PENDING, 1);
                        Uri target = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, meta);
                        if (target == null)
                            throw new java.io.IOException("MediaStore no creo documento");
                        boolean published = false;
                        try {
                            long size = copyAndVerify(activity, path, target, receiver,
                                    "D", index, files.length);
                            ContentValues done = new ContentValues();
                            done.put(MediaStore.MediaColumns.IS_PENDING, 0);
                            if (resolver.update(target, done, null, null) != 1)
                                throw new java.io.IOException("No se pudo publicar en Descargas");
                            published = true;
                            send(receiver, "OK|D|" + index + "/" + files.length
                                    + "|" + safeName(path) + "|" + size);
                        } finally {
                            if (!published)
                                try { resolver.delete(target, null, null); } catch (Exception ignored) {}
                        }
                    }
                    send(receiver, "DONE|D|" + files.length + " archivo(s) verificados");
                } catch (Exception ex) {
                    send(receiver, "ERROR|D|" + ex.getClass().getSimpleName()
                            + ": " + ex.getMessage());
                }
            }
        }, "BiofitMediaStoreExport").start();
    }

    /** Starts an independent Android system file picker; each file is selected. */
    public static void startSaf(final Activity activity, final String[] files,
                                final String receiver) {
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                try {
                    requireInput(files);
                    FragmentManager fm = activity.getFragmentManager();
                    if (fm.findFragmentByTag(FRAGMENT_TAG) != null)
                        throw new IllegalStateException("Ya hay un exportador SAF activo");
                    SafFragment fragment = new SafFragment();
                    fm.beginTransaction().add(fragment, FRAGMENT_TAG).commit();
                    fm.executePendingTransactions();
                    fragment.begin(files, receiver);
                } catch (Exception ex) {
                    send(receiver, "ERROR|S|" + ex.getClass().getSimpleName()
                            + ": " + ex.getMessage());
                }
            }
        });
    }

    public static final class SafFragment extends Fragment {
        private String[] files;
        private String receiver;
        private int current;

        public void begin(String[] paths, String callbackObject) {
            files = paths;
            receiver = callbackObject;
            current = 0;
            launch();
        }

        private void launch() {
            try {
                Intent picker = new Intent(Intent.ACTION_CREATE_DOCUMENT);
                picker.addCategory(Intent.CATEGORY_OPENABLE);
                picker.setType("text/csv");
                picker.putExtra(Intent.EXTRA_TITLE, safeName(files[current]));
                picker.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION
                        | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
                send(receiver, "PICKER|S|" + (current + 1) + "/" + files.length
                        + "|" + safeName(files[current]));
                startActivityForResult(picker, REQUEST_SAF);
            } catch (Exception ex) {
                send(receiver, "ERROR|S|" + ex.getMessage());
                finish();
            }
        }

        @Override public void onActivityResult(int requestCode, int resultCode, Intent result) {
            super.onActivityResult(requestCode, resultCode, result);
            if (requestCode != REQUEST_SAF) return;
            if (resultCode != Activity.RESULT_OK || result == null
                    || result.getData() == null) {
                send(receiver, "CANCEL|S|Selector cancelado; originales conservados");
                finish();
                return;
            }
            final Uri uri = result.getData();
            final Activity activity = getActivity();
            final int index = current;
            final String path = files[index];
            send(receiver, "PROGRESS|S|" + (index+1) + "/" + files.length
                    + "|" + safeName(path) + "|0");
            new Thread(new Runnable() {
                @Override public void run() {
                    try {
                        long size = copyAndVerify(activity, path, uri, receiver,
                                "S", index+1, files.length);
                        send(receiver, "OK|S|" + (index+1) + "/" + files.length
                                + "|" + safeName(path) + "|" + size);
                        activity.runOnUiThread(new Runnable() {
                            @Override public void run() {
                                if (!isAdded()) return;
                                current++;
                                if (current < files.length) launch();
                                else {
                                    send(receiver, "DONE|S|" + files.length
                                            + " archivo(s) verificados");
                                    finish();
                                }
                            }
                        });
                    } catch (final Exception ex) {
                        // Best effort remove a damaged SAF document; private source is untouched.
                        try { activity.getContentResolver().delete(uri, null, null); }
                        catch (Exception ignored) {}
                        send(receiver, "ERROR|S|" + ex.getClass().getSimpleName()
                                + ": " + ex.getMessage());
                        activity.runOnUiThread(new Runnable() {
                            @Override public void run() { finish(); }
                        });
                    }
                }
            }, "BiofitSafExport").start();
        }

        private void finish() {
            if (isAdded()) {
                try { getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss(); }
                catch (Exception ignored) {}
            }
        }
    }
}
