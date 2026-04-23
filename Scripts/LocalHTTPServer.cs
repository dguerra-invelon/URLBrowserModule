using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.IO.Compression;
using UnityEngine.Networking;

namespace URLBrowserModule
{
    public class LocalHTTPServer : MonoBehaviour
    {
        [SerializeField] private bool openWebBrowserOnStart = true;

        private HttpListener httpListener;
        private Thread listenerThread;
        private bool isRunning = false;
        private int port = 8080;
        private string contentPath = "";

        public Action<string> OnServerStarted;
        public Action OnServerStopped;
        public Action<string> OnError;

        private Dictionary<string, string> mimeTypes = new Dictionary<string, string>()
        {
            { ".html", "text/html" },
            { ".htm", "text/html" },
            { ".css", "text/css" },
            { ".js", "application/javascript" },
            { ".json", "application/json" },
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".svg", "image/svg+xml" },
            { ".ico", "image/x-icon" },
            { ".txt", "text/plain" },
            { ".pdf", "application/pdf" },
            { ".mp4", "video/mp4" },
            { ".webm", "video/webm" },
            { ".mp3", "audio/mpeg" },
            { ".wav", "audio/wav" }
        };

        public void StartServer(int serverPort, string zipFileName = "WebContent.zip")
        {
            if (isRunning) return;

            port = serverPort;
            
            // La carpeta de destino será donde se extraiga el ZIP
            string folderName = Path.GetFileNameWithoutExtension(zipFileName);
            contentPath = Path.Combine(Application.persistentDataPath, folderName);

            // Iniciar proceso de despliegue y luego el servidor
            #if UNITY_ANDROID && !UNITY_EDITOR
                StartCoroutine(DeployZipAndroid(zipFileName));
            #else
                DeployZipPC(zipFileName);
            #endif
        }

        public void StartServer(int serverPort)
        {
            if (isRunning) return;

            port = serverPort;

            // Usamos directamente el Application.persistentDataPath como raíz del servidor, sin descomprimir ningún ZIP
            contentPath = Application.persistentDataPath;

            // Inicializo directamente el servidor sin descomprimir contenido, 
            // asumiendo que ya está listo en contentPath
            StartHttpListener();
            if (openWebBrowserOnStart) OpenLocalServer();
        }

        private void DeployZipPC(string zipFileName)
        {
            try
            {
                string sourceZip = Path.Combine(Application.streamingAssetsPath, zipFileName);
                if (File.Exists(sourceZip))
                {
                    if (Directory.Exists(contentPath)) Directory.Delete(contentPath, true);
                    Directory.CreateDirectory(contentPath);
                    ZipFile.ExtractToDirectory(sourceZip, contentPath);
                    Debug.Log($"[LocalHTTPServer] ZIP desplegado en PC: {contentPath}");
                }
                else
                {
                    Debug.LogError($"[LocalHTTPServer] ZIP no encontrado en: {sourceZip}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalHTTPServer] Error desplegando ZIP en PC: {ex.Message}");
                OnError?.Invoke($"Error desplegando ZIP: {ex.Message}");
            }

            StartHttpListener();
            if (openWebBrowserOnStart) OpenLocalServer();
        }

        private System.Collections.IEnumerator DeployZipAndroid(string zipFileName)
        {
            
            {
                string sourceZip = Path.Combine(Application.streamingAssetsPath, zipFileName);
                string tempZipPath = Path.Combine(Application.temporaryCachePath, zipFileName);

                // 1. Extraer el ZIP del APK a un archivo temporal
                Debug.Log($"[LocalHTTPServer] Leyendo ZIP desde StreamingAssets...");
                using (UnityWebRequest www = UnityWebRequest.Get(sourceZip))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        File.WriteAllBytes(tempZipPath, www.downloadHandler.data);
                        Debug.Log($"[LocalHTTPServer] ZIP copiado a memoria temporal: {tempZipPath}");
                    }
                    else
                    {
                        Debug.LogError($"[LocalHTTPServer] Error al leer ZIP de StreamingAssets: {www.error}");
                        OnError?.Invoke($"Error leyendo ZIP: {www.error}");
                        yield break;
                    }
                }

                // 2. Descomprimir en PersistentDataPath
                Debug.Log($"[LocalHTTPServer] Descomprimiendo ZIP en: {contentPath}");
                if (Directory.Exists(contentPath)) Directory.Delete(contentPath, true);
                Directory.CreateDirectory(contentPath);
                
                ZipFile.ExtractToDirectory(tempZipPath, contentPath);
                File.Delete(tempZipPath); // Limpiar archivo temporal
                
                Debug.Log($"[LocalHTTPServer] Descompresion completada en Quest");
            }


            StartHttpListener();
            if (openWebBrowserOnStart) OpenLocalServer();
        }

        public void OpenLocalServer()
        {
            if (this == null || !this.IsRunning)
            {
                Debug.LogWarning("Local server is not running. Start it first with StartLocalServer().");
                return;
            }

            string serverURL = this.GetServerURL();
            if (!string.IsNullOrEmpty(serverURL))
            {
                Debug.Log($"Opening local server: {serverURL}");
                OpenURL(serverURL);
            }
        }

         public void OpenURL(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogWarning("URL is null or empty. Cannot open.");
                return;
            }

            if (!IsValidURL(url))
            {
                Debug.LogWarning($"Invalid URL format: {url}");
                return;
            }

            Debug.Log($"Opening URL: {url}");
            Application.OpenURL(url);
        }

        private bool IsValidURL(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return url.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase);
        }

        private void StartHttpListener()
        {
            try
            {
                httpListener = new HttpListener();
                httpListener.Prefixes.Add($"http://127.0.0.1:{port}/");
                httpListener.Start();
                isRunning = true;

                listenerThread = new Thread(ListenerThread);
                listenerThread.IsBackground = true;
                listenerThread.Start();

                string serverUrl = $"http://127.0.0.1:{port}";
                Debug.Log($"[LocalHTTPServer] Server started at {serverUrl}");
                OnServerStarted?.Invoke(serverUrl);
            }
            catch (Exception ex)
            {
                string errorMsg = $"[LocalHTTPServer] Error starting server: {ex.Message}";
                Debug.LogError(errorMsg);
                OnError?.Invoke(errorMsg);
                isRunning = false;
            }
        }

        public void StopServer()
        {
            if (!isRunning)
            {
                Debug.LogWarning("[LocalHTTPServer] Server is not running.");
                return;
            }

            try
            {
                isRunning = false;
                httpListener?.Stop();
                httpListener?.Close();
                
                if (listenerThread != null && listenerThread.IsAlive)
                {
                    listenerThread.Join(2000);
                }

                Debug.Log("[LocalHTTPServer] Server stopped.");
                OnServerStopped?.Invoke();
            }
            catch (Exception ex)
            {
                string errorMsg = $"[LocalHTTPServer] Error stopping server: {ex.Message}";
                Debug.LogError(errorMsg);
                OnError?.Invoke(errorMsg);
            }
        }

        private void ListenerThread()
        {
            while (isRunning)
            {
                try
                {
                    HttpListenerContext context = httpListener.GetContext();
                    ProcessRequest(context);
                }
                catch (HttpListenerException)
                {
                    // Server was stopped
                    break;
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"[LocalHTTPServer] Thread error: {ex.Message}");
                    }
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string requestPath = request.Url.LocalPath;
                if (requestPath == "/")
                {
                    requestPath = "/index.html";
                }

                string filePath = Path.Combine(contentPath, requestPath.TrimStart('/'));

                // Security: Prevent directory traversal attacks
                string fullPath = Path.GetFullPath(filePath);
                string fullContentPath = Path.GetFullPath(contentPath);
                
                if (!fullPath.StartsWith(fullContentPath))
                {
                    RespondWithError(response, 403, "Access Denied");
                    return;
                }

                if (File.Exists(filePath))
                {
                    ServeFile(response, filePath);
                }
                else if (Directory.Exists(filePath))
                {
                    ServeListing(response, filePath, requestPath);
                }
                else
                {
                    RespondWithError(response, 404, "Not Found");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalHTTPServer] Error processing request: {ex.Message}");
                try
                {
                    RespondWithError(context.Response, 500, "Internal Server Error");
                }
                catch { }
            }
        }

        private void ServeFile(HttpListenerResponse response, string filePath)
        {
            try
            {
                byte[] fileContent = File.ReadAllBytes(filePath);
                string fileExtension = Path.GetExtension(filePath).ToLower();
                
                string contentType = "application/octet-stream";
                if (mimeTypes.TryGetValue(fileExtension, out var type))
                {
                    contentType = type;
                }

                response.ContentType = contentType;
                response.ContentLength64 = fileContent.Length;
                response.StatusCode = 200;
                response.OutputStream.Write(fileContent, 0, fileContent.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalHTTPServer] Error serving file: {ex.Message}");
                RespondWithError(response, 500, "Internal Server Error");
            }
        }

        private void ServeListing(HttpListenerResponse response, string directoryPath, string requestPath)
        {
            try
            {
                var dirInfo = new DirectoryInfo(directoryPath);
                var htmlContent = new System.Text.StringBuilder();

                htmlContent.Append("<html><head><title>Directory Listing</title>");
                htmlContent.Append("<style>body { font-family: Arial; } a { display: block; padding: 5px; } </style>");
                htmlContent.Append("</head><body>");
                htmlContent.Append($"<h1>Directory: {requestPath}</h1>");
                htmlContent.Append("<ul>");

                if (requestPath != "/")
                {
                    htmlContent.Append($"<li><a href=\"../\">..</a></li>");
                }

                foreach (var dir in dirInfo.GetDirectories())
                {
                    string dirLink = requestPath.TrimEnd('/') + "/" + dir.Name + "/";
                    htmlContent.Append($"<li><a href=\"{dirLink}\">[DIR] {dir.Name}</a></li>");
                }

                foreach (var file in dirInfo.GetFiles())
                {
                    string fileLink = requestPath.TrimEnd('/') + "/" + file.Name;
                    htmlContent.Append($"<li><a href=\"{fileLink}\">{file.Name}</a></li>");
                }

                htmlContent.Append("</ul></body></html>");

                byte[] content = System.Text.Encoding.UTF8.GetBytes(htmlContent.ToString());
                response.ContentType = "text/html";
                response.ContentLength64 = content.Length;
                response.StatusCode = 200;
                response.OutputStream.Write(content, 0, content.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalHTTPServer] Error serving directory: {ex.Message}");
                RespondWithError(response, 500, "Internal Server Error");
            }
        }

        private void RespondWithError(HttpListenerResponse response, int statusCode, string message)
        {
            try
            {
                string html = $"<html><body><h1>{statusCode} {message}</h1></body></html>";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
                response.StatusCode = statusCode;
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalHTTPServer] Error sending error response: {ex.Message}");
            }
        }

        public bool IsRunning => isRunning;
        public string GetServerURL() => isRunning ? $"http://127.0.0.1:{port}" : null;

        private void OnDestroy()
        {
            if (isRunning)
            {
                StopServer();
            }
        }
    }
}
