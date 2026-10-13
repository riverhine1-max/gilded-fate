using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    /// <summary>Restores the unchanged FBX from checksum-verified repository transport parts.</summary>
    public static class ChronicleBookBinaryRestore
    {
        const string Model="Assets/Resources/Chronicle/BookModel/Chronicle.fbx";
        const string Manifest="SourceAssets/Chronicle/Transport/BookBinaryManifest.json";
        [Serializable] sealed class Index { public Entry[] files; }
        [Serializable] sealed class Entry { public string target,sha256;public long size;public Part[] parts; }
        [Serializable] sealed class Part { public string path,sha256;public long size; }
        static string Hash(string file)
        {
            using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        public static bool EnsureModel()
        {
            if(File.Exists(Model))return true;
            if(!File.Exists(Manifest))return false;
            string temporary=Model+".restoring";
            try
            {
                var index=JsonUtility.FromJson<Index>(File.ReadAllText(Manifest));
                Entry selected=null;
                foreach(var file in index.files)if(file.target==Model)selected=file;
                if(selected==null || selected.parts==null || selected.parts.Length==0)return false;
                Directory.CreateDirectory(Path.GetDirectoryName(Model));
                using(var output=File.Create(temporary))
                {
                    foreach(var part in selected.parts)
                    {
                        if(!part.path.StartsWith("SourceAssets/Chronicle/Transport/",StringComparison.Ordinal)
                            || part.path.Contains("..") || !File.Exists(part.path)
                            || new FileInfo(part.path).Length!=part.size || Hash(part.path)!=part.sha256)
                            throw new InvalidDataException("Missing or changed Chronicle transport part: "+part.path);
                        using(var input=File.OpenRead(part.path))input.CopyTo(output);
                    }
                }
                if(new FileInfo(temporary).Length!=selected.size || Hash(temporary)!=selected.sha256)
                    throw new InvalidDataException("Reconstructed Chronicle FBX checksum differs.");
                File.Move(temporary,Model);
                AssetDatabase.ImportAsset(Model,ImportAssetOptions.ForceSynchronousImport);
                return true;
            }
            catch(Exception error)
            {
                Debug.LogError("[Chronicle] Could not restore the finished book: "+error.Message);
                return false;
            }
            finally { if(File.Exists(temporary))File.Delete(temporary); }
        }
    }
}
