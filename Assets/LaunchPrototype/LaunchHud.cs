using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Responsive launch controls and telemetry, built once per scene.</summary>
public sealed class LaunchHud : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.025f, 0.045f, 0.065f, 0.96f);
    private static readonly Color Muted = new Color(0.53f, 0.65f, 0.7f);
    private static readonly Color Cyan = new Color(0.35f, 0.91f, 0.88f);
    private static readonly Color Orange = new Color(1f, 0.49f, 0.2f);
    private LaunchDemo mission;
    private TextMeshProUGUI fuelText, altitudeText, speedText, statusText, phaseText, timerText, countdownText, stageText, windSpeedText, windDirectionText, windArrow;
    private UnityEngine.UI.Image fuelFill, flightFill;
    private UnityEngine.UI.Button fuelButton, launchButton, abortButton;
    private RectTransform canvasRoot;

    public void Initialize(LaunchDemo controller)
    {
        mission = controller;
        var canvasObject = new GameObject("Mission Control", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasRoot = canvasObject.GetComponent<RectTransform>();
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("Launch Event System", typeof(EventSystem));
            events.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            events.AddComponent<InputSystemUIInputModule>();
#else
            events.AddComponent<StandaloneInputModule>();
#endif
        }

        var header = Panel(canvasRoot, "Header", new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(550f, 90f), Color.clear);
        Label(header, "SPACEFLIGHT / LAB", 17, Cyan, 0, 0, 520, 25);
        Label(header, "AURORA", 48, Color.white, 0, 27, 340, 60, FontStyles.Bold);
        Label(header, "01", 26, Orange, 245, 45, 80, 36);

        var panel = Panel(canvasRoot, "Launch console", new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(365f, 570f), Ink);
        Panel(panel, "Accent", new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 570f), Cyan);
        Label(panel, "CONTROL DE LANZAMIENTO", 16, Muted, 24, 22, 320, 24);
        phaseText = Label(panel, "01 / PREPARACIÓN", 26, Color.white, 24, 56, 320, 38, FontStyles.Bold);
        Label(panel, "PROPULSANTE", 13, Muted, 24, 123, 200, 22);
        fuelText = Label(panel, "0<color=#78929E><size=24>%</size></color>", 54, Color.white, 24, 143, 320, 68, FontStyles.Bold);
        var fuelTrack = Panel(panel, "Fuel track", new Vector2(0f, 1f), new Vector2(24f, -224f), new Vector2(317f, 7f), new Color(0.13f, 0.21f, 0.25f));
        fuelFill = Panel(fuelTrack, "Fuel", new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 7f), Cyan).GetComponent<UnityEngine.UI.Image>();
        Label(panel, "Carga recomendada: 100%", 14, Muted, 24, 241, 320, 24);
        fuelButton = Button(panel, "FuelButton", "+  CARGAR COMBUSTIBLE", 24, 290, 317, 55, new Color(0.12f, 0.24f, 0.28f), Color.white, mission.AddFuel);
        Label(panel, "+25% por carga  /  tecla F", 12, Muted, 24, 351, 317, 21);
        launchButton = Button(panel, "LaunchButton", "INICIAR DESPEGUE", 24, 390, 317, 62, Orange, Ink, mission.Launch);
        Label(panel, "Cuenta regresiva automática  /  ESPACIO", 12, Muted, 24, 460, 317, 24);
        Button(panel, "ResetButton", "REINICIAR MISIÓN   /   R", 24, 511, 317, 36, new Color(0.08f, 0.13f, 0.17f), Muted, mission.ResetDemo);

        var telemetry = Panel(canvasRoot, "Telemetry", new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(242f, 195f), Ink);
        Label(telemetry, "TELEMETRÍA EN VIVO", 13, Cyan, 20, 16, 210, 22);
        Label(telemetry, "ALTITUD", 12, Muted, 20, 56, 200, 20);
        altitudeText = Label(telemetry, "0000 <size=17>m</size>", 34, Color.white, 20, 76, 210, 42, FontStyles.Bold);
        Label(telemetry, "VELOCIDAD VERTICAL", 12, Muted, 20, 129, 210, 20);
        speedText = Label(telemetry, "0.0 <size=14>m/s</size>", 25, Color.white, 20, 151, 210, 34);

        var wind = Panel(canvasRoot, "Wind compass", new Vector2(1f, 1f), new Vector2(-28f, -238f), new Vector2(242f, 132f), Ink);
        Label(wind, "VIENTO SUPERFICIAL", 13, Cyan, 16, 10, 210, 20);
        Label(wind, "N", 10, Muted, 62, 31, 16, 16, FontStyles.Bold).alignment = TextAlignmentOptions.Center;
        Label(wind, "E", 10, Muted, 110, 70, 16, 16, FontStyles.Bold).alignment = TextAlignmentOptions.Center;
        Label(wind, "S", 10, Muted, 62, 105, 16, 16, FontStyles.Bold).alignment = TextAlignmentOptions.Center;
        Label(wind, "O", 10, Muted, 14, 70, 16, 16, FontStyles.Bold).alignment = TextAlignmentOptions.Center;
        windArrow = Label(wind, "▲", 22, Orange, 70, 77, 32, 32, FontStyles.Bold);
        windArrow.alignment = TextAlignmentOptions.Center;
        windArrow.rectTransform.anchorMin = windArrow.rectTransform.anchorMax = new Vector2(0f, 1f);
        windArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        windSpeedText = Label(wind, "0.0 <size=13>m/s</size>", 24, Color.white, 137, 43, 96, 32, FontStyles.Bold);
        windDirectionText = Label(wind, "HACIA 000°", 12, Muted, 137, 77, 96, 22);

        var flight = Panel(canvasRoot, "Flight callout", new Vector2(0.68f, 0.56f), Vector2.zero, new Vector2(420f, 160f), Color.clear);
        countdownText = Label(flight, "", 85, Color.white, 0, 0, 420, 110, FontStyles.Bold);
        countdownText.alignment = TextAlignmentOptions.Center;
        stageText = Label(flight, "", 17, Cyan, 0, 112, 420, 38);
        stageText.alignment = TextAlignmentOptions.Center;

        var bottom = Panel(canvasRoot, "Mission status", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1544f, 92f), Ink);
        Label(bottom, "MISIÓN / 600 M", 14, Cyan, 22, 14, 200, 25);
        statusText = Label(bottom, "", 18, Color.white, 22, 45, 1120, 32);
        timerText = Label(bottom, "T − 00:05", 28, Color.white, 1320, 23, 200, 40, FontStyles.Bold);
        timerText.alignment = TextAlignmentOptions.Right;
        abortButton = Button(bottom, "AbortButton", "ABORTAR", 1155, 22, 140, 46, Orange, Ink, mission.AbortMission);
        var flightTrack = Panel(bottom, "Mission track", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1544f, 3f), new Color(0.1f, 0.2f, 0.24f));
        flightFill = Panel(flightTrack, "Progress", new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 3f), Cyan).GetComponent<UnityEngine.UI.Image>();
    }

    private void Update()
    {
        if (mission == null) return;
        fuelText.text = Mathf.CeilToInt(mission.Fuel) + "<color=#78929E><size=24>%</size></color>";
        fuelFill.rectTransform.sizeDelta = new Vector2(317f * mission.Fuel / 100f, 7f);
        fuelFill.color = mission.Fuel <= 25f ? Orange : Cyan;
        flightFill.rectTransform.sizeDelta = new Vector2(1544f * Mathf.Clamp01(mission.Altitude / LaunchDemo.TargetAltitude), 3f);
        altitudeText.text = mission.Altitude.ToString("0000") + " <size=17>m</size>";
        speedText.text = mission.Speed.ToString("0.0") + " <size=14>m/s</size>";
        windSpeedText.text = mission.WindSpeed.ToString("0.0") + " <size=13>m/s</size>";
        windDirectionText.text = "HACIA " + Mathf.RoundToInt(mission.WindDirection).ToString("000") + "°";
        windArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -mission.WindDirection);
        statusText.text = mission.Status;
        bool preparing = mission.State == LaunchDemo.FlightState.Preparing;
        fuelButton.interactable = preparing && mission.Fuel < 100f;
        launchButton.interactable = preparing && mission.Fuel >= 25f;
        bool counting = mission.State == LaunchDemo.FlightState.Countdown;
        bool complete = mission.State == LaunchDemo.FlightState.Complete;
        bool failed = mission.State == LaunchDemo.FlightState.Failed;
        abortButton.interactable = counting || mission.State == LaunchDemo.FlightState.Flying || mission.State == LaunchDemo.FlightState.Coasting;
        phaseText.text = preparing ? "01 / PREPARACIÓN" : counting ? "02 / ENCENDIDO" : complete ? "04 / OBJETIVO LOGRADO" : failed ? "VUELO TERMINADO" : "03 / EN VUELO";
        phaseText.fontSize = complete ? 22 : 26;
        countdownText.text = counting ? Mathf.CeilToInt(mission.Countdown).ToString("00") : complete ? "MISIÓN\nCUMPLIDA" : failed ? "INTÉNTALO\nDE NUEVO" : "";
        countdownText.fontSize = counting ? 85 : 48;
        stageText.text = counting ? "SECUENCIA DE LANZAMIENTO" : complete ? "600 METROS · PRUEBA SUPERADA" : failed ? "CARGA MÁS COMBUSTIBLE Y REINICIA" : "";
        int seconds = Mathf.FloorToInt(mission.FlightTime);
        timerText.text = preparing || counting ? "T − 00:" + (counting ? Mathf.CeilToInt(mission.Countdown) : 5).ToString("00") : "T + " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }

    private static TextMeshProUGUI Label(Transform parent, string content, float size, Color color, float x, float y, float width, float height, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = content; label.fontSize = size; label.color = color; label.fontStyle = style;
        label.font = TMP_Settings.defaultFontAsset;
        label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    private static UnityEngine.UI.Button Button(Transform parent, string name, string text, float x, float y, float width, float height, Color background, Color foreground, UnityAction action)
    {
        var rect = Panel(parent, name, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height), background);
        var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.7f, 0.85f, 0.9f); colors.disabledColor = new Color(0.4f, 0.45f, 0.48f, 0.65f); button.colors = colors;
        button.onClick.AddListener(action);
        var label = Label(rect, text, 16, foreground, 6, 0, width - 12, height, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }
}
