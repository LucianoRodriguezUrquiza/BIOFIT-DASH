using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;

// Self-contained UTF8 file. Hash covers every byte before END_BIOFIT.
public static class BiofitCaptureArchive
{
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    private static string Hash(string text)
    {
        using (var sha=SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(text))).Replace("-","").ToLowerInvariant();
    }
    public static string Build(string csv,string summary)
    {
        string body=csv.Replace("\r\n","\n").TrimEnd('\n')+"\n";
        foreach(string line in summary.Replace("\r\n","\n").Split('\n'))
            if(line.Length>0) body+="# RESUMEN "+line.Replace('\r',' ')+"\n";
        return body+"# END_BIOFIT sha256="+Hash(body)+"\n";
    }
    public static bool Verify(string text)
    {
        int end=text.LastIndexOf("# END_BIOFIT sha256=",StringComparison.Ordinal);
        if(end<0) return false;
        string body=text.Substring(0,end);
        if(text.Substring(end)!="# END_BIOFIT sha256="+Hash(body)+"\n") return false;
        if(!body.StartsWith("tipo;etapa;",StringComparison.Ordinal)) return false;
        int rows=0,events=0,total=-1,expectedEvents=-1;
        bool finished=false;
        string[] lines=body.Split('\n');
        for(int i=1;i<lines.Length;i++)
        {
            string line=lines[i];
            if(line.Length==0)continue;
            if(line.StartsWith("# filas_total=",StringComparison.Ordinal))
            {
                if(!int.TryParse(line.Split(';')[0].Substring(14),NumberStyles.Integer,CultureInfo.InvariantCulture,out total))return false;
                finished=line.EndsWith("finalizada=1",StringComparison.Ordinal);
            }
            else if(line.StartsWith("# filas_eventos=",StringComparison.Ordinal))
            {
                if(!int.TryParse(line.Split(';')[0].Substring(16),NumberStyles.Integer,CultureInfo.InvariantCulture,out expectedEvents))return false;
            }
            if(line[0]=='#')continue;
            if(line.Split(';').Length!=15)return false;
            rows++;if(line.StartsWith("EV;",StringComparison.Ordinal))events++;
        }
        return finished && rows==total && events==expectedEvents;
    }
    // Fixed-memory verification of already-closed private CSVs. Preserve the
    // original Verify(string) for legacy regression callers.
    public static bool VerifyFileStreaming(string path)
    {
        if(string.IsNullOrEmpty(path) || !File.Exists(path))return false;
        try
        {
            using(var reader=new StreamReader(path,new UTF8Encoding(false,true),true))
            using(var digest=SHA256.Create())
            {
                int rows=0,events=0,total=-1,expectedEvents=-1;
                bool finished=false,header=false;
                string line;
                while((line=reader.ReadLine())!=null)
                {
                    if(line.StartsWith("# END_BIOFIT sha256=",StringComparison.Ordinal))
                    {
                        if(reader.ReadLine()!=null || !header)return false;
                        digest.TransformFinalBlock(new byte[0],0,0);
                        string hash=BitConverter.ToString(digest.Hash).Replace("-","").ToLowerInvariant();
                        return line=="# END_BIOFIT sha256="+hash &&
                               finished && rows==total && events==expectedEvents;
                    }
                    if(!header)
                    {
                        if(!line.StartsWith("tipo;etapa;",StringComparison.Ordinal))return false;
                        header=true;
                    }
                    else if(line.StartsWith("# filas_total=",StringComparison.Ordinal))
                    {
                        if(!int.TryParse(line.Split(';')[0].Substring(14),
                            NumberStyles.Integer,CultureInfo.InvariantCulture,out total))return false;
                        finished=line.EndsWith("finalizada=1",StringComparison.Ordinal);
                    }
                    else if(line.StartsWith("# filas_eventos=",StringComparison.Ordinal))
                    {
                        if(!int.TryParse(line.Split(';')[0].Substring(16),
                            NumberStyles.Integer,CultureInfo.InvariantCulture,out expectedEvents))return false;
                    }
                    else if(!line.StartsWith("#",StringComparison.Ordinal))
                    {
                        if(line.Split(';').Length!=15)return false;
                        rows++; if(line.StartsWith("EV;",StringComparison.Ordinal))events++;
                    }
                    byte[] part=Utf8.GetBytes(line+"\n");
                    digest.TransformBlock(part,0,part.Length,part,0);
                }
                return false;
            }
        }
        catch(Exception){return false;}
    }

    public static string FindNewestValid(string directory)
    {
        // APK updates retain Application.persistentDataPath when package/signature match.
        // Never accept arbitrary paths or unverified partial archives.
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return "";
        string newest = "";
        DateTime newestTime = DateTime.MinValue;
        try
        {
            foreach (string path in Directory.GetFiles(directory, "BIOFIT_v0*.csv"))
            {
                try
                {
                    if (!VerifyFileStreaming(path)) continue;
                    DateTime written = File.GetLastWriteTimeUtc(path);
                    if (written >= newestTime)
                    {
                        newest = path;
                        newestTime = written;
                    }
                }
                catch (IOException) { /* skip unreadable archive */ }
                catch (UnauthorizedAccessException) { /* skip inaccessible archive */ }
            }
        }
        catch (IOException) { return ""; }
        catch (UnauthorizedAccessException) { return ""; }
        return newest;
    }

    public static string Save(string directory,string csv,string summary)
    {
        string text=Build(csv,summary);
        if(!Verify(text))throw new InvalidDataException("CSV incompleto o recuento inconsistente");
        Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"BIOFIT_v09_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".csv");
        File.WriteAllText(path,text,Utf8);
        if(!Verify(File.ReadAllText(path,Utf8)))throw new IOException("Fallo de verificacion de archivo");
        return path;
    }
}
