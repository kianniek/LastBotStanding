using UnityEngine;
using UnityEditor;
using System.IO;

public class FolderStructureCreator
{
    [MenuItem("Tools/Create Project Folders")]
    public static void CreateFolders()
    {
        // Define the root directory of your Unity project
        string root = Application.dataPath;

        // List of folders to create
        string[] folders =
        {
            "Scenes",
            "Scripts",
            "Prefabs",
            "Prefabs/Player",
            "Prefabs/SpiderBot",
            "Prefabs/FlyingDrone",
            "Prefabs/TankBot",
            "Prefabs/Enemies",
            "Art/Textures/Backgrounds",
            "Art/Textures/Robots",
            "Art/Textures/UI",
            "Art/Sprites/Icons",
            "Art/Sprites/Abilities",
            "Art/Materials",
            "Art/Shaders",
            "Animations/Player",
            "Animations/SpiderBot",
            "Animations/FlyingDrone",
            "Animations/TankBot",
            "Audio/Music",
            "Audio/SFX",
            "Audio/Voiceovers",
            "UI/MainMenu",
            "UI/HUD",
            "UI/UpgradeScreen",
            "UI/FlashbackScreen",
            "Environment/PostApocalypticCity",
            "Environment/RustedFactory",
            "Environment/OldWarZone",
            "Plugins",
            "ThirdParty"
        };

        // Create each folder
        foreach (string folder in folders)
        {
            string dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Debug.Log($"Created: {dir}");
            }
        }

        // Refresh the asset database to show changes immediately in Unity Editor
        AssetDatabase.Refresh();
    }
}
