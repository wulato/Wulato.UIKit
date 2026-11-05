using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Wulato.UIKit.Editor.Authoring
{
    [Serializable] internal sealed class UIPreviewOwnedFile {public string path,guid,hash;}
    [Serializable] internal sealed class UIPreviewState
    {
        public int schema=1;
        public string request,phase,profileGuid,scenePath,directory,directoryGuid,error;
        public string[] originalGuids,selection;
        public bool createdAssetRoot;
        public string assetRootGuid,directoryMetaHash,assetRootMetaHash;
        public SceneSetup[] scenes;
        public UIPreviewOwnedFile[] files;
    }
    internal static class UIPreviewOperation
    {
        internal const string StatePath="Library/WulatoUIKit/preview.json";
        internal const string AssetRoot="Assets/Wulato.UIKit.Preview";
        public static string Hash(string path)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static bool Matches(string path,string guid,string hash)
            =>File.Exists(path)&&AssetDatabase.AssetPathToGUID(path)==guid&&Hash(path)==hash;
        internal static UIPreviewState Read()
        {
            if(!File.Exists(StatePath))return null;
            var state=JsonUtility.FromJson<UIPreviewState>(File.ReadAllText(StatePath));
            if(state==null||state.schema!=1||!Guid.TryParseExact(state.request,"N",out _)||state.directory!=AssetRoot+"/"+state.request||state.scenePath!=state.directory+"/Preview.unity"||state.scenes==null||state.originalGuids==null||state.scenes.Length!=state.originalGuids.Length)
                throw new InvalidDataException("Preview recovery record is invalid. Keep the scene and inspect the preserved record.");
            return state;
        }
        internal static void Save(UIPreviewState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath));var tmp=StatePath+".tmp";File.WriteAllText(tmp,JsonUtility.ToJson(state,true));
            if(File.Exists(StatePath))File.Replace(tmp,StatePath,null);else File.Move(tmp,StatePath);
        }
        internal static void CaptureFiles(UIPreviewState state)
        {
            state.directoryGuid=AssetDatabase.AssetPathToGUID(state.directory);state.assetRootGuid=AssetDatabase.AssetPathToGUID(AssetRoot);state.directoryMetaHash=Hash(state.directory+".meta");state.assetRootMetaHash=Hash(AssetRoot+".meta");
            state.files=Directory.GetFiles(state.directory,"*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Select(p=>new UIPreviewOwnedFile{path=p,guid=AssetDatabase.AssetPathToGUID(p.EndsWith(".meta")?p.Substring(0,p.Length-5):p),hash=Hash(p)}).ToArray();
        }
        internal static bool DeleteOwned(UIPreviewState state)
        {
            if(!Directory.Exists(state.directory))return true;
            if(state.directory!=AssetRoot+"/"+state.request||state.directoryGuid!=AssetDatabase.AssetPathToGUID(state.directory)||state.files==null||string.IsNullOrEmpty(state.directoryMetaHash)||!File.Exists(state.directory+".meta")||Hash(state.directory+".meta")!=state.directoryMetaHash)return false;
            var paths=Directory.GetFiles(state.directory,"*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).OrderBy(p=>p).ToArray();
            if(!paths.SequenceEqual(state.files.Select(f=>f.path).OrderBy(p=>p)))return false;
            foreach(var file in state.files)
            {
                if(!file.path.StartsWith(state.directory+"/",StringComparison.Ordinal)||!File.Exists(file.path)||Hash(file.path)!=file.hash)return false;
                var asset=file.path.EndsWith(".meta")?file.path.Substring(0,file.path.Length-5):file.path;
                if(AssetDatabase.AssetPathToGUID(asset)!=file.guid)return false;
            }
            var deleted=AssetDatabase.DeleteAsset(state.directory);
            if(deleted&&state.createdAssetRoot&&AssetDatabase.AssetPathToGUID(AssetRoot)==state.assetRootGuid&&Directory.Exists(AssetRoot)&&File.Exists(AssetRoot+".meta")&&Hash(AssetRoot+".meta")==state.assetRootMetaHash&&!Directory.EnumerateFileSystemEntries(AssetRoot).Any())AssetDatabase.DeleteAsset(AssetRoot);
            return deleted;
        }
        internal static void Archive()
        {
            if(!File.Exists(StatePath))return;
            var dir=Path.GetDirectoryName(StatePath)+"/PreviewHistory";Directory.CreateDirectory(dir);
            File.Move(StatePath,dir+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff")+".json");
        }
    }
}
