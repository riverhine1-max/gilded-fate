using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    public sealed class GildedAudioImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith("Assets/Resources/Audio/"))return;
            var importer=(AudioImporter)assetImporter;var music=assetPath.Contains("/Music/");
            importer.forceToMono=false;importer.ambisonic=false;importer.loadInBackground=music;
            var settings=importer.defaultSampleSettings;
            settings.loadType=music?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=music?AudioCompressionFormat.Vorbis:AudioCompressionFormat.PCM;
            settings.quality=1;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=settings;
        }
    }
}
