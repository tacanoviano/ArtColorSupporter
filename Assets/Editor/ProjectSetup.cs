using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArtColorSupporter.Editor
{
    /// <summary>
    /// 初回にプロジェクトを開いたとき、アプリ名・横持ち専用設定・最初のシーンを自動で作る。
    /// メニュー「ArtColorSupporter > Setup Project」からいつでも再適用できる。
    /// </summary>
    [InitializeOnLoad]
    static class ProjectSetup
    {
        const string ProductName = "ArtColorSupporter";
        const string ApplicationId = "com.DefaultCompany.ArtColorSupporter";
        const string ScenePath = "Assets/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(ScenePath)) return; // セットアップ済み
                Setup();
            };
        }

        [MenuItem("ArtColorSupporter/Setup Project")]
        static void Setup()
        {
            ApplyPlayerSettings();
            if (!File.Exists(ScenePath)) CreateMainScene();
            AddSceneToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtColorSupporter] Project setup completed.");
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = ProductName;

            // 横持ち専用（左右どちらの横向きにも回転する）
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            if (string.IsNullOrEmpty(PlayerSettings.iOS.cameraUsageDescription))
                PlayerSettings.iOS.cameraUsageDescription =
                    "絵の参考にする写真を撮影するためにカメラを使用します。";

            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                var id = PlayerSettings.GetApplicationIdentifier(target);
                if (string.IsNullOrEmpty(id) || id.EndsWith(".ProductName") || id == "com.Company.ProductName")
                    PlayerSettings.SetApplicationIdentifier(target, ApplicationId);
            }
        }

        static void CreateMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(0x22, 0x24, 0x28, 0xFF);
            }
            new GameObject("App").AddComponent<AppController>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        static void AddSceneToBuild()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
