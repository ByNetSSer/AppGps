using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.Android;

public class HyperPreciseGPS : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI coordinatesText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI stepsText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI precisionText;

    [Header("Map References")]
    public RawImage mapDisplay;
    public RectTransform playerIcon;
    public int mapWidth = 512;
    public int mapHeight = 512;

    // Datos GPS - PRECISIÓN MÁXIMA
    private double currentLat;
    private double currentLon;
    private double previousLat;
    private double previousLon;
    private double startLat;
    private double startLon;

    // Estadísticas
    private float totalDistance = 0f;
    private int totalSteps = 0;
    private float currentSpeed = 0f;
    private bool isGPSReady = false;
    private float horizontalAccuracy = 0f;

    // Configuración HIPER PRECISA
    private float gpsUpdateInterval = 0.1f; // 10 veces por segundo!
    private float stepDistance = 0.3f; // Cada 30cm cuenta como paso
    private float minMovement = 0.01f; // 1 CM de movimiento mínimo!
    private int mapZoom = 18; // Zoom máximo para ver calles

    // Control del mapa
    private Vector2 mapPixelOffset = Vector2.zero;
    private Texture2D currentMapTexture;
    private bool isMapLoading = false;

    // Historial para suavizado
    private Vector2[] positionHistory = new Vector2[5];
    private int historyIndex = 0;

    void Start()
    {
        InitializePositionHistory();
        StartCoroutine(InitializeGPS());
    }

    void InitializePositionHistory()
    {
        for (int i = 0; i < positionHistory.Length; i++)
        {
            positionHistory[i] = Vector2.zero;
        }
    }

    IEnumerator InitializeGPS()
    {
        statusText.text = "??? Iniciando GPS de alta precisión...";

#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
            statusText.text = "?? Esperando permisos...";

            float timeout = 10f;
            while (timeout > 0 && !Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            statusText.text = "? Activa el GPS en ajustes";
            yield break;
        }

        // MÁXIMA PRECISIÓN - 1 metro, actualización cada 0.1 segundos
        Input.location.Start(0.1f, 0.1f);

        int maxWait = 30; // Más tiempo para alta precisión
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
            statusText.text = $"?? Obteniendo alta precisión... {maxWait}s";
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            statusText.text = "? Error de GPS";
            yield break;
        }

        isGPSReady = true;

        // Esperar primera lectura de alta precisión
        yield return new WaitForSeconds(2f);

        currentLat = Input.location.lastData.latitude;
        currentLon = Input.location.lastData.longitude;
        startLat = currentLat;
        startLon = currentLon;
        previousLat = currentLat;
        previousLon = currentLon;
        horizontalAccuracy = Input.location.lastData.horizontalAccuracy;

        // Cargar mapa de alta resolución
        yield return StartCoroutine(LoadHighPrecisionMap(currentLat, currentLon));

        statusText.text = $"? GPS de alta precisión!\nPrecisión: {horizontalAccuracy:F1}m";
        StartCoroutine(UpdateGPSHyperPrecise());
    }

    IEnumerator LoadHighPrecisionMap(double latitude, double longitude)
    {
        if (isMapLoading) yield break;

        isMapLoading = true;

        // OpenStreetMap con zoom máximo
        string url = $"https://tile.openstreetmap.org/{mapZoom}/{LonToTileX(longitude, mapZoom)}/{LatToTileY(latitude, mapZoom)}.png";

        Debug.Log($"Cargando mapa: {url}");

        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                currentMapTexture = DownloadHandlerTexture.GetContent(www);
                mapDisplay.texture = currentMapTexture;

                // Resetear offset del mapa
                mapPixelOffset = Vector2.zero;
                UpdateMapDisplay();

                statusText.text = "??? Mapa de alta precisión cargado!";
            }
            else
            {
                statusText.text = "? Error cargando mapa";
            }
        }

        isMapLoading = false;
    }

    IEnumerator UpdateGPSHyperPrecise()
    {
        while (true)
        {
            if (isGPSReady && Input.location.status == LocationServiceStatus.Running)
            {
                UpdateHyperPrecisePosition();
            }
            yield return new WaitForSeconds(gpsUpdateInterval);
        }
    }

    void UpdateHyperPrecisePosition()
    {
        double newLat = Input.location.lastData.latitude;
        double newLon = Input.location.lastData.longitude;
        horizontalAccuracy = Input.location.lastData.horizontalAccuracy;

        // Calcular distancia con máxima precisión
        float distanceMoved = CalculateHighPrecisionDistance(previousLat, previousLon, newLat, newLon);
        currentSpeed = distanceMoved / gpsUpdateInterval;

        // DETECCIÓN HIPER SENSIBLE - hasta 1 CM de movimiento
        if (distanceMoved >= minMovement && horizontalAccuracy < 10f) // Solo si la precisión es buena
        {
            currentLat = newLat;
            currentLon = newLon;

            totalDistance += distanceMoved;

            // Contar pasos cada 30cm
            if (distanceMoved >= stepDistance)
            {
                totalSteps += Mathf.RoundToInt(distanceMoved / stepDistance);
            }

            // ACTUALIZAR MAPA CON MÁXIMA PRECISIÓN
            UpdateMapWithHighPrecision();

            previousLat = currentLat;
            previousLon = currentLon;

            // Agregar a historial para suavizado
            AddToPositionHistory(distanceMoved);
        }

        UpdatePreciseUI();
    }

    void UpdateMapWithHighPrecision()
    {
        // Calcular desplazamiento en PÍXELES del mapa
        // 1 grado de longitud ? 111,319.9 metros al ecuador
        // 1 grado de latitud ? 111,134.9 metros

        double latOffsetMeters = (currentLat - startLat) * 111134.9;
        double lonOffsetMeters = (currentLon - startLon) * 111319.9;

        // Convertir metros a píxeles (asumiendo 256px = 156.412 metros a zoom 18)
        float pixelsPerMeter = 1.637f; // Ajustado para zoom 18
        float pixelOffsetX = (float)lonOffsetMeters * pixelsPerMeter;
        float pixelOffsetY = (float)latOffsetMeters * pixelsPerMeter;

        // Aplicar offset al MAPA (dirección contraria al movimiento)
        mapPixelOffset = new Vector2(-pixelOffsetX, -pixelOffsetY);

        UpdateMapDisplay();

        // Si nos salimos del tile actual, cargar nuevo tile
        if (Mathf.Abs(pixelOffsetX) > 100f || Mathf.Abs(pixelOffsetY) > 100f)
        {
            StartCoroutine(LoadHighPrecisionMap(currentLat, currentLon));
            startLat = currentLat;
            startLon = currentLon;
        }
    }

    void UpdateMapDisplay()
    {
        if (mapDisplay != null)
        {
            RectTransform mapRect = mapDisplay.rectTransform;

            // Aplicar transformación al mapa
            mapRect.anchoredPosition = mapPixelOffset;

            // Efecto de suavizado opcional
            mapRect.localPosition = Vector3.Lerp(mapRect.localPosition,
                new Vector3(mapPixelOffset.x, mapPixelOffset.y, 0), 0.5f);
        }
    }

    void AddToPositionHistory(float distance)
    {
        positionHistory[historyIndex] = new Vector2((float)currentLon, (float)currentLat);
        historyIndex = (historyIndex + 1) % positionHistory.Length;
    }

    void UpdatePreciseUI()
    {
        coordinatesText.text = $"?? POSICIÓN EXACTA:\nLat: {currentLat:F8}\nLon: {currentLon:F8}";
        distanceText.text = $"?? Distancia: {totalDistance:F3} m";
        stepsText.text = $"?? Pasos: {totalSteps}";
        speedText.text = $"?? Velocidad: {(currentSpeed * 3.6f):F2} km/h";
        precisionText.text = $"?? Precisión: ±{horizontalAccuracy:F2} m";

        if (currentSpeed > 0.1f)
        {
            statusText.text = $"?? CAMINANDO - {totalDistance:F2}m";
        }
        else
        {
            statusText.text = "?? DETENIDO";
        }
    }

    float CalculateHighPrecisionDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Fórmula Haversine de alta precisión
        double R = 6371000.0; // Radio Tierra en metros
        double dLat = (lat2 - lat1) * Mathf.Deg2Rad;
        double dLon = (lon2 - lon1) * Mathf.Deg2Rad;

        double a = System.Math.Sin(dLat / 2.0) * System.Math.Sin(dLat / 2.0) +
                  System.Math.Cos(lat1 * Mathf.Deg2Rad) * System.Math.Cos(lat2 * Mathf.Deg2Rad) *
                  System.Math.Sin(dLon / 2.0) * System.Math.Sin(dLon / 2.0);

        double c = 2.0 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1.0 - a));
        return (float)(R * c);
    }

    // Conversiones precisas para tiles
    int LonToTileX(double lon, int zoom)
    {
        return (int)System.Math.Floor((lon + 180.0) / 360.0 * (1 << zoom));
    }

    int LatToTileY(double lat, int zoom)
    {
        double latRad = lat * System.Math.PI / 180.0;
        return (int)System.Math.Floor((1.0 - System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad)) / System.Math.PI) / 2.0 * (1 << zoom));
    }

    // SIMULACIÓN EN EDITOR - MOVIMIENTO DE ALTA PRECISIÓN
    void Update()
    {
#if UNITY_EDITOR
        bool moved = false;

        // Movimientos MUY PEQUEÑOS para simular precisión
        if (Input.GetKey(KeyCode.UpArrow))
        {
            SimulatePreciseMovement(0f, 0.000001f); // ~11cm por frame
            moved = true;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            SimulatePreciseMovement(0f, -0.000001f);
            moved = true;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            SimulatePreciseMovement(-0.000001f, 0f);
            moved = true;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            SimulatePreciseMovement(0.000001f, 0f);
            moved = true;
        }

        if (moved)
        {
            currentSpeed = 0.5f;
            horizontalAccuracy = 1.5f;
            UpdatePreciseUI();
        }
#endif
    }

    void SimulatePreciseMovement(double lonChange, double latChange)
    {
        currentLon += lonChange;
        currentLat += latChange;

        float distance = CalculateHighPrecisionDistance(previousLat, previousLon, currentLat, currentLon);
        totalDistance += distance;

        if (distance >= stepDistance)
        {
            totalSteps++;
        }

        UpdateMapWithHighPrecision();
        previousLat = currentLat;
        previousLon = currentLon;
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && Input.location.isEnabledByUser)
        {
            Input.location.Stop();
        }
        else if (!pauseStatus && isGPSReady)
        {
            Input.location.Start(0.1f, 0.1f);
        }
    }

    void OnDestroy()
    {
        if (Input.location.isEnabledByUser)
        {
            Input.location.Stop();
        }
    }
}