using UnityEngine;
using System.IO;
public class FileHandler : MonoBehaviour
{
    public void OnClickPickFile()
{
    string[] fileTypes = { "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };

    // Không gán vào biến permission nữa, gọi trực tiếp hàm luôn
    NativeFilePicker.PickFile((path) =>
    {
        if (path == null)
        {
            Debug.Log("Hủy chọn file");
        }
        else
        {
            Debug.Log("Đã chọn file tại: " + path);
            ProcessFile(path);
        }
    }, fileTypes);
}

    void ProcessFile(string path)
    {
        if (path.ToLower().EndsWith(".pdf")) {
            // Gọi hàm hiển thị PDF
        } else {
            // Gọi hệ thống mở file Word bằng app ngoài
            Application.OpenURL("file://" + path);
        }
    }
}