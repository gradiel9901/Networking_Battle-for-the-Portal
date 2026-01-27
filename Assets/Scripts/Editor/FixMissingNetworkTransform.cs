using UnityEngine;
using UnityEditor;
using Fusion;
using System.IO;

namespace Com.MyCompany.MyGame.Editor
{
    [InitializeOnLoad]
    public class FixMissingNetworkTransform
    {
        static FixMissingNetworkTransform()
        {
            EditorApplication.delayCall += CheckAndFixPlayerPrefab;
        }

        private static void CheckAndFixPlayerPrefab()
        {
            string prefabPath = "Assets/Prefab/Player.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogWarning($"[FixMissingNetworkTransform] Could not load prefab at {prefabPath}. Please verify the path.");
                return;
            }

            if (prefab.GetComponent<NetworkTransform>() == null)
            {
                Debug.Log($"[FixMissingNetworkTransform] NetworkTransform missing on {prefab.name}. Adding it now...");

                prefab.AddComponent<NetworkTransform>();

                EditorUtility.SetDirty(prefab);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                Debug.Log($"[FixMissingNetworkTransform] Successfully added NetworkTransform to {prefab.name} and saved.");
            }
            else
            {

            }
        }
    }
}
