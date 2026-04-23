using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class AssetDeployer : MonoBehaviour
{
    public string folderName = "WebApp"; // Nombre de tu carpeta en StreamingAssets

    public delegate void OnDeployComplete(string localPath);
    public event OnDeployComplete OnReady;

    public void Deploy()
    {
        string sourcePath = Path.Combine(Application.streamingAssetsPath, folderName);
        string destinationPath = Path.Combine(Application.persistentDataPath, folderName);

        // Si ya existe, podrías saltarte este paso o borrar y reescribir
        if (!Directory.Exists(destinationPath))
        {
            Directory.CreateDirectory(destinationPath);
        }

        StartCoroutine(CopyAssets(sourcePath, destinationPath));
    }

    private IEnumerator CopyAssets(string source, string destination)
    {
        // En Android, StreamingAssets se lee vía WebRequest
        // Aquí necesitarías una lista de nombres de archivos (index.html, style.css, etc.)
        string[] filesToCopy = { "index.html", "bundle.js", "style.css" }; 

        foreach (string fileName in filesToCopy)
        {
            string sPath = Path.Combine(source, fileName);
            string dPath = Path.Combine(destination, fileName);

            using (UnityWebRequest www = UnityWebRequest.Get(sPath))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(dPath, www.downloadHandler.data);
                }
            }
        }

        Debug.Log("Despliegue completado en: " + destination);
        OnReady?.Invoke(destination);
    }
}
