using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// One bounded producer queue per file. No disk IO in Add or the BLE callback.
// .partial is retained after failure; only a verified closed file is renamed .csv.
public sealed class BiofitSessionFile
{
    private readonly BlockingCollection<string> queue;
    private readonly Task worker;
    private readonly string partial;
    private string error = "";
    private int count, events, rejected;
    private bool closed;
    private readonly object gate = new object();
    public string Path { get; }
    public string Error => Volatile.Read(ref error);
    public int Rejected => Volatile.Read(ref rejected);
    public int Rows => Volatile.Read(ref count);
    public int Pending => queue.Count;
    public Task Completion => worker;
    public BiofitSessionFile(string path, string header, int capacity = 8192)
    {
        Path = path; partial = path + ".partial";
        queue = new BlockingCollection<string>(capacity);
        worker = Task.Run(() => Write(header));
    }
    public bool Add(string row)
    {
        lock(gate)
        {
            if(closed || Error.Length != 0 || !queue.TryAdd(row))
            {
                Interlocked.Increment(ref rejected);
                if(!closed) Interlocked.CompareExchange(ref error,"Cola de escritura saturada o archivo no disponible","");
                return false;
            }
            count++; if(row.StartsWith("EV;",StringComparison.Ordinal)) events++;
            return true;
        }
    }
    public Task Finish()
    {
        lock(gate) { if(!closed) { closed=true; queue.CompleteAdding(); } }
        return worker;
    }
    private void Write(string header)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            using(var file = new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
            using(var writer = new StreamWriter(file,new UTF8Encoding(false)))
            {
                writer.NewLine="\n";writer.WriteLine(header);
                int sinceFlush=0;
                while(!queue.IsCompleted)
                {
                    if(queue.TryTake(out string row,250)) { writer.WriteLine(row); sinceFlush++; }
                    if(sinceFlush>=100 || queue.Count==0) { writer.Flush(); sinceFlush=0; }
                }
                if(Error.Length!=0) throw new IOException(Error);
                writer.WriteLine("# filas_total="+count+"; finalizada=1");
                writer.WriteLine("# filas_eventos="+events+"; descartadas_por_limite="+rejected);
                writer.Flush(); file.Flush(true);
            }
            // Finalization/hashing is also off the Unity thread.
            string body=File.ReadAllText(partial,new UTF8Encoding(false,true));
            string archive=BiofitCaptureArchive.Build(body,"");
            if(!BiofitCaptureArchive.Verify(archive)) throw new InvalidDataException("Recuento de sesion inconsistente");
            File.WriteAllText(partial,archive,new UTF8Encoding(false));
            using(var file=new FileStream(partial,FileMode.Open,FileAccess.Write,FileShare.Read))file.Flush(true);
            if(!BiofitCaptureArchive.Verify(File.ReadAllText(partial)))throw new IOException("SHA256 de sesion incorrecto");
            File.Move(partial,Path);
        }
        catch(Exception ex) { Interlocked.Exchange(ref error,ex.Message); }
    }
}
