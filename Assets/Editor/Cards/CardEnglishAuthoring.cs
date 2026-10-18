using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardEnglishAuthoring
    {
        private static readonly Dictionary<string, string> Text = new Dictionary<string, string>
        {
            { "Raylı Sistem", "Rail System" },
            { "Dalgakıran", "Elevated Floor" },
            { "Yangın", "Fire Protection" },
            { "Raylı Sistem\n2 KART", "Rail System\n2 ITEMS" },
            { "Dalgakıran\n2 KART", "Elevated Floor\n2 ITEMS" },
            { "Yangın\n2 KART", "Fire Protection\n2 ITEMS" },
            { "Normal insan", "Person" },
            { "Eğitimli insan", "Educated Person" },
            { "İnsan", "Person" },
            { "Eğitimli", "Educated" },
            { "Özellikler", "Details" },
            { "En fazla 2 kişi", "Up to 2 workers" },
            { "En fazla 2 öğrenci", "Up to 2 students" },
            { "EV", "HOUSE" },
            { "İNSANLAR", "PEOPLE" },
            { "ÇALIŞANLAR", "WORKERS" },
            { "Keşfedilen blueprintler", "Discovered blueprints" },
            { "KİLİTLİ", "" },
            { "BOŞ YUVA", "" },
            { "KEŞFEDİLDİ", "DISCOVERED" },
            { "Araştırma ile aç", "" },
            { "Plan yok", "No plans" },
            { "Araştırma", "Research" },
            { "Eğitim", "Education" },
            { "Üretim", "Production" },
            { "Blueprint bırak", "Drop a blueprint" },
            { "Blueprint seç", "Choose a blueprint" },
            { "Eğitim sürüyor", "Educating" },
            { "Araştırılıyor", "Researching" },
            { "Üretiliyor", "Producing" },
            { "1 plan → 2 kart", "1 plan -> 2 items" },
            { "2 KART ÜRETİR", "PRODUCES 2 ITEMS" },
            { "Her blueprint: 2 kart\nİnsan korunur.", "Each blueprint: 2 items\nWorkers are kept." },
            { "Normal insanları\neğitimli insanlara dönüştürür.", "Turns people into\neducated people." },
            { "Okulda eğitim alabilir.", "Can be educated at school." },
            { "Araştırmayı daha hızlı bitirir.", "Completes research faster." },
            { "Blueprint araştırmak için\ninsan kartlarını bırak.", "Assign people to\nresearch blueprints." },
            { "Blueprint ve en az bir\ninsan kartı gerekiyor.", "Requires a blueprint\nand at least one worker." },
            { "Fabrikada bir insanla\nüretilmesi gereken plan.", "A plan to manufacture\nat a staffed factory." },
            { "Fabrikaya sürükle.\nÜretim için en az\n1 insan gerekir.", "Drag to the factory.\nProduction needs\nat least one worker." },
            { "Fabrikaya veya\naraştırmaya gönder.", "Assign to a factory\nor research center." },
            { "Okula, fabrikaya veya\naraştırmaya gönder.", "Assign to a school, factory\nor research center." },
            { "Ev geliştirmeleri", "House upgrades" },
            { "Evi geliştirmek için\nüretilmiş logoları tak.", "Install manufactured items\nto upgrade this house." },
            { "Bu üretilmiş kartı\nevin uygun yuvasına bırak.", "Drop this manufactured item\ninto its matching house slot." },
            { "Blueprint değildir.\nHer evde tür başına 1 yuva.", "Manufactured item.\nOne slot per type in each house." },
            { "ARAŞTIRMA TAMAMLANDI", "RESEARCH COMPLETE" },
            { "Bir blueprint keşfet", "Discover a blueprint" },
            { "Seçtiğin plan Vault'a eklenir · Fabrikada 2 kart üretir", "Your plan goes to the Vault · Produces 2 items at the factory" },
            { "Keşif kaydı, planı kullandıktan sonra da Vault'ta kalır.", "Your discovery stays in the Vault after you use the plan." },
            { "Üretim için blueprint\nve en az 1 insan gerekir", "Requires a blueprint\nand at least 1 worker" },
            { "İNSAN GEREKİYOR\nEn az 1 kişi bırak", "WORKER REQUIRED\nAssign at least 1 person" },
            { "İnsanları klasöre bırak.\nİki kişi yerleşebilir.", "Assign people to the folder.\nIt holds up to two workers." },
            { "Üretilmiş logoları alt\nyuvalara tak. İnsanlar\nkorunur, geri alınabilir.", "Install manufactured items\nin the lower slots.\nWorkers can be released." },
        };

        [MenuItem("Tools/Card Game/Save English UI and Empty Undiscovered Vault")]
        public static void SaveForBatch()
        {
            var unmapped = new HashSet<string>();
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(guid => AssetDatabase.GUIDToAssetPath(guid)))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { TranslateLabels(root, unmapped); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            var data = new SerializedObject(game);
            data.FindProperty("population").objectReferenceValue = null;
            data.FindProperty("hint").objectReferenceValue = null;
            var options = data.FindProperty("blueprints");
            for (int i = 0; i < options.arraySize; i++)
            {
                var title = options.GetArrayElementAtIndex(i).FindPropertyRelative("title");
                title.stringValue = Translate(title.stringValue);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (string name in new[] { "Population", "CardInstructions", "CardInstructors" })
            {
                var old = game.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            foreach (var root in scene.GetRootGameObjects()) TranslateLabels(root, unmapped);
            foreach (var slot in game.Vault.Slots) slot.Refresh(false, 0);
            var empty = game.Vault.Area.Find("BlueprintSlot3/EmptyLabel");
            if (empty != null) empty.gameObject.SetActive(false);
            if (unmapped.Count > 0)
            {
                string report = Path.GetFullPath("Library/CardGameTestResults/english-unmapped.txt");
                File.WriteAllLines(report, unmapped);
                throw new InvalidOperationException("Untranslated UI labels; see " + report);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved English scene and prefabs, removed HUD labels, and empty undiscovered Vault slots.");
        }

        private static void TranslateLabels(GameObject root, HashSet<string> unmapped)
        {
            foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label.text = Translate(label.text);
                if (Regex.IsMatch(label.text, "[çğıöşüÇĞİÖŞÜ]")) unmapped.Add(root.name + "/" + label.name + ": " + label.text);
                if (PrefabUtility.IsPartOfPrefabInstance(label)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
        }

        private static string Translate(string value)
        {
            if (value == null) return string.Empty;
            if (Text.TryGetValue(value, out var translated)) return translated;
            if (value.StartsWith("Ürünleri evdeki", StringComparison.Ordinal))
                return "Install the items in the matching house slot.\\nYour discovery stays in the Vault.".Replace("\\n", "\n");
            if (value.StartsWith("Her tür için bir yuva:", StringComparison.Ordinal))
                return "One slot per type:\\nRail System, Elevated Floor\\nand Fire Protection.".Replace("\\n", "\n");
            return Regex.Replace(value, @"^(\d+\s*/\s*\d+)\s+kişi$", "$1 workers");
        }
    }
}

