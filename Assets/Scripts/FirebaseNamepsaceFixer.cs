#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

public class FirebaseNamespaceFixer : EditorWindow
{
    [MenuItem("Tools/Sửa Lỗi Firebase Namespace")]
    public static void FixNamespaces()
    {
        // Trỏ thẳng vào thư mục Plugins/Android gốc trong project
        string pluginsPath = Path.Combine(Application.dataPath, "Plugins/Android");
        
        if (!Directory.Exists(pluginsPath))
        {
            Debug.LogError("<color=red>Không tìm thấy thư mục Plugins/Android trong dự án!</color>");
            return;
        }

        string[] directories = Directory.GetDirectories(pluginsPath, "*.androidlib", SearchOption.AllDirectories);
        int fixedCount = 0;

        foreach (string dir in directories)
        {
            string manifestPath = Path.Combine(dir, "AndroidManifest.xml");
            string gradlePath = Path.Combine(dir, "build.gradle");

            if (File.Exists(manifestPath) && File.Exists(gradlePath))
            {
                string manifestContent = File.ReadAllText(manifestPath);
                // Tìm dòng package="..." trong file XML
                Match match = Regex.Match(manifestContent, @"package\s*=\s*""([^""]+)""");
                
                if (match.Success)
                {
                    string pkgName = match.Groups[1].Value;
                    string gradleContent = File.ReadAllText(gradlePath);

                    // Kiểm tra xem đã có chữ namespace chưa, nếu chưa thì nhét vào
                    if (!gradleContent.Contains("namespace") && gradleContent.Contains("android {"))
                    {
                        gradleContent = gradleContent.Replace("android {", "android {\n    namespace '" + pkgName + "'");
                        File.WriteAllText(gradlePath, gradleContent);
                        Debug.Log("<color=green>✔ Đã sửa file gốc thành công: " + Path.GetFileName(dir) + "</color>");
                        fixedCount++;
                    }
                    else if (gradleContent.Contains("namespace"))
                    {
                         Debug.Log("<color=yellow>File này đã có namespace rồi, bỏ qua: " + Path.GetFileName(dir) + "</color>");
                    }
                }
            }
        }

        // Bắt Unity làm mới lại toàn bộ file trong thư mục Assets
        AssetDatabase.Refresh();

        if (fixedCount > 0)
        {
            EditorUtility.DisplayDialog("Thành công!", $"Đã sửa tận gốc {fixedCount} thư mục Firebase. Bây giờ bạn có thể Build game!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Thông báo", "Không có file nào cần sửa hoặc đã sửa hết rồi.", "OK");
        }
    }
}
#endif