using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dibuja ondas clínicas en una textura transparente. Este método es estable
/// tanto en Canvas Screen Space como World Space y no depende de shaders externos.
/// </summary>
[ExecuteAlways]
public class MonitorWaveformGraphic : RawImage
{
    public enum WaveType { ECG, Respiracion, Pleth, Trend }

    public WaveType waveType;
    [Range(0.5f, 8f)] public float thickness = 2f;
    [Range(0.1f, 4f)] public float speed = 1f;
    [Range(0.1f, 1f)] public float amplitude = 0.72f;
    public float rate = 75f;

    private const int TextureWidth = 512;
    private const int TextureHeight = 112;
    private const float RefreshInterval = 1f / 20f;

    private Texture2D waveformTexture;
    private Color32[] pixels;
    private float phase;
    private float nextRefresh;
    private float nextTrendSample;
    private bool editorPreviewInitialized;
    private float editorPreviewValue = float.NaN;
    private readonly List<float> trendHistory = new List<float>();

    public void SetTrendValue(float value, float minimum, float maximum)
    {
        if (waveType != WaveType.Trend || maximum <= minimum) return;

        float normalizedValue = Mathf.InverseLerp(minimum, maximum, value);

        // Fuera de Play se dibuja una historia inicial completa y determinista.
        // No se añaden muestras con el reloj del Editor, por lo que seleccionar
        // objetos, mover la cámara o usar la rueda no desplaza la gráfica.
        if (!Application.isPlaying)
        {
            if (!editorPreviewInitialized ||
                !Mathf.Approximately(editorPreviewValue, normalizedValue))
            {
                PopulateInitialTrend(normalizedValue);
                editorPreviewValue = normalizedValue;
                editorPreviewInitialized = true;
            }

            DrawWaveform();
            return;
        }

        // Al comenzar Play el trazado no aparece vacío: se precarga un historial
        // estable y desde allí continúan entrando las muestras reales.
        if (trendHistory.Count == 0)
        {
            PopulateInitialTrend(normalizedValue);
            nextTrendSample = Time.unscaledTime + 0.5f;
            DrawWaveform();
            return;
        }

        if (Time.unscaledTime < nextTrendSample && trendHistory.Count > 0) return;

        nextTrendSample = Time.unscaledTime + 0.5f;
        trendHistory.Add(normalizedValue);
        if (trendHistory.Count > 120)
            trendHistory.RemoveAt(0);
        DrawWaveform();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        CreateTexture();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (waveformTexture == null)
            CreateTexture();

        phase = 0f;
        nextRefresh = 0f;
        nextTrendSample = 0f;

        if (Application.isPlaying)
        {
            trendHistory.Clear();
            editorPreviewInitialized = false;
            editorPreviewValue = float.NaN;
        }

        DrawWaveform();
    }

    private void Update()
    {
        // ExecuteAlways también ejecuta Update durante la edición. Se corta aquí
        // para conservar exactamente el gráfico de arranque hasta pulsar Play.
        if (!Application.isPlaying) return;

        if (waveType != WaveType.Trend)
            phase += Time.unscaledDeltaTime * speed;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + RefreshInterval;
        DrawWaveform();
    }

    protected override void OnDestroy()
    {
        if (waveformTexture != null)
        {
            if (Application.isPlaying)
                Destroy(waveformTexture);
            else
                DestroyImmediate(waveformTexture);
        }
        base.OnDestroy();
    }

    private void PopulateInitialTrend(float center)
    {
        trendHistory.Clear();

        const int initialSamples = 102;
        for (int i = 0; i < initialSamples; i++)
        {
            float t = i / (float)(initialSamples - 1);
            float variation =
                Mathf.Sin(t * Mathf.PI * 2f * 1.15f) * 0.012f +
                Mathf.Sin((t * 3.4f + 0.2f) * Mathf.PI * 2f) * 0.005f;

            trendHistory.Add(Mathf.Clamp01(center + variation));
        }
    }

    private void CreateTexture()
    {
        waveformTexture = new Texture2D(
            TextureWidth,
            TextureHeight,
            TextureFormat.RGBA32,
            false);
        waveformTexture.name = "ClinicalWaveform_" + waveType;
        waveformTexture.hideFlags = HideFlags.DontSave;
        waveformTexture.filterMode = FilterMode.Bilinear;
        waveformTexture.wrapMode = TextureWrapMode.Clamp;
        pixels = new Color32[TextureWidth * TextureHeight];
        texture = waveformTexture;
        uvRect = new Rect(0f, 0f, 1f, 1f);
        canvasRenderer.SetAlpha(1f);
    }

    private void DrawWaveform()
    {
        if (waveformTexture == null || pixels == null) return;

        Color32 clear = new Color32(0, 0, 0, 0);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        DrawGrid();

        if (waveType == WaveType.Trend)
        {
            DrawTrend();
        }
        else
        {
            int previousY = SampleToPixel(0);
            for (int x = 1; x < TextureWidth; x++)
            {
                int currentY = SampleToPixel(x);
                DrawLine(x - 1, previousY, x, currentY);
                previousY = currentY;
            }
        }

        waveformTexture.SetPixels32(pixels);
        waveformTexture.Apply(false, false);
    }

    private void DrawTrend()
    {
        if (trendHistory.Count == 0) return;

        int count = trendHistory.Count;
        int firstY = TrendToPixel(trendHistory[0]);
        if (count == 1)
        {
            DrawLine(TextureWidth - 8, firstY, TextureWidth - 1, firstY);
            return;
        }

        const int step = 5;
        int firstVisible = Mathf.Max(0, count - (TextureWidth / step));
        int visibleCount = count - firstVisible;
        int startX = TextureWidth - 1 - ((visibleCount - 1) * step);
        int previousX = startX;
        int previousY = TrendToPixel(trendHistory[firstVisible]);
        for (int i = firstVisible + 1; i < count; i++)
        {
            int x = previousX + step;
            int y = TrendToPixel(trendHistory[i]);
            DrawLine(previousX, previousY, x, y);
            previousX = x;
            previousY = y;
        }
    }

    private static int TrendToPixel(float normalized)
    {
        return Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(8f, TextureHeight - 9f, normalized)),
            1,
            TextureHeight - 2);
    }

    private void DrawGrid()
    {
        Color32 grid = new Color32(255, 255, 255, 20);
        for (int x = 0; x < TextureWidth; x += 32)
            for (int y = 0; y < TextureHeight; y++)
                SetPixel(x, y, grid);

        for (int y = 8; y < TextureHeight; y += 24)
            for (int x = 0; x < TextureWidth; x++)
                SetPixel(x, y, grid);
    }

    private int SampleToPixel(int x)
    {
        float x01 = x / (float)(TextureWidth - 1);
        float cycles = Mathf.Max(0.25f, rate / 60f) * 4.8f;
        float t = x01 * cycles + phase;
        float y;

        if (waveType == WaveType.ECG)
        {
            float c = Mathf.Repeat(t, 1f);
            y = 0.025f * Mathf.Sin(c * Mathf.PI * 2f);
            y += Gaussian(c, 0.18f, 0.035f) * 0.10f;
            y -= Gaussian(c, 0.38f, 0.018f) * 0.20f;
            y += Gaussian(c, 0.415f, 0.012f) * 1.00f;
            y -= Gaussian(c, 0.45f, 0.020f) * 0.35f;
            y += Gaussian(c, 0.68f, 0.070f) * 0.24f;
        }
        else if (waveType == WaveType.Respiracion)
        {
            y = Mathf.Sin(t * Mathf.PI * 2f) * 0.48f +
                Mathf.Sin(t * Mathf.PI * 4f) * 0.06f;
        }
        else
        {
            float c = Mathf.Repeat(t, 1f);
            y = Gaussian(c, 0.18f, 0.085f) * 0.85f +
                Gaussian(c, 0.46f, 0.12f) * 0.30f - 0.18f;
        }

        float normalized = 0.5f + y * 0.46f * amplitude;
        return Mathf.Clamp(
            Mathf.RoundToInt(normalized * (TextureHeight - 1)),
            1,
            TextureHeight - 2);
    }

    private static float Gaussian(float x, float center, float width)
    {
        float d = (x - center) / width;
        return Mathf.Exp(-0.5f * d * d);
    }

    private void DrawLine(int x0, int y0, int x1, int y1)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx - dy;
        int radius = Mathf.Clamp(Mathf.RoundToInt(thickness), 1, 4);
        Color32 line = new Color32(255, 255, 255, 255);

        while (true)
        {
            for (int ox = -radius; ox <= radius; ox++)
                for (int oy = -radius; oy <= radius; oy++)
                    if (ox * ox + oy * oy <= radius * radius)
                        SetPixel(x0 + ox, y0 + oy, line);

            if (x0 == x1 && y0 == y1) break;
            int twiceError = error * 2;
            if (twiceError > -dy) { error -= dy; x0 += sx; }
            if (twiceError < dx) { error += dx; y0 += sy; }
        }
    }

    private void SetPixel(int x, int y, Color32 value)
    {
        if (x < 0 || x >= TextureWidth || y < 0 || y >= TextureHeight) return;
        pixels[y * TextureWidth + x] = value;
    }
}
