using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Rediseña automáticamente la zona de parámetros como un monitor clínico real.
/// Se añade en ejecución al objeto que contiene MonitorTextos y no modifica la escena.
/// </summary>
[ExecuteAlways]
public class MonitorClinicoUI : MonoBehaviour
{
    private MonitorTextos monitor;
    private GameObject root;
    private TMP_Text hrValue, spo2Value, rrValue, bpValue, tempValue, modeLabel;
    private MonitorWaveformGraphic ecgWave, respWave, plethWave;
    private MonitorWaveformGraphic systolicTrend, diastolicTrend, temperatureTrend;
    private float nextRefresh;

    private void Awake()
    {
        monitor = GetComponent<MonitorTextos>();
        if (Application.isPlaying && monitor != null)
            monitor.segundosPorMuestra = 1f;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            ReuseOrBuildInterface();
    }

    private void Start()
    {
        // Si la interfaz ya fue colocada manualmente en la escena, Play debe
        // reutilizarla. Destruirla y crearla otra vez reiniciaba su RectTransform.
        if (Application.isPlaying)
            ReuseOrBuildInterface();
    }

    public void ReuseOrBuildInterface()
    {
        monitor = GetComponent<MonitorTextos>();
        Canvas monitorCanvas = GetComponentInParent<Canvas>();
        Transform host = monitorCanvas != null ? monitorCanvas.transform : transform;
        Transform existing = host.Find("Monitor_Clinico_Realista");

        if (existing == null)
        {
            RebuildInterface();
            return;
        }

        root = existing.gameObject;
        OcultarPanelAnterior(host);
        PrepareCanvasForXR(monitorCanvas);
        RestorePhysicalMonitorScreen(monitorCanvas);
        CacheGeneratedReferences();
        EnsureGeneratedInterfaceVisible();
        DisableLegacyTestButton();
        Refresh();
    }

    public void RebuildInterface()
    {
        monitor = GetComponent<MonitorTextos>();
        Canvas monitorCanvas = GetComponentInParent<Canvas>();
        if (monitorCanvas != null)
        {
            // El monitor es una pantalla situada físicamente en la escena.
            // Algunos Building Blocks de Interaction SDK pueden modificar el
            // Canvas; al reconstruirlo recuperamos explícitamente su modo 3D.
            monitorCanvas.enabled = true;
            monitorCanvas.renderMode = RenderMode.WorldSpace;
        }

        Transform host = monitorCanvas != null ? monitorCanvas.transform : transform;
        Transform previous = host.Find("Monitor_Clinico_Realista");
        if (previous != null)
        {
            if (Application.isPlaying)
                Destroy(previous.gameObject);
            else
                DestroyImmediate(previous.gameObject);
        }

        Build();
        PrepareCanvasForXR(monitorCanvas);
        RestorePhysicalMonitorScreen(monitorCanvas);
        EnsureGeneratedInterfaceVisible();
        Refresh();
    }

    private void Update()
    {
        if (monitor == null) return;

        // La previsualización del monitor debe ser una fotografía fija. Unity
        // llama Update en objetos ExecuteAlways cuando la escena se repinta
        // (por ejemplo, al usar la rueda del mouse), pero eso no debe avanzar
        // muestras ni gráficas antes de pulsar Play.
        if (!Application.isPlaying) return;

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.15f;
            Refresh();
        }
    }

    private void Build()
    {
        Canvas monitorCanvas = GetComponentInParent<Canvas>();
        RectTransform host = monitorCanvas != null
            ? monitorCanvas.transform as RectTransform
            : transform as RectTransform;
        if (host == null) return;

        // Ocultamos solamente el panel provisional. Antes se desactivaban todos
        // los Graphics y Renderers del Canvas; eso también podía apagar objetos
        // que Interaction SDK añadía después y dejar la pantalla completamente
        // invisible. MonitorTextos permanece activo y continúa leyendo PhysioNet.
        Transform legacyPanel = host.Find("Monitor_panel");
        if (legacyPanel != null)
        {
            CanvasRenderer[] oldCanvasRenderers =
                legacyPanel.GetComponentsInChildren<CanvasRenderer>(true);
            foreach (CanvasRenderer oldRenderer in oldCanvasRenderers)
                oldRenderer.SetAlpha(0f);

            Graphic[] oldGraphics = legacyPanel.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic oldGraphic in oldGraphics)
                oldGraphic.enabled = false;
        }

        root = new GameObject("Monitor_Clinico_Realista", typeof(RectTransform), typeof(Image));
        RectTransform panel = root.GetComponent<RectTransform>();
        panel.SetParent(host, false);
        Stretch(panel, 0, 0, 0, 0);
        panel.SetAsLastSibling();
        root.GetComponent<Image>().color = new Color(0.008f, 0.012f, 0.015f, 1f);

        CreateTopBar(panel);
        CreateWaveArea(panel);
        CreateNumberArea(panel);
        // La reaccion se controla unicamente desde el boton general
        // "INDUCIR FIEBRE" de la escena. No se crea un segundo control dentro
        // del monitor.
    }

    private static void OcultarPanelAnterior(Transform host)
    {
        Transform panel = host.Find("Monitor_panel");
        if (panel == null) return;
        foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true))
            graphic.enabled = false;
        foreach (CanvasRenderer renderer in panel.GetComponentsInChildren<CanvasRenderer>(true))
            renderer.SetAlpha(0f);
    }

    private void EnsureGeneratedInterfaceVisible()
    {
        if (root == null) return;

        root.SetActive(true);

        CanvasRenderer[] renderers = root.GetComponentsInChildren<CanvasRenderer>(true);
        foreach (CanvasRenderer canvasRenderer in renderers)
            canvasRenderer.SetAlpha(1f);

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
            graphic.enabled = true;
    }

    private void PrepareCanvasForXR(Canvas monitorCanvas)
    {
        if (monitorCanvas == null || root == null) return;

        // La Scene View muestra todas las capas, pero la camara XR puede excluir
        // capas auxiliares como Overlay UI. Se usa Default solo para este Canvas
        // y para la interfaz clinica, sin modificar ningun RectTransform.
        const int visibleLayer = 0;
        monitorCanvas.gameObject.layer = visibleLayer;
        SetLayerRecursively(root.transform, visibleLayer);
        // Pantalla 3D, no HUD: no dibujar el fondo/curvas por encima de bolsas
        // transparentes que estén físicamente delante del monitor.
        monitorCanvas.overrideSorting = false;
        monitorCanvas.sortingOrder = 0;

        if (Application.isPlaying && monitorCanvas.worldCamera == null)
        {
            Camera xrCamera = Camera.main;
            if (xrCamera == null)
                xrCamera = FindObjectOfType<Camera>();
            if (xrCamera != null)
                monitorCanvas.worldCamera = xrCamera;
        }
    }

    private static void SetLayerRecursively(Transform target, int layer)
    {
        if (target == null) return;
        target.gameObject.layer = layer;
        foreach (Transform child in target)
            SetLayerRecursively(child, layer);
    }

    private static void RestorePhysicalMonitorScreen(Canvas monitorCanvas)
    {
        if (monitorCanvas == null) return;

        Transform assembly = monitorCanvas.transform.parent != null
            ? monitorCanvas.transform.parent
            : monitorCanvas.transform;
        Renderer[] renderers = assembly.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer candidate in renderers)
        {
            // En este modelo MonitorScreen no es un plano independiente: su
            // unica malla tambien forma la parte frontal de la carcasa. Apagar
            // el Renderer hace desaparecer el monitor fisico completo. La UI
            // clinica, con fondo negro y colocada delante de la pantalla, es la
            // que cubre la imagen antigua sin destruir la carcasa.
            if (candidate.gameObject.name == "MonitorScreen")
                candidate.enabled = true;
        }
    }

    private void CacheGeneratedReferences()
    {
        if (root == null) return;

        Transform header = root.transform.Find("Cabecera");
        if (header != null)
        {
            TMP_Text[] headerTexts = header.GetComponentsInChildren<TMP_Text>(true);
            if (headerTexts.Length > 1)
                modeLabel = headerTexts[1];
        }

        hrValue = FindValueText("Valores/FC_Block");
        spo2Value = FindValueText("Valores/SpO2_Block");
        rrValue = FindValueText("Valores/FR_Block");
        bpValue = FindValueText("Valores/PNI_Block");
        tempValue = FindValueText("Valores/TEMP_Block");

        // Algunas filas incluyen '/' en su nombre visible. Transform.Find lo
        // interpreta como separador de ruta, por eso buscamos los trazados por
        // el nombre real de su componente sin modificar la jerarquia existente.
        ecgWave = FindWave("ECG II / FC_Wave");
        plethWave = FindWave("PLETH / SpO2_Wave");
        respWave = FindWave("RESP / FR_Wave");
        systolicTrend = FindWave("PNI_Sistolica");
        diastolicTrend = FindWave("PNI_Diastolica");
        temperatureTrend = FindWave("TEMP_Wave");
    }

    private TMP_Text FindValueText(string path)
    {
        Transform block = root.transform.Find(path);
        if (block == null) return null;
        TMP_Text[] texts = block.GetComponentsInChildren<TMP_Text>(true);
        return texts.Length == 0 ? null : texts[texts.Length - 1];
    }

    private MonitorWaveformGraphic FindWave(string objectName)
    {
        MonitorWaveformGraphic[] waves = root.GetComponentsInChildren<MonitorWaveformGraphic>(true);
        foreach (MonitorWaveformGraphic wave in waves)
        {
            if (wave.gameObject.name == objectName)
                return wave;
        }

        return null;
    }

    private void DisableLegacyTestButton()
    {
        if (root == null) return;
        Transform buttonTransform = root.transform.Find("Boton_Prueba_Normal_Reaccion");
        if (buttonTransform == null) return;


        BoxCollider legacyCollider = buttonTransform.GetComponent<BoxCollider>();
        if (legacyCollider != null)
            legacyCollider.enabled = false;

        // Se conserva el resto de la interfaz exactamente como estaba. Solo
        // ocultamos el control duplicado que ya pudiera estar serializado.
        buttonTransform.gameObject.SetActive(false);
    }

    private void CreateTopBar(RectTransform parent)
    {
        RectTransform bar = Panel("Cabecera", parent, new Color(0.025f, 0.04f, 0.05f, 1f));
        SetAnchors(bar, 0f, 0.91f, 1f, 1f);
        Text("MONITOR MULTIPARAMETRO", bar, 25, Color.white, TextAlignmentOptions.MidlineLeft,
            new Vector2(0.025f, 0f), new Vector2(0.55f, 1f));
        modeLabel = Text("ESTADO NORMAL | MUESTRA 01/60", bar, 18, new Color(0.25f, 1f, 0.45f),
            TextAlignmentOptions.MidlineRight, new Vector2(0.55f, 0f), new Vector2(0.975f, 1f));
    }

    private void CreateWaveArea(RectTransform parent)
    {
        RectTransform area = Panel("Trazados", parent, Color.clear);
        SetAnchors(area, 0.015f, 0.12f, 0.72f, 0.90f);
        ecgWave = WaveRow(area, "ECG II / FC", 0.805f, 1f, new Color(0.15f, 1f, 0.35f), MonitorWaveformGraphic.WaveType.ECG);
        plethWave = WaveRow(area, "PLETH / SpO2", 0.605f, 0.795f, new Color(0.1f, 0.85f, 1f), MonitorWaveformGraphic.WaveType.Pleth);
        respWave = WaveRow(area, "RESP / FR", 0.405f, 0.595f, new Color(1f, 0.86f, 0.12f), MonitorWaveformGraphic.WaveType.Respiracion);
        CreatePressureTrendRow(area, 0.205f, 0.395f);
        temperatureTrend = WaveRow(area, "TEMP", 0.005f, 0.195f, new Color(1f, 0.55f, 0.18f), MonitorWaveformGraphic.WaveType.Trend);
    }

    private MonitorWaveformGraphic WaveRow(RectTransform parent, string label, float yMin, float yMax, Color color, MonitorWaveformGraphic.WaveType type)
    {
        RectTransform row = Panel(label + "_Row", parent, new Color(0.012f, 0.02f, 0.024f, 1f));
        SetAnchors(row, 0f, yMin, 1f, yMax);
        Text(label, row, 17, color, TextAlignmentOptions.TopLeft, new Vector2(0.015f, 0.72f), new Vector2(0.20f, 0.98f));
        GameObject graph = new GameObject(label + "_Wave", typeof(RectTransform), typeof(MonitorWaveformGraphic));
        RectTransform rt = graph.GetComponent<RectTransform>();
        rt.SetParent(row, false);
        SetAnchors(rt, 0.02f, 0.08f, 0.985f, 0.86f);
        MonitorWaveformGraphic wave = graph.GetComponent<MonitorWaveformGraphic>();
        wave.waveType = type;
        wave.color = color;
        wave.thickness = type == MonitorWaveformGraphic.WaveType.ECG ? 2.4f : 2f;
        return wave;
    }

    private void CreatePressureTrendRow(RectTransform parent, float yMin, float yMax)
    {
        RectTransform row = Panel("PNI_Trend_Row", parent, new Color(0.012f, 0.02f, 0.024f, 1f));
        SetAnchors(row, 0f, yMin, 1f, yMax);
        Text("PNI  SYS", row, 15, Color.white, TextAlignmentOptions.TopLeft,
            new Vector2(0.015f, 0.70f), new Vector2(0.20f, 0.98f));
        Text("DIA", row, 15, new Color(1f, 0.25f, 0.75f), TextAlignmentOptions.TopLeft,
            new Vector2(0.15f, 0.70f), new Vector2(0.28f, 0.98f));

        systolicTrend = CreateTrendGraphic(row, "PNI_Sistolica", Color.white);
        diastolicTrend = CreateTrendGraphic(row, "PNI_Diastolica", new Color(1f, 0.25f, 0.75f));
    }

    private MonitorWaveformGraphic CreateTrendGraphic(RectTransform parent, string name, Color color)
    {
        GameObject graph = new GameObject(name, typeof(RectTransform), typeof(MonitorWaveformGraphic));
        RectTransform rt = graph.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        SetAnchors(rt, 0.02f, 0.08f, 0.985f, 0.84f);
        MonitorWaveformGraphic wave = graph.GetComponent<MonitorWaveformGraphic>();
        wave.waveType = MonitorWaveformGraphic.WaveType.Trend;
        wave.color = color;
        wave.thickness = 1.3f;
        return wave;
    }

    private void CreateNumberArea(RectTransform parent)
    {
        RectTransform area = Panel("Valores", parent, new Color(0.015f, 0.023f, 0.028f, 1f));
        SetAnchors(area, 0.73f, 0.12f, 0.985f, 0.90f);
        hrValue = VitalBlock(area, "FC", "--", "bpm", 0.75f, 1f, new Color(0.15f, 1f, 0.35f), 58);
        spo2Value = VitalBlock(area, "SpO2", "--", "%", 0.51f, 0.75f, new Color(0.1f, 0.85f, 1f), 55);
        rrValue = VitalBlock(area, "FR", "--", "rpm", 0.30f, 0.51f, new Color(1f, 0.86f, 0.12f), 46);
        bpValue = VitalBlock(area, "PNI", "---/--", "mmHg", 0.12f, 0.30f, Color.white, 36);
        tempValue = VitalBlock(area, "TEMP", "--.-", "°C", 0f, 0.12f, new Color(1f, 0.55f, 0.18f), 30);
    }

    private TMP_Text VitalBlock(RectTransform parent, string label, string value, string unit,
        float yMin, float yMax, Color color, float valueSize)
    {
        RectTransform block = Panel(label + "_Block", parent, new Color(0.02f, 0.03f, 0.035f, 1f));
        SetAnchors(block, 0.02f, yMin + 0.008f, 0.98f, yMax - 0.008f);
        Text(label, block, 16, color, TextAlignmentOptions.TopLeft, new Vector2(0.04f, 0.62f), new Vector2(0.48f, 0.96f));
        Text(unit, block, 13, new Color(color.r, color.g, color.b, 0.8f), TextAlignmentOptions.TopRight,
            new Vector2(0.50f, 0.62f), new Vector2(0.96f, 0.96f));
        return Text(value, block, valueSize, color, TextAlignmentOptions.Center,
            new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.78f));
    }

    private void Refresh()
    {
        if (monitor == null || hrValue == null) return;
        bool preview = !Application.isPlaying;
        int bpm = preview ? 72 : monitor.BPMActual;
        int spo2 = preview ? 97 : monitor.OxigenoActual;
        int rr = preview ? 15 : monitor.RespiracionActual;
        int sys = preview ? 127 : monitor.SistolicaActual;
        int dia = preview ? 74 : monitor.DiastolicaActual;
        float temp = preview ? 36.6f : monitor.TemperaturaActual;

        hrValue.text = bpm.ToString();
        if (spo2Value != null) spo2Value.text = spo2.ToString();
        if (rrValue != null) rrValue.text = rr.ToString();
        if (bpValue != null) bpValue.text = sys + "/" + dia;
        if (tempValue != null) tempValue.text = temp.ToString("F1");
        bool reaction = !preview && monitor.EstadoReaccionActivo;
        int sample = preview ? 1 : monitor.MuestraActual + 1;
        if (modeLabel != null)
        {
            modeLabel.text = (reaction ? "REACCION" : "ESTADO NORMAL") +
                " | MUESTRA " + sample.ToString("00") + "/60";
            modeLabel.color = reaction ? new Color(1f, 0.22f, 0.18f) : new Color(0.25f, 1f, 0.45f);
        }

        if (ecgWave != null)
        {
            ecgWave.rate = Mathf.Max(35, bpm);
            ecgWave.speed = reaction ? 1.20f : 1f;
        }
        if (plethWave != null) plethWave.rate = Mathf.Max(35, bpm);
        if (respWave != null) respWave.rate = Mathf.Max(6, rr);
        if (systolicTrend != null) systolicTrend.SetTrendValue(sys, 70f, 180f);
        if (diastolicTrend != null) diastolicTrend.SetTrendValue(dia, 35f, 120f);
        if (temperatureTrend != null) temperatureTrend.SetTrendValue(temp, 34f, 41f);
    }

    private static RectTransform Panel(string name, RectTransform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static TMP_Text Text(string value, RectTransform parent, float size, Color color,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject("Text_" + value, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        SetAnchors(rt, min.x, min.y, max.x, max.y);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10, size * 0.55f);
        text.fontSizeMax = size;
        text.raycastTarget = false;
        return text;
    }

    private static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

}

