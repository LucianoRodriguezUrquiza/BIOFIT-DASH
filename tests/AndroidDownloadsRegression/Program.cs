using System;
using System.IO;
using System.Linq;
using System.Text;

class Program
{
    static int checks;
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL: " + label);
        checks++;
    }
    static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "biofit-mediastore-tests-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            const string csv =
                "tipo;etapa;reloj_unity_procesado_s;tracker_id;evento;reloj_esp32_ms;secuencia;giro_dps;acel_vertical_g;par_bilateral;espera_cola_unity_ms;reloj_callback_monotonico_s;reloj_evento_estimado_s;reloj_alineado;demora_relativa_estimacion_ms\n" +
                "MARCA;INICIO;1.0;;;;;;;;;;;;\n" +
                "# filas_total=1; finalizada=1\n" +
                "# filas_eventos=0\n";
            string source = BiofitCaptureArchive.Save(dir, csv, "resumen de prueba");
            byte[] original = File.ReadAllBytes(source);
            Check(BiofitCaptureArchive.Verify(File.ReadAllText(source)), "valid private archive");

            FakeStore.Reset();
            Check(BiofitAndroidExport.SaveToDownloads(source),
                  "Android native path finishes without picker");
            Check(FakeStore.Entries.Count == 1 &&
                  FakeStore.Entries[0].Name == Path.GetFileName(source),
                  "MediaStore Download entry named correctly");
            Check(original.SequenceEqual(FakeStore.Entries[0].Bytes),
                  "every CSV byte written into provider");
            Check(BiofitAndroidExport.LastStatus.StartsWith("GUARDADO en Descargas") &&
                  BiofitAndroidExport.LastFileName == Path.GetFileName(source),
                  "success status only after output closes");
            Check(File.ReadAllBytes(source).SequenceEqual(original),
                  "private source preserved");

            Check(FakeStore.StreamClosed && FakeStore.DescriptorClosed,"Java resources explicitly closed");
            Check(FakeStore.Entries[0].Published,"publish only after verification");
            FakeStore.Reset(); FakeStore.Corrupt=true;
            Check(!BiofitAndroidExport.SaveToDownloads(source) && FakeStore.Entries.Count==0,"same-size corruption rejected");
            FakeStore.Reset(); FakeStore.FailClose = true;
            Check(!BiofitAndroidExport.SaveToDownloads(source) && FakeStore.Entries.Count==0,
                  "close failure cannot report success or leave incomplete entry");
            FakeStore.Reset();
            FakeStore.FailWrite = true;
            Check(!BiofitAndroidExport.SaveToDownloads(source) &&
                  BiofitAndroidExport.LastStatus.StartsWith("ERROR") &&
                  FakeStore.Entries.Count == 0,
                  "failed write reports error and cleans incomplete MediaStore entry");
            Check(original.SequenceEqual(File.ReadAllBytes(source)),
                  "failed export retains original");
            FakeStore.Reset();
            FakeStore.WrongSize = true;
            Check(!BiofitAndroidExport.SaveToDownloads(source) &&
                  FakeStore.Entries.Count == 0,
                  "truncated target recognized and removed");
            FakeStore.Reset();
            FakeStore.Sdk = 28;
            Check(!BiofitAndroidExport.SaveToDownloads(source) &&
                  BiofitAndroidExport.LastStatus.Contains("Android 10") &&
                  FakeStore.Entries.Count == 0,
                  "older Android rejects automatic Downloads without broad storage perms");

            FakeStore.Reset();
            File.AppendAllText(source, "damaged");
            Check(!BiofitAndroidExport.SaveToDownloads(source) &&
                  FakeStore.Entries.Count == 0,
                  "invalid private checksum cannot be exported");
            Check(!BiofitAndroidExport.SaveToDownloads(Path.Combine(dir,"missing.csv")),
                  "missing private file rejected");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
        Console.WriteLine("PASS: " + checks +
            " simulated Android MediaStore checks; no real Unity/APK/device");
    }
}
