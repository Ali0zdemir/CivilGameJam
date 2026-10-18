using System;
using System.Linq;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CivilGameJam.Cards.Editor
{
    public static class CardGodzillaFireAudioAuthoring
    {
        private const string Folder = "Assets/Art/GodzillaFire/";

        [MenuItem("Tools/Disasters/Bind Godzilla Fire Audio")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Bind Godzilla fire audio in Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var fire = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FireTestController>(true)).Single();
            var data = new SerializedObject(fire);
            Bind(data, "monsterChargeSound", "godzilla fire scream 1.wav");
            Bind(data, "monsterFireStartSound", "godzilla fire blaze start loop.wav");
            Bind(data, "monsterFireLoopSound", "godzilla fire blaze loop.wav");
            Bind(data, "monsterFireEndSound", "godzilla fire blaze end loop.wav");
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Godzilla fire audio.");
            AssetDatabase.SaveAssets();
        }

        private static void Bind(SerializedObject data, string field, string filename)
        {
            string path = Folder + filename;
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
            data.FindProperty(field).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
