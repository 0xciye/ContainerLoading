using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuilder
{
    public static void Build()
    {
        var output = CommandLineValue("-buildOutput") ?? "Builds/Android/ContainerLoadingApp.apk";
        var directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.ciye.containerloading");
        PlayerSettings.bundleVersion = CommandLineValue("-versionName") ?? "4.1.0";
        PlayerSettings.Android.bundleVersionCode = int.TryParse(CommandLineValue("-versionCode"), out var versionCode) ? versionCode : 7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        var requireSigning = string.Equals(CommandLineValue("-requireSigning"), "true", StringComparison.OrdinalIgnoreCase);
        ConfigureSigning(requireSigning);
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        ApplyAppIcon();
        EditorUserBuildSettings.buildAppBundle = false;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception($"Android build failed: {report.summary.result}, {report.summary.totalErrors} errors");
    }

    static void ConfigureSigning(bool required)
    {
        var path = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
        var storePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
        var alias = Environment.GetEnvironmentVariable("ANDROID_KEY_ALIAS");
        var aliasPass = Environment.GetEnvironmentVariable("ANDROID_KEY_ALIAS_PASS");
        var values = new[] { path, storePass, alias, aliasPass };
        var anyProvided = values.Any(value => !string.IsNullOrWhiteSpace(value));
        var allProvided = values.All(value => !string.IsNullOrWhiteSpace(value));

        if (!allProvided)
        {
            if (required || anyProvided)
                throw new InvalidOperationException("Release signing is incomplete. Configure all ANDROID_KEYSTORE_* and ANDROID_KEY_ALIAS* variables.");
            return;
        }

        if (!File.Exists(path)) throw new FileNotFoundException("Release keystore not found.", path);
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = path;
        PlayerSettings.Android.keystorePass = storePass;
        PlayerSettings.Android.keyaliasName = alias;
        PlayerSettings.Android.keyaliasPass = aliasPass;
    }

    static void ApplyAppIcon()
    {
        const string iconPath = "Assets/Resources/AppIcon.png";
        AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        if (icon == null) throw new FileNotFoundException("App icon not found", iconPath);
        var count = PlayerSettings.GetIconSizes(NamedBuildTarget.Android, IconKind.Application).Length;
        PlayerSettings.SetIcons(NamedBuildTarget.Android, Enumerable.Repeat(icon, count).ToArray(), IconKind.Application);
    }

    static string CommandLineValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.FindIndex(args, x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
