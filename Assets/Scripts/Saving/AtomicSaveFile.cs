using System;
using System.IO;
using System.Text;

namespace GildedFate.Saving
{
    // Only promotes a fully flushed file. The previous successful save remains recoverable.
    public static class AtomicSaveFile
    {
        public static bool TryWrite(string path,string json,out string error,bool preserveBackup=false)
        {
            error="";var temporary=path+".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var bytes=new UTF8Encoding(false).GetBytes(json);
                using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
                {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(path))File.Replace(temporary,path,preserveBackup?null:path+".bak",true);
                else File.Move(temporary,path);
                return true;
            }
            catch(Exception exception) when(exception is IOException||exception is UnauthorizedAccessException||exception is System.Security.SecurityException||exception is ArgumentException||exception is NotSupportedException)
            {error=exception.Message;return false;}
        }

        public static bool TryRead<T>(string path,Func<string,T> parse,Func<T,bool> validate,out T data,out bool recovered,out string error) where T:class
        {
            data=null;recovered=false;error="";
            foreach(var candidate in new[]{path,path+".bak"})
            {
                if(!File.Exists(candidate))continue;
                try
                {
                    if(new FileInfo(candidate).Length>8*1024*1024)throw new IOException("Save is unexpectedly large.");
                    var parsed=parse(File.ReadAllText(candidate));
                    if(parsed==null||!validate(parsed))throw new InvalidDataException("Save data is incomplete or uses an unsupported format.");
                    data=parsed;recovered=candidate!=path;return true;
                }
                catch(Exception exception) when(!(exception is OutOfMemoryException))
                {error=exception.Message;}
            }
            return false;
        }
    }
}
