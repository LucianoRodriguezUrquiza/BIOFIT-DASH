using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static int checks;
    static void Check(bool yes,string why)
    {
        if(!yes)throw new Exception(why);
        checks++;
    }
    static string Row(string type,int i)
    {
        // 15 columns, same number required by BIOFIT master and RAW contracts.
        return type+";ELEVACION;"+i+";1;TEST;"+i+";1;0;0;0;0;0;0;0;0";
    }
    static async Task Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"biofit-recovery096-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string header="tipo;etapa;unity;id;event;ms;seq;gx;gy;gz;ax;ay;az;q;lat";
            string oldPath=Path.Combine(root,"BIOFIT_v095_20261117_123000_aaaa1111.csv");
            string newPath=Path.Combine(root,"BIOFIT_v096_20261118_123000_bbbb2222.csv");
            var writer=new BiofitSessionFile(oldPath,header);
            for(int i=0;i<48000;i++)
            {
                Check(writer.Add(Row(i%10==0?"EV":"APP",i)),"write long row "+i);
                if(i%40==0)await Task.Delay(1);
            }
            await writer.Finish();
            Check(writer.Error=="","long write error: "+writer.Error);
            Check(File.Exists(oldPath),"long archive was not published");
            Check(!File.Exists(oldPath+".partial"),"finalized partial still present");
            Check(BiofitCaptureArchive.VerifyFileStreaming(oldPath),"long archive SHA and row count");
            Check(BiofitCaptureArchive.Verify(File.ReadAllText(oldPath)),"compatible with original SHA parser");

            // Short captures cannot erase or displace the earlier raw evidence.
            var shortWriter=new BiofitSessionFile(newPath,header);
            for(int i=0;i<3;i++)Check(shortWriter.Add(Row("APP",i)),"short write");
            await shortWriter.Finish();
            Check(BiofitCaptureArchive.VerifyFileStreaming(newPath),"new short valid");
            Check(File.Exists(oldPath) && File.Exists(newPath),"old+new independent files coexist");

            string rawDir=Path.Combine(root,"RawCaptures");
            Directory.CreateDirectory(rawDir);
            string oldRaw=Path.Combine(rawDir,Path.GetFileNameWithoutExtension(oldPath)+"_RAW.csv");
            var rawWriter=new BiofitSessionFile(oldRaw,header);
            for(int i=0;i<31000;i++){Check(rawWriter.Add(Row("RAW",i)),"raw write "+i);if(i%40==0)await Task.Delay(1);}
            await rawWriter.Finish();
            Check(BiofitCaptureArchive.VerifyFileStreaming(oldRaw),"paired RAW is independent and SHA-valid");

            // A partial must never be promoted as a complete archive.
            string partial=Path.Combine(root,"BIOFIT_v095_incomplete.csv.partial");
            File.WriteAllText(partial,header+"\n"+Row("APP",0)+"\n");
            Check(!BiofitCaptureArchive.VerifyFileStreaming(partial),"partial must fail verification");
            Check(File.Exists(partial),"incomplete original preserved");

            string tampered=Path.Combine(root,"BIOFIT_v095_tampered.csv");
            File.WriteAllText(tampered,File.ReadAllText(oldPath).Replace("EV;ELEVACION;0;", "EV;ELEVACION;9;"));
            Check(!BiofitCaptureArchive.VerifyFileStreaming(tampered),"modified bytes must fail SHA256");

            string newest=BiofitCaptureArchive.FindNewestValid(root);
            Check(newest==newPath || newest==oldPath,"latest archive must be complete, never partial");
            Check(File.Exists(oldPath),"old private source retained after reads");
            Console.WriteLine("PASS "+checks+" recovery 0.9.6 checks; 48,000 master + 31,000 RAW rows, streaming hashes.");
        }
        finally { Directory.Delete(root,true); }
    }
}
