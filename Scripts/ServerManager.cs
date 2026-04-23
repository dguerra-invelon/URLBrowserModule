using UnityEngine;
using URLBrowserModule;

public class ServerManager : MonoBehaviour
{
    public LocalHTTPServer httpServer;
    public int port = 8080;
    [Tooltip("Si está habilitado 'useContentFolder', se usará el contenido de contentFolder dentro de StreamingAssets y se descomprime en runtime. Si no, el desarrollador tendrá la responsabilidad de meter los recursos a mano.")]
    public string contentFolder = "WebContent.zip"; 
    public bool useContentFolder = true; 

    private void Start()
    {
        // Iniciar servidor al comenzar
        httpServer.OnServerStarted += OnServerStarted;
        httpServer.OnError += OnServerError;
   
        
        if(useContentFolder)
        {
            httpServer.StartServer(port, contentFolder);
        }
        else
        {
            httpServer.StartServer(port); 
        }
    }

    private void OnServerStarted(string url)
    {
        Debug.Log($"Servidor iniciado en: {url}");
        // Aquí puedes abrir un navegador, cargar un WebView, etc.
    }

    private void OnServerError(string error)
    {
        Debug.LogError($"Error del servidor: {error}");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Detener Servidor", GUILayout.Height(50)))
        {
            httpServer.StopServer();
        }

        if (httpServer.IsRunning)
        {
            GUILayout.Label($"Servidor activo: {httpServer.GetServerURL()}");
        }
    }
}
