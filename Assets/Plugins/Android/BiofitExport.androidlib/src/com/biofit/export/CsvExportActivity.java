package com.biofit.export;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.widget.Toast;
import java.io.File;
import java.io.FileInputStream;
import java.io.OutputStream;

/** Android Storage Access Framework. No storage permissions or third-party library. */
public final class CsvExportActivity extends Activity {
    private static final int SAVE_CSV = 804;
    public static void start(Activity activity, String path) {
        Intent intent = new Intent(activity, CsvExportActivity.class);
        intent.putExtra("biofit_path", path);
        activity.startActivity(intent);
    }
    private File source() throws Exception {
        String path=getIntent().getStringExtra("biofit_path");
        if(path==null)throw new Exception("Archivo ausente");
        File file=new File(path).getCanonicalFile();
        File root=getExternalFilesDir(null);
        boolean external=root!=null && file.getPath().startsWith(root.getCanonicalPath()+File.separator);
        boolean internal=file.getPath().startsWith(getFilesDir().getCanonicalPath()+File.separator);
        if((!external&&!internal)||!file.isFile()||!file.getName().endsWith(".csv"))
            throw new Exception("Archivo no autorizado");
        return file;
    }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if(state!=null)return; // preserve pending document picker across recreation
        try {
            File file=source();
            Intent pick=new Intent(Intent.ACTION_CREATE_DOCUMENT);
            pick.addCategory(Intent.CATEGORY_OPENABLE);
            pick.setType("text/csv");pick.putExtra(Intent.EXTRA_TITLE,file.getName());
            startActivityForResult(pick,SAVE_CSV);
        } catch(Exception e) { done("No se pudo exportar: "+e.getMessage()); }
    }
    @Override protected void onActivityResult(int request,int result,Intent data) {
        super.onActivityResult(request,result,data);
        if(request!=SAVE_CSV)return;
        if(result!=RESULT_OK||data==null||data.getData()==null){done("Exportacion cancelada; original conservado");return;}
        try(FileInputStream in=new FileInputStream(source());
            OutputStream out=getContentResolver().openOutputStream(data.getData(),"wt")) {
            if(out==null)throw new Exception("Destino no disponible");
            byte[] buffer=new byte[8192];int n;
            while((n=in.read(buffer))!=-1)out.write(buffer,0,n);
            out.flush();
        }catch(Exception e){done("Error al guardar: "+e.getMessage());return;}
        done("CSV guardado completo. Conservar sin convertir a PDF.");
    }
    private void done(String message){Toast.makeText(this,message,Toast.LENGTH_LONG).show();finish();}
}
